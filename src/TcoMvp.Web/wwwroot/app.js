// Kundenansicht – Nachbau des Reiters "Betriebskosten".
// Gerechnet wird ausschließlich auf dem Server (/api/calculate); hier werden nur Eingaben
// erfasst und die Ergebnisse angezeigt.
"use strict";

const STORAGE_KEY = "tco-mvp-inputs-v1";
const HIDDEN_KEY = "tco-mvp-hidden-locos-v1";
const { nf, pct0, formatValue, toInputText, parseInput, isValid } = window.TcoFormat;
const fmtEuro = nf(0);

let model = null;   // { locomotives: [...], defaults: {...} }
let inputs = null;  // aktueller Eingabestand
let pending = null;
let hidden = new Set(); // IDs der ausgeblendeten Loks (Standard: alle sichtbar)
let lastResult = null;

const visibleLocos = () => model.locomotives.filter((l) => !hidden.has(l.id));

// ---- Zeilendefinition (entspricht Betriebskosten!D5:K30) ---------------------------------
// kind: "central" = Spalte "Zentrale Inputs", "loco" = je Lok
const ROWS = [
  { section: "Annahmen" },
  { group: "Nutzungsprofil des Kunden" },
  { label: "Kilometer", unit: "km / Jahr", kind: "central", key: "mileageKmPerYear", digits: 0 },
  { label: "Stunden (aufgerüstet)", unit: "Stunden / Jahr", kind: "central", key: "operatingHoursPerYear", digits: 0 },
  { label: "davon rollende Stunden", unit: "%", kind: "central", key: "rollingShare", pct: true },
  { label: "davon parkende Stunden", unit: "%", kind: "central", calc: (i) => 1 - i.rollingShare, pct: true },
  { label: "Parkende Stunden mit laufendem Dieselmotor", unit: "%", kind: "loco", when: (l) => l.startStopEditable,
    calc: (li) => 1 - (li.startStopShare ?? 0), pct: true },
  { label: "Parkende Stunden ohne laufenden Dieselmotor (Start-Stop)", unit: "%", kind: "loco", when: (l) => l.startStopEditable,
    key: "startStopShare", pct: true },
  { spacer: true },
  { label: "(Möglicher) elektrifizierter Streckenanteil (Kilometer und Stunden)", unit: "%", kind: "central", key: "electricShare", pct: true },
  { label: "Streckenanteil mit Diesel", unit: "%", kind: "central", calc: (i) => 1 - i.electricShare, pct: true },
  { label: (i) => `Anteil Rangieren an den ${pct0(1 - i.electricShare)} Streckenanteil Diesel`, unit: "%", kind: "central", key: "shuntingShare", pct: true },
  { label: (i) => `Anteil Streckenfahrt an den ${pct0(1 - i.electricShare)} Streckenanteil Diesel`, unit: "%", kind: "central", calc: (i) => 1 - i.shuntingShare, pct: true },
  { group: "Energieverbrauch und -kosten" },
  { label: "Diesel pro km, Rangierbetrieb", unit: "Liter / km", kind: "loco", key: "dieselShuntingLitersPerKm", digits: 1 },
  { label: "Diesel pro km, Streckenbetrieb", unit: "Liter / km", kind: "loco", key: "dieselMainlineLitersPerKm", digits: 1 },
  { label: "Diesel pro geparkter Stunde mit laufendem Motor", unit: "Liter / Stunde", kind: "loco", key: "dieselParkedLitersPerHour", digits: 1 },
  { spacer: true },
  { label: "kWh / km", unit: "kWh / km", kind: "loco", when: (l) => l.electricCapable, key: "kwhPerKm", digits: 0 },
  { label: "kWh / Stunde, parkend", unit: "kWh / Stunde", kind: "loco", when: (l) => l.electricCapable, key: "kwhPerHourParked", digits: 0 },
  { spacer: true },
  { label: "Dieselpreis / Liter", unit: "€ / Liter", kind: "central", key: "dieselPricePerLiter", digits: 2 },
  { label: "Strompreis / kWh", unit: "€ / kWh", kind: "central", key: "electricityPricePerKwh", digits: 2 },
];

