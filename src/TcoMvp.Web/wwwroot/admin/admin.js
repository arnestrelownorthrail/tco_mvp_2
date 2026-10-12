// Admin-Bereich: Standardannahmen der Kundenansicht bearbeiten (/api/admin/defaults).
"use strict";

const { toInputText, parseInput, isValid } = window.TcoFormat;
const ROWS = window.TcoRows.filter((r) => r.section || r.group || r.spacer || r.key);

let locos = [];      // Loks der Kundenansicht (aus /api/model)
let saved = null;    // { current, original, customized } – Stand auf dem Server
let draft = null;    // bearbeitete Standardannahmen
let invalid = new Set();

const label = (row) => (typeof row.label === "function" ? row.label(draft) : row.label);

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
    th.innerHTML = `${escapeHtml(l.name)}<br><span class="muted">${escapeHtml(l.subtitle)}</span>`;
    tr.appendChild(th);
  }
}

function makeInput(row, id, get, set, original) {
  const el = document.createElement("input");
  el.type = "text";
  el.inputMode = "decimal";
  el.value = toInputText(row, get());
  el.setAttribute("aria-label", label(row));
  const markChanged = () => {
    const changed = get() !== original;
    el.classList.toggle("changed", changed);
    el.title = changed ? `Auslieferungswert: ${toInputText(row, original)}${row.pct ? " %" : ""}` : "";
  };
  markChanged();
  el.addEventListener("input", () => {
    const v = parseInput(row, el.value);
    const ok = isValid(row, v);
    el.classList.toggle("invalid", !ok);
    if (ok) { invalid.delete(id); set(v); markChanged(); refreshLabels(); } else invalid.add(id);
    updateButtons();
  });
  el.addEventListener("blur", () => { if (!el.classList.contains("invalid")) el.value = toInputText(row, get()); });
  return el;
}

function render() {
  const body = document.getElementById("assumptions");
  body.replaceChildren();
  invalid = new Set();
  const colCount = 3 + locos.length;
  const original = saved.original;

  for (const row of ROWS) {
    const tr = document.createElement("tr");
    if (row.section || row.group || row.spacer) {
      tr.className = row.section ? "section" : row.group ? "group" : "";
      const cell = td(null, row.section ?? row.group ?? "");
      cell.colSpan = colCount;
      if (row.spacer) cell.style.borderBottom = "none";
      tr.appendChild(cell);
    } else {
      const labelCell = td(null, label(row));
      if (typeof row.label === "function") row._labelCell = labelCell;
      if (row.indent) labelCell.classList.add(`indent-${row.indent}`);
      tr.append(labelCell, td(null, row.unit));
      if (row.kind === "central") {
        tr.appendChild(td(null, makeInput(row, row.key, () => draft[row.key], (v) => (draft[row.key] = v), original[row.key])));
        for (const l of locos) tr.appendChild(td(l.isReference ? "ref" : ""));
      } else {
        tr.appendChild(td());
        for (const l of locos) {
          const cls = l.isReference ? "ref" : "";
          if (row.when && !row.when(l)) { tr.appendChild(td(cls)); continue; }
          const li = draft.locomotives[l.id];
          tr.appendChild(td(cls, makeInput(row, `${l.id}.${row.key}`, () => li[row.key], (v) => (li[row.key] = v),
            original.locomotives[l.id]?.[row.key])));
        }
      }
    }
    body.appendChild(tr);
  }
  document.getElementById("origin").textContent = saved.customized
    ? "Aktiv: im Admin-Bereich gespeicherte Werte."
    : "Aktiv: Auslieferungswerte aus model-data.json.";
  updateButtons();
}

function refreshLabels() {
  for (const row of ROWS) if (row._labelCell) row._labelCell.textContent = label(row);
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
    const errors = Object.entries(problem.errors ?? {}).map(([k, v]) => `${k}: ${v.join(" ")}`);
    throw new Error("Bitte Eingaben prüfen: " + errors.join("; "));
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
    try { apply(await request("PUT", draft)); status("Gespeichert.", true); }
    catch (e) { status("Speichern fehlgeschlagen: " + e.message); }
  });
  document.getElementById("discard").addEventListener("click", () => { apply(saved); status(""); });
  document.getElementById("restore").addEventListener("click", async () => {
    if (!confirm("Gespeicherte Standardannahmen löschen und die Auslieferungswerte verwenden?")) return;
    try { apply(await request("DELETE")); status("Auslieferungswerte wiederhergestellt.", true); }
    catch (e) { status("Zurücksetzen fehlgeschlagen: " + e.message); }
  });
  window.addEventListener("beforeunload", (e) => { if (dirty()) e.preventDefault(); });
}

init().catch((e) => status("Laden fehlgeschlagen: " + e.message));
