// Tests der Zahlen-Ein-/Ausgabe der Kundenansicht.
// Aufruf: npm test  (oder: node --test "tests/frontend/*.test.js")
"use strict";
const test = require("node:test");
const assert = require("node:assert/strict");
const path = require("node:path");
const f = require(path.join(__dirname, "../../src/TcoMvp.Web/wwwroot/format.js"));

const KM = { digits: 0 };          // Kilometer, Stunden
const LITRES = { digits: 1 };      // Liter / km
const PRICE = { digits: 2 };       // € / Liter
const PCT = { pct: true };         // Prozentfelder

test("ganzzahlige Felder akzeptieren Tausenderpunkte", () => {
  assert.equal(f.parseInput(KM, "40.000"), 40000);
  assert.equal(f.parseInput(KM, "1.234.567"), 1234567);
  assert.equal(f.parseInput(KM, "40000"), 40000);
  assert.equal(f.parseInput(KM, " 40 000 "), 40000);
});

test("Dezimalkomma und Dezimalpunkt werden beide verstanden", () => {
  assert.equal(f.parseInput(PRICE, "1,8"), 1.8);
  assert.equal(f.parseInput(PRICE, "1.8"), 1.8);
  assert.equal(f.parseInput(LITRES, "13,5"), 13.5);
  assert.equal(f.parseInput(PRICE, "0,15"), 0.15);
});

test("Punkt in Dezimalfeldern ist kein Tausendertrenner (1.800 € → 1,8 €, nicht 1800 €)", () => {
  assert.equal(f.parseInput(PRICE, "1.800"), 1.8);
});

test("Tausenderpunkt zusammen mit Komma", () => {
  assert.equal(f.parseInput(KM, "40.000,5"), 40000.5);
});

test("Prozentfelder werden in Anteile umgerechnet, %-Zeichen ist erlaubt", () => {
  assert.equal(f.parseInput(PCT, "50"), 0.5);
  assert.equal(f.parseInput(PCT, "70 %"), 0.7);
  assert.equal(f.parseInput(PCT, "12,5"), 0.125);
});

test("ungültige Eingaben ergeben NaN", () => {
  for (const t of ["", "   ", "abc", "1,2,3", "12a", "1..2", "--5"]) {
    assert.ok(Number.isNaN(f.parseInput(PRICE, t)), `"${t}" sollte NaN sein`);
  }
});

test("isValid entspricht den Server-Regeln", () => {
  assert.ok(f.isValid(KM, 0));
  assert.ok(f.isValid(KM, 120000));
  assert.ok(!f.isValid(KM, -1));
  assert.ok(!f.isValid(KM, NaN));
  assert.ok(!f.isValid(KM, Infinity));
  assert.ok(f.isValid(PCT, 1));
  assert.ok(!f.isValid(PCT, 1.01));
});

test("toInputText zeigt Dezimalkomma und Prozent ohne Rundungsartefakte", () => {
  assert.equal(f.toInputText(PRICE, 1.8), "1,8");
  assert.equal(f.toInputText(PCT, 0.7), "70");
  assert.equal(f.toInputText(PCT, 0.30000000000000004), "30");
  assert.equal(f.toInputText(KM, 40000), "40000");
  assert.equal(f.toInputText(PRICE, null), "");
});

test("Eingabe → Anzeige → Eingabe ist verlustfrei", () => {
  for (const [row, v] of [[PRICE, 1.85], [PCT, 0.125], [KM, 40000], [LITRES, 13.5]]) {
    assert.equal(f.parseInput(row, f.toInputText(row, v)), v);
  }
});

test("formatValue formatiert wie die Excel-Zahlenformate", () => {
  assert.equal(f.formatValue(PCT, 0.5), "50 %");
  assert.equal(f.formatValue(LITRES, 4.8), "4,8");
  assert.equal(f.formatValue(KM, 40000), "40.000");
  assert.equal(f.formatValue(PRICE, null), "");
});

test("pct0 für dynamische Beschriftung (Betriebskosten!D17)", () => {
  assert.equal(f.pct0(1 - 0.7), "30%");
});
