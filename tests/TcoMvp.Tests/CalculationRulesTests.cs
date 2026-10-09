using TcoMvp.Core;
using TcoMvp.Core.Calculation;
using TcoMvp.Core.Model;

namespace TcoMvp.Tests;

/// <summary>
/// Fachliche Regeln des TCO Models, die unabhängig von konkreten Preisen gelten.
/// Läuft mit den fiktiven Beispieldaten, also auch ohne sensible Datei.
/// </summary>
public class CalculationRulesTests
{
    private static readonly ModelData Data = SensitiveData.ExampleModel();
    private static readonly TcoCalculator Calc = new(Data);
    private static CustomerInputs Defaults => Data.CustomerDefaults;

    private static TcoResult Run(CustomerInputs i) => Calc.Calculate(i);

    private static CustomerInputs WithLoco(CustomerInputs i, string id, Func<LocoInputs, LocoInputs> change)
        => i with { Locomotives = new Dictionary<string, LocoInputs>(i.Locomotives) { [id] = change(i.Locomotives[id]) } };

    public static TheoryData<string> AllLocos()
    {
        var d = new TheoryData<string>();
        foreach (var l in Data.Locomotives) d.Add(l.Id);
        return d;
    }

    // ---- Aufteilung km und Stunden ----------------------------------------------------------

    [Theory]
    [MemberData(nameof(AllLocos))]
    public void Kilometres_are_fully_allocated(string id)
    {
        var b = Run(Defaults)[id];
        Assert.Equal(Defaults.MileageKmPerYear, b.DieselKm + b.ElectricKm, 6);
        Assert.Equal(b.DieselKm, b.ShuntingKm + b.MainlineKm, 6);
    }

    [Theory]
    [MemberData(nameof(AllLocos))]
    public void Hours_are_fully_allocated(string id)
    {
        var b = Run(Defaults)[id];
        Assert.Equal(Defaults.OperatingHoursPerYear, b.RollingHours + b.ParkingHours, 6);
        Assert.Equal(b.RollingHours, b.DieselRollingHours + b.ElectricRollingHours, 6);
        Assert.Equal(b.ParkingHours, b.DieselParkingHours + b.StartStopParkingHours + b.ElectricParkingHours, 6);
    }

    [Theory]
    [InlineData("g1206")]
    [InlineData("de18-3a")]
    [InlineData("de18-5sh")]
    public void Diesel_only_locos_ignore_electric_share_and_power_price(string id)
    {
        var a = Run(Defaults)[id];
        var b = Run(Defaults with { ElectricShare = 1, ElectricityPricePerKwh = 99 })[id];
        Assert.Equal(0, a.ElectricKm);
        Assert.Equal(0, a.ElectricityCost);
        Assert.Equal(a.TotalCost, b.TotalCost);
    }

    [Fact]
    public void Fully_electric_operation_needs_no_diesel_for_electric_locos()
    {
        var r = Run(Defaults with { ElectricShare = 1 });
        foreach (var id in new[] { "vdm", "traxx" })
        {
            Assert.Equal(0, r[id].DieselKm, 9);
            Assert.Equal(0, r[id].DieselRollingHours, 9);
        }
    }

    // ---- Energiekosten -----------------------------------------------------------------------

    [Theory]
    [MemberData(nameof(AllLocos))]
    public void Diesel_cost_scales_linearly_with_diesel_price(string id)
    {
        var a = Run(Defaults)[id];
        var b = Run(Defaults with { DieselPricePerLiter = Defaults.DieselPricePerLiter * 2 })[id];
        Assert.Equal(a.DieselCost * 2, b.DieselCost, 6);
        Assert.Equal(a.ElectricityCost, b.ElectricityCost, 9);
    }

    [Fact]
    public void Energy_cost_is_diesel_plus_electricity()
    {
        foreach (var b in Run(Defaults).Locomotives)
        {
            Assert.Equal(b.DieselCost + b.ElectricityCost, b.EnergyCost, 9);
            Assert.Equal(b.DieselCostShunting + b.DieselCostMainline + b.DieselCostParked, b.DieselCost, 9);
            Assert.Equal(b.ElectricityCostRolling + b.ElectricityCostParked, b.ElectricityCost, 9);
        }
    }

    [Fact]
    public void Diesel_shunting_cost_follows_formula_km_times_litres_times_price_per_month()
    {
        var li = Defaults.Locomotives["g1206"];
        var b = Run(Defaults)["g1206"];
        var expected = Defaults.MileageKmPerYear * Defaults.ShuntingShare * li.DieselShuntingLitersPerKm / 12 * Defaults.DieselPricePerLiter;
        Assert.Equal(expected, b.DieselCostShunting, 6);
    }

    [Fact]
    public void More_start_stop_lowers_parked_diesel_cost()
    {
        var low = Run(WithLoco(Defaults, "de18-3a", l => l with { StartStopShare = 0 }))["de18-3a"];
        var high = Run(WithLoco(Defaults, "de18-3a", l => l with { StartStopShare = 0.6 }))["de18-3a"];
        Assert.True(high.DieselCostParked < low.DieselCostParked);
        Assert.Equal(low.DieselCostParked * 0.4, high.DieselCostParked, 6);
    }

