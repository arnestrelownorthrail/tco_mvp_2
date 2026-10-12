using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using Microsoft.AspNetCore.Mvc.Testing;
using TcoMvp.Core;
using TcoMvp.Core.Model;

namespace TcoMvp.Tests;

/// <summary>Admin-Bereich (/admin, /api/admin/defaults) mit Beispieldaten und temporärer Defaults-Datei.</summary>
public sealed class AdminApiTests : IDisposable
{
    private const string Password = "geheim";
    private readonly string _defaultsPath = Path.Combine(Path.GetTempPath(), $"customer-defaults-{Guid.NewGuid():N}.json");
    private readonly WebApplicationFactory<Program> _factory;

    public AdminApiTests()
    {
        _factory = CreateFactory(Password);
    }

    private WebApplicationFactory<Program> CreateFactory(string? password) =>
        new TcoAppFactory(SensitiveData.ExamplePath).WithWebHostBuilder(b =>
        {
            b.UseSetting("CustomerDefaultsPath", _defaultsPath);
            b.UseSetting("Admin:Password", password ?? "");
        });

    public void Dispose()
    {
        _factory.Dispose();
        if (File.Exists(_defaultsPath)) File.Delete(_defaultsPath);
    }

    private sealed record DefaultsInfo(CustomerInputs Current, CustomerInputs Original, bool Customized);

    private HttpClient Client(string? password = Password)
    {
        var client = _factory.CreateClient();
        if (password is not null)
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic",
                Convert.ToBase64String(Encoding.UTF8.GetBytes($"admin:{password}")));
        return client;
    }

    [Theory]
    [InlineData("/admin/")]
    [InlineData("/admin/admin.js")]
    [InlineData("/api/admin/defaults")]
    public async Task Admin_requires_password(string path)
    {
        Assert.Equal(HttpStatusCode.Unauthorized, (await Client(null).GetAsync(path)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await Client("falsch").GetAsync(path)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Client().GetAsync(path)).StatusCode);
    }

    [Fact]
    public async Task Admin_is_disabled_without_configured_password()
    {
        using var factory = CreateFactory(null);
        var res = await factory.CreateClient().GetAsync("/api/admin/defaults");
        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }

    [Fact]
    public async Task Saved_defaults_are_served_to_customers_and_survive_restart()
    {
        var client = Client();
        var info = (await client.GetFromJsonAsync<DefaultsInfo>("/api/admin/defaults", ModelDataLoader.JsonOptions))!;
        Assert.False(info.Customized);

        var changed = info.Current with { MileageKmPerYear = 55000, DieselPricePerLiter = 1.65 };
        var res = await client.PutAsJsonAsync("/api/admin/defaults", changed, ModelDataLoader.JsonOptions);
        res.EnsureSuccessStatusCode();
        Assert.True(File.Exists(_defaultsPath));

        var model = await Client(null).GetFromJsonAsync<CustomerView.ModelInfo>("/api/model", ModelDataLoader.JsonOptions);
        Assert.Equal(55000, model!.Defaults.MileageKmPerYear);
        Assert.Equal(1.65, model.Defaults.DieselPricePerLiter);

        // Neustart liest die gespeicherte Datei
        using var restarted = CreateFactory(Password);
        var after = await restarted.CreateClient().GetFromJsonAsync<CustomerView.ModelInfo>("/api/model", ModelDataLoader.JsonOptions);
        Assert.Equal(55000, after!.Defaults.MileageKmPerYear);
    }

    [Fact]
    public async Task Invalid_defaults_are_rejected_and_not_saved()
    {
        var client = Client();
        var info = (await client.GetFromJsonAsync<DefaultsInfo>("/api/admin/defaults", ModelDataLoader.JsonOptions))!;
        var res = await client.PutAsJsonAsync("/api/admin/defaults", info.Current with { RollingShare = 1.5 }, ModelDataLoader.JsonOptions);
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.False(File.Exists(_defaultsPath));
    }

    [Fact]
    public async Task Delete_restores_original_defaults()
    {
        var client = Client();
        var info = (await client.GetFromJsonAsync<DefaultsInfo>("/api/admin/defaults", ModelDataLoader.JsonOptions))!;
        (await client.PutAsJsonAsync("/api/admin/defaults", info.Current with { MileageKmPerYear = 1 }, ModelDataLoader.JsonOptions)).EnsureSuccessStatusCode();

        var res = await client.DeleteAsync("/api/admin/defaults");
        var restored = (await res.Content.ReadFromJsonAsync<DefaultsInfo>(ModelDataLoader.JsonOptions))!;
        Assert.False(restored.Customized);
        Assert.Equal(restored.Original.MileageKmPerYear, restored.Current.MileageKmPerYear);
        Assert.False(File.Exists(_defaultsPath));
    }

    [Fact]
    public async Task Customer_endpoints_stay_public()
    {
        Assert.Equal(HttpStatusCode.OK, (await Client(null).GetAsync("/")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Client(null).GetAsync("/api/model")).StatusCode);
    }
}
