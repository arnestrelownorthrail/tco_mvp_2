using System.Text.Json;
using TcoMvp.Core;
using TcoMvp.Core.Calculation;

namespace TcoMvp.Tests;

/// <summary>Prüft die Trennung unkritisch / sensibel. Läuft auch ohne echte Stammdaten.</summary>
public class DataSeparationTests
{
    private static readonly string[] SensitiveFields =
    [
        "dryLeasePerMonth", "etcsStepUpPerMonth", "availability", "doubleEngineImpact",
        "loadFactor", "maintenance", "backendInputs",
    ];

    [Fact]
    public void Public_file_contains_no_sensitive_fields()
    {
        var json = File.ReadAllText(SensitiveData.PublicPath);
        foreach (var field in SensitiveFields)
            Assert.DoesNotContain($"\"{field}\"", json);
        Assert.DoesNotContain("vdm-light", json);
    }

    [Fact]
    public void Loading_without_sensitive_file_fails_with_clear_message()
    {
        var ex = Assert.Throws<FileNotFoundException>(() =>
            ModelDataLoader.Load(SensitiveData.PublicPath, Path.Combine(Path.GetTempPath(), "gibt-es-nicht.json")));
        Assert.Contains("Sensible Stammdaten fehlen", ex.Message);
    }

    [Fact]
    public void Example_file_merges_into_complete_model()
    {
        var data = SensitiveData.ExampleModel();
        Assert.Contains(data.Locomotives, l => l.Id == "vdm-light" && !l.VisibleToCustomer);
        Assert.All(data.Locomotives, l => Assert.True(l.DryLeasePerMonth > 0));
        var result = new TcoCalculator(data).Calculate(data.CustomerDefaults);
        Assert.All(result.Locomotives, l => Assert.NotNull(l.TotalCost));
    }

    [Fact]
    public void Customer_api_payloads_never_expose_backend_data()
    {
        var data = SensitiveData.ExampleModel();
        var payloads = JsonSerializer.Serialize(new object[]
        {
            CustomerView.Describe(data),
            CustomerView.From(data, new TcoCalculator(data).Calculate(data.CustomerDefaults)),
        }, ModelDataLoader.JsonOptions);

        foreach (var field in SensitiveFields)
            Assert.DoesNotContain($"\"{field}\"", payloads);
        Assert.DoesNotContain("vdm-light", payloads);
    }
}
