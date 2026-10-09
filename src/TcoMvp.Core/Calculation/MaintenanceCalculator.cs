using TcoMvp.Core.Model;
using static TcoMvp.Core.Calculation.ExcelFunctions;

namespace TcoMvp.Core.Calculation;

/// <summary>Wartungskosten € / Monat – TCO Model!H115:M115.</summary>
internal sealed class MaintenanceCalculator(MaintenanceTables tables)
{
    public readonly record struct Result(double? Cost, string? Error);

    public Result Calculate(LocoConfig loco, double maintenanceHours, double mileage) => loco.Maintenance switch
    {
        MaintenanceModel.G1206 => HoursTable(tables.G1206, maintenanceHours),
        MaintenanceModel.De18 => HoursTable(tables.De18, maintenanceHours),
        MaintenanceModel.Vdm => Vdm(maintenanceHours, mileage),
        MaintenanceModel.VdmLight => VdmLight(maintenanceHours, mileage),
        MaintenanceModel.Traxx => Traxx(maintenanceHours, mileage, loco.LoadFactor),
        _ => throw new ArgumentOutOfRangeException(nameof(loco)),
    };

    // =IFERROR(XLOOKUP(h, Stunden, Werte), K8*h + K7)
    private static Result HoursTable(HoursRegressionTable t, double hours)
    {
        var idx = ExactMatch(hours, t.Hours);
        return new(idx is int i ? t.Values[i] : t.PerHour * hours + t.Fixed, null);
    }

    // =IFERROR(INDEX(Matrix, MATCH(km), MATCH(h)), K23 + K24*h + K25*km)
    private Result Vdm(double hours, double mileage)
    {
        var t = tables.Vdm;
        var row = ExactMatch(mileage, t.Mileages);
        var col = ExactMatch(hours, t.Hours);
        if (row is int r && col is int c) return new(t.Values[r][c], null);
        return new(t.Fixed + t.PerHour * hours + t.PerKm * mileage, null);
    }

    // =IFERROR(INDEX(...), K30 + K31*h + K32*km)
    // Hinweis: In der Excel sucht das zweite MATCH in einem 2D-Bereich (C29:G33) und liefert daher
    // immer #NV – es greift also immer die Regression. Das wird hier bewusst genau so abgebildet.
    private Result VdmLight(double hours, double mileage)
    {
        var t = tables.Vdm;
        var factor = 1 - tables.VdmLightDiscount;
        var m = t.Values.Select(row => row.Select(v => v * factor).ToArray()).ToArray();

        // K31 = SLOPE(BYCOL(Matrix, AVERAGE), Stunden)
        var colAvg = Enumerable.Range(0, t.Hours.Length).Select(c => Average(m.Select(r => r[c]))).ToArray();
        var perHour = Slope(colAvg, t.Hours);
        // K32 = SLOPE(BYROW(Matrix, AVERAGE), km)
        var rowAvg = m.Select(r => Average(r)).ToArray();
        var perKm = Slope(rowAvg, t.Mileages);
        // K30 = AVERAGE(Matrix) - K31*AVERAGE(Stunden) - K32*AVERAGE(km)
        var fixedPart = Average(m.SelectMany(r => r)) - perHour * Average(t.Hours) - perKm * Average(t.Mileages);

        return new(fixedPart + perHour * hours + perKm * mileage, null);
    }

