// Zahlen-Ein- und -Ausgabe der Kundenansicht (deutsche Schreibweise).
// Läuft im Browser (window.TcoFormat) und in Node (require) – dort für die Tests.
(function (root) {
  "use strict";

  const nf = (digits) => new Intl.NumberFormat("de-DE", { minimumFractionDigits: digits, maximumFractionDigits: digits });

  /** 0.3 → "30%" (für dynamische Beschriftungen wie in Betriebskosten!D17) */
  function pct0(v) { return `${Math.round(v * 100)}%`; }

  /** Anzeige berechneter Felder: Prozent ohne Nachkommastellen, sonst row.digits (Standard 2). */
  function formatValue(row, v) {
    if (v === null || v === undefined || Number.isNaN(v)) return "";
    return row.pct ? nf(0).format(v * 100) + " %" : nf(row.digits ?? 2).format(v);
  }

  /** Wert → Text im Eingabefeld (Prozentfelder in %, Dezimalkomma, ohne Tausenderpunkte). */
  function toInputText(row, v) {
    if (v === null || v === undefined) return "";
    const n = row.pct ? v * 100 : v;
    return String(Math.round(n * 1e6) / 1e6).replace(".", ",");
  }

  /**
   * Text aus dem Eingabefeld → Zahl (Prozentfelder → Anteil 0..1). NaN bei ungültiger Eingabe,
   * also bei allem außer Ziffern, Vorzeichen, Komma/Punkt und Leerzeichen. Ein %-Zeichen ist nur am
   * Ende eines Prozentfelds erlaubt.
   * "1,8" und "1.8" → 1,8. Ein Punkt gilt nur dann als Tausendertrenner, wenn zusätzlich ein Komma
   * vorkommt ("40.000,5") oder das Feld ganzzahlig ist und genau Dreiergruppen folgen ("40.000").
   */
  function parseInput(row, text) {
    let t = String(text).trim();
    if (row.pct && t.endsWith("%")) t = t.slice(0, -1);
    t = t.replace(/\s/g, "");
    if (t === "") return NaN;
    const integerField = row.digits === 0;
    if (t.includes(",")) t = t.replace(/\./g, "").replace(",", ".");
    else if (integerField && /^\d{1,3}(\.\d{3})+$/.test(t)) t = t.replace(/\./g, "");
    if (!/^[+-]?(\d+\.?\d*|\.\d+)$/.test(t)) return NaN;
    const n = Number(t);
    return row.pct ? n / 100 : n;
  }

  /** Technische Obergrenze, wie InputValidator.MaxValue auf dem Server. */
  const MAX_VALUE = 1e9;

  /** Gleiche Regeln wie InputValidator auf dem Server: endlich, 0 ≤ v ≤ MAX_VALUE, Prozent ≤ 100 %, Preise > 0. */
  function isValid(row, v) {
    return Number.isFinite(v) && v >= 0 && v <= MAX_VALUE && (!row.pct || v <= 1) && (!row.positive || v > 0);
  }

  const api = { nf, pct0, formatValue, toInputText, parseInput, isValid };
  if (typeof module !== "undefined" && module.exports) module.exports = api;
  else root.TcoFormat = api;
})(this);
