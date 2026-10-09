using TcoMvp.Core;
using TcoMvp.Core.Model;

namespace TcoMvp.Tests;

/// <summary>
/// Zugriff auf die Testdaten. Sensible Stammdaten und Excel-Fixtures sind per .gitignore
/// ausgeschlossen; Tests, die sie brauchen, werden ohne sie übersprungen.
/// </summary>
internal static class SensitiveData
{
    public static string PublicPath => Path.Combine(AppContext.BaseDirectory, "model-data.json");
    public static string SensitivePath => Path.Combine(AppContext.BaseDirectory, ModelDataLoader.SensitiveFileName);
    public static string ExamplePath => Path.Combine(AppContext.BaseDirectory, "model-data.sensitive.example.json");
    public static string FixturesPath => Path.Combine(AppContext.BaseDirectory, "Fixtures", "excel-scenarios.json");

    public static bool Available => File.Exists(SensitivePath) && File.Exists(FixturesPath);

    private static readonly Lazy<ModelData> Real = new(() => ModelDataLoader.Load(PublicPath, SensitivePath));
    public static ModelData RealModel => Real.Value;

    /// <summary>Unkritische Daten + Beispieldatei mit fiktiven Werten – immer verfügbar.</summary>
    public static ModelData ExampleModel() => ModelDataLoader.Load(PublicPath, ExamplePath);
}

/// <summary>Fact, der übersprungen wird, wenn sensible Daten oder Fixtures fehlen.</summary>
public sealed class SensitiveDataFactAttribute : FactAttribute
{
    public SensitiveDataFactAttribute()
    {
        if (!SensitiveData.Available)
            Skip = $"Sensible Stammdaten ({ModelDataLoader.SensitiveFileName}) oder Excel-Fixtures fehlen – nicht im Repository.";
    }
}

/// <summary>Theory, die übersprungen wird, wenn sensible Daten oder Fixtures fehlen.</summary>
public sealed class SensitiveDataTheoryAttribute : TheoryAttribute
{
    public SensitiveDataTheoryAttribute()
    {
        if (!SensitiveData.Available)
            Skip = $"Sensible Stammdaten ({ModelDataLoader.SensitiveFileName}) oder Excel-Fixtures fehlen – nicht im Repository.";
    }
}
