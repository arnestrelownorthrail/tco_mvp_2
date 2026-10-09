using System.Text.Json;
using TcoMvp.Core;
using TcoMvp.Core.Calculation;
using TcoMvp.Core.Model;

namespace TcoMvp.Tests;

/// <summary>
/// Vergleicht die C#-Berechnung mit Werten, die Excel selbst berechnet hat
/// (Fixtures/excel-scenarios.json, erzeugt durch tools/generate-excel-fixtures.ps1).
/// Benötigt die sensiblen Stammdaten und die Fixtures (beide nicht im Repo) – fehlen sie,
/// werden diese Tests übersprungen.
/// </summary>
public class ExcelParityTests
{
    private const double Tolerance = 1e-6;

    private static ModelData BaseData => SensitiveData.RealModel;

    public sealed record Scenario(
        string Name,
        double LoadFactor,
        CustomerInputs Inputs,
        Dictionary<string, ExpectedCustomer> ExpectedCustomer,
        Dictionary<string, ExpectedModel> ExpectedModel);

    public sealed record ExpectedCustomer(double? RentAndMaintenance, double? EnergyCost, double? TotalOperatingCost, double? DifferenceToReference);

    public sealed record ExpectedModel(
        double? DieselKm, double? ElectricKm, double? DieselRollingHours, double? DieselParkingHours, double? ElectricParkingHours,
        double? VehicleCost, double? DieselCost, double? ElectricityCost, double? EnergyCost,
        double? MaintenanceHours, double? MaintenanceCost, double? TotalCost);

    private static readonly Lazy<List<Scenario>> LazyScenarios = new(() => SensitiveData.Available
        ? JsonSerializer.Deserialize<List<Scenario>>(File.ReadAllText(SensitiveData.FixturesPath), ModelDataLoader.JsonOptions)!
        : []);

    private static List<Scenario> Scenarios => LazyScenarios.Value;

    public static TheoryData<string> ScenarioNames()
    {
        var data = new TheoryData<string>();
        foreach (var s in Scenarios) data.Add(s.Name);
        if (data.Count == 0) data.Add("(keine Fixtures)"); // Platzhalter, Test wird übersprungen
        return data;
    }

    /// <summary>Kopie der Stammdaten mit anderem Load factor (TCO Model!M41).</summary>
    private static ModelData WithLoadFactor(double loadFactor)
    {
        var node = JsonSerializer.SerializeToNode(BaseData, ModelDataLoader.JsonOptions)!;
        foreach (var loco in node["locomotives"]!.AsArray())
            if ((string?)loco!["id"] == BaseData.ReferenceLocoId) loco["loadFactor"] = loadFactor;
        return node.Deserialize<ModelData>(ModelDataLoader.JsonOptions)!;
    }

    private static void AssertClose(double? expected, double? actual, string what)
    {
        if (expected is null)
        {
            Assert.True(actual is null, $"{what}: Excel liefert #NV, C# liefert {actual}");
            return;
        }
        Assert.True(actual is not null, $"{what}: Excel liefert {expected}, C# liefert null");
        var diff = Math.Abs(expected.Value - actual!.Value);
        Assert.True(diff <= Tolerance * Math.Max(1, Math.Abs(expected.Value)),
            $"{what}: Excel {expected.Value:R} ≠ C# {actual.Value:R} (Δ {diff:E2})");
    }

    [SensitiveDataTheory]
    [MemberData(nameof(ScenarioNames))]
    public void TcoModel_matches_Excel(string name)
    {
        var s = Scenarios.Single(x => x.Name == name);
        var result = new TcoCalculator(WithLoadFactor(s.LoadFactor)).Calculate(s.Inputs);

        foreach (var (id, e) in s.ExpectedModel)
        {
            var b = result[id];
            AssertClose(e.DieselKm, b.DieselKm, $"{id} DieselKm");
            AssertClose(e.ElectricKm, b.ElectricKm, $"{id} ElectricKm");
            AssertClose(e.DieselRollingHours, b.DieselRollingHours, $"{id} DieselRollingHours");
            AssertClose(e.DieselParkingHours, b.DieselParkingHours, $"{id} DieselParkingHours");
            AssertClose(e.ElectricParkingHours, b.ElectricParkingHours, $"{id} ElectricParkingHours");
            AssertClose(e.VehicleCost, b.VehicleCost, $"{id} VehicleCost");
            AssertClose(e.DieselCost, b.DieselCost, $"{id} DieselCost");
            AssertClose(e.ElectricityCost, b.ElectricityCost, $"{id} ElectricityCost");
            AssertClose(e.EnergyCost, b.EnergyCost, $"{id} EnergyCost");
            AssertClose(e.MaintenanceHours, b.MaintenanceHours, $"{id} MaintenanceHours");
            AssertClose(e.MaintenanceCost, b.MaintenanceCost, $"{id} MaintenanceCost");
            AssertClose(e.TotalCost, b.TotalCost, $"{id} TotalCost");
        }
    }

    [SensitiveDataTheory]
    [MemberData(nameof(ScenarioNames))]
    public void CustomerView_matches_Betriebskosten_sheet(string name)
    {
        var s = Scenarios.Single(x => x.Name == name);
        var data = WithLoadFactor(s.LoadFactor);
        var view = CustomerView.From(data, new TcoCalculator(data).Calculate(s.Inputs));

        foreach (var (id, e) in s.ExpectedCustomer)
        {
            var row = view.Locomotives.Single(l => l.Id == id);
            AssertClose(e.RentAndMaintenance, row.RentAndMaintenance, $"{id} Miete und Instandhaltung");
            AssertClose(e.EnergyCost, row.EnergyCost, $"{id} Energiekosten");
            AssertClose(e.TotalOperatingCost, row.TotalOperatingCost, $"{id} Summe");
            AssertClose(e.DifferenceToReference, row.DifferenceToReference, $"{id} Kostenunterschied");
        }
    }
}
