// Zahlen-Ein- und -Ausgabe der Kundenansicht (deutsche Schreibweise).
// Läuft im Browser (window.TcoFormat) und in Node (require) – dort für die Tests.
(function (root) {
  "use strict";

  const i18n = () => root.TcoI18n;
  const T = (s) => (i18n() ? i18n().T(s) : s);
  const english = () => i18n()?.lang === "en";
  const nf = (digits) => new Intl.NumberFormat(i18n()?.locale ?? "de-DE", { minimumFractionDigits: digits, maximumFractionDigits: digits });

  /** 0.3 → "30%" (für dynamische Beschriftungen wie in Betriebskosten!D17) */
  function pct0(v) { return `${Math.round(v * 100)}%`; }

  /** Anzeige berechneter Felder: Prozent ohne Nachkommastellen und ohne %-Zeichen (steht in der Einheit), sonst row.digits (Standard 2). */
  function formatValue(row, v) {
    if (v === null || v === undefined || Number.isNaN(v)) return "";
    return row.pct ? nf(0).format(v * 100) : nf(row.digits ?? 2).format(v);
  }

  /** Wert → Text im Eingabefeld (Prozentfelder in %, Dezimalkomma, ohne Tausenderpunkte). */
  function toInputText(row, v) {
    if (v === null || v === undefined) return "";
    const n = row.pct ? v * 100 : v;
    const text = String(Math.round(n * 1e6) / 1e6);
    return english() ? text : text.replace(".", ",");
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
    if (english()) t = t.replace(/,/g, "");
    else if (t.includes(",")) t = t.replace(/\./g, "").replace(",", ".");
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

  /** Fehlermeldung zu einer ungültigen Eingabe (v = parseInput-Ergebnis); "" wenn gültig. */
  function errorText(row, v) {
    if (isValid(row, v)) return "";
    if (!Number.isFinite(v)) return T("Bitte eine Zahl eingeben.");
    if (v < 0) return T("Der Wert darf nicht negativ sein.");
    if (row.pct && v > 1) return T("Maximal 100 % möglich.");
    if (row.positive && v <= 0) return T("Der Wert muss größer als 0 sein.");
    return T("Der Wert ist zu groß.");
  }

  /** Verpackt das Eingabefeld mit einer Fehlerzeile darunter; setError("") blendet sie aus. */
  function withError(input) {
    const wrap = document.createElement("span");
    wrap.className = "field";
    const msg = document.createElement("small");
    msg.className = "field-error";
    msg.hidden = true;
    msg.id = `err-${Math.random().toString(36).slice(2)}`;
    wrap.append(input, msg);
    const setError = (text) => {
      msg.textContent = text;
      msg.hidden = !text;
      input.toggleAttribute("aria-invalid", !!text);
      if (text) input.setAttribute("aria-describedby", msg.id); else input.removeAttribute("aria-describedby");
    };
    return { wrap, setError };
  }

  const api = { nf, pct0, formatValue, toInputText, parseInput, isValid, errorText, withError };
  if (typeof module !== "undefined" && module.exports) module.exports = api;
  else root.TcoFormat = api;
})(this);
