using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using TcoMvp.Core;
using TcoMvp.Core.Model;

namespace TcoMvp.Tests;

/// <summary>Startet die Web-App im Testhost mit der angegebenen sensiblen Datei.</summary>
public sealed class TcoAppFactory(string sensitivePath) : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
        => builder.UseSetting("SensitiveModelDataPath", sensitivePath);
}

/// <summary>HTTP-Tests der API mit fiktiven Beispieldaten (laufen immer).</summary>
public class ApiTests : IClassFixture<ApiTests.ExampleApp>
{
    public sealed class ExampleApp() : IDisposable
    {
        public TcoAppFactory Factory { get; } = new(SensitiveData.ExamplePath);
        public void Dispose() => Factory.Dispose();
    }

    private readonly HttpClient _client;
    private static readonly JsonSerializerOptions Json = ModelDataLoader.JsonOptions;

    public ApiTests(ExampleApp app) => _client = app.Factory.CreateClient();

    private async Task<CustomerInputs> GetDefaultsAsync()
    {
        var model = await _client.GetFromJsonAsync<CustomerView.ModelInfo>("/api/model", Json);
        return model!.Defaults;
    }

    [Fact]
    public async Task Start_page_is_served()
    {
        var res = await _client.GetAsync("/");
        res.EnsureSuccessStatusCode();
        Assert.Contains("Betriebskostenvergleich", await res.Content.ReadAsStringAsync());
    }

    [Theory]
    [InlineData("/app.js")]
    [InlineData("/styles.css")]
    public async Task Static_assets_are_served(string path)
        => Assert.Equal(HttpStatusCode.OK, (await _client.GetAsync(path)).StatusCode);

    [Fact]
    public async Task Model_endpoint_lists_visible_locos_and_defaults()
    {
        var model = await _client.GetFromJsonAsync<CustomerView.ModelInfo>("/api/model", Json);
        Assert.Equal(["g1206", "de18-3a", "de18-5sh", "vdm", "traxx"], model!.Locomotives.Select(l => l.Id));
        Assert.Single(model.Locomotives, l => l.IsReference);
        Assert.Equal(40000, model.Defaults.MileageKmPerYear);
    }

    [Fact]
    public async Task Calculate_returns_one_row_per_visible_loco()
    {
        var res = await _client.PostAsJsonAsync("/api/calculate", await GetDefaultsAsync(), Json);
        res.EnsureSuccessStatusCode();
        var result = await res.Content.ReadFromJsonAsync<CustomerView.Result>(Json);

        Assert.Equal(5, result!.Locomotives.Count);
        Assert.All(result.Locomotives, l => Assert.NotNull(l.TotalOperatingCost));
        Assert.Null(result.Locomotives.Single(l => l.Id == "traxx").DifferenceToReference);
    }

