using TcoMvp.Core.Calculation;
using TcoMvp.Core.Model;

namespace TcoMvp.Tests;

/// <summary>
/// Wartungslogik je Lok mit eigens konstruierten Tabellen, damit jede Regel
/// (Tabellentreffer vs. Regression, Preisbänder, Heavy Maintenance) gezielt geprüft wird.
/// </summary>
public class MaintenanceTests
{
    private static readonly TraxxTables ExampleTraxx = SensitiveData.ExampleModel().Maintenance.Traxx;

    private static readonly MaintenanceTables Tables = new()
    {
        G1206 = new HoursRegressionTable { Hours = [2000, 3000], Values = [111, 222], Fixed = 10, PerHour = 2 },
        De18 = new HoursRegressionTable { Hours = [2000, 3000], Values = [333, 444], Fixed = 20, PerHour = 3 },
        Vdm = new VdmTable
        {
            Mileages = [35000, 50000], Hours = [750, 1000],
            Values = [[1, 2], [3, 10]],
            Fixed = 100, PerHour = 1, PerKm = 0.01,
        },
        VdmLightDiscount = 0.5,
        Traxx = new TraxxTables
        {
            KmBandUpperLimits = [37500, 42499, 47499],
            KmRates = [3, 2, 1],
            IncludedDieselHours = 500,
            DieselHourThresholds = [0, 500, 1000, 1500, 2000, 2500],
            DieselHourRates = [[0, 0, 0], [10, 20, 30], [40, 50, 60], [41, 51, 61], [42, 52, 62], [43, 53, 63]],
            HeavyMaintenance = ExampleTraxx.HeavyMaintenance,
        },
    };

    private static readonly MaintenanceCalculator Calc = new(Tables);

    private static LocoConfig Loco(MaintenanceModel m) => new() { Id = "t", Maintenance = m };

    // ---- G1206 / DE18 ---------------------------------------------------------------------

    [Fact]
    public void Hours_table_uses_table_value_on_exact_hit()
    {
        Assert.Equal(111, Calc.Calculate(Loco(MaintenanceModel.G1206), 2000, 40000).Cost);
        Assert.Equal(444, Calc.Calculate(Loco(MaintenanceModel.De18), 3000, 40000).Cost);
    }

    [Fact]
    public void Hours_table_falls_back_to_regression_between_table_values()
    {
        Assert.Equal(10 + 2 * 2500.0, Calc.Calculate(Loco(MaintenanceModel.G1206), 2500, 40000).Cost);
        Assert.Equal(20 + 3 * 900.0, Calc.Calculate(Loco(MaintenanceModel.De18), 900, 40000).Cost);
    }

    [Fact]
    public void Hours_table_ignores_mileage()
        => Assert.Equal(
            Calc.Calculate(Loco(MaintenanceModel.G1206), 2500, 10000).Cost,
            Calc.Calculate(Loco(MaintenanceModel.G1206), 2500, 120000).Cost);

    // ---- VDM ------------------------------------------------------------------------------

    [Fact]
    public void Vdm_uses_matrix_only_when_mileage_and_hours_both_match()
        => Assert.Equal(10, Calc.Calculate(Loco(MaintenanceModel.Vdm), 1000, 50000).Cost);

    [Theory]
    [InlineData(1000, 40000)] // Stunden passen, km nicht
    [InlineData(900, 50000)]  // km passen, Stunden nicht
    public void Vdm_uses_regression_when_one_axis_does_not_match(double hours, double km)
        => Assert.Equal(100 + 1 * hours + 0.01 * km, Calc.Calculate(Loco(MaintenanceModel.Vdm), hours, km).Cost!.Value, 9);

    // ---- VDM light ------------------------------------------------------------------------

    [Fact]
    public void Vdm_light_always_uses_regression_even_on_exact_matrix_coordinates()
    {
        // Abgeleitete Matrix = VDM × (1 − 50 %) = [[0,5; 1], [1,5; 5]]
        // Spaltenmittel (Stunden 750/1000): 1 / 3      → Steigung 2/250
        // Zeilenmittel (km 35000/50000):   0,75 / 3,25 → Steigung 2,5/15000
        const double perHour = 2.0 / 250, perKm = 2.5 / 15000;
        const double fixedPart = 2.0 - perHour * 875 - perKm * 42500;
        var expected = fixedPart + perHour * 750 + perKm * 35000;

        var cost = Calc.Calculate(Loco(MaintenanceModel.VdmLight), 750, 35000).Cost!.Value;
        Assert.Equal(expected, cost, 9);
        Assert.NotEqual(0.5, cost, 6); // Matrixwert würde 0,5 ergeben – Excel nutzt ihn nie
    }

    // ---- Traxx ----------------------------------------------------------------------------

    [Theory]
    [InlineData(37500, 0)] // Bandgrenze gehört noch zum ersten Band
    [InlineData(37501, 1)]
    [InlineData(47499, 2)]
    public void Traxx_km_price_uses_band_boundaries_like_contract(double km, int band)
    {
        var heavy = Calc.HeavyMaintenanceMatrix();
        var hmCol = ExcelFunctions.XLookupExactOrNextLarger(km, Tables.Traxx.HeavyMaintenance.Mileages)!.Value;
        var expected = heavy[0][hmCol] + km * Tables.Traxx.KmRates[band] / 12; // ≤ 500 h → keine Dieselstunden

        Assert.Equal(expected, Calc.Calculate(Loco(MaintenanceModel.Traxx), 400, km).Cost!.Value, 9);
    }

