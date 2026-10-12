using TcoMvp.Core.Calculation;
using TcoMvp.Core.Model;

namespace TcoMvp.Core;

/// <summary>
/// Kundenansicht – entspricht dem Reiter "Betriebskosten" der Vertriebsversion.
/// Enthält bewusst nur die dort sichtbaren Werte (Miete und Instandhaltung werden
/// zusammengefasst, damit keine Leasingraten oder Wartungspreise einzeln sichtbar sind).
/// </summary>
public static class CustomerView
{
    public sealed record LocoInfo(
        string Id, string Name, string Subtitle, bool StartStopEditable, bool ElectricCapable, bool IsReference);

    public sealed record ModelInfo(IReadOnlyList<LocoInfo> Locomotives, CustomerInputs Defaults);

    public sealed record LocoCosts(
        string Id,
        double? RentAndMaintenance,   // Zeile 34
        double EnergyCost,            // Zeile 35
        double? TotalOperatingCost,   // Zeile 36
        double? DifferenceToReference,// Zeile 37
        string? Error);

    public sealed record Result(IReadOnlyList<LocoCosts> Locomotives);

    /// <param name="defaults">Aktuelle Standardannahmen; Standard: die aus model-data.json.</param>
    public static ModelInfo Describe(ModelData data, CustomerInputs? defaults = null) => new(
        data.Locomotives
            .Where(l => l.VisibleToCustomer)
            .Select(l => new LocoInfo(l.Id, l.Name, l.Subtitle, l.StartStopEditable, l.ElectricCapable, l.Id == data.ReferenceLocoId))
            .ToList(),
        defaults ?? data.CustomerDefaults);

    public static Result From(ModelData data, TcoResult tco)
    {
        double? Total(LocoBreakdown b) => b.MaintenanceCost is double m ? b.VehicleCost + m + b.EnergyCost : null;

        var referenceTotal = Total(tco[data.ReferenceLocoId]);
        var rows = data.Locomotives
            .Where(l => l.VisibleToCustomer)
            .Select(l =>
            {
                var b = tco[l.Id];
                // G34 = H46 + H48 (Vehicle + Maintenance), G36 = SUM(G34:G35), G37 = G36 - $K$36
                var rent = b.MaintenanceCost is double m ? b.VehicleCost + m : (double?)null;
                var total = rent is double r ? r + b.EnergyCost : (double?)null;
                double? diff = l.Id == data.ReferenceLocoId || total is null || referenceTotal is null
                    ? null
                    : total - referenceTotal;
                return new LocoCosts(l.Id, rent, b.EnergyCost, total, diff, b.MaintenanceError);
            })
            .ToList();
        return new Result(rows);
    }
}
