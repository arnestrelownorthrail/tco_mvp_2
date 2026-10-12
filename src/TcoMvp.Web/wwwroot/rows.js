// Zeilen des Reiters "Betriebskosten" (Annahmen) – gemeinsam für Kundenansicht (app.js) und Admin-Bereich.
"use strict";

(function () {
  const { pct0 } = window.TcoFormat;

  // ---- Zeilendefinition (entspricht Betriebskosten!D5:K30) ---------------------------------
  // kind: "central" = Spalte "Zentrale Inputs", "loco" = je Lok
  window.TcoRows = [
    { section: "Annahmen" },
    { group: "Nutzungsprofil des Kunden" },
    { label: "Kilometer", unit: "km / Jahr", kind: "central", key: "mileageKmPerYear", digits: 0 },
    { label: "Stunden (aufgerüstet)", unit: "Stunden / Jahr", kind: "central", key: "operatingHoursPerYear", digits: 0 },
    { label: "davon rollend", indent: 1, unit: "%", kind: "central", key: "rollingShare", pct: true },
    { label: "davon parkend", indent: 1, unit: "%", kind: "central", calc: (i) => 1 - i.rollingShare, pct: true },
    { label: "davon mit laufendem Dieselmotor", indent: 2, unit: "%", kind: "loco", when: (l) => l.startStopEditable,
      calc: (li) => 1 - (li.startStopShare ?? 0), pct: true },
    { label: "davon ohne laufenden Dieselmotor (Start-Stop)", indent: 2, unit: "%", kind: "loco", when: (l) => l.startStopEditable,
      key: "startStopShare", pct: true },
    { spacer: true },
    { label: "(Möglicher) elektrifizierter Streckenanteil (Kilometer und Stunden)", unit: "%", kind: "central", key: "electricShare", pct: true },
    { label: "Streckenanteil mit Diesel", unit: "%", kind: "central", calc: (i) => 1 - i.electricShare, pct: true },
    { label: (i) => `davon Rangieren (an ${pct0(1 - i.electricShare)} Diesel)`, indent: 1, unit: "%", kind: "central", key: "shuntingShare", pct: true },
    { label: (i) => `davon Streckenfahrt (an ${pct0(1 - i.electricShare)} Diesel)`, indent: 1, unit: "%", kind: "central", calc: (i) => 1 - i.shuntingShare, pct: true },
    { group: "Diesel" },
    { label: "Diesel pro km, Rangierbetrieb", indent: 1, unit: "Liter / km", kind: "loco", key: "dieselShuntingLitersPerKm", digits: 1 },
    { label: "Diesel pro km, Streckenbetrieb", indent: 1, unit: "Liter / km", kind: "loco", key: "dieselMainlineLitersPerKm", digits: 1 },
    { label: "Diesel pro geparkter Stunde mit laufendem Motor", indent: 1, unit: "Liter / Stunde", kind: "loco", key: "dieselParkedLitersPerHour", digits: 1 },
    { label: "Dieselpreis / Liter", indent: 1, unit: "€ / Liter", kind: "central", key: "dieselPricePerLiter", digits: 2, positive: true },
    { group: "Strom" },
    { label: "kWh / km", indent: 1, unit: "kWh / km", kind: "loco", when: (l) => l.electricCapable, key: "kwhPerKm", digits: 0 },
    { label: "kWh / Stunde, parkend", indent: 1, unit: "kWh / Stunde", kind: "loco", when: (l) => l.electricCapable, key: "kwhPerHourParked", digits: 0 },
    { label: "Strompreis / kWh", indent: 1, unit: "€ / kWh", kind: "central", key: "electricityPricePerKwh", digits: 2, positive: true },
  ];
})();
