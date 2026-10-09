# TCO-Rechner Traxx Shunter – Web-MVP

Web-Version der Excel `Traxx Shunter_TCO model_26.08`:

| Excel | Web-App |
|---|---|
| Vertriebsversion, Reiter **Betriebskosten** (Kundenansicht) | `src/TcoMvp.Web/wwwroot` (Browser) |
| Reiter **TCO Model**, Zeilen 56–124 | `src/TcoMvp.Core/Calculation/TcoCalculator.cs` |
| Reiter **Maint. Cost G1206 DE18 VDM** / **Maint. Cost Traxx Shunter** | `src/TcoMvp.Core/Calculation/MaintenanceCalculator.cs` |
| Stammdaten (Leasing, Verfügbarkeit, Wartungstabellen …) – **sensibel** | `src/TcoMvp.Web/Data/model-data.sensitive.json` (nicht im Repo) |
| Reiter **Standardannahmen** + Makro „Annahmen zurücksetzen“ | `customerDefaults` in `model-data.json`, Button im UI |

## Sensible und unkritische Daten

| Datei | Inhalt | Im Repo? |
|---|---|---|
| `src/TcoMvp.Web/Data/model-data.json` | Loks der Kundenansicht (Name, sichtbare Felder), Standardannahmen | ja |
| `src/TcoMvp.Web/Data/model-data.sensitive.json` | Leasingraten, ETCS-Aufschlag, Verfügbarkeit, Double engine impact, Load factor, ausgeblendete Lok „VDM light“, alle Wartungs- und Vertragstabellen | **nein** (`.gitignore`) |
| `src/TcoMvp.Web/Data/model-data.sensitive.example.json` | gleiche Struktur mit **fiktiven** Werten | ja |
| `excel/`, `tests/TcoMvp.Tests/Fixtures/excel-scenarios.json`, `reports/` | Excel-Quellen, Excel-Referenzwerte (enthalten Leasing-/Wartungskosten), Vergleichsberichte | **nein** (`.gitignore`) |

Beim Start werden beide JSON-Dateien zusammengeführt (Loks über ihre `id`). Fehlt die sensible
Datei, bricht die App mit einer klaren Meldung ab. Zum Ausprobieren ohne echte Daten:

```
copy src\TcoMvp.Web\Data\model-data.sensitive.example.json src\TcoMvp.Web\Data\model-data.sensitive.json
```

Für den Betrieb kann die sensible Datei auch außerhalb des Deployments liegen:
Umgebungsvariable `SensitiveModelDataPath=<Pfad>`.

Gerechnet wird nur auf dem Server. Der Browser bekommt ausschließlich die Werte, die auch im
Reiter „Betriebskosten“ stehen (Miete und Instandhaltung zusammengefasst, Energiekosten, Summe,
Differenz zur Traxx). Leasingraten, Wartungstabellen und die ausgeblendete Lok „VDM light“
verlassen den Server nicht.

## Starten

Voraussetzung: .NET 10 SDK.

```
dotnet run --project src/TcoMvp.Web --urls http://localhost:5080
```

Danach http://localhost:5080 öffnen.

## API

- `GET /api/model` – Loks der Kundenansicht und Standardannahmen
- `POST /api/calculate` – Eingaben (`CustomerInputs`) → Betriebskosten je Lok

## Tests

```
dotnet test      # C#: Rechenlogik, API, Datentrennung, Excel-Abgleich
npm test         # Browser-Logik (Zahlen-Ein-/Ausgabe), nur Node nötig – keine Pakete
```

| Testdatei | Prüft | Braucht sensible Daten? |
|---|---|---|
| `ExcelParityTests` | alle Zwischenwerte und die Kundenansicht gegen von Excel berechnete Szenarien | ja |
| `ApiExcelParityTests` | dieselben Szenarien Ende-zu-Ende über HTTP | ja |
| `CalculationRulesTests` | fachliche Regeln (km/Stunden vollständig verteilt, Preis linear, Start-Stop, Summen, #NV-Verhalten) | nein |
| `MaintenanceTests` | Wartungslogik je Lok mit konstruierten Tabellen (Tabellentreffer vs. Regression, Preisband-Grenzen, inkl. 500 h, Load factor) | nein |
| `ExcelFunctionsTests` | XLOOKUP (exakt/nächstgrößer), MATCH, SLOPE | nein |
| `InputValidatorTests` | Eingabeprüfung | nein |
| `ApiTests` | Endpunkte, Fehlercodes, keine internen Felder in Antworten, Datendateien nicht abrufbar, Start ohne sensible Daten schlägt fehl | nein (Beispieldaten) |
| `DataSeparationTests` | unkritische Datei und API-Antworten enthalten keine sensiblen Felder | nein |
| `tests/frontend/format.test.js` | Zahleneingabe deutsch („40.000", „1,8"), Prozentfelder, Validierung | nein |

### Abgleich mit Excel

`tests/TcoMvp.Tests/Fixtures/excel-scenarios.json` enthält 90 Szenarien (Standardannahmen, Preisband-Grenzen,
Grenzfälle und Zufallswerte), die **Excel selbst** berechnet hat. Die Tests prüfen alle
Zwischenwerte des TCO Models (km, Stunden, Fahrzeug-, Energie- und Wartungskosten) und die
Kundenansicht auf eine Abweichung von höchstens 1e-6.

Ohne `model-data.sensitive.json` und Fixtures (z.B. frischer Checkout) werden diese Paritätstests
übersprungen. Die Tests in `DataSeparationTests` laufen immer: Sie prüfen, dass die unkritische
Datei und die API-Antworten keine sensiblen Felder enthalten.

Wenn sich die Excel ändert, die Referenzwerte neu erzeugen (Windows und Excel nötig, die
Excel-Datei wird nur als Kopie geöffnet und nicht verändert):

```
powershell -ExecutionPolicy Bypass -File tools\generate-excel-fixtures.ps1
```

## Bewusst übernommene Excel-Eigenheiten

- **VDM light Wartung:** In `TCO Model!L115` sucht das zweite `MATCH` in einem 2D-Bereich
  (`C29:G33`). Das ergibt immer `#NV`, darum greift immer die Regression. Die Web-App macht es genauso.
- **Traxx außerhalb der Vertragstabelle** (über 120.000 km/Jahr – die Heavy-Maintenance-Tabelle endet dort, obwohl die km-Preisbänder bis 122.500 km gehen – oder über 3.000 Wartungsstunden):
  Excel zeigt `#NV`. Die Web-App zeigt „n. v.“ mit einem Hinweistext.
- **Load factor** (`TCO Model!M41`, 15/20/25 %) ist keine Kundeneingabe, sondern steht in
  `model-data.sensitive.json` (`traxx.loadFactor`).
- **Start-Stop + Elektro über 100 %** ergibt negative Dieselparkstunden und damit negative
  Dieselkosten (Traxx zu günstig). Die Web-App übernimmt das unverändert.

Alle Auffälligkeiten mit Zellbezügen, Beträgen und offenen Fragen stehen in **[BEFUNDE.md](BEFUNDE.md)**.

## Nicht übernommen (nicht Teil der Kundenansicht)

Die Sensitivitätstabellen (`TCO Model` ab Zeile 128, Excel-Datentabellen) und die Reiter
Input robustness, Fleet extract, Operator Profiles, SBB Prima H4 Daten, Traxx Shunter FS
Offer_ALT und tco model Alstom orig. Keiner davon fließt in den Reiter „Betriebskosten“ ein.