    [Fact]
    public async Task Calculate_response_contains_only_customer_fields()
    {
        var res = await _client.PostAsJsonAsync("/api/calculate", await GetDefaultsAsync(), Json);
        using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync());
        var fields = doc.RootElement.GetProperty("locomotives")[0].EnumerateObject().Select(p => p.Name).Order();
        Assert.Equal(["differenceToReference", "energyCost", "error", "id", "rentAndMaintenance", "totalOperatingCost"], fields);
    }

    [Fact]
    public async Task Invalid_inputs_return_400_with_field_errors()
    {
        var input = await GetDefaultsAsync() with { MileageKmPerYear = -1, RollingShare = 2 };
        var res = await _client.PostAsJsonAsync("/api/calculate", input, Json);

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync());
        var errors = doc.RootElement.GetProperty("errors");
        Assert.True(errors.TryGetProperty("MileageKmPerYear", out _));
        Assert.True(errors.TryGetProperty("RollingShare", out _));
    }

    [Theory]
    [InlineData("{ kein json")]
    [InlineData("{\"mileageKmPerYear\": \"viel\"}")]
    public async Task Malformed_json_returns_400(string body)
    {
        var res = await _client.PostAsync("/api/calculate", new StringContent(body, Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task Traxx_out_of_range_is_reported_not_crashed()
    {
        var input = await GetDefaultsAsync() with { MileageKmPerYear = 200000 };
        var result = await (await _client.PostAsJsonAsync("/api/calculate", input, Json)).Content.ReadFromJsonAsync<CustomerView.Result>(Json);
        var traxx = result!.Locomotives.Single(l => l.Id == "traxx");
        Assert.Null(traxx.TotalOperatingCost);
        Assert.Contains("Laufleistung", traxx.Error);
    }

    [Theory]
    [InlineData("/Data/model-data.json")]
    [InlineData("/Data/model-data.sensitive.json")]
    [InlineData("/model-data.sensitive.json")]
    [InlineData("/appsettings.json")]
    public async Task Data_and_config_files_are_not_downloadable(string path)
        => Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync(path)).StatusCode);

    [Fact]
    public async Task Get_on_calculate_is_not_allowed()
        => Assert.Equal(HttpStatusCode.MethodNotAllowed, (await _client.GetAsync("/api/calculate")).StatusCode);
}

public class ApiStartupTests
{
    [Fact]
    public void App_refuses_to_start_without_sensitive_data()
    {
        using var factory = new TcoAppFactory(Path.Combine(Path.GetTempPath(), "gibt-es-nicht.json"));
        var ex = Record.Exception(() => factory.CreateClient());
        Assert.NotNull(ex);
        Assert.Contains("Sensible Stammdaten fehlen", ex.ToString());
    }
}

/// <summary>
/// End-to-End: Excel-Referenzwerte gegen die HTTP-API mit echten Stammdaten.
/// Wird übersprungen, wenn sensible Daten / Fixtures fehlen.
/// </summary>
public class ApiExcelParityTests
{
    private sealed record Scenario(string Name, double LoadFactor, CustomerInputs Inputs,
        Dictionary<string, ExcelParityTests.ExpectedCustomer> ExpectedCustomer);

    [SensitiveDataFact]
    public async Task Api_returns_Excel_values_for_all_load_factor_15_scenarios()
    {
        using var factory = new TcoAppFactory(SensitiveData.SensitivePath);
        var client = factory.CreateClient();
        var scenarios = JsonSerializer.Deserialize<List<Scenario>>(File.ReadAllText(SensitiveData.FixturesPath), ModelDataLoader.JsonOptions)!
            .Where(s => s.LoadFactor == 0.15) // Load factor ist keine API-Eingabe
            .ToList();
        Assert.NotEmpty(scenarios);

        foreach (var s in scenarios)
        {
            var res = await client.PostAsJsonAsync("/api/calculate", s.Inputs, ModelDataLoader.JsonOptions);
            res.EnsureSuccessStatusCode();
            var result = (await res.Content.ReadFromJsonAsync<CustomerView.Result>(ModelDataLoader.JsonOptions))!;
            foreach (var (id, e) in s.ExpectedCustomer)
            {
                var row = result.Locomotives.Single(l => l.Id == id);
                Close(e.TotalOperatingCost, row.TotalOperatingCost, $"{s.Name}/{id} Summe");
                Close(e.DifferenceToReference, row.DifferenceToReference, $"{s.Name}/{id} Differenz");
                Close(e.RentAndMaintenance, row.RentAndMaintenance, $"{s.Name}/{id} Miete");
                Close(e.EnergyCost, row.EnergyCost, $"{s.Name}/{id} Energie");
            }
        }
    }

    private static void Close(double? expected, double? actual, string what)
    {
        if (expected is null) { Assert.True(actual is null, $"{what}: Excel #NV, API {actual}"); return; }
        Assert.True(actual is not null && Math.Abs(expected.Value - actual.Value) < 1e-6, $"{what}: Excel {expected}, API {actual}");
    }
}
