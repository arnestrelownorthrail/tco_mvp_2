using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using TcoMvp.Core.Model;

namespace TcoMvp.Core;

/// <summary>
/// Lädt die Modelldaten aus zwei Dateien:
/// <list type="bullet">
/// <item><c>model-data.json</c> – unkritisch (Loks der Kundenansicht, Standardannahmen), liegt im Repo.</item>
/// <item><c>model-data.sensitive.json</c> – firmeninterne Stammdaten (Leasing, Verfügbarkeit, Wartungs- und
/// Vertragspreise, ausgeblendete Loks); per .gitignore ausgeschlossen.</item>
/// </list>
/// Loks werden über ihre <c>id</c> zusammengeführt; Loks, die nur in der sensiblen Datei stehen, werden angehängt.
/// </summary>
public static class ModelDataLoader
{
    public const string SensitiveFileName = "model-data.sensitive.json";

    public static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        ReadCommentHandling = JsonCommentHandling.Skip,
        Converters = { new JsonStringEnumConverter() },
    };

    /// <param name="publicPath">Pfad zu model-data.json</param>
    /// <param name="sensitivePath">Pfad zur sensiblen Datei; Standard: model-data.sensitive.json im selben Ordner.</param>
    public static ModelData Load(string publicPath, string? sensitivePath = null)
    {
        sensitivePath ??= Path.Combine(Path.GetDirectoryName(publicPath) ?? ".", SensitiveFileName);
        if (!File.Exists(sensitivePath))
            throw new FileNotFoundException(
                $"Sensible Stammdaten fehlen: '{sensitivePath}'. Die Datei ist nicht im Repository enthalten. " +
                "Echte Datei beim Modellverantwortlichen anfordern oder zum Ausprobieren " +
                "'model-data.sensitive.example.json' (fiktive Werte) dorthin kopieren.", sensitivePath);

        var merged = ReadNode(publicPath);
        Merge(merged, ReadNode(sensitivePath));

        var data = merged.Deserialize<ModelData>(JsonOptions)
                   ?? throw new InvalidOperationException("Modelldaten sind leer.");
        Validate(data);
        return data;
    }

    private static JsonObject ReadNode(string path) =>
        JsonNode.Parse(File.ReadAllText(path), documentOptions: new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip })?.AsObject()
        ?? throw new InvalidOperationException($"'{path}' ist leer.");

    private static void Merge(JsonObject target, JsonObject source)
    {
        foreach (var (key, value) in source.ToList())
        {
            if (key.StartsWith('_')) continue; // Kommentarfelder
            if (key == "locomotives" && target[key] is JsonArray targetLocos && value is JsonArray sourceLocos)
            {
                foreach (var loco in sourceLocos.ToList())
                {
                    var id = (string?)loco?["id"];
                    var existing = targetLocos.FirstOrDefault(t => (string?)t?["id"] == id)?.AsObject();
                    if (existing is null) { targetLocos.Add(loco!.DeepClone()); continue; }
                    foreach (var (k, v) in loco!.AsObject()) existing[k] = v?.DeepClone();
                }
            }
            else
            {
                target[key] = value?.DeepClone();
            }
        }
    }

    private static void Validate(ModelData data)
    {
        // Jede Lok braucht ihre kommerziellen Daten aus der sensiblen Datei.
        var missing = data.Locomotives.Where(l => l.DryLeasePerMonth <= 0 || l.Availability <= 0).Select(l => l.Id).ToList();
        if (missing.Count > 0)
            throw new InvalidOperationException(
                $"Für folgende Loks fehlen Stammdaten in '{SensitiveFileName}': {string.Join(", ", missing)}");
        if (data.Locomotives.All(l => l.Id != data.ReferenceLocoId))
            throw new InvalidOperationException($"Referenzlok '{data.ReferenceLocoId}' ist nicht definiert.");
    }
}
