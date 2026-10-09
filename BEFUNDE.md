# Befunde aus der Übersetzung Excel → Web-App

Stand: 09.10.2026 · Quelle: `Traxx Shunter_TCO model_26.08` (Original und Vertriebsversion)

Diese Punkte sind beim Nachbau der Excel und beim automatischen Abgleich (90 Szenarien, von
Excel selbst berechnet) aufgefallen. **Die Web-App rechnet überall genau wie die Excel**, auch
bei den Auffälligkeiten unten. Nichts davon ist in der Web-App „korrigiert“. Was davon gewollt ist
und was angepasst werden soll, muss fachlich entschieden werden.

Beträge sind Monatswerte je Lok aus der Kundenansicht („Summe der Betriebskosten“) bei den
Standardannahmen, sofern nicht anders angegeben.

| # | Befund | Auswirkung | Priorität |
|---|---|---|---|
| 1 | Negative Dieselparkstunden bei Start-Stop + Elektro > 100 % | Traxx wird zu günstig gerechnet | **hoch** |
| 2 | Traxx-Kosten sinken an km-Bandgrenzen | 1 km mehr ist bis zu ~1.000 € günstiger | **hoch** (Vertrieb) |
| 3 | Traxx rechnet nur bis 120.000 km, Preisbänder gehen bis 122.500 km | `#NV` zwischen 120.001 und 122.500 km | mittel |
| 4 | VDM: Sprung zwischen Tabellenwert und Regression | bis ±320 € je nach exakter Eingabe | mittel |
| 5 | VDM light: Tabellensuche greift nie (Formelfehler) | immer Regression; Lok ist ausgeblendet | niedrig |
| 6 | G1206 / DE18: Tabellenwerte und Regression fast identisch | < 1 € Unterschied | niedrig |
| 7 | Heavy-Maintenance-Tabelle: kein Zwischenwert 2.750 h | 2.501–3.000 h nutzen den 3.000-h-Wert | niedrig |
| 8 | Fehlende Eingabegrenzen in der Kundenansicht | Unplausible Eingaben werden still gerechnet | mittel |

---

## 1 · Negative Dieselparkstunden (Start-Stop + Elektro > 100 %)

**Wo:** `TCO Model!M75` = `M74 − M76 − M77`, also Parkstunden − Start-Stop-Stunden − elektrische Parkstunden.
Beide Abzüge beziehen sich auf **dieselben** Parkstunden: `M76 = Parkstunden × Start-Stop-Anteil`,
`M77 = Parkstunden × Elektro-Anteil`.

**Problem:** Ist Start-Stop-Anteil + elektrifizierter Anteil größer als 100 %, werden die
Diesel-Parkstunden negativ. Daraus entstehen **negative Dieselkosten** (Zeile 104), und die
Wartungsstunden sinken (Zeile 117).

**Beispiel:** Standardannahmen, aber Start-Stop 100 % → Traxx: −1.050 Diesel-Parkstunden/Jahr.
Das tritt aber schon bei realistischen Werten auf: 70 % Elektro + **mehr als 30 % Start-Stop**
(Standard ist 10 %). Betroffen sind die Traxx und die ausgeblendete VDM light. Die DE18 sind
nicht betroffen, weil sie keinen Elektroanteil haben.

**Frage an die Fachseite:** Soll Start-Stop nur auf die *nicht elektrischen* Parkstunden wirken,
etwa `M76 = (M74 − M77) × M60`? Alternativ könnte die Eingabe begrenzt werden.

## 2 · Traxx-Kosten sinken an den km-Bandgrenzen

**Wo:** `TCO Model!M115` (LET), `kmPreis = km × Kilometerpreis des Bandes / 12`.

**Problem:** Der km-Preis gilt für **alle** Kilometer im Band (keine Staffel). Beim Wechsel ins
nächste Band sinkt der Satz für alle Kilometer, deshalb fällt die Summe:

| Laufleistung | Traxx-Summe ändert sich um |
|---|---|
| 37.500 → 37.501 km | **−1.026,74 €** |
| 42.499 → 42.500 km | −907,82 € |
| 77.500 → 77.501 km | −273,75 € |

