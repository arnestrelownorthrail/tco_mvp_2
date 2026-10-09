namespace TcoMvp.Core.Model;

/// <summary>
/// Firmeninterne Stammdaten ("Backend"): alles, was in der Excel in den ausgeblendeten
/// Reitern "TCO Model", "Maint. Cost G1206 DE18 VDM" und "Maint. Cost Traxx Shunter" steht.
/// Diese Daten verlassen niemals den Server.
/// </summary>
public sealed class ModelData
{
    /// <summary>Lok, gegen die alle anderen verglichen werden ("Variance to Traxx shunter").</summary>
    public string ReferenceLocoId { get; init; } = "traxx";

    /// <summary>Standardannahmen (ausgeblendeter Reiter "Standardannahmen").</summary>
    public CustomerInputs CustomerDefaults { get; init; } = new();

    public List<LocoConfig> Locomotives { get; init; } = new();

    public MaintenanceTables Maintenance { get; init; } = new();
}

public enum MaintenanceModel
{
    /// <summary>G1206: exakter Treffer in Stundentabelle, sonst Regression (TCO Model!H115).</summary>
    G1206,
    /// <summary>DE18: exakter Treffer in Stundentabelle, sonst Regression (TCO Model!I115/J115).</summary>
    De18,
    /// <summary>VDM: exakter Treffer in km×Stunden-Matrix, sonst Regression (TCO Model!K115).</summary>
    Vdm,
    /// <summary>VDM light: Regression über abgeleitete VDM-Matrix (TCO Model!L115).</summary>
    VdmLight,
    /// <summary>Traxx Shunter: Vertragspreise km-Rate + Dieselstunden + Heavy Maintenance (TCO Model!M115).</summary>
    Traxx,
}

public sealed class LocoConfig
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public string Subtitle { get; init; } = "";

    /// <summary>In der Kundenansicht sichtbar? (VDM light ist in der Excel ausgeblendet.)</summary>
    public bool VisibleToCustomer { get; init; } = true;

    /// <summary>Elektrischer Betrieb möglich → nutzt den elektrifizierten Streckenanteil (TCO Model!Zeile 59).</summary>
    public bool ElectricCapable { get; init; }

    /// <summary>Start-Stop-Anteil ist Kundeneingabe (sonst fester Wert aus <see cref="FixedStartStopShare"/>).</summary>
    public bool StartStopEditable { get; init; }

    /// <summary>Fester Start-Stop-Anteil, wenn nicht editierbar (TCO Model!Zeile 60).</summary>
    public double FixedStartStopShare { get; init; }

    /// <summary>Verbrauchswerte für Loks, die nicht in der Kundenansicht stehen (z.B. VDM light, TCO Model!L25:L30).</summary>
    public LocoInputs? BackendInputs { get; init; }

    /// <summary>Dry lease € / Monat (TCO Model!Zeile 36)</summary>
    public double DryLeasePerMonth { get; init; }

    /// <summary>Step-up for ETCS retrofit € / Monat (Zeile 37)</summary>
    public double EtcsStepUpPerMonth { get; init; }

    /// <summary>Verfügbarkeit 0..1 (Zeile 38)</summary>
    public double Availability { get; init; }

    /// <summary>Double engine impact (Zeile 42, nur Traxx; 0 = keine Wirkung)</summary>
    public double DoubleEngineImpact { get; init; }

    /// <summary>Load factor (Zeile 41, nur Traxx: 0.15 / 0.20 / 0.25)</summary>
    public double LoadFactor { get; init; }

    public MaintenanceModel Maintenance { get; init; }
}

public sealed class MaintenanceTables
{
    public HoursRegressionTable G1206 { get; init; } = new();
    public HoursRegressionTable De18 { get; init; } = new();
    public VdmTable Vdm { get; init; } = new();
    /// <summary>Abschlag VDM light gegenüber VDM ('Maint. Cost G1206 DE18 VDM'!D28)</summary>
    public double VdmLightDiscount { get; init; }
    public TraxxTables Traxx { get; init; } = new();
}

/// <summary>Wartungskosten je Dieselstunden (G1206 / DE18), Werte € / Monat.</summary>
public sealed class HoursRegressionTable
{
    public double[] Hours { get; init; } = [];
    public double[] Values { get; init; } = [];
    public double Fixed { get; init; }
    public double PerHour { get; init; }
}

