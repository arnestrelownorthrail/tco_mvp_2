namespace TcoMvp.Core.Calculation;

/// <summary>Nachbildung der in der Excel verwendeten Funktionen.</summary>
internal static class ExcelFunctions
{
    /// <summary>
    /// XLOOKUP(x, array, index, , 1): exakter Treffer oder nächstgrößerer Wert.
    /// Liefert den 0-basierten Index oder null (#NV).
    /// </summary>
    public static int? XLookupExactOrNextLarger(double x, IReadOnlyList<double> array)
    {
        int? best = null;
        for (var i = 0; i < array.Count; i++)
        {
            if (array[i] == x) return i;
            if (array[i] > x && (best is null || array[i] < array[best.Value])) best = i;
        }
        return best;
    }

    /// <summary>XLOOKUP / MATCH mit exakter Übereinstimmung. Liefert den Index oder null (#NV).</summary>
    public static int? ExactMatch(double x, IReadOnlyList<double> array)
    {
        for (var i = 0; i < array.Count; i++)
            if (array[i] == x) return i;
        return null;
    }

    public static double Average(IEnumerable<double> values) => values.Average();

    /// <summary>SLOPE(known_y, known_x)</summary>
    public static double Slope(IReadOnlyList<double> ys, IReadOnlyList<double> xs)
    {
        var mx = xs.Average();
        var my = ys.Average();
        double num = 0, den = 0;
        for (var i = 0; i < xs.Count; i++)
        {
            num += (xs[i] - mx) * (ys[i] - my);
            den += (xs[i] - mx) * (xs[i] - mx);
        }
        return num / den;
    }
}
