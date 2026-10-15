// Zeilen des Reiters "Betriebskosten" (Annahmen) – gemeinsam für Kundenansicht (app.js) und Admin-Bereich.
"use strict";

(function () {
  const { pct0 } = window.TcoFormat;
  const T = (...a) => window.TcoI18n.T(...a);

  window.TcoRows = {
    central: [
      { group: "Nutzungsprofil des Kunden" },
      { label: "Kilometer", unit: "km / Jahr", key: "mileageKmPerYear", digits: 0 },
      { label: "Stunden (aufgerüstet)", unit: "Stunden / Jahr", key: "operatingHoursPerYear", digits: 0 },
      { label: "davon rollend", indent: 1, unit: "%", key: "rollingShare", pct: true },
      { label: "davon parkend", indent: 1, unit: "%", calc: (i) => 1 - i.rollingShare, pct: true },
      { group: "Streckenprofil" },
      { label: "(Möglicher) elektrifizierter Streckenanteil (Kilometer und Stunden)", unit: "%", key: "electricShare", pct: true },
      { label: "Streckenanteil mit Diesel", unit: "%", calc: (i) => 1 - i.electricShare, pct: true },
      { label: (i) => T("davon Rangieren (an {0} Diesel)", pct0(1 - i.electricShare)), indent: 1, unit: "%", key: "shuntingShare", pct: true },
      { label: (i) => T("davon Streckenfahrt (an {0} Diesel)", pct0(1 - i.electricShare)), indent: 1, unit: "%", calc: (i) => 1 - i.shuntingShare, pct: true },
      { group: "Energiepreise" },
      { label: "Dieselpreis / Liter", unit: "€ / Liter", key: "dieselPricePerLiter", digits: 2, positive: true },
      { label: "Strompreis / kWh", unit: "€ / kWh", key: "electricityPricePerKwh", digits: 2, positive: true },
    ],
    loco: [
      { section: "Lok-spezifische Annahmen" },
      { group: "Parkzeit (Anteil an „davon parkend“)" },
      { label: "davon mit laufendem Dieselmotor", indent: 1, unit: "%", when: (l) => l.startStopEditable,
        calc: (li) => 1 - (li.startStopShare ?? 0), pct: true },
      { label: "davon ohne laufenden Dieselmotor (Start-Stop)", indent: 1, unit: "%", when: (l) => l.startStopEditable,
        key: "startStopShare", pct: true },
      { group: "Dieselverbrauch" },
      { label: "Rangierdienst", indent: 1, unit: "Liter / km", key: "dieselShuntingLitersPerKm", digits: 1 },
      { label: "Streckendienst", indent: 1, unit: "Liter / km", key: "dieselMainlineLitersPerKm", digits: 1 },
      { label: "Parkend bei laufendem Motor", indent: 1, unit: "Liter / Stunde", key: "dieselParkedLitersPerHour", digits: 1 },
      { group: "Strom" },
      { label: "kWh / km", indent: 1, unit: "kWh / km", when: (l) => l.electricCapable, key: "kwhPerKm", digits: 0 },
      { label: "kWh / Stunde, parkend", indent: 1, unit: "kWh / Stunde", when: (l) => l.electricCapable, key: "kwhPerHourParked", digits: 0 },
    ],
  };
})();