    // =LET(...) in TCO Model!M115
    private Result Traxx(double maintenanceHours, double km, double loadFactor)
    {
        var t = tables.Traxx;
        var dh = Math.Max(maintenanceHours - t.IncludedDieselHours, 0);
        var hm = t.HeavyMaintenance;

        var kmCol = XLookupExactOrNextLarger(km, t.KmBandUpperLimits);
        var dieselRow = XLookupExactOrNextLarger(dh, t.DieselHourThresholds);
        var hmRow = XLookupExactOrNextLarger(maintenanceHours, hm.Hours);
        var hmCol = XLookupExactOrNextLarger(km, hm.Mileages);

        if (kmCol is null || hmCol is null)
        {
            // Wirksame Grenze ist die kleinere der beiden Tabellen (km-Preisbänder / Heavy Maintenance)
            var maxKm = Math.Min(t.KmBandUpperLimits.Max(), hm.Mileages.Max());
            return new(null, $"Laufleistung von {km:N0} km/Jahr liegt außerhalb der Vertragstabelle (max. {maxKm:N0} km).");
        }
        if (dieselRow is null || hmRow is null)
            return new(null, $"Wartungsstunden von {maintenanceHours:N0} h/Jahr liegen außerhalb der Vertragstabelle (max. {hm.Hours.Max():N0} h).");

        var heavy = HeavyMaintenanceMatrix(loadFactor);
        var hmPrice = heavy[hmRow.Value][hmCol.Value];
        var kmPrice = km * t.KmRates[kmCol.Value] / 12;
        var dieselPrice = dh * t.DieselHourRates[dieselRow.Value][kmCol.Value] / 12;
        return new(hmPrice + kmPrice + dieselPrice, null);
    }

    /// <summary>'Maint. Cost Traxx Shunter'!C25:P34 – Rücklage Heavy Maintenance € / Monat.</summary>
    internal double[][] HeavyMaintenanceMatrix(double loadFactor)
    {
        var hm = tables.Traxx.HeavyMaintenance;

        // Fixe Overhauls (E45..E48, E50, E53)
        var r8 = hm.R8Price / hm.R8Months;
        var r10 = hm.R10Price / hm.R10Months;
        var r16 = (hm.R16Price - hm.R8Price) / hm.R16Months;
        var r20 = (hm.R20Price - hm.R10Price) / hm.R20Months;
        var gearbox = hm.GearboxPrice / hm.GearboxMonths;
        var rubbers = hm.RubbersPrice / hm.RubbersMonths;

        // Intervalle je Load factor (F65/G65: IF(LF=15%, …, IF(LF=20%, …, …)))
        var intervals = hm.IntervalsByLoadFactor.FirstOrDefault(i => i.LoadFactor == loadFactor)
                        ?? hm.IntervalsByLoadFactor[^1];

        var rows = new double[hm.Hours.Length][];
        for (var r = 0; r < hm.Hours.Length; r++)
        {
            var h = hm.Hours[r];
            if (!hm.CalculatedHours.Contains(h)) continue;

            // Zeilen 65–77: Intervall in Jahren und monatliche Rücklage
            var altInterval = hm.AlternatorIntervalHours / h;
            var topInterval = intervals.TopEndHours / h;
            var majorInterval = intervals.MajorHours / h;
            var alt = altInterval <= hm.LifetimeYears ? hm.AlternatorPrice / altInterval / 12 : 0;
            var top = topInterval <= hm.LifetimeYears ? hm.TopEndPrice / topInterval / 12 : 0;
            var major = majorInterval <= hm.LifetimeYears ? hm.MajorAdditionalPrice / majorInterval / 12 : 0;

            rows[r] = new double[hm.Mileages.Length];
            for (var c = 0; c < hm.Mileages.Length; c++)
            {
                var wheel = hm.WheelsetOverhaulPrice * hm.WheelsetOverhaulsPerInterval[c] / hm.WheelsetOverhaulMonths[c];
                rows[r][c] = r8 + r10 + r16 + r20 + wheel + gearbox + rubbers + alt + top + major;
            }
        }

        // "extrapolated": AVERAGE(Zeile darüber, Zeile darunter)
        for (var r = 0; r < hm.Hours.Length; r++)
        {
            if (rows[r] is not null) continue;
            rows[r] = new double[hm.Mileages.Length];
            for (var c = 0; c < hm.Mileages.Length; c++)
                rows[r][c] = (rows[r - 1][c] + rows[r + 1][c]) / 2;
        }
        return rows;
    }
}
