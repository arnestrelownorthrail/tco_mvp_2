// Kundenansicht – Nachbau des Reiters "Betriebskosten".
// Gerechnet wird ausschließlich auf dem Server (/api/calculate); hier werden nur Eingaben
// erfasst und die Ergebnisse angezeigt.
"use strict";

const STORAGE_KEY = "tco-mvp-inputs-v1";
const BASE_KEY = "tco-mvp-inputs-base-v1"; // Standardannahmen, auf denen die gespeicherten Eingaben beruhen
const HIDDEN_KEY = "tco-mvp-hidden-locos-v1";
const { nf, formatValue, toInputText, parseInput, isValid, errorText, withError } = window.TcoFormat;
const { T, TS, locale } = window.TcoI18n;
const fmtEuro = nf(0);

let model = null;   // { locomotives: [...], defaults: {...} }
let inputs = null;  // aktueller Eingabestand
let pending = null;
let hidden = new Set(); // IDs der ausgeblendeten Loks (Standard: alle sichtbar)
let lastResult = null;

const visibleLocos = () => model.locomotives.filter((l) => !hidden.has(l.id));

const ROWS = window.TcoRows;
const rowLabel = (row) => (typeof row.label === "function" ? row.label(inputs) : T(row.label));

// ---- Aufbau der Tabelle -------------------------------------------------------------------
function buildHeader() {
  const tr = document.getElementById("head-row");
  while (tr.children.length > 2) tr.lastChild.remove();
  for (const l of visibleLocos()) {
    const th = document.createElement("th");
    if (l.isReference) th.className = "ref";
    th.innerHTML = `${escapeHtml(l.name)}<br><span class="muted">${escapeHtml(T(l.subtitle))}</span>`;
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
  el.setAttribute("aria-label", rowLabel(row));
  const { wrap, setError } = withError(el);
  el.addEventListener("input", () => {
    const v = parseInput(row, el.value);
    const ok = isValid(row, v);
    el.classList.toggle("invalid", !ok);
    setError(errorText(row, v));
    if (ok) { set(v); onChange(); }
  });
  el.addEventListener("blur", () => { if (!el.classList.contains("invalid")) el.value = toInputText(row, get()); });
  return wrap;
}

// Block "Zentrale Inputs": gilt für alle Loks, getrennt von der Lok-Tabelle
function renderCentral() {
  const box = document.getElementById("central");
  box.replaceChildren();
  let group = null;
  for (const row of ROWS.central) {
    if (row.group) {
      group = document.createElement("div");
      group.className = "central-group";
      const h = document.createElement("h3");
      h.textContent = T(row.group);
      group.appendChild(h);
      box.appendChild(group);
      continue;
    }
    const line = document.createElement("div");
    line.className = "central-row" + (row.indent ? ` indent-${row.indent}` : "");
    const label = document.createElement("span");
    label.textContent = rowLabel(row);
    if (typeof row.label === "function") row._labelCell = label;
    let value;
    if (row.key) {
      value = makeInput(row, () => inputs[row.key], (v) => (inputs[row.key] = v));
    } else {
      value = document.createElement("span");
      value.className = "calc";
      value.textContent = formatValue(row, row.calc(inputs));
      row._calcCells = [[value, null]];
    }
    const unit = document.createElement("span");
    unit.className = "unit";
    unit.textContent = T(row.unit);
    line.append(label, value, unit);
    group.appendChild(line);
  }
}

function renderAssumptions() {
  const body = document.getElementById("assumptions");
  body.replaceChildren();
  const colCount = 2 + visibleLocos().length;

  for (const row of ROWS.loco) {
    const tr = document.createElement("tr");
    if (row.section || row.group) {
      tr.className = row.section ? "section" : "group";
      const cell = td(null, T(row.section ?? row.group)); cell.colSpan = colCount; tr.appendChild(cell);
    } else {
      const labelCell = td(null, rowLabel(row));
      if (row.indent) labelCell.classList.add(`indent-${row.indent}`);
      tr.append(labelCell, td(null, T(row.unit)));
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
    body.appendChild(tr);
  }
}

function refreshDerived() {
  for (const row of [...ROWS.central, ...ROWS.loco]) {
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
  const colCount = 2 + visibleLocos().length;

  const head = document.createElement("tr");
  head.className = "section";
  const h = td(null, T("Betriebskosten")); h.colSpan = colCount; head.appendChild(h);
  body.appendChild(head);

  const byId = Object.fromEntries((result?.locomotives ?? []).map((r) => [r.id, r]));
  for (const row of RESULT_ROWS) {
    const tr = document.createElement("tr");
    tr.className = "result" + (row.total ? " total" : "");
    tr.append(td(null, T(row.label)), td(null, T("€ / Monat")));
    for (const l of visibleLocos()) {
      const r = byId[l.id];
      let cls = l.isReference ? "ref" : "";
      let text = "";
      if (r) {
        const v = r[row.key];
        if (v === null || v === undefined) {
          // Entspricht #NV in der Excel (z.B. Laufleistung außerhalb der Traxx-Vertragstabelle)
          if (!(row.diff && l.isReference)) { text = T("n. v."); cls += " err"; }
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
    name.innerHTML = `${escapeHtml(l.name)}<br><span class="muted">${escapeHtml(T(l.isReference ? "Referenz" : l.subtitle))}</span>`;

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
        seg.dataset.tip = `${l.name}\n${T(s.label)}: ${euro(v)}\n${T("Summe")}: ${euro(r.totalOperatingCost)}`;
        bar.appendChild(seg);
      }
      track.appendChild(bar);
      const d = r.differenceToReference;
      value.innerHTML = `<strong>${euro(r.totalOperatingCost)}</strong>` +
        (typeof d === "number" ? `<br><span class="muted ${d > 0 ? "pos" : d < 0 ? "neg" : ""}">${d > 0 ? "+" : ""}${euro(d)} ${T("ggü. Referenz")}</span>` : "");
    } else if (r) {
      value.innerHTML = `<span class="muted">${T("n. v.")}</span>`;
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
      label.title = T("Referenzlok – immer im Vergleich");
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
  try {
    localStorage.setItem(STORAGE_KEY, JSON.stringify(inputs));
    localStorage.setItem(BASE_KEY, JSON.stringify(model.defaults));
  } catch { /* optional */ }
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
      status.textContent = T("Bitte Eingaben prüfen: ") + Object.values(problem.errors ?? {}).flat().map(TS).join(" ");
      return;
    }
    if (!res.ok) throw new Error(`HTTP ${res.status}`);
    const result = await res.json();
    lastResult = result;
    renderResults(result);
    const errors = result.locomotives.filter((l) => l.error).map((l) => TS(l.error));
    status.textContent = errors.join(" ");
  } catch (e) {
    status.textContent = T("Berechnung fehlgeschlagen: ") + e.message;
  }
}

function loadInputs() {
  try {
    // Wurden die Standardannahmen seitdem im Admin-Bereich geändert, gelten die neuen Werte.
    if (localStorage.getItem(BASE_KEY) !== JSON.stringify(model.defaults)) return structuredClone(model.defaults);
    const saved = JSON.parse(localStorage.getItem(STORAGE_KEY) ?? "null");
    if (saved && saved.locomotives && model.locomotives.every((l) => saved.locomotives[l.id])) return saved;
  } catch { /* ignorieren */ }
  return structuredClone(model.defaults);
}

// Tabellenkopf klebt direkt unter dem festen Kopfbereich
function setupStickyOffset() {
  const bar = document.querySelector(".sticky-top");
  const set = () => document.documentElement.style.setProperty("--sticky-h", `${bar.offsetHeight}px`);
  new ResizeObserver(set).observe(bar);
  set();
}

async function init() {
  const res = await fetch("/api/model");
  model = await res.json();
  inputs = loadInputs();
  hidden = loadHidden();
  buildLocoFilter();
  setupChartTooltip();
  setupStickyOffset();
  buildHeader();
  renderCentral();
  renderAssumptions();
  renderResults(null);
  await calculate();

  document.getElementById("reset").addEventListener("click", () => {
    inputs = structuredClone(model.defaults);
    renderCentral();
    renderAssumptions();
    onChange();
    document.getElementById("status").textContent = "";
  });
  document.getElementById("print").addEventListener("click", () => window.print());
}

init().catch((e) => { document.getElementById("status").textContent = T("Laden fehlgeschlagen: ") + e.message; });

// Druckdatum (Fußzeile im PDF); steht hier statt inline, weil die CSP keine Inline-Skripte erlaubt
document.getElementById("print-date").textContent = new Date().toLocaleString(locale, { dateStyle: "short", timeStyle: "short" });
