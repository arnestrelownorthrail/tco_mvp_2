using TcoMvp.Core;
using TcoMvp.Core.Model;

namespace TcoMvp.Tests;

public class InputValidatorTests
{
    private static readonly ModelData Data = SensitiveData.ExampleModel();
    private static CustomerInputs Defaults => Data.CustomerDefaults;

    private static CustomerInputs WithLoco(string id, Func<LocoInputs, LocoInputs> change)
    {
        var locos = new Dictionary<string, LocoInputs>(Defaults.Locomotives) { [id] = change(Defaults.Locomotives[id]) };
        return Defaults with { Locomotives = locos };
    }

    [Fact]
    public void Default_assumptions_are_valid()
        => Assert.Empty(InputValidator.Validate(Data, Defaults));

    [Theory]
    [InlineData(-1)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(1e9 + 1)]
    [InlineData(1e200)]
    public void Mileage_must_be_finite_and_not_negative(double km)
        => Assert.Contains(nameof(CustomerInputs.MileageKmPerYear), InputValidator.Validate(Data, Defaults with { MileageKmPerYear = km }).Keys);

    [Theory]
    [InlineData(-0.01)]
    [InlineData(1.01)]
    public void Shares_must_be_between_0_and_100_percent(double share)
    {
        var errors = InputValidator.Validate(Data, Defaults with { RollingShare = share, ElectricShare = share, ShuntingShare = share });
        Assert.Contains(nameof(CustomerInputs.RollingShare), errors.Keys);
        Assert.Contains(nameof(CustomerInputs.ElectricShare), errors.Keys);
        Assert.Contains(nameof(CustomerInputs.ShuntingShare), errors.Keys);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void Share_boundaries_are_allowed(double share)
        => Assert.Empty(InputValidator.Validate(Data, Defaults with { RollingShare = share, ElectricShare = share, ShuntingShare = share }));

    [Fact]
    public void Prices_must_be_greater_than_zero()
    {
        var errors = InputValidator.Validate(Data, Defaults with { DieselPricePerLiter = 0, ElectricityPricePerKwh = 0 });
        Assert.Contains(nameof(CustomerInputs.DieselPricePerLiter), errors.Keys);
        Assert.Contains(nameof(CustomerInputs.ElectricityPricePerKwh), errors.Keys);
    }

    [Fact]
    public void Prices_must_not_be_negative()
    {
        var errors = InputValidator.Validate(Data, Defaults with { DieselPricePerLiter = -1, ElectricityPricePerKwh = -0.1 });
        Assert.Equal(2, errors.Count);
    }

    [Fact]
    public void Missing_locomotive_inputs_are_reported_per_loco()
    {
        var errors = InputValidator.Validate(Data, Defaults with { Locomotives = new() });
        Assert.Equal(5, errors.Count);
        Assert.Contains("Locomotives.traxx", errors.Keys);
    }

    [Fact]
    public void Null_locomotive_inputs_are_reported_not_crashed()
    {
        Assert.Contains("Locomotives", InputValidator.Validate(Data, Defaults with { Locomotives = null! }).Keys);
        Assert.Contains("Locomotives.traxx",
            InputValidator.Validate(Data, WithLoco("traxx", _ => null!)).Keys);
    }

    [Fact]
    public void Hidden_locomotives_need_no_customer_inputs()
        => Assert.DoesNotContain(InputValidator.Validate(Data, Defaults).Keys, k => k.Contains("vdm-light"));

    [Fact]
    public void Start_stop_is_only_checked_where_it_is_editable()
    {
        // G1206 hat kein Start-Stop-Feld → ein ungültiger Wert dort spielt keine Rolle
        Assert.Empty(InputValidator.Validate(Data, WithLoco("g1206", l => l with { StartStopShare = 5 })));
        Assert.Contains("Locomotives.traxx.StartStopShare",
            InputValidator.Validate(Data, WithLoco("traxx", l => l with { StartStopShare = 1.5 })).Keys);
    }

    [Fact]
    public void Electric_consumption_is_required_only_for_electric_locos()
    {
        Assert.Contains("Locomotives.vdm.KwhPerKm",
            InputValidator.Validate(Data, WithLoco("vdm", l => l with { KwhPerKm = null })).Keys);
        Assert.Empty(InputValidator.Validate(Data, WithLoco("g1206", l => l with { KwhPerKm = null })));
    }

    [Fact]
    public void Negative_consumption_is_rejected()
        => Assert.Contains("Locomotives.de18-3a.DieselShuntingLitersPerKm",
            InputValidator.Validate(Data, WithLoco("de18-3a", l => l with { DieselShuntingLitersPerKm = -2 })).Keys);
}
