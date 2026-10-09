namespace TcoMvp.Core.Calculation;

/// <summary>
/// Vollständige Berechnung einer Lok – entspricht einer Spalte (H..M) im Reiter "TCO Model".
/// Enthält interne Werte (Leasingrate etc.) und darf daher nie direkt an den Kunden gehen.
/// </summary>
public sealed class LocoBreakdown
{
    public required string LocoId { get; init; }

    // Allocation of hours and kilometers (Zeilen 64–77)
    public double DieselKm { get; init; }            // 65
    public double ShuntingKm { get; init; }          // 66
    public double MainlineKm { get; init; }          // 67
    public double ElectricKm { get; init; }          // 68
    public double RollingHours { get; init; }        // 70
    public double DieselRollingHours { get; init; }  // 71
    public double ElectricRollingHours { get; init; }// 72
    public double ParkingHours { get; init; }        // 74
    public double DieselParkingHours { get; init; }  // 75
    public double StartStopParkingHours { get; init; } // 76
    public double ElectricParkingHours { get; init; }  // 77

    // Vehicle cost (Zeilen 90–94), € / Monat
    public double DryLease { get; init; }            // 91
    public double EtcsStepUp { get; init; }          // 92
    public double Replacement { get; init; }         // 94
    public double VehicleCost { get; init; }         // 90

    // Energy cost (Zeilen 96–113), € / Monat
    public double DieselCostShunting { get; init; }  // 98
    public double DieselCostMainline { get; init; }  // 101
    public double DieselCostParked { get; init; }    // 104
    public double DieselCost { get; init; }          // 97
    public double ElectricityCostRolling { get; init; } // 108
    public double ElectricityCostParked { get; init; }  // 111
    public double ElectricityCost { get; init; }     // 107
    public double EnergyCost { get; init; }          // 96

    // Maintenance (Zeilen 115–120)
    public double DieselHoursPerYear { get; init; }  // 117
    public double MaintenanceHours { get; init; }    // 116
    /// <summary>€ / Monat; null, wenn die Excel hier #NV liefern würde.</summary>
    public double? MaintenanceCost { get; init; }    // 115
    public string? MaintenanceError { get; init; }

    /// <summary>Gesamtkosten € / Monat (Zeile 123); null bei Fehler.</summary>
    public double? TotalCost { get; init; }
}

public sealed class TcoResult
{
    public required IReadOnlyList<LocoBreakdown> Locomotives { get; init; }
    public required string ReferenceLocoId { get; init; }

    public LocoBreakdown this[string id] => Locomotives.First(l => l.LocoId == id);

    /// <summary>Kostenunterschied zur Referenzlok (Zeile 124); null, wenn eine Seite nicht berechenbar ist.</summary>
    public double? VarianceToReference(string id)
    {
        var self = this[id].TotalCost;
        var reference = this[ReferenceLocoId].TotalCost;
        return self is null || reference is null ? null : self - reference;
    }
}