Das entspricht vermutlich dem Vertrag (Pauschalsatz je Band). Im Kundengespräch kann es aber
zu Fragen führen („mehr fahren ist billiger“).

**Nebenbefund:** Die Bandgrenzen in der Formel (`37500; 42499; 47499; …; 77500; 82499; …`)
passen nicht ganz zu den Überschriften (`72.500 – 77.500` und `77.500 – 82.499`). 77.500 km
taucht in zwei Bändern auf.

## 3 · Traxx rechnet nur bis 120.000 km

**Wo:** `TCO Model!M115`. Die km-Preisbänder gehen bis 122.500 km, die Heavy-Maintenance-Tabelle
(`hmKmArr`, `'Maint. Cost Traxx Shunter'!C24:P24`) aber nur bis 120.000 km.

**Folge:** Zwischen 120.001 und 122.500 km zeigt Excel `#NV`, obwohl ein km-Preis existiert.
Ebenso `#NV` über 3.000 Wartungsstunden/Jahr, das sind etwa 4.050 Dieselstunden. Bei `#NV`
fallen in der Kundenansicht auch **alle** Kostenunterschiede (Zeile 37) weg.

**Web-App:** zeigt „n. v.“ und den Hinweis „max. 120.000 km“.

## 4 · VDM: Sprung zwischen Tabellenwert und Regression

**Wo:** `TCO Model!K115`. Der Tabellenwert wird nur bei **exaktem** Treffer verwendet
(km ∈ {35.000; 50.000; 75.000; 100.000} **und** Wartungsstunden ∈ {750; 1.000; 1.250; 2.000}),
sonst die Regression (R² = 0,98).

**Folge:** Die Kosten springen je nach Zeile der Tabelle:

| km | Tabellenwert − Regression |
|---|---|
| 35.000 | +95 € |
| 50.000 | −278 € |
| 75.000 | +320 € |
| 100.000 | −126 € |

Beispiel: Bei 50.000 km und genau 1.000 Wartungsstunden ist die VDM 278 € günstiger als bei
50.001 km.

## 5 · VDM light: Tabellensuche greift nie

**Wo:** `TCO Model!L115`: `MATCH($L$116, 'Maint. Cost G1206 DE18 VDM'!$C$29:$G$33, 0)`.
`MATCH` über einen 2D-Bereich ergibt immer `#NV`. Gemeint war vermutlich `$C$29:$G$29`, analog
zu `K115`. Dadurch greift immer die Regression.

Die Auswirkung ist gering, weil die Lok in der Kundenansicht ausgeblendet ist.

## 6 · G1206 / DE18: Tabelle und Regression fast identisch

**Wo:** `TCO Model!H115:J115`. Bei exakt 2.000/2.500/3.000/3.500/4.000 Stunden kommt der Tabellenwert,
sonst die Regression. Die Abweichung liegt unter 0,50 €/Monat und ist unkritisch.
Alle km-Zeilen der Tabellen sind identisch, die Laufleistung hat also keinen Einfluss.

## 7 · Heavy-Maintenance-Tabelle ohne 2.750-h-Zeile

**Wo:** `'Maint. Cost Traxx Shunter'!B25:B34` mit den Stunden 500, 750, …, 2.500 und **3.000**.
Für 2.501–3.000 Wartungsstunden wird der 3.000-h-Wert verwendet (XLOOKUP „nächstgrößer“). Das
ist konservativ und vermutlich gewollt.

## 8 · Fehlende Eingabegrenzen in der Kundenansicht

Die Excel prüft keine Eingaben. Negative Werte, Anteile über 100 % oder die Kombination aus
Befund 1 werden still gerechnet.

**Web-App:** lehnt negative Werte und Anteile außerhalb 0–100 % ab. Die Kombination aus Befund 1
wird **nicht** abgefangen, damit die Ergebnisse identisch zur Excel bleiben. Das lässt sich
nachrüsten, sobald fachlich entschieden ist.

---

## Nicht in die Web-App übernommen

- Sensitivitätstabellen (`TCO Model` ab Zeile 128, Excel-Datentabellen)
- Reiter Input robustness, Fleet extract, Operator Profiles, SBB Prima H4 Daten,
  Traxx Shunter FS Offer_ALT, tco model Alstom orig. Sie fließen nicht in die Kundenansicht ein.
