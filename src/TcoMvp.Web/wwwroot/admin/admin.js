// Admin-Bereich: Standardannahmen der Kundenansicht bearbeiten (/api/admin/defaults).
"use strict";

const { T, TS } = window.TcoI18n;
const { toInputText, parseInput, isValid, errorText, withError } = window.TcoFormat;
// Nur Eingabezeilen; berechnete Zeilen haben keine Standardannahme
const editable = (rows) => rows.filter((r) => r.section || r.group || r.key);
const ROWS = { central: editable(window.TcoRows.central), loco: editable(window.TcoRows.loco) };

let locos = [];      // Loks der Kundenansicht (aus /api/model)
let saved = null;    // { current, original, customized } – Stand auf dem Server
let draft = null;    // bearbeitete Standardannahmen
let invalid = new Set();

const label = (row) => (typeof row.label === "function" ? row.label(draft) : T(row.label));

function status(text, ok = false) {
  const el = document.getElementById("status");
  el.textContent = text;
  el.classList.toggle("ok", ok);
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

function buildHeader() {
  const tr = document.getElementById("head-row");
  for (const l of locos) {
    const th = document.createElement("th");
    if (l.isReference) th.className = "ref";
    th.innerHTML = `${escapeHtml(l.name)}<br><span class="muted">${escapeHtml(T(l.subtitle))}</span>`;
    tr.appendChild(th);
  }
}

function makeInput(row, id, get, set, original) {
  const el = document.createElement("input");
  el.type = "text";
  el.inputMode = "decimal";
  el.value = toInputText(row, get());
  el.setAttribute("aria-label", label(row));
  const { wrap, setError } = withError(el);
  const markChanged = () => {
    const changed = get() !== original;
    el.classList.toggle("changed", changed);
    el.title = changed ? T("Auslieferungswert: {0}", `${toInputText(row, original)}${row.pct ? " %" : ""}`) : "";
  };
  markChanged();
  el.addEventListener("input", () => {
    const v = parseInput(row, el.value);
    const ok = isValid(row, v);
    el.classList.toggle("invalid", !ok);
    setError(errorText(row, v));
    if (ok) { invalid.delete(id); set(v); markChanged(); refreshLabels(); } else invalid.add(id);
    updateButtons();
  });
  el.addEventListener("blur", () => { if (!el.classList.contains("invalid")) el.value = toInputText(row, get()); });
  return wrap;
}

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
    const labelEl = document.createElement("span");
    labelEl.textContent = label(row);
    if (typeof row.label === "function") row._labelCell = labelEl;
    const unit = document.createElement("span");
    unit.className = "unit";
    unit.textContent = T(row.unit);
    line.append(labelEl,
      makeInput(row, row.key, () => draft[row.key], (v) => (draft[row.key] = v), saved.original[row.key]), unit);
    group.appendChild(line);
  }
  // Gruppen teilen sich die Zeilen des Rasters, damit Zeilen nebeneinander gleich hoch sind
  for (const g of box.children) g.style.setProperty("--rows", g.children.length);
}

function render() {
  const body = document.getElementById("assumptions");
  body.replaceChildren();
  invalid = new Set();
  renderCentral();
  const colCount = 2 + locos.length;
  const original = saved.original;

  for (const row of ROWS.loco) {
    const tr = document.createElement("tr");
    if (row.section || row.group) {
      tr.className = row.section ? "section" : "group";
      const cell = td(null, T(row.section ?? row.group));
      cell.colSpan = colCount;
      tr.appendChild(cell);
    } else {
      const labelCell = td(null, label(row));
      if (row.indent) labelCell.classList.add(`indent-${row.indent}`);
      tr.append(labelCell, td(null, T(row.unit)));
      for (const l of locos) {
        const cls = l.isReference ? "ref" : "";
        if (row.when && !row.when(l)) { tr.appendChild(td(cls)); continue; }
        const li = draft.locomotives[l.id];
        tr.appendChild(td(cls, makeInput(row, `${l.id}.${row.key}`, () => li[row.key], (v) => (li[row.key] = v),
          original.locomotives[l.id]?.[row.key])));
      }
    }
    body.appendChild(tr);
  }
  document.getElementById("origin").textContent = saved.customized
    ? T("Aktiv: im Admin-Bereich gespeicherte Werte.")
    : T("Aktiv: Auslieferungswerte aus model-data.json.");
  updateButtons();
}

function refreshLabels() {
  for (const row of ROWS.central) if (row._labelCell) row._labelCell.textContent = label(row);
}

const dirty = () => JSON.stringify(draft) !== JSON.stringify(saved.current);

function updateButtons() {
  document.getElementById("save").disabled = invalid.size > 0 || !dirty();
  document.getElementById("discard").disabled = !dirty();
  document.getElementById("restore").disabled = !saved.customized;
}

function apply(info) {
  saved = info;
  draft = structuredClone(info.current);
  render();
}

async function request(method, body) {
  const res = await fetch("/api/admin/defaults", {
    method,
    headers: body ? { "Content-Type": "application/json" } : {},
    body: body ? JSON.stringify(body) : undefined,
  });
  if (res.status === 400) {
    const problem = await res.json();
    const errors = Object.entries(problem.errors ?? {}).map(([k, v]) => `${k}: ${v.map(TS).join(" ")}`);
    throw new Error(T("Bitte Eingaben prüfen: ") + errors.join("; "));
  }
  if (!res.ok) throw new Error(`HTTP ${res.status}`);
  return res.json();
}

async function init() {
  const [model, info] = await Promise.all([fetch("/api/model").then((r) => r.json()), request("GET")]);
  locos = model.locomotives;
  buildHeader();
  apply(info);

  document.getElementById("save").addEventListener("click", async () => {
    try { apply(await request("PUT", draft)); status(T("Gespeichert."), true); }
    catch (e) { status(T("Speichern fehlgeschlagen: ") + e.message); }
  });
  document.getElementById("discard").addEventListener("click", () => { apply(saved); status(""); });
  document.getElementById("restore").addEventListener("click", async () => {
    if (!confirm(T("Gespeicherte Standardannahmen löschen und die Auslieferungswerte verwenden?"))) return;
    try { apply(await request("DELETE")); status(T("Auslieferungswerte wiederhergestellt."), true); }
    catch (e) { status(T("Zurücksetzen fehlgeschlagen: ") + e.message); }
  });
  window.addEventListener("beforeunload", (e) => { if (dirty()) e.preventDefault(); });
}

init().catch((e) => status(T("Laden fehlgeschlagen: ") + e.message));
