using TcoMvp.Core.Calculation;

namespace TcoMvp.Tests;

/// <summary>Nachgebildete Excel-Funktionen (XLOOKUP, MATCH, SLOPE).</summary>
public class ExcelFunctionsTests
{
    private static readonly double[] Bands = [37500, 42499, 47499];

    [Theory]
    [InlineData(0, 0)]        // kleiner als alles → erstes Band
    [InlineData(37500, 0)]    // exakter Treffer
    [InlineData(37501, 1)]    // nächstgrößerer Wert
    [InlineData(42499, 1)]
    [InlineData(47499, 2)]
    public void XLookup_match_mode_1_returns_exact_or_next_larger(double x, int expected)
        => Assert.Equal(expected, ExcelFunctions.XLookupExactOrNextLarger(x, Bands));

    [Fact]
    public void XLookup_returns_null_like_NA_when_value_exceeds_all()
        => Assert.Null(ExcelFunctions.XLookupExactOrNextLarger(47500, Bands));

    [Fact]
    public void XLookup_finds_smallest_larger_value_in_unsorted_array()
        => Assert.Equal(2, ExcelFunctions.XLookupExactOrNextLarger(15, new double[] { 30, 10, 20 }));

    [Fact]
    public void ExactMatch_finds_only_exact_values()
    {
        double[] hours = [2000, 2500, 3000];
        Assert.Equal(1, ExcelFunctions.ExactMatch(2500, hours));
        Assert.Null(ExcelFunctions.ExactMatch(2500.0001, hours));
        Assert.Null(ExcelFunctions.ExactMatch(1000, hours));
    }

    [Fact]
    public void Slope_matches_Excel_SLOPE()
    {
        // Excel: =SLOPE({2;4;5;4;5};{1;2;3;4;5}) = 0,6
        Assert.Equal(0.6, ExcelFunctions.Slope([2, 4, 5, 4, 5], [1, 2, 3, 4, 5]), 12);
        Assert.Equal(-2, ExcelFunctions.Slope([10, 8, 6], [1, 2, 3]), 12);
    }
}
