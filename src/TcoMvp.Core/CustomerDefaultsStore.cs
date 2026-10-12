using System.Text.Json;
using TcoMvp.Core.Model;

namespace TcoMvp.Core;

/// <summary>
/// Aktuelle Standardannahmen ("Annahmen zurücksetzen"). Ausgangswert ist <c>customerDefaults</c> aus
/// <c>model-data.json</c>; im Admin-Bereich geänderte Werte werden in einer eigenen Datei gespeichert
/// und beim Start wieder geladen. Löschen der Datei stellt die Werte aus <c>model-data.json</c> wieder her.
/// </summary>
public sealed class CustomerDefaultsStore
{
    private readonly ModelData _data;
    private readonly string _path;
    private readonly Lock _lock = new();
    private CustomerInputs _current;

    public CustomerDefaultsStore(ModelData data, string path)
    {
        _data = data;
        _path = path;
        _current = File.Exists(path)
            ? JsonSerializer.Deserialize<CustomerInputs>(File.ReadAllText(path), ModelDataLoader.JsonOptions)
              ?? throw new InvalidOperationException($"'{path}' ist leer.")
            : data.CustomerDefaults;

        var errors = InputValidator.Validate(data, _current);
        if (errors.Count > 0)
            throw new InvalidOperationException(
                $"Gespeicherte Standardannahmen in '{path}' sind ungültig: " +
                string.Join("; ", errors.Select(e => $"{e.Key}: {string.Join(" ", e.Value)}")));
    }

    /// <summary>Werte aus model-data.json (Auslieferungszustand).</summary>
    public CustomerInputs Original => _data.CustomerDefaults;

    public CustomerInputs Current { get { lock (_lock) return _current; } }

    /// <summary>Wurden die Standardannahmen im Admin-Bereich geändert?</summary>
    public bool IsCustomized { get { lock (_lock) return File.Exists(_path); } }

    /// <summary>Prüft und speichert neue Standardannahmen. Liefert Feldfehler; leer = gespeichert.</summary>
    public Dictionary<string, string[]> Save(CustomerInputs defaults)
    {
        var errors = InputValidator.Validate(_data, defaults);
        if (errors.Count > 0) return errors;

        // Nur Loks der Kundenansicht übernehmen, unbekannte Schlüssel verwerfen.
        var visible = _data.Locomotives.Where(l => l.VisibleToCustomer).Select(l => l.Id).ToHashSet();
        var cleaned = defaults with
        {
            Locomotives = defaults.Locomotives.Where(kv => visible.Contains(kv.Key)).ToDictionary(),
        };

        lock (_lock)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(_path))!);
            var tmp = _path + ".tmp";
            File.WriteAllText(tmp, JsonSerializer.Serialize(cleaned, new JsonSerializerOptions(ModelDataLoader.JsonOptions) { WriteIndented = true }));
            File.Move(tmp, _path, overwrite: true);
            _current = cleaned;
        }
        return errors;
    }

    /// <summary>Verwirft die gespeicherten Änderungen (zurück auf model-data.json).</summary>
    public void Reset()
    {
        lock (_lock)
        {
            if (File.Exists(_path)) File.Delete(_path);
            _current = _data.CustomerDefaults;
        }
    }
}
