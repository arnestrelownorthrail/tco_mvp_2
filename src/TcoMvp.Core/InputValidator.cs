using TcoMvp.Core.Model;

namespace TcoMvp.Core;

/// <summary>Plausibilitätsprüfung der Kundeneingaben. Liefert Feldname → Fehlermeldungen.</summary>
public static class InputValidator
{
    public static Dictionary<string, string[]> Validate(ModelData data, CustomerInputs input)
    {
        var errors = new Dictionary<string, string[]>();
        void NonNegative(string field, double? v) { if (v is null || v < 0 || !double.IsFinite(v.Value)) errors[field] = ["Muss eine Zahl ≥ 0 sein."]; }
        void Share(string field, double? v) { if (v is null || v < 0 || v > 1 || !double.IsFinite(v.Value)) errors[field] = ["Muss zwischen 0 % und 100 % liegen."]; }

        NonNegative(nameof(input.MileageKmPerYear), input.MileageKmPerYear);
        NonNegative(nameof(input.OperatingHoursPerYear), input.OperatingHoursPerYear);
        Share(nameof(input.RollingShare), input.RollingShare);
        Share(nameof(input.ElectricShare), input.ElectricShare);
        Share(nameof(input.ShuntingShare), input.ShuntingShare);
        NonNegative(nameof(input.DieselPricePerLiter), input.DieselPricePerLiter);
        NonNegative(nameof(input.ElectricityPricePerKwh), input.ElectricityPricePerKwh);

        foreach (var loco in data.Locomotives.Where(l => l.VisibleToCustomer))
        {
            if (!input.Locomotives.TryGetValue(loco.Id, out var li))
            {
                errors[$"Locomotives.{loco.Id}"] = ["Eingaben fehlen."];
                continue;
            }
            var p = $"Locomotives.{loco.Id}.";
            NonNegative(p + nameof(li.DieselShuntingLitersPerKm), li.DieselShuntingLitersPerKm);
            NonNegative(p + nameof(li.DieselMainlineLitersPerKm), li.DieselMainlineLitersPerKm);
            NonNegative(p + nameof(li.DieselParkedLitersPerHour), li.DieselParkedLitersPerHour);
            if (loco.StartStopEditable) Share(p + nameof(li.StartStopShare), li.StartStopShare);
            if (loco.ElectricCapable)
            {
                NonNegative(p + nameof(li.KwhPerKm), li.KwhPerKm);
                NonNegative(p + nameof(li.KwhPerHourParked), li.KwhPerHourParked);
            }
        }
        return errors;
    }
}