    [Fact]
    public void Start_stop_input_is_ignored_for_locos_without_start_stop()
    {
        var a = Run(Defaults)["g1206"];
        var b = Run(WithLoco(Defaults, "g1206", l => l with { StartStopShare = 0.9 }))["g1206"];
        Assert.Equal(a.TotalCost, b.TotalCost);
    }

    [Fact]
    public void Zero_usage_means_zero_energy_cost()
    {
        var r = Run(Defaults with { MileageKmPerYear = 0, OperatingHoursPerYear = 0 });
        Assert.All(r.Locomotives, b => Assert.Equal(0, b.EnergyCost));
    }

    // ---- Fahrzeug, Wartung, Summe --------------------------------------------------------------

    [Theory]
    [MemberData(nameof(AllLocos))]
    public void Vehicle_cost_is_lease_plus_replacement_for_unavailability_plus_etcs(string id)
    {
        var loco = Data.Locomotives.Single(l => l.Id == id);
        var b = Run(Defaults)[id];
        var expected = loco.DryLeasePerMonth + loco.DryLeasePerMonth * (1 - loco.Availability) + loco.EtcsStepUpPerMonth;
        Assert.Equal(expected, b.VehicleCost, 6);
    }

    [Fact]
    public void Vehicle_cost_does_not_depend_on_usage()
        => Assert.Equal(
            Run(Defaults)["traxx"].VehicleCost,
            Run(Defaults with { MileageKmPerYear = 90000, OperatingHoursPerYear = 5000 })["traxx"].VehicleCost);

    [Fact]
    public void Double_engine_impact_scales_maintenance_hours_for_traxx_only()
    {
        var r = Run(Defaults);
        var traxx = Data.Locomotives.Single(l => l.Id == "traxx");
        Assert.Equal(r["traxx"].DieselHoursPerYear * traxx.DoubleEngineImpact, r["traxx"].MaintenanceHours, 9);
        Assert.Equal(r["g1206"].DieselHoursPerYear, r["g1206"].MaintenanceHours);
    }

    [Theory]
    [MemberData(nameof(AllLocos))]
    public void Total_cost_is_vehicle_plus_energy_plus_maintenance(string id)
    {
        var b = Run(Defaults)[id];
        Assert.Equal(b.VehicleCost + b.EnergyCost + b.MaintenanceCost!.Value, b.TotalCost!.Value, 6);
    }

    [Fact]
    public void Hidden_loco_uses_backend_consumption_not_customer_values()
    {
        var a = Run(Defaults)["vdm-light"];
        var b = Run(WithLoco(Defaults, "vdm", l => l with { DieselShuntingLitersPerKm = 50 }))["vdm-light"];
        Assert.Equal(a.TotalCost, b.TotalCost);
    }

    [Fact]
    public void Missing_inputs_for_visible_loco_throw()
        => Assert.Throws<ArgumentException>(() => Run(Defaults with { Locomotives = new() }));

    // ---- Kundenansicht ---------------------------------------------------------------------------

    [Fact]
    public void Customer_view_matches_model_totals_and_differences()
    {
        var tco = Run(Defaults);
        var view = CustomerView.From(Data, tco);
        foreach (var row in view.Locomotives)
        {
            var b = tco[row.Id];
            Assert.Equal(b.VehicleCost + b.MaintenanceCost!.Value, row.RentAndMaintenance!.Value, 9);
            Assert.Equal(b.EnergyCost, row.EnergyCost);
            Assert.Equal(b.TotalCost!.Value, row.TotalOperatingCost!.Value, 6);
            if (row.Id == Data.ReferenceLocoId) Assert.Null(row.DifferenceToReference);
            else Assert.Equal(tco.VarianceToReference(row.Id)!.Value, row.DifferenceToReference!.Value, 6);
        }
    }

    [Fact]
    public void Traxx_out_of_range_makes_all_differences_unavailable_like_Excel_NA()
    {
        var view = CustomerView.From(Data, Run(Defaults with { MileageKmPerYear = 200000 }));
        var traxx = view.Locomotives.Single(l => l.Id == "traxx");

        Assert.Null(traxx.TotalOperatingCost);
        Assert.NotNull(traxx.Error);
        Assert.True(traxx.EnergyCost > 0); // Energiekosten bleiben berechenbar (Excel K35)
        Assert.All(view.Locomotives, l => Assert.Null(l.DifferenceToReference));
        Assert.All(view.Locomotives.Where(l => l.Id != "traxx"), l => Assert.NotNull(l.TotalOperatingCost));
    }

    [Fact]
    public void Traxx_mileage_limit_is_heavy_maintenance_table_not_price_band()
    {
        // km-Preisbänder gehen bis 122.500, die Heavy-Maintenance-Tabelle nur bis 120.000 → Excel #NV ab 120.001
        Assert.NotNull(Run(Defaults with { MileageKmPerYear = 120000 })["traxx"].TotalCost);
        var over = Run(Defaults with { MileageKmPerYear = 120001 })["traxx"];
        Assert.Null(over.TotalCost);
        Assert.Matches(@"max\. 120[.,]000 km", over.MaintenanceError);
    }

    [Fact]
    public void Customer_view_lists_locos_in_sheet_order()
        => Assert.Equal(["g1206", "de18-3a", "de18-5sh", "vdm", "traxx"],
            CustomerView.From(Data, Run(Defaults)).Locomotives.Select(l => l.Id));
}