// ---- Aufbau der Tabelle -------------------------------------------------------------------
function buildHeader() {
  const tr = document.getElementById("head-row");
  while (tr.children.length > 3) tr.lastChild.remove();
  for (const l of visibleLocos()) {
    const th = document.createElement("th");
    if (l.isReference) th.className = "ref";
    th.innerHTML = `${escapeHtml(l.name)}<br><span class="muted">${escapeHtml(l.subtitle)}</span>`;
    tr.appendChild(th);
  }
}

function escapeHtml(s) {
  return s.replace(/[&<>"']/g, (c) => ({ "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&#39;" }[c]));
}

function td(className, content) {
  const el = document.createElement("td");
  if (className) el.className = className;
  if (content instanceof Node) el.appendChild(content); else if (content !== undefined) el.textContent = content;
  return el;
}

function makeInput(row, get, set) {
  const el = document.createElement("input");
  el.type = "text";
  el.inputMode = "decimal";
  el.value = toInputText(row, get());
  el.setAttribute("aria-label", typeof row.label === "function" ? row.label(inputs) : row.label);
  el.addEventListener("input", () => {
    const v = parseInput(row, el.value);
    const ok = isValid(row, v);
    el.classList.toggle("invalid", !ok);
    if (ok) { set(v); onChange(); }
  });
  el.addEventListener("blur", () => { if (!el.classList.contains("invalid")) el.value = toInputText(row, get()); });
  return el;
}

function renderAssumptions() {
  const body = document.getElementById("assumptions");
  body.replaceChildren();
  const colCount = 3 + visibleLocos().length;

  for (const row of ROWS) {
    const tr = document.createElement("tr");
    if (row.section) {
      tr.className = "section";
      const cell = td(null, row.section); cell.colSpan = colCount; tr.appendChild(cell);
    } else if (row.group) {
      tr.className = "group";
      const cell = td(null, row.group); cell.colSpan = colCount; tr.appendChild(cell);
    } else if (row.spacer) {
      const cell = td(null, ""); cell.colSpan = colCount; cell.style.borderBottom = "none"; tr.appendChild(cell);
    } else {
      const labelCell = td(null, typeof row.label === "function" ? row.label(inputs) : row.label);
      if (typeof row.label === "function") row._labelCell = labelCell;
      tr.append(labelCell, td(null, row.unit));

      if (row.kind === "central") {
        if (row.key) tr.appendChild(td(null, makeInput(row, () => inputs[row.key], (v) => (inputs[row.key] = v))));
        else { const c = td("calc", formatValue(row, row.calc(inputs))); row._calcCells = [[c, null]]; tr.appendChild(c); }
        for (const l of visibleLocos()) tr.appendChild(td(l.isReference ? "ref" : ""));
      } else {
        tr.appendChild(td());
        row._calcCells = [];
        for (const l of visibleLocos()) {
          const cls = l.isReference ? "ref" : "";
          if (row.when && !row.when(l)) { tr.appendChild(td(cls)); continue; }
          const li = inputs.locomotives[l.id];
          if (row.key) {
            tr.appendChild(td(cls, makeInput(row, () => li[row.key], (v) => (li[row.key] = v))));
          } else {
            const c = td(`${cls} calc`, formatValue(row, row.calc(li)));
            row._calcCells.push([c, l.id]);
            tr.appendChild(c);
          }
        }
      }
    }
    body.appendChild(tr);
  }
}

function refreshDerived() {
  for (const row of ROWS) {
    if (row._labelCell) row._labelCell.textContent = row.label(inputs);
    for (const [cell, locoId] of row._calcCells ?? []) {
      cell.textContent = formatValue(row, locoId ? row.calc(inputs.locomotives[locoId]) : row.calc(inputs));
    }
  }
}

const RESULT_ROWS = [
  { label: "Miete und Instandhaltung", key: "rentAndMaintenance" },
  { label: "Energiekosten", key: "energyCost" },
  { label: "Summe der Betriebskosten*", key: "totalOperatingCost", total: true },
  { label: "Kostenunterschied zur Traxx Shunter", key: "differenceToReference", diff: true },
];

function renderResults(result) {
  const body = document.getElementById("results");
  body.replaceChildren();
  const colCount = 3 + visibleLocos().length;

  const head = document.createElement("tr");
  head.className = "section";
  const h = td(null, "Betriebskosten"); h.colSpan = colCount; head.appendChild(h);
  body.appendChild(head);

  const byId = Object.fromEntries((result?.locomotives ?? []).map((r) => [r.id, r]));
  for (const row of RESULT_ROWS) {
    const tr = document.createElement("tr");
    tr.className = "result" + (row.total ? " total" : "");
    tr.append(td(null, row.label), td(null, "€ / Monat"), td());
    for (const l of visibleLocos()) {
      const r = byId[l.id];
      let cls = l.isReference ? "ref" : "";
      let text = "";
      if (r) {
        const v = r[row.key];
        if (v === null || v === undefined) {
          // Entspricht #NV in der Excel (z.B. Laufleistung außerhalb der Traxx-Vertragstabelle)
          if (!(row.diff && l.isReference)) { text = "n. v."; cls += " err"; }
        } else {
          text = (row.diff && v > 0 ? "+" : "") + fmtEuro.format(v) + " €";
          if (row.diff) cls += v > 0 ? " pos" : v < 0 ? " neg" : "";
        }
      }
      tr.appendChild(td(cls, text));
    }
    body.appendChild(tr);
  }
  renderChart(result);
}

// ---- Diagramm: gestapelte Balken je Lok (Miete und Instandhaltung + Energie) -------------
const CHART_SEGMENTS = [
  { key: "rentAndMaintenance", label: "Miete und Instandhaltung", cls: "s-rent" },
  { key: "energyCost", label: "Energiekosten", cls: "s-energy" },
];

function renderChart(result) {
  const chart = document.getElementById("chart");
  chart.replaceChildren();
  const byId = Object.fromEntries((result?.locomotives ?? []).map((r) => [r.id, r]));
  const locos = visibleLocos();
  const totals = locos.map((l) => byId[l.id]?.totalOperatingCost).filter((v) => typeof v === "number");
  const max = Math.max(0, ...totals);
  const ref = byId[model.locomotives.find((l) => l.isReference)?.id]?.totalOperatingCost;
  const euro = (v) => fmtEuro.format(v) + " €";

  for (const l of locos) {
    const r = byId[l.id];
    const row = document.createElement("div");
    row.className = "chart-row" + (l.isReference ? " ref" : "");

    const name = document.createElement("div");
    name.className = "chart-name";
    name.innerHTML = `${escapeHtml(l.name)}<br><span class="muted">${escapeHtml(l.isReference ? "Referenz" : l.subtitle)}</span>`;

    const track = document.createElement("div");
    track.className = "chart-track";
    const value = document.createElement("div");
    value.className = "chart-value";

    if (r && typeof r.totalOperatingCost === "number" && max > 0) {
      const bar = document.createElement("div");
      bar.className = "chart-bar";
      bar.style.width = `${(r.totalOperatingCost / max) * 100}%`;
      for (const s of CHART_SEGMENTS) {
        const v = r[s.key];
        if (!(v > 0)) continue;
        const seg = document.createElement("div");
        seg.className = `chart-seg ${s.cls}`;
        seg.style.flexGrow = v;
        seg.dataset.tip = `${l.name}\n${s.label}: ${euro(v)}\nSumme: ${euro(r.totalOperatingCost)}`;
        bar.appendChild(seg);
      }
      track.appendChild(bar);
      const d = r.differenceToReference;
      value.innerHTML = `<strong>${euro(r.totalOperatingCost)}</strong>` +
        (typeof d === "number" ? `<br><span class="muted">${d > 0 ? "+" : ""}${euro(d)} ggü. Referenz</span>` : "");
    } else if (r) {
      value.innerHTML = `<span class="muted">n. v.</span>`;
    }
    row.append(name, track, value);
    chart.appendChild(row);
  }

  // Senkrechte Referenzlinie bei der Summe der Traxx Shunter
  if (typeof ref === "number" && max > 0) {
    for (const track of chart.querySelectorAll(".chart-track")) {
      const line = document.createElement("div");
      line.className = "chart-ref";
      line.style.left = `${(ref / max) * 100}%`;
      track.appendChild(line);
    }
  }
}

function setupChartTooltip() {
  const chart = document.getElementById("chart");
  const tip = document.getElementById("chart-tip");
  chart.addEventListener("mousemove", (e) => {
    const seg = e.target.closest(".chart-seg");
    if (!seg) { tip.hidden = true; return; }
    tip.textContent = seg.dataset.tip;
    tip.hidden = false;
    const box = chart.parentElement.getBoundingClientRect();
    tip.style.left = `${e.clientX - box.left + 12}px`;
    tip.style.top = `${e.clientY - box.top + 12}px`;
  });
  chart.addEventListener("mouseleave", () => { tip.hidden = true; });
}

// ---- Lok-Filter ---------------------------------------------------------------------------
function buildLocoFilter() {
  const box = document.getElementById("loco-filter");
  for (const l of model.locomotives) {
    const label = document.createElement("label");
    const cb = document.createElement("input");
    cb.type = "checkbox";
    cb.checked = !hidden.has(l.id);
    if (l.isReference) {
      // Die Referenzlok (Traxx Shunter) ist immer Teil des Vergleichs.
      cb.disabled = true;
      label.className = "locked";
      label.title = "Referenzlok – immer im Vergleich";
    }
    cb.addEventListener("change", () => {
      if (cb.checked) hidden.delete(l.id); else hidden.add(l.id);
      try { localStorage.setItem(HIDDEN_KEY, JSON.stringify([...hidden])); } catch { /* optional */ }
      buildHeader();
      renderAssumptions();
      renderResults(lastResult);
    });
    label.append(cb, l.name);
    box.appendChild(label);
  }
}

function loadHidden() {
  try {
    const saved = JSON.parse(localStorage.getItem(HIDDEN_KEY) ?? "[]");
    const ids = new Set(model.locomotives.filter((l) => !l.isReference).map((l) => l.id));
    if (Array.isArray(saved)) return new Set(saved.filter((id) => ids.has(id)));
  } catch { /* ignorieren */ }
  return new Set();
}

// ---- Server-Kommunikation -----------------------------------------------------------------
function onChange() {
  refreshDerived();
  try { localStorage.setItem(STORAGE_KEY, JSON.stringify(inputs)); } catch { /* optional */ }
  clearTimeout(pending);
  pending = setTimeout(calculate, 200);
}

async function calculate() {
  const status = document.getElementById("status");
  try {
    const res = await fetch("/api/calculate", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(inputs),
    });
    if (res.status === 400) {
      const problem = await res.json();
      status.textContent = "Bitte Eingaben prüfen: " + Object.values(problem.errors ?? {}).flat().join(" ");
      return;
    }
    if (!res.ok) throw new Error(`HTTP ${res.status}`);
    const result = await res.json();
    lastResult = result;
    renderResults(result);
    const errors = result.locomotives.filter((l) => l.error).map((l) => l.error);
    status.textContent = errors.join(" ");
  } catch (e) {
    status.textContent = "Berechnung fehlgeschlagen: " + e.message;
  }
}

function loadInputs() {
  try {
    const saved = JSON.parse(localStorage.getItem(STORAGE_KEY) ?? "null");
    if (saved && saved.locomotives && model.locomotives.every((l) => saved.locomotives[l.id])) return saved;
  } catch { /* ignorieren */ }
  return structuredClone(model.defaults);
}

async function init() {
  const res = await fetch("/api/model");
  model = await res.json();
  inputs = loadInputs();
  hidden = loadHidden();
  buildLocoFilter();
  setupChartTooltip();
  buildHeader();
  renderAssumptions();
  renderResults(null);
  await calculate();

  document.getElementById("reset").addEventListener("click", () => {
    inputs = structuredClone(model.defaults);
    renderAssumptions();
    onChange();
    document.getElementById("status").textContent = "";
  });
  document.getElementById("print").addEventListener("click", () => window.print());
}

init().catch((e) => { document.getElementById("status").textContent = "Laden fehlgeschlagen: " + e.message; });
