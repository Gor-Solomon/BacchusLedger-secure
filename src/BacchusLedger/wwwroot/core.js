// Bacchus Ledger — core: helpers, source chips, icons, Jury/Expert mode, shell and router. No external libraries, works offline.
"use strict";

// ---------------------------------------------------------------- helpers
const $ = (s, el = document) => el.querySelector(s);
const $$ = (s, el = document) => Array.from(el.querySelectorAll(s));
const esc = (s) => String(s ?? "").replace(/[&<>"']/g, (c) => ({ "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&#39;" }[c]));
const app = () => $("#app");

async function api(path, opts = {}) {
  const o = { ...opts, headers: { "content-type": "application/json", ...(opts.headers || {}) } };
  if (o.body && typeof o.body !== "string") o.body = JSON.stringify(o.body);
  const r = await fetch(path, o);
  const j = await r.json().catch(() => ({}));
  if (!r.ok) throw new Error(j.error || r.statusText);
  return j;
}
const post = (path, body) => api(path, { method: "POST", body: body || {} });

function fmt(v, d = 0) {
  if (v === null || v === undefined || v === "" || Number.isNaN(Number(v))) return "—";
  return Number(v).toLocaleString("en-US", { minimumFractionDigits: d, maximumFractionDigits: d });
}
function fmtA(v, max = 2) {
  if (v === null || v === undefined || v === "") return "—";
  return Number(v).toLocaleString("en-US", { maximumFractionDigits: max });
}
const usd = (v) => "$" + fmt(v, 2);
function dt(iso) { if (!iso) return "—"; const s = String(iso); return s.length >= 10 ? `${s.slice(8, 10)}.${s.slice(5, 7)}.${s.slice(0, 4)}` : s; }
function dtt(iso) { if (!iso) return "—"; const s = String(iso); return s.length >= 16 ? `${dt(s)} ${s.slice(11, 16)}` : dt(s); }
const localNow = () => new Date(Date.now() - new Date().getTimezoneOffset() * 60000).toISOString().slice(0, 16);
const svgEl = (w, h, inner, label, cls = "") => `<div class="svgbox ${cls}"><svg viewBox="0 0 ${w} ${h}" role="img" aria-label="${esc(label)}">${inner}</svg></div>`;
const reducedMotion = () => window.matchMedia && window.matchMedia("(prefers-reduced-motion: reduce)").matches;

// ---------------------------------------------------------------- Jury / Expert mode (remembered in localStorage)
let MODE = (() => { try { return localStorage.getItem("bl.mode") || "jury"; } catch { return "jury"; } })();
const isJury = () => MODE === "jury";
function setMode(m) {
  MODE = m;
  try { localStorage.setItem("bl.mode", m); } catch { /* storage unavailable */ }
  document.body.classList.toggle("mode-jury", m === "jury");
  document.body.classList.toggle("mode-expert", m === "expert");
  $$("#modeSwitch button").forEach((b) => b.classList.toggle("on", b.dataset.mode === m));
}
/** Tables and long detail: collapsed behind "Show details" in Jury mode, open in Expert mode. */
const more = (title, html, cls = "") => `<details class="more ${cls}" ${isJury() ? "" : "open"}><summary>${esc(title)}</summary><div class="more-body">${html}</div></details>`;

// ---------------------------------------------------------------- source chips
function chipClass(tag) {
  if (!tag) return "entered";
  if (tag.startsWith("Dataset")) return "ds";
  if (tag === "Calculated") return "calc";
  if (tag === "DEMO ASSUMPTION") return "demo";
  if (tag === "SIMULATED") return "sim";
  if (tag.startsWith("Entered")) return "entered";
  if (tag === "time not in dataset") return "notime";
  return "ref";
}
/** Source chip. Expert: visible chip. Jury: a small "i" that shows the chip on hover/tap. */
const chip = (tag, title) => (tag ? `<span class="src" tabindex="0"><span class="src-i" aria-hidden="true">i</span><span class="chip ${chipClass(tag)}" title="${esc(title || "Source: " + tag)}">${esc(tag)}</span></span>` : "");
/** Several sources behind one marker (Jury shows a single "i"). Items: tag or [tag, title]. */
const chips = (...items) => `<span class="src multi" tabindex="0"><span class="src-i" aria-hidden="true">i</span><span class="chips">${items.map((it) => {
  const [tag, title] = Array.isArray(it) ? it : [it];
  return `<span class="chip ${chipClass(tag)}" title="${esc(title || "Source: " + tag)}">${esc(tag)}</span>`;
}).join("")}</span></span>`;
/** Chip whose hover shows the formula (Update 2). */
const chipF = (tag, formula) => chip(tag, formula);
/** SIMULATED chip is never hidden — live data must always be labelled as simulated. */
const simChip = () => `<span class="chip sim always" title="Live data is simulated today. Real sensors plug into the same endpoint.">SIMULATED</span>`;
const N = (v, tag, unit = "", d = 0) => `<span class="num">${fmt(v, d)}${unit ? " " + unit : ""}</span>${chip(tag)}`;

/** Printable ledger report (read-only) in a new tab; vessel = "W01/F-2" adds the SIMULATED live section. */
function openReport(batch, vessel) {
  const q = new URLSearchParams();
  if (batch) q.set("batch", batch);
  if (vessel) q.set("vessel", vessel);
  window.open(`report.html?${q}`, "_blank", "noopener");
}

// ---------------------------------------------------------------- icons (Lucide, vendored in /icons, ISC)
const ico = (name, cls = "") => `<span class="ico ${cls}" style="--i:url('icons/${name}.svg')" aria-hidden="true"></span>`;

// ---------------------------------------------------------------- vessels: cellar label first, internal code small
let VMAP = {};
async function loadVessels() {
  try { const vs = await api("/api/vessels"); VMAP = {}; vs.forEach((v) => { VMAP[`${v.winery}/${v.code}`] = v; if (!VMAP[v.code]) VMAP[v.code] = v; }); return vs; }
  catch { return []; }
}
function vfind(code, winery) { return (winery && VMAP[`${winery}/${code}`]) || VMAP[code]; }
function vname(code, winery) {
  if (!code) return "";
  const v = vfind(code, winery);
  return v && v.cellar_label ? `<span class="vlabel">${esc(v.cellar_label)}</span><span class="vcode">${esc(code)}</span>` : `<span class="vlabel">${esc(code)}</span>`;
}
function vshort(code, winery) { const v = vfind(code, winery); return v && v.cellar_label ? v.cellar_label : code; }
function vtext(v) { return v.cellar_label ? `${v.cellar_label} (${v.code})` : v.code; }

// ---------------------------------------------------------------- labels
const OP_LABEL = { intake: "Intake", separation: "Separation", racking: "Racking", racking_first: "First racking", stock_check: "Stock check",
  filtration: "Filtration", transfer: "Transfer", bottling: "Bottling", warehouse: "Warehouse" };
function opLabel(k) {
  if (k === "racking_first") return LANG === "ka" ? t("Racking") + " (I)" : "First racking";
  return t(OP_LABEL[k] || k);
}
/** Plain words for Jury mode ("Wine lost", not "racking_first loss"). */
const PLAIN = { intake: "Grapes in", separation: "Pressed off", racking_first: "First racking", racking: "Racking", stock_check: "Stock check",
  filtration: "Filtering", transfer: "Transfer", bottling: "Bottling", warehouse: "To warehouse" };
const plain = (k) => (LANG === "ka" ? opLabel(k) : PLAIN[k] || opLabel(k));
const OP_ICON = { intake: "grape", separation: "wine", racking: "barrel", racking_first: "barrel", stock_check: "scale", filtration: "flask", transfer: "barrel",
  bottling: "wine", warehouse: "warehouse", fermentation: "thermometer" };

function statusBadge(s) {
  const txt = { ok: t("Normal"), above: t("Above normal"), negative: "Check the reading", pass: "✓", fail: "✗", info: "info", "n/a": "—", na: "—" }[s] || s;
  return `<span class="status ${s === "n/a" ? "na" : esc(s)}">${esc(txt)}</span>`;
}
function checkIcon(s) {
  return { pass: `<span class="ck pass">${ico("check-circle")}</span>`, fail: `<span class="ck fail">${ico("alert-triangle")}</span>`,
    info: `<span class="ck info">${ico("info")}</span>`, na: '<span class="ck na">–</span>' }[s] || "";
}

// ---------------------------------------------------------------- toasts (top-right)
function toast(msg, level = "info", ms, html = false) {
  let box = $("#toasts");
  if (!box) { box = document.createElement("div"); box.id = "toasts"; box.setAttribute("aria-live", "polite"); document.body.appendChild(box); }
  const d = document.createElement("div");
  d.className = `toast ${level}`;
  const icon = { red: "alert-triangle", amber: "alert-triangle", green: "check-circle", info: "info" }[level] || "info";
  d.innerHTML = `${ico(icon)}<div>${html ? msg : esc(msg)}</div>`;
  box.appendChild(d);
  setTimeout(() => { d.classList.add("out"); setTimeout(() => d.remove(), 250); }, ms ?? (level === "info" ? 2600 : 6000));
}

// ---------------------------------------------------------------- animated numbers (money counter, KPI tiles)
function countUp(el, to, { d = 0, prefix = "", suffix = "", from = 0, ms = 700 } = {}) {
  if (!el) return;
  if (reducedMotion() || from === to) { el.textContent = prefix + fmt(to, d) + suffix; return; }
  const t0 = performance.now();
  const stepf = (now) => {
    const k = Math.min(1, (now - t0) / ms), e = 1 - Math.pow(1 - k, 3);
    el.textContent = prefix + fmt(from + (to - from) * e, d) + suffix;
    if (k < 1) requestAnimationFrame(stepf);
  };
  requestAnimationFrame(stepf);
}

// ---------------------------------------------------------------- shell
const NAV = [
  ["Story", [["overview", "Overview", "layout-dashboard"], ["cellar", "Cellar", "map"], ["live", "Live board", "tv"], ["batches", "Batches", "layers"]]],
  ["Record", [["record", "Record operation", "pencil-line"], ["readings", "Readings", "thermometer"], ["deliveries", "Deliveries", "truck"],
    ["farmers", "Farmers & plots", "users"], ["vessels", "Vessels", "barrel"]]],
  ["Reports", [["journals", "Journals", "book-open"], ["sources", "Data sources", "database"], ["settings", "Settings", "settings"]]],
];
function renderNav(active) {
  $("#nav").innerHTML = NAV.map(([sec, items]) => `<div class="nav-sec">${esc(sec)}</div>` +
    items.map(([k, l, i]) => `<a href="#/${k}" class="${k === active ? "active" : ""}">${ico(i)}<span>${esc(t(l))}</span></a>`).join("")).join("");
  $$(".lang button").forEach((b) => b.classList.toggle("on", b.dataset.lang === LANG));
}

const ROUTES = [
  [/^\/?(overview)?$/, "overview", () => pageOverview()],
  [/^\/cellar$/, "cellar", (m, q) => pageCellar(q)],
  [/^\/live$/, "live", () => pageLiveBoard()],
  [/^\/batches$/, "batches", () => pageBatches()],
  [/^\/batch\/(\d+)$/, "batches", (m, q) => pageBatch(+m[1], q)],
  [/^\/history\/(\d+)$/, "batches", (m) => pageHistory(+m[1])],
  [/^\/record$/, "record", (m, q) => pageRecord(q)],
  [/^\/readings$/, "readings", (m, q) => pageReadings(q)],
  [/^\/demo\/readings$/, "overview", async () => { await pageOverview(); await RPlayer.open(); }],
  [/^\/farmers$/, "farmers", () => pageFarmers()],
  [/^\/deliveries$/, "deliveries", () => pageDeliveries()],
  [/^\/vessels$/, "vessels", () => pageVessels()],
  [/^\/vessel\/([^/]+)\/([^/]+)$/, "vessels", (m) => pageVessel(decodeURIComponent(m[1]), decodeURIComponent(m[2]))],
  [/^\/journals$/, "journals", (m, q) => pageJournals(q)],
  [/^\/journal\/(\d)$/, "journals", (m, q) => pageJournal(+m[1], q)],
  [/^\/sources$/, "sources", () => pageSources()],
  [/^\/demo\/(w01|w03)$/, "overview", async (m, q) => { await pageOverview(); await Player.open(m[1]); if (q.get("step")) await Player.go(+q.get("step")); }],
  [/^\/settings$/, "settings", () => pageSettings()],
];

async function route() {
  const h = location.hash.replace(/^#/, "") || "/overview";
  const [path, qs] = h.split("?");
  const q = new URLSearchParams(qs || "");
  $("#sidebar").classList.remove("open");
  if (typeof ReadingsView !== "undefined" && ReadingsView.tipEl) ReadingsView.tipEl.classList.add("hidden");
  if (typeof Live !== "undefined") { Live.clearPageHandlers(); Live.closeDrawer(); }
  document.body.classList.remove("board");
  for (const [re, nav, fn] of ROUTES) {
    const m = path.match(re);
    if (m) {
      renderNav(nav);
      await loadVessels();
      try { await fn(m, q); } catch (e) { app().innerHTML = `<div class="card"><p class="err">Error: ${esc(e.message)}</p></div>`; console.error(e); }
      // the first page heading becomes the top-bar title
      const h1 = $("#app h1");
      $("#pageTitle").innerHTML = h1 ? h1.innerHTML : esc(t(NAV.flatMap((s) => s[1]).find((x) => x[0] === nav)?.[1] || ""));
      if (h1) h1.classList.add("page-h1");
      app().focus({ preventScroll: true });
      return;
    }
  }
  app().innerHTML = `<p>Page not found. <a href="#/overview">Overview</a></p>`;
}

function boot() {
  $("#menuBtn").onclick = () => $("#sidebar").classList.toggle("open");
  $$(".lang button").forEach((b) => (b.onclick = () => { setLang(b.dataset.lang); post("/api/settings", { language: b.dataset.lang }).catch(() => {}); route(); }));
  $$("#modeSwitch button").forEach((b) => (b.onclick = () => { setMode(b.dataset.mode); route(); }));
  window.addEventListener("hashchange", route);
  setLang(LANG);
  setMode(MODE);
  Live.init();
  route();
}
