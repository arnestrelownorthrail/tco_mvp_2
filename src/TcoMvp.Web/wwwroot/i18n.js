// Sprachumschaltung Deutsch/Englisch. Deutsche Texte sind zugleich der Schlüssel; fehlt eine
// Übersetzung, wird der deutsche Text angezeigt. Läuft im Browser (window.TcoI18n) und in Node.
(function (root) {
  "use strict";

  const KEY = "tco-lang";
  const EN = {
    // Seite
    "Betriebskostenvergleich Traxx Shunter": "Operating Cost Comparison Traxx Shunter",
    "Admin – Standardannahmen": "Admin – Default assumptions",
    "Betriebskostenvergleich": "Operating cost comparison",
    "Traxx Shunter E-Diesel im Vergleich zu Bestandsloks": "Traxx Shunter E-Diesel compared with existing locomotives",
    "Annahmen zurücksetzen": "Reset assumptions",
    "Standardannahmen in alle Eingabefelder übernehmen": "Apply the default assumptions to all input fields",
    "Drucken / PDF": "Print / PDF",
    "Zum dunklen Modus wechseln": "Switch to dark mode",
    "Zum hellen Modus wechseln": "Switch to light mode",
    "Auf Deutsch wechseln": "Switch to German",
    "Auf Englisch wechseln": "Switch to English",
    "Eingabefelder": "Input fields",
    "alle anderen Felder werden automatisch berechnet.": "all other fields are calculated automatically.",
    "Loks im Vergleich": "Locomotives compared",
    "Zentrale Inputs": "Central inputs",
    "gelten für alle Loktypen": "apply to all locomotive types",
    "Vergleich je Lok": "Comparison per locomotive",
    "Einheit": "Unit",
    "Betriebskosten* je Lok": "Operating costs* per locomotive",
    "Miete und Instandhaltung": "Rent and maintenance",
    "Energiekosten": "Energy costs",
    "Traxx Shunter (Referenz)": "Traxx Shunter (reference)",
    "* exkl. Versicherungskosten. Alle Kosten in € pro Monat und Lok.": "* excl. insurance costs. All costs in € per month and locomotive.",
    // Zeilen
    "Nutzungsprofil des Kunden": "Customer usage profile",
    "Kilometer": "Kilometres",
    "Stunden (aufgerüstet)": "Hours (equipped)",
    "davon rollend": "of which rolling",
    "davon parkend": "of which parked",
    "Streckenprofil": "Route profile",
    "(Möglicher) elektrifizierter Streckenanteil (Kilometer und Stunden)": "(Possible) electrified share of route (kilometres and hours)",
    "Streckenanteil mit Diesel": "Share of route on diesel",
    "davon Rangieren (an {0} Diesel)": "of which shunting (on {0} diesel)",
    "davon Streckenfahrt (an {0} Diesel)": "of which mainline running (on {0} diesel)",
    "Energiepreise": "Energy prices",
    "Dieselpreis / Liter": "Diesel price / litre",
    "Strompreis / kWh": "Electricity price / kWh",
    "Lok-spezifische Annahmen": "Locomotive-specific assumptions",
    "Lok-spezifische Inputs": "Locomotive-specific inputs",
    "Parkzeit (Anteil an „davon parkend“)": "Parking time (share of “of which parked”)",
    "davon mit laufendem Dieselmotor": "of which with diesel engine running",
    "davon ohne laufenden Dieselmotor (Start-Stop)": "of which with diesel engine off (start-stop)",
    "Dieselverbrauch": "Diesel consumption",
    "Rangierdienst": "Shunting service",
    "Streckendienst": "Mainline service",
    "Parkend bei laufendem Motor": "Parked with engine running",
    "Strom": "Electricity",
    "kWh / km": "kWh / km",
    "kWh / Stunde, parkend": "kWh / hour, parked",
    "km / Jahr": "km / year",
    "Stunden / Jahr": "hours / year",
    "€ / Liter": "€ / litre",
    "Liter / km": "litres / km",
    "Liter / Stunde": "litres / hour",
    "kWh / Stunde": "kWh / hour",
    "€ / Monat": "€ / month",
    // Ergebnis, Diagramm, Filter
    "Betriebskosten": "Operating costs",
    "Summe der Betriebskosten*": "Total operating costs*",
    "Kostenunterschied zur Traxx Shunter": "Cost difference to Traxx Shunter",
    "n. v.": "n/a",
    "Referenz": "Reference",
    "Summe": "Total",
    "ggü. Referenz": "vs. reference",
    "Referenzlok – immer im Vergleich": "Reference locomotive – always included",
    // Lok-Untertitel (aus model-data.json)
    "ohne Start-Stop": "without start-stop",
    "inkl. Start-Stop": "incl. start-stop",
    "Einmotorig": "Single-engine",
    "Zweimotorig": "Twin-engine",
    "Einmotorig, Start-Stop": "Single-engine, start-stop",
    // Eingabefehler
    "Bitte eine Zahl eingeben.": "Please enter a number.",
    "Der Wert darf nicht negativ sein.": "The value must not be negative.",
    "Maximal 100 % möglich.": "100 % at most.",
    "Der Wert muss größer als 0 sein.": "The value must be greater than 0.",
    "Der Wert ist zu groß.": "The value is too large.",
    // Status
    "Bitte Eingaben prüfen: ": "Please check your inputs: ",
    "Berechnung fehlgeschlagen: ": "Calculation failed: ",
    "Laden fehlgeschlagen: ": "Loading failed: ",
    // Admin
    "Standardannahmen": "Default assumptions",
    "Admin · Werte für „Annahmen zurücksetzen“ und für neue Besucher": "Admin · Values for “Reset assumptions” and for new visitors",
    "Speichern": "Save",
    "Änderungen verwerfen": "Discard changes",
    "Auslieferungswerte": "Factory values",
    "Gespeicherte Änderungen löschen und die Werte aus model-data.json verwenden": "Delete saved changes and use the values from model-data.json",
    "Zur Kundenansicht": "Customer view",
    "abweichend vom Auslieferungswert (Wert im Tooltip)": "differs from the factory value (value in tooltip)",
    "Gespeicherte Werte gelten sofort für „Annahmen zurücksetzen“ und für Besucher ohne eigene, im Browser gespeicherte Eingaben.": "Saved values apply immediately to “Reset assumptions” and to visitors without their own inputs saved in the browser.",
    "Aktiv: im Admin-Bereich gespeicherte Werte.": "Active: values saved in the admin area.",
    "Aktiv: Auslieferungswerte aus model-data.json.": "Active: factory values from model-data.json.",
    "Auslieferungswert: {0}": "Factory value: {0}",
    "Gespeichert.": "Saved.",
    "Speichern fehlgeschlagen: ": "Saving failed: ",
    "Gespeicherte Standardannahmen löschen und die Auslieferungswerte verwenden?": "Delete the saved default assumptions and use the factory values?",
    "Auslieferungswerte wiederhergestellt.": "Factory values restored.",
    "Zurücksetzen fehlgeschlagen: ": "Reset failed: ",
  };

  const num = (s) => s.replace(/\./g, ",");

  // Meldungen des Servers (feste deutsche Texte) → Englisch
  const SERVER = [
    [/^Muss eine Zahl zwischen 0 und 1\.000\.000\.000 sein\.$/, () => "Must be a number between 0 and 1,000,000,000."],
    [/^Muss größer als 0 sein\.$/, () => "Must be greater than 0."],
    [/^Muss zwischen 0 % und 100 % liegen\.$/, () => "Must be between 0 % and 100 %."],
    [/^Eingaben fehlen\.$/, () => "Inputs missing."],
    [/^Laufleistung von (.+) km\/Jahr liegt außerhalb der Vertragstabelle \(max\. (.+) km\)\.$/,
      (m) => `Mileage of ${num(m[1])} km/year is outside the contract table (max. ${num(m[2])} km).`],
    [/^Wartungsstunden von (.+) h\/Jahr liegen außerhalb der Vertragstabelle \(max\. (.+) h\)\.$/,
      (m) => `Maintenance hours of ${num(m[1])} h/year are outside the contract table (max. ${num(m[2])} h).`],
  ];

  function stored() {
    try { return localStorage.getItem(KEY); } catch { return null; }
  }

  const lang = typeof localStorage !== "undefined" && stored() === "en" ? "en" : "de";

  /** Text in der gewählten Sprache; {0}, {1} … werden durch args ersetzt. */
  function T(de, ...args) {
    const s = lang === "en" && Object.hasOwn(EN, de) ? EN[de] : de;
    return args.length ? s.replace(/\{(\d+)\}/g, (_, i) => args[i]) : s;
  }

  /** Servermeldung übersetzen; unbekannte Texte bleiben unverändert. */
  function TS(msg) {
    if (lang !== "en") return msg;
    for (const [re, fn] of SERVER) {
      const m = re.exec(msg);
      if (m) return fn(m);
    }
    return msg;
  }

  const api = { lang, locale: lang === "en" ? "en-GB" : "de-DE", T, TS };

  if (typeof document === "undefined") {
    if (typeof module !== "undefined" && module.exports) module.exports = api;
    return;
  }

  root.TcoI18n = api;
  document.documentElement.lang = lang;

  // Statische Texte: data-i18n (Textinhalt) und data-i18n-title (title-Attribut); der deutsche Text ist der Schlüssel
  document.addEventListener("DOMContentLoaded", () => {
    document.title = T(document.title);
    for (const el of document.querySelectorAll("[data-i18n]")) el.textContent = T(el.textContent.trim());
    for (const el of document.querySelectorAll("[data-i18n-title]")) el.title = T(el.title);

    const button = document.getElementById("lang-toggle");
    if (!button) return;
    button.textContent = lang === "en" ? "DE" : "EN";
    button.title = T(lang === "en" ? "Auf Deutsch wechseln" : "Auf Englisch wechseln");
    button.lang = lang === "en" ? "de" : "en";
    button.addEventListener("click", () => {
      try { localStorage.setItem(KEY, lang === "en" ? "de" : "en"); } catch { /* Speichern optional */ }
      location.reload();
    });
  });
})(typeof window !== "undefined" ? window : globalThis);