    [Fact]
    public void Traxx_charges_diesel_hours_only_above_included_hours()
    {
        var at500 = Calc.Calculate(Loco(MaintenanceModel.Traxx), 500, 30000).Cost!.Value;
        var at499 = Calc.Calculate(Loco(MaintenanceModel.Traxx), 499, 30000).Cost!.Value;
        var at501 = Calc.Calculate(Loco(MaintenanceModel.Traxx), 501, 30000).Cost!.Value;

        Assert.Equal(at499, at500, 9);
        // 501 h: 1 zusätzliche Stunde, Satz aus Zeile "500" und km-Band 0 = 10 €/h, monatlich /12;
        // Heavy-Maintenance-Zeile springt von 500 auf 750 h.
        var heavy = Calc.HeavyMaintenanceMatrix();
        Assert.Equal(heavy[1][0] - heavy[0][0] + 1 * 10.0 / 12, at501 - at500, 9);
    }

    [Fact]
    public void Traxx_returns_error_instead_of_value_when_mileage_exceeds_contract()
    {
        var r = Calc.Calculate(Loco(MaintenanceModel.Traxx), 400, 47500);
        Assert.Null(r.Cost);
        Assert.Contains("Laufleistung", r.Error);
    }

    [Fact]
    public void Traxx_returns_error_when_maintenance_hours_exceed_contract()
    {
        var max = Tables.Traxx.HeavyMaintenance.Hours.Max();
        Assert.NotNull(Calc.Calculate(Loco(MaintenanceModel.Traxx), max, 30000).Cost);
        var r = Calc.Calculate(Loco(MaintenanceModel.Traxx), max + 1, 30000);
        Assert.Null(r.Cost);
        Assert.Contains("Wartungsstunden", r.Error);
    }

    [Fact]
    public void Traxx_negative_diesel_hours_are_treated_like_zero_extra_hours()
        => Assert.Equal(
            Calc.Calculate(Loco(MaintenanceModel.Traxx), 0, 30000).Cost!.Value,
            Calc.Calculate(Loco(MaintenanceModel.Traxx), -100, 30000).Cost!.Value, 9);

    // ---- Heavy Maintenance ----------------------------------------------------------------

    [Fact]
    public void Heavy_maintenance_extrapolated_rows_are_average_of_neighbours()
    {
        var hm = Tables.Traxx.HeavyMaintenance;
        var m = Calc.HeavyMaintenanceMatrix();
        for (var r = 0; r < hm.Hours.Length; r++)
        {
            if (hm.CalculatedHours.Contains(hm.Hours[r])) continue;
            for (var c = 0; c < hm.Mileages.Length; c++)
                Assert.Equal((m[r - 1][c] + m[r + 1][c]) / 2, m[r][c], 9);
        }
    }

    [Fact]
    public void Heavy_maintenance_grows_with_diesel_hours()
    {
        var m = Calc.HeavyMaintenanceMatrix();
        for (var r = 1; r < m.Length; r++)
            Assert.True(m[r][0] >= m[r - 1][0], $"Zeile {r} kleiner als Zeile {r - 1}");
    }

    [Fact]
    public void Heavy_maintenance_uses_per_column_wheelset_price_and_interval()
    {
        var hm = Tables.Traxx.HeavyMaintenance;
        var m = Calc.HeavyMaintenanceMatrix();
        // Spalten unterscheiden sich nur im Radsatz-Anteil (Zeile 49)
        for (var c = 1; c < hm.Mileages.Length; c++)
            Assert.Equal(
                hm.WheelsetOverhaulPrices[c] / hm.WheelsetOverhaulMonths[c] - hm.WheelsetOverhaulPrices[0] / hm.WheelsetOverhaulMonths[0],
                m[0][c] - m[0][0], 9);
    }

    [Fact]
    public void Diesel_measures_with_interval_over_lifetime_are_not_charged()
    {
        var hm = Tables.Traxx.HeavyMaintenance;
        var longIntervals = new MaintenanceTables
        {
            Traxx = new TraxxTables
            {
                HeavyMaintenance = new HeavyMaintenanceData
                {
                    Mileages = hm.Mileages, Hours = hm.Hours, CalculatedHours = hm.CalculatedHours,
                    R8Months = 1, R10Months = 1, R16Months = 1, R20Months = 1, GearboxMonths = 1,
                    WheelsetOverhaulPrices = hm.WheelsetOverhaulPrices.Select(_ => 0.0).ToArray(),
                    WheelsetOverhaulMonths = hm.WheelsetOverhaulMonths,
                    AlternatorPrice = 1200, TopEndPrice = 1200, RubbersPrice = 1200,
                    AlternatorYears = hm.CalculatedHours.Select(_ => 31.0).ToArray(),
                    TopEndYears = hm.CalculatedHours.Select(_ => 31.0).ToArray(),
                    RubbersYears = hm.CalculatedHours.Select(_ => 10.0).ToArray(),
                    MajorPerMonth = hm.CalculatedHours.Select(_ => 0.0).ToArray(),
                    LifetimeYears = 30,
                },
            },
        };
        // Nur Rubbers (10 Jahre ≤ 30) wird umgelegt: 1200 / 10 / 12 = 10 € / Monat
        Assert.Equal(10, new MaintenanceCalculator(longIntervals).HeavyMaintenanceMatrix()[0][0], 9);
    }
}