/// <summary>VDM-Matrix: Zeilen = Laufleistung, Spalten = Dieselstunden, Werte € / Monat.</summary>
public sealed class VdmTable
{
    public double[] Mileages { get; init; } = [];
    public double[] Hours { get; init; } = [];
    public double[][] Values { get; init; } = [];
    public double Fixed { get; init; }
    public double PerHour { get; init; }
    public double PerKm { get; init; }
}

/// <summary>Traxx-Shunter-Vertragsdaten (Reiter "Maint. Cost Traxx Shunter").</summary>
public sealed class TraxxTables
{
    /// <summary>km-Obergrenzen der Preisbänder (LET kmArr).</summary>
    public double[] KmBandUpperLimits { get; init; } = [];
    /// <summary>Kilometerpreis € / km je Band (C10:P10).</summary>
    public double[] KmRates { get; init; } = [];

    /// <summary>Schwellen der zusätzlichen Dieselstunden über Basis (LET dhArr).</summary>
    public double[] DieselHourThresholds { get; init; } = [];
    /// <summary>Preis je zusätzlicher Dieselstunde [Schwelle][km-Band] (C12:P17).</summary>
    public double[][] DieselHourRates { get; init; } = [];
    /// <summary>Im Grundpreis enthaltene Dieselstunden (B12 = 500).</summary>
    public double IncludedDieselHours { get; init; }

    public HeavyMaintenanceData HeavyMaintenance { get; init; } = new();
}

/// <summary>
/// Rücklage Heavy Maintenance (C25:P34) – wird aus den Overhaul-Daten (Zeilen 37–77) berechnet,
/// weil sie vom Load factor abhängt.
/// </summary>
public sealed class HeavyMaintenanceData
{
    /// <summary>Kilometer pro Jahr der Matrixspalten (C24:P24 bzw. E38:R38).</summary>
    public double[] Mileages { get; init; } = [];
    /// <summary>Dieselstunden pro Jahr der Matrixzeilen (B25:B34).</summary>
    public double[] Hours { get; init; } = [];
    /// <summary>Zeilen, die direkt berechnet werden; alle anderen sind Mittelwert der Nachbarzeilen ("extrapolated").</summary>
    public double[] CalculatedHours { get; init; } = [];

    public double R8Price { get; init; }
    public double R8Months { get; init; }
    public double R10Price { get; init; }
    public double R10Months { get; init; }
    public double R16Price { get; init; }
    public double R16Months { get; init; }
    public double R20Price { get; init; }
    public double R20Months { get; init; }

    public double WheelsetOverhaulPrice { get; init; }
    /// <summary>OVH-Monate je km-Spalte (E40:R40).</summary>
    public double[] WheelsetOverhaulMonths { get; init; } = [];
    /// <summary>Radsatz-Overhauls je OVH-Intervall je km-Spalte (1 bzw. 2, Zeile 49).</summary>
    public double[] WheelsetOverhaulsPerInterval { get; init; } = [];

    public double GearboxPrice { get; init; }
    public double GearboxMonths { get; init; }
    public double RubbersPrice { get; init; }
    public double RubbersMonths { get; init; }

    public double AlternatorIntervalHours { get; init; }
    public double AlternatorPrice { get; init; }
    public double TopEndPrice { get; init; }
    /// <summary>Major OVH ohne Top End ('Maint. Cost Traxx Shunter'!D60 − D57).</summary>
    public double MajorAdditionalPrice { get; init; }

    /// <summary>Intervalle (Std.) für Top End / Major je Load factor (C57:C62). Fallback = letzter Eintrag.</summary>
    public List<LoadFactorIntervals> IntervalsByLoadFactor { get; init; } = new();

    /// <summary>Nur Maßnahmen mit Intervall ≤ dieser Jahreszahl werden umgelegt ("angenommene Lebensdauer 30 Jahre").</summary>
    public double LifetimeYears { get; init; }
}

public sealed class LoadFactorIntervals
{
    public double LoadFactor { get; init; }
    public double TopEndHours { get; init; }
    public double MajorHours { get; init; }
}
