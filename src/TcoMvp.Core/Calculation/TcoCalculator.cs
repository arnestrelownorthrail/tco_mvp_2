using TcoMvp.Core.Model;

namespace TcoMvp.Core.Calculation;

/// <summary>
/// Übersetzung des Reiters "TCO Model" (Zeilen 56–124). Rechenreihenfolge und
/// Zwischenwerte entsprechen den Excel-Formeln, damit die Ergebnisse identisch sind.
/// </summary>
public sealed class TcoCalculator(ModelData data)
{
    private readonly MaintenanceCalculator _maintenance = new(data.Maintenance);

    public ModelData Data => data;

    public TcoResult Calculate(CustomerInputs inputs)
    {
        var results = data.Locomotives.Select(loco => CalculateLoco(loco, inputs)).ToList();
        return new TcoResult { Locomotives = results, ReferenceLocoId = data.ReferenceLocoId };
    }

    private LocoBreakdown CalculateLoco(LocoConfig loco, CustomerInputs input)
    {
        var li = ResolveLocoInputs(loco, input);

        // Usage profile (56–60)
        var mileage = input.MileageKmPerYear;
        var hours = input.OperatingHoursPerYear;
        var rollingShare = input.RollingShare;
        var electricShare = loco.ElectricCapable ? input.ElectricShare : 0;
        var startStop = loco.StartStopEditable ? li.StartStopShare ?? 0 : loco.FixedStartStopShare;
        var mainlineShare = 1 - input.ShuntingShare; // G20 = 1 - G19

        // Allocation of hours and kilometers (64–77)
        var dieselKm = mileage * (1 - electricShare);
        var shuntingKm = dieselKm * input.ShuntingShare;
        var mainlineKm = dieselKm * mainlineShare;
        var electricKm = mileage * electricShare;

        var rollingHours = hours * rollingShare;
        var electricRollingHours = rollingHours * electricShare;
        var dieselRollingHours = rollingHours - electricRollingHours;

        var parkingHours = hours - rollingHours;
        var startStopHours = parkingHours * startStop;
        var electricParkingHours = (hours - rollingHours) * electricShare;
        var dieselParkingHours = parkingHours - startStopHours - electricParkingHours;

        // Vehicle cost (90–94)
        var replacement = loco.DryLeasePerMonth * (1 - loco.Availability);
        var vehicleCost = loco.DryLeasePerMonth + replacement + loco.EtcsStepUpPerMonth;

        // Energy cost (96–113)
        var kwhPerKm = loco.ElectricCapable ? li.KwhPerKm ?? 0 : 0;
        var kwhPerHourParked = loco.ElectricCapable ? li.KwhPerHourParked ?? 0 : 0;

        var dieselShunting = shuntingKm * li.DieselShuntingLitersPerKm / 12 * input.DieselPricePerLiter;
        var dieselMainline = mainlineKm * li.DieselMainlineLitersPerKm / 12 * input.DieselPricePerLiter;
        var dieselParked = dieselParkingHours * li.DieselParkedLitersPerHour / 12 * input.DieselPricePerLiter;
        var dieselCost = dieselShunting + dieselParked + dieselMainline;

        var elecRolling = electricKm * kwhPerKm / 12 * input.ElectricityPricePerKwh;
        var elecParked = electricParkingHours * kwhPerHourParked / 12 * input.ElectricityPricePerKwh;
        var elecCost = elecRolling + elecParked;
        var energyCost = dieselCost + elecCost;

        // Maintenance (115–120)
        var dieselHours = dieselRollingHours + dieselParkingHours;
        var maintenanceHours = dieselHours * (loco.DoubleEngineImpact == 0 ? 1 : loco.DoubleEngineImpact);
        var maintenance = _maintenance.Calculate(loco, maintenanceHours, mileage);

        return new LocoBreakdown
        {
            LocoId = loco.Id,
            DieselKm = dieselKm,
            ShuntingKm = shuntingKm,
            MainlineKm = mainlineKm,
            ElectricKm = electricKm,
            RollingHours = rollingHours,
            DieselRollingHours = dieselRollingHours,
            ElectricRollingHours = electricRollingHours,
            ParkingHours = parkingHours,
            DieselParkingHours = dieselParkingHours,
            StartStopParkingHours = startStopHours,
            ElectricParkingHours = electricParkingHours,
            DryLease = loco.DryLeasePerMonth,
            EtcsStepUp = loco.EtcsStepUpPerMonth,
            Replacement = replacement,
            VehicleCost = vehicleCost,
            DieselCostShunting = dieselShunting,
            DieselCostMainline = dieselMainline,
            DieselCostParked = dieselParked,
            DieselCost = dieselCost,
            ElectricityCostRolling = elecRolling,
            ElectricityCostParked = elecParked,
            ElectricityCost = elecCost,
            EnergyCost = energyCost,
            DieselHoursPerYear = dieselHours,
            MaintenanceHours = maintenanceHours,
            MaintenanceCost = maintenance.Cost,
            MaintenanceError = maintenance.Error,
            // Zeile 123: =H115+H90+H96
            TotalCost = maintenance.Cost is double m ? m + vehicleCost + energyCost : null,
        };
    }

    private static LocoInputs ResolveLocoInputs(LocoConfig loco, CustomerInputs input)
    {
        if (!loco.VisibleToCustomer)
            return loco.BackendInputs ?? throw new InvalidOperationException($"Lok '{loco.Id}' hat keine Backend-Verbrauchswerte.");
        return input.Locomotives.TryGetValue(loco.Id, out var li)
            ? li
            : throw new ArgumentException($"Eingaben für Lok '{loco.Id}' fehlen.");
    }
}
