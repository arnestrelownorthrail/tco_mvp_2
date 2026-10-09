namespace TcoMvp.Core.Model;

/// <summary>
/// Eingaben der Kundenansicht (Excel-Reiter "Betriebskosten", gelbe Felder).
/// Zellbezüge in Klammern verweisen auf "Betriebskosten" bzw. "TCO Model".
/// </summary>
public sealed record CustomerInputs
{
    /// <summary>Kilometer / Jahr (Betriebskosten!F7 → TCO Model!G10)</summary>
    public double MileageKmPerYear { get; init; }

    /// <summary>Stunden (aufgerüstet) / Jahr (F8 → G11)</summary>
    public double OperatingHoursPerYear { get; init; }

    /// <summary>davon rollende Stunden, 0..1 (F9 → G12)</summary>
    public double RollingShare { get; init; }

    /// <summary>(Möglicher) elektrifizierter Streckenanteil, 0..1 (F14 → G17)</summary>
    public double ElectricShare { get; init; }

    /// <summary>Anteil Rangieren am Diesel-Streckenanteil, 0..1 (F17 → G19)</summary>
    public double ShuntingShare { get; init; }

    /// <summary>Dieselpreis € / Liter (F29 → G32)</summary>
    public double DieselPricePerLiter { get; init; }

    /// <summary>Strompreis € / kWh (F30 → G33)</summary>
    public double ElectricityPricePerKwh { get; init; }

    /// <summary>Lokspezifische Eingaben, Schlüssel = Lok-Id (z.B. "g1206").</summary>
    public Dictionary<string, LocoInputs> Locomotives { get; init; } = new();
}

/// <summary>Lokspezifische Eingaben (Spalten G..K in "Betriebskosten").</summary>
public sealed record LocoInputs
{
    /// <summary>Parkende Stunden ohne laufenden Dieselmotor (Start-Stop), 0..1 (Zeile 12)</summary>
    public double? StartStopShare { get; init; }

    /// <summary>Diesel pro km, Rangierbetrieb (Zeile 22)</summary>
    public double DieselShuntingLitersPerKm { get; init; }

    /// <summary>Diesel pro km, Streckenbetrieb (Zeile 23)</summary>
    public double DieselMainlineLitersPerKm { get; init; }

    /// <summary>Diesel pro geparkter Stunde mit laufendem Motor (Zeile 24)</summary>
    public double DieselParkedLitersPerHour { get; init; }

    /// <summary>kWh / km (Zeile 26)</summary>
    public double? KwhPerKm { get; init; }

    /// <summary>kWh / Stunde, parkend (Zeile 27)</summary>
    public double? KwhPerHourParked { get; init; }
}
