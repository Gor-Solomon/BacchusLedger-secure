// Bacchus Ledger — LIVE mode client (Update 3). Everything live is SIMULATED and labelled so; it never touches journals or ROI.
"use strict";

const SCENARIOS = [
  ["hot", "Hot fermentation", "thermometer", "F-2 temperature climbs 27 → 32 °C"],
  ["loss", "Unrecorded wine loss", "droplet", "Q-3 level drops 25 L, no operation recorded"],
  ["stuck", "Stuck fermentation", "activity", "F-2 density flat for 48 h (simulated)"],
  ["missing", "Missing data", "alert-triangle", "Fermentation readings, no Initial Brix"],
  ["allgood", "All good", "check-circle", "Back to normal"],
];
const SENSOR_LINE = "Live data is simulated today. Real sensors plug into the same endpoint.";

const Live = {
  state: null, es: null, notes: [], handlers: [], series: {}, retry: 0,

  async init() {
    $("#liveSwitch").onclick = () => this.setOn(!(this.state && this.state.on));
    $("#bell").onclick = () => this.openDrawer();
    try { this.state = await api("/api/live/state"); } catch { /* offline */ }
    try { this.notes = await api("/api/live/events?kind=notification&limit=100"); } catch { this.notes = []; }
    try { (await api("/api/live/events?kind=reading&limit=400")).reverse().forEach((e) => this.push(e)); } catch { /* none */ }
    this.renderSwitch(); this.renderBell();
    this.connect();
  },

  connect() {
    if (!window.EventSource) return;
    this.es = new EventSource("/api/live/stream");
    this.es.addEventListener("state", (e) => { this.state = JSON.parse(e.data); this.renderSwitch(); this.emit("state", this.state); });
    this.es.addEventListener("tick", (e) => this.onTick(JSON.parse(e.data)));
    this.es.addEventListener("sensor", (e) => { const ev = JSON.parse(e.data); this.push(ev); this.emit("sensor", ev); });
    this.es.addEventListener("ack", (e) => { const d = JSON.parse(e.data); this.notes.forEach((n) => { if (d.all || n.id === d.id) n.acknowledged = true; }); this.renderBell(); this.emit("notes"); });
    this.es.addEventListener("cleared", () => { this.notes = []; this.series = {}; this.renderBell(); this.emit("notes"); });
  },

  push(e) {
    if (e.kind !== "reading" || e.value == null) return;
    const k = `${e.vessel_code}:${e.metric}`;
    const s = (this.series[k] = this.series[k] || []);
    s.push({ t: e.sim_time, v: e.value });
    if (s.length > 80) s.shift();
  },

  onTick(p) {
    this.state = p.state;
    p.readings.forEach((r) => this.push(r));
    for (const n of p.notifications) {
      this.notes.unshift(n);
      toast(`${esc(n.message)}${n.value_usd ? ` <span class="toast-usd">≈ ${usd(n.value_usd)}</span>` : ""}<div class="toast-sim">SIMULATED</div>`, n.level, 6000, true);
    }
    if (this.notes.length > 200) this.notes.length = 200;
    this.renderSwitch(); this.renderBell();
    this.emit("tick", p);
    if (p.notifications.length) this.emit("notes");
  },

  on(evt, fn) { this.handlers.push([evt, fn]); },
  emit(evt, data) { this.handlers.filter(([e]) => e === evt).forEach(([, fn]) => { try { fn(data); } catch (err) { console.error(err); } }); },
  clearPageHandlers() { this.handlers = []; clearViz(); },

  async setOn(on) {
    try { this.state = await post(on ? "/api/live/on" : "/api/live/off"); } catch (e) { toast(e.message); }
    this.renderSwitch(); this.emit("state", this.state);
    if (on) toast("Live mode on — data is SIMULATED", "info");
  },
  async scenario(name) {
    try { this.state = await post(`/api/live/scenario/${name}`); this.renderSwitch(); this.emit("state", this.state); }
    catch (e) { toast(e.message); }
  },
  async ack(id) { await post(`/api/live/events/${id}/ack`); const n = this.notes.find((x) => x.id === id); if (n) n.acknowledged = true; this.renderBell(); this.emit("notes"); },
  liveMap() { const m = {}; (this.state?.on ? this.state.vessels : []).forEach((v) => (m[v.code] = v)); return m; },
  unread() { return this.notes.filter((n) => !n.acknowledged && n.level !== "green").length; },

  renderSwitch() {
    const on = !!(this.state && this.state.on);
    const b = $("#liveSwitch");
    b.setAttribute("aria-checked", on ? "true" : "false");
    b.classList.toggle("on", on);
    $("#liveText").textContent = on ? "Live" : "Live off";
  },
  renderBell() {
    const n = this.unread();
    $("#bellCount").textContent = n > 99 ? "99+" : String(n);
    $("#bellCount").classList.toggle("hidden", n === 0);
    $("#bell").setAttribute("aria-label", `Notifications: ${n} unread`);
    if ($("#drawer.open")) this.renderDrawer();
  },

  openDrawer() {
    let d = $("#drawer");
    if (!d) {
      d = document.createElement("aside"); d.id = "drawer"; d.setAttribute("aria-label", "Notification center");
      document.body.appendChild(d);
      const sc = document.createElement("div"); sc.id = "scrim"; sc.onclick = () => this.closeDrawer(); document.body.appendChild(sc);
    }
    d.classList.add("open"); $("#scrim").classList.add("open");
    this.renderDrawer();
  },
  closeDrawer() { $("#drawer")?.classList.remove("open"); $("#scrim")?.classList.remove("open"); },
  renderDrawer() {
    const d = $("#drawer");
    d.innerHTML = `<div class="drawer-head">${ico("bell")}<h3>Notifications</h3>${simChip()}<button class="icon-btn" id="drClose" aria-label="Close">${ico("x")}</button></div>
      <div class="drawer-actions"><button class="btn ghost small" id="drAck">${ico("check-circle")} Acknowledge all</button><a class="btn ghost small" href="#/live" id="drLive">${ico("tv")} Live board</a></div>
      <div class="drawer-body">${feedHtml(this.notes, { limit: 100, ack: true })}</div>`;
    $("#drClose").onclick = () => this.closeDrawer();
    $("#drAck").onclick = async () => { await post("/api/live/ack-all"); this.notes.forEach((n) => (n.acknowledged = true)); this.renderBell(); this.renderDrawer(); this.emit("notes"); };
    $("#drLive").onclick = () => this.closeDrawer();
    bindAcks(d);
  },
};

function feedHtml(notes, { limit = 8, ack = false, empty } = {}) {
  const list = notes.slice(0, limit);
  if (!list.length) return `<div class="feed-empty">${ico("activity")}<p>${empty || "Nothing happening yet. Turn on <b>Live</b> and pick a scenario."}</p></div>`;
  const icon = { red: "alert-triangle", amber: "alert-triangle", green: "check-circle" };
  return `<ul class="feed">${list.map((n) => `<li class="lv-${esc(n.level)} ${n.acknowledged ? "acked" : ""}">
      <span class="feed-ic">${ico(icon[n.level] || "bell")}</span>
      <div class="feed-main"><div class="feed-msg">${esc(n.message)}</div>
        <div class="feed-meta"><span>${esc((n.created_at || "").slice(11, 19))}</span>${n.vessel_name ? `<span class="feed-v">${esc(n.vessel_name)}</span>` : ""}
          ${n.value_usd ? `<span>≈ ${usd(n.value_usd)} ${chip("Calculated", `${fmt(n.value_usd / 2.99, 0)} L × value per litre`)}</span>` : ""}${simChip()}</div></div>
      ${ack && !n.acknowledged && n.level !== "green" ? `<button class="btn ghost small ack" data-id="${n.id}">Acknowledge</button>` : n.acknowledged ? `<span class="feed-ok">${ico("check-circle")}</span>` : ""}</li>`).join("")}</ul>`;
}
function bindAcks(el) { $$(".ack", el).forEach((b) => (b.onclick = () => Live.ack(+b.dataset.id))); }

function scenarioPanel() {
  const on = Live.state?.on;
  return `<div class="scen">
    <div class="scen-head">${ico("zap")}<b>Live scenarios</b><span class="muted">for the presenter</span>${simChip()}</div>
    <div class="scen-grid">${SCENARIOS.map(([k, label, icon, hint]) => `<button class="scen-btn ${Live.state?.scenario === k && on ? "on" : ""} sc-${k}" data-sc="${k}" title="${esc(hint)}">${ico(icon)}<span>${esc(label)}</span></button>`).join("")}</div>
    <div class="scen-foot"><button class="btn ${on ? "secondary" : ""} small" id="scOn">${ico(on ? "pause" : "play")} ${on ? "Stop live" : "Go live"}</button>
      <button class="btn ghost small" id="scClear" title="Delete all simulated events">Clear simulated data</button></div>
    <p class="small muted">Thresholds are DEMO ASSUMPTIONS ${chip("DEMO ASSUMPTION")} — temperature, density and level ranges in <code>config/ranges.json</code>, level-drop and stuck rules in <a href="#/settings">Settings</a>.</p></div>`;
}
function bindScenarioPanel(el, rerender) {
  $$(".scen-btn", el).forEach((b) => (b.onclick = async () => { await Live.scenario(b.dataset.sc); rerender(); }));
  $("#scOn", el).onclick = async () => { await Live.setOn(!Live.state?.on); rerender(); };
  $("#scClear", el).onclick = async () => { if (!confirm("Delete all simulated live events?")) return; await post("/api/live/clear"); Live.notes = []; Live.series = {}; Live.renderBell(); rerender(); };
}

/** Update live vessels on an already drawn cellar map: level animates, the status ring pulses. */
function updateMapLive(root, cellarVessels) {
  const lm = Live.liveMap();
  for (const v of cellarVessels) {
    const lv = lm[v.code];
    if (!lv) continue;
    const cell = $(`.cm-cell[data-key="${v.winery}/${v.code}"]`, root);
    if (!cell) continue;
    const st = lv.status === "red" ? "red" : lv.status === "amber" ? "amber" : "ok";
    cell.className = `cm-cell st-${st} is-live`;
    const k = v.capacity_l ? Math.min(1, lv.level / v.capacity_l) : 0;
    const liq = $(".liquid", cell); if (liq) liq.style.transform = `scaleY(${k.toFixed(4)})`;
    const ring = $(".ring", cell); if (ring) { ring.setAttribute("stroke", { red: C.red, amber: C.amber, ok: C.green }[st]); ring.classList.toggle("pulse", st !== "ok"); }
    const lit = $(".glyph-litres", cell); if (lit) lit.textContent = `${fmt(lv.level)} L`;
  }
}

// ---------------------------------------------------------------- Cellar page
async function pageCellar(q) {
  let dateQ = q.get("date");
  if (!dateQ && !Live.state?.on) dateQ = (await api("/api/cellar")).peak_date;
  const data = await api(`/api/cellar${dateQ ? `?date=${dateQ}` : ""}`);
  const dates = data.dates;
  const today = new Date().toISOString().slice(0, 10);
  const idx = dates.findIndex((d) => d >= data.date);
  app().innerHTML = `<h1>${ico("map")} Cellar</h1>
    <div class="grid">
      <section class="card span-8">
        <div class="card-head"><h3>Cellar on ${dt(data.date)}</h3>
          <div class="seg-mini"><button data-d="${data.peak_date}" ${data.date === data.peak_date ? 'class="on"' : ""}>Fullest day</button><button data-d="${today}" ${data.date === today ? 'class="on"' : ""}>Today</button></div></div>
        <div class="date-slider"><input type="range" id="cDate" min="0" max="${dates.length}" value="${idx < 0 ? dates.length : idx}" aria-label="Date">
          <div class="range-labels"><span>${dt(dates[0])}</span><span>record dates ${chips("Dataset A6.1", "Dataset A6.2")}</span><span>today</span></div></div>
        <div id="cmap" class="cmap"></div>
        <div class="legend cm-legend"><span><i style="background:${C.wine2}"></i>wine</span><span><i style="background:${C.amber}"></i>wine in qvevri</span><span><i class="dots"></i>grapes fermenting (level not recorded)</span>
          <span><i class="ring-ok"></i>ok</span><span><i class="ring-amber"></i>attention</span><span><i class="ring-red"></i>alert</span></div>
      </section>
      <section class="span-4 stack">
        <div class="card"><div class="card-head"><h3>${ico("activity")} What's happening now</h3>${simChip()}</div><div id="cfeed"></div></div>
        <div class="card" id="cscen"></div>
      </section>
    </div>
    <aside id="vpanel" aria-label="Vessel"></aside>`;
  const drawMap = () => { $("#cmap").innerHTML = cellarMap(data.vessels, { liveMap: Live.liveMap() }); bindCells(); };
  const bindCells = () => $$(".cm-cell", $("#cmap")).forEach((b) => (b.onclick = () => openVesselPanel(data.vessels.find((v) => `${v.winery}/${v.code}` === b.dataset.key))));
  const drawFeed = () => { $("#cfeed").innerHTML = feedHtml(Live.notes, { limit: 6, ack: true }); bindAcks($("#cfeed")); };
  const drawScen = () => { $("#cscen").innerHTML = scenarioPanel(); bindScenarioPanel($("#cscen"), () => { drawScen(); drawMap(); }); };
  drawMap(); drawFeed(); drawScen();
  $("#cDate").oninput = (e) => { const i = +e.target.value; location.hash = `#/cellar?date=${i >= dates.length ? today : dates[i]}`; };
  $$(".seg-mini button").forEach((b) => (b.onclick = () => (location.hash = `#/cellar?date=${b.dataset.d}`)));
  Live.on("tick", () => updateMapLive($("#cmap"), data.vessels));
  Live.on("state", () => { drawMap(); drawScen(); });
  Live.on("notes", drawFeed);
}

async function openVesselPanel(v) {
  if (!v) return;
  const p = $("#vpanel");
  p.classList.add("open");
  p.innerHTML = `<div class="vp-head"><div><div class="vp-label">${esc(v.cellar_label || v.code)}</div><div class="muted small">${v.cellar_label ? esc(v.code) + " · " : ""}${esc(v.name)} · ${esc(v.winery)}</div></div>
    <button class="icon-btn" id="vpClose" aria-label="Close">${ico("x")}</button></div><div class="vp-body"><p class="muted">Loading…</p></div>`;
  $("#vpClose").onclick = () => p.classList.remove("open");
  const rd = await api(`/api/readings?vessel=${encodeURIComponent(v.code)}&winery=${encodeURIComponent(v.winery)}`);
  const lv = Live.liveMap()[v.code];
  const last = (ty) => rd.rows.filter((r) => r.type === ty).slice(-1)[0];
  const series = (ty) => rd.rows.filter((r) => r.type === ty).map((r) => r.value);
  const mini = (ty, label, unit, d) => {
    const l = last(ty); if (!l) return "";
    return `<div class="vp-mini"><div><div class="muted small">${esc(label)}</div><b>${fmt(l.value, d)} ${unit}</b> <span class="small muted">${dtt(l.taken_at)}</span>${chip(l.source)}</div>${sparkline(series(ty), { w: 110, h: 34 })}</div>`;
  };
  $(".vp-body", p).innerHTML = `<div class="vp-glyph">${vesselGlyph(lv ? { ...v, litres: lv.level, status: lv.status } : v, { w: 120, h: 160, live: !!lv, big: true })}</div>
    ${v.status_reason ? `<div class="callout amber">${ico("alert-triangle")} ${esc(v.status_reason)}</div>` : ""}
    ${lv ? `<div class="callout purple">${ico("activity")} Live: ${fmt(lv.temp, 1)} °C · ${fmt(lv.level)} L${lv.density ? ` · SG ${fmt(lv.density, 3)}` : ""} ${simChip()}</div>` : ""}
    <h4>Batch</h4>${v.contents.length ? v.contents.map((c) => `<p><a href="#/batch/${c.batch_id}">${esc(c.batch)}</a> · <b>${fmt(c.litres, 1)} L</b>${chip(c.source || "Calculated")}</p>`).join("")
      : v.grapes ? `<p><a href="#/batch/${v.grapes.batch_id}">${esc(v.grapes.batch)}</a> · ${fmt(v.grapes.kg)} kg grapes since ${dt(v.grapes.since)}${chip(v.grapes.source)}</p>` : `<p class="muted">Empty on this date.</p>`}
    <h4>Last readings</h4>${["temperature", "ph", "ta", "hydrometer_density"].map((ty) => mini(ty, RTYPES[ty].label(), RTYPES[ty].unit, RTYPES[ty].d)).join("") || `<p class="muted">No readings for this vessel.</p>`}
    <div class="btn-row"><a class="btn small" href="#/vessel/${encodeURIComponent(v.winery)}/${encodeURIComponent(v.code)}">${ico("barrel")} Vessel page</a>
      <a class="btn ghost small" href="#/readings?vessel=${encodeURIComponent(v.code)}&winery=${encodeURIComponent(v.winery)}">${ico("thermometer")} Add reading</a></div>`;
}

// ---------------------------------------------------------------- Live board (TV / projector)
async function pageLiveBoard() {
  const cellar = await api("/api/cellar");
  document.body.classList.add("board");
  app().innerHTML = `<h1>${ico("tv")} Live board</h1>
    <div class="board-bar"><span class="live-pill ${Live.state?.on ? "on" : ""}" id="lbPill">${ico("radio")} <span id="lbState"></span></span>${simChip()}
      <span class="muted">${esc(SENSOR_LINE)}</span><span class="grow"></span><span class="lb-rep" id="lbRep"></span><span class="muted small" id="lbSim"></span></div>
    <div class="grid">
      <section class="card span-12 lb-alerts" id="lbAlerts"></section>
      <section class="card span-7"><div class="card-head"><h3>Live vessels</h3></div><div id="lbMap" class="lb-map"></div></section>
      <section class="card span-5"><div class="card-head"><h3>${ico("activity")} What's happening now</h3></div><div id="lbFeed" class="lb-feed"></div></section>
      <section class="card span-4"><div class="card-head"><h3>${ico("thermometer")} <span id="lbT1">F-2</span> temperature</h3></div><div id="lbC1"></div></section>
      <section class="card span-4"><div class="card-head"><h3>${ico("droplet")} <span id="lbT2">Q-3</span> level</h3></div><div id="lbC2"></div></section>
      <section class="card span-4"><div class="card-head"><h3>${ico("flask")} <span id="lbT3">F-2</span> density</h3></div><div id="lbC3"></div></section>
      <section class="card span-12" id="lbScen"></section>
    </div>`;
  const byCode = Object.fromEntries(cellar.vessels.map((v) => [v.code, v]));
  const drawMap = () => {
    const live = Live.state?.vessels || [];
    if (!Live.state?.on || !live.length) {
      $("#lbMap").innerHTML = `<div class="feed-empty big">${ico("radio")}<p>Live is off.</p><button class="btn" id="lbGo">${ico("play")} Go live</button></div>`;
      $("#lbGo").onclick = async () => { await Live.setOn(true); drawAll(); };
      return;
    }
    $("#lbMap").innerHTML = `<div class="lb-vessels">${live.map((lv) => {
      const v = { ...(byCode[lv.code] || {}), litres: lv.level, status: lv.status };
      // green = last reading in range, red = out of range (config/ranges.json); no colour before the first check
      const val = (m, icon, value, d, unit) => {
        const r = lv.ranges?.[m];
        const cls = r?.in_range === true ? "in" : r?.in_range === false ? "out" : "";
        const hint = r ? `allowed ${fmtA(r.min, 3)}–${fmtA(r.max, 3)} ${r.unit} (${r.from})` : "no range set";
        return `<div class="rv ${cls}" title="${esc(hint)}"><span>${ico(icon)}</span><b>${fmt(value, d)}</b> ${unit}</div>`;
      };
      return `<div class="lb-v st-${lv.status}">${vesselGlyph(v, { w: 120, h: 160, live: true, pulse: lv.status !== "ok", big: true, showLitres: false })}
        <div class="lb-vals">${val("temperature", "thermometer", lv.temp, 1, "°C")}${val("level", "droplet", lv.level, 0, "L")}
        ${lv.density ? val("density", "flask", lv.density, 3, "SG") : ""}</div></div>`;
    }).join("")}</div>`;
  };
  const drawCharts = () => {
    const s = Live.state, set = Number(s && s.on ? 1 : 0);
    const th = Live.state?.thresholds?.temp_max ?? 30; // the rule's threshold from Settings (DEMO ASSUMPTION)
    paintLive("#lbC1", "F-2:temperature", "°C", C.red, th, 1);
    paintLive("#lbC2", "Q-3:level", "L", C.amber, null, 0);
    paintLive("#lbC3", "F-2:density", "SG", C.wine, null, 3);
    $("#lbT1").textContent = vshort("F-2", "W01"); $("#lbT2").textContent = vshort("Q-3", "W03"); $("#lbT3").textContent = vshort("F-2", "W01");
    return set;
  };
  const drawFeed = () => { $("#lbFeed").innerHTML = feedHtml(Live.notes, { limit: 7, ack: true }); bindAcks($("#lbFeed")); };
  // Active range alerts (3 out-of-range readings in a row) + "Simulate spike" (= the Hot fermentation scenario, SIMULATED data only)
  const drawAlerts = () => {
    const al = Live.state?.on ? Live.state.range_alerts || [] : [];
    const spike = `<button class="btn small danger" id="lbSpike" title="F-2 temperature climbs 27 → 32 °C (SIMULATED, live_events only)">${ico("zap")} Simulate spike</button>`;
    $("#lbAlerts").classList.toggle("on", al.length > 0);
    $("#lbAlerts").innerHTML = `<div class="card-head"><h3>${ico("alert-triangle")} ${al.length ? `${al.length} active alert${al.length > 1 ? "s" : ""}` : "No active range alerts"}</h3>${simChip()}<span class="grow"></span>${spike}</div>
      ${al.length ? `<div class="ra-list">${al.map((a) => `<div class="ra"><b>${esc(a.vessel_name)}</b>${a.batch ? ` <span class="muted">· ${esc(a.batch)}</span>` : ""}
        <span class="ra-what">${esc(a.metric)} <b>${esc(a.direction)}</b></span><span class="ra-val">${fmtA(a.value, 3)} ${esc(a.unit)}</span>
        <span class="muted">allowed ${fmtA(a.min, 3)}–${fmtA(a.max, 3)} ${esc(a.unit)}</span><span class="muted small">since ${dtt(a.since)}</span></div>`).join("")}</div>`
        : `<p class="small muted">An alert appears after 3 readings in a row outside the allowed range (config/ranges.json) and clears when the value is back in range.</p>`}`;
    $("#lbSpike").onclick = async () => { await Live.scenario("hot"); drawAll(); };
  };
  // Ledger report for the batch in the selected vessel (its simulated alerts go in a separate SIMULATED section).
  // Redrawn only when the vessel list changes, so an open dropdown is not reset by the 3 s tick.
  let repVessel = "", repSig = null;
  const drawRep = () => {
    const lv = Live.state?.on ? Live.state.vessels || [] : [];
    const key = (v) => `${v.winery}/${v.code}`;
    const sig = lv.map((v) => `${key(v)}=${v.batch || ""}`).join("|");
    if (sig === repSig) return;
    repSig = sig;
    if (!lv.some((v) => key(v) === repVessel)) { const d = lv.find((v) => v.batch) || lv[0]; repVessel = d ? key(d) : ""; }
    $("#lbRep").innerHTML = `<select id="lbRepV" aria-label="Vessel for the ledger report" ${lv.length ? "" : "disabled"}>${lv.length
      ? lv.map((v) => `<option value="${esc(key(v))}" ${key(v) === repVessel ? "selected" : ""}>${esc(v.code)} · ${esc(v.batch || "no batch")}</option>`).join("")
      : `<option>Turn on Live to pick a vessel</option>`}</select>
      <button class="btn small" id="lbReport" ${lv.length ? "" : "disabled"} title="Printable double-entry ledger of the batch in the selected vessel (read-only)">${ico("book-open")} Generate ledger report</button>`;
    $("#lbRepV").onchange = (e) => (repVessel = e.target.value);
    $("#lbReport").onclick = () => { const v = lv.find((x) => key(x) === repVessel); if (v) openReport(v.batch, repVessel); };
  };
  const drawHead = () => {
    drawRep();
    $("#lbPill").classList.toggle("on", !!Live.state?.on);
    $("#lbState").textContent = Live.state?.on ? `LIVE · ${Live.state.scenario_title}` : "Live off";
    $("#lbSim").textContent = Live.state?.on ? `simulated time ${dtt(Live.state.sim_time)}` : "";
  };
  const drawScen = () => { $("#lbScen").innerHTML = scenarioPanel(); bindScenarioPanel($("#lbScen"), drawAll); };
  const drawAll = () => { drawHead(); drawAlerts(); drawMap(); drawCharts(); drawFeed(); drawScen(); };
  drawAll();
  Live.on("tick", () => { drawHead(); drawAlerts(); drawMap(); drawCharts(); });
  Live.on("state", drawAll);
  Live.on("notes", drawFeed);
}
function paintLive(sel, key, unit, color, threshold, d) {
  const el = $(sel); if (!el) return;
  const w = Math.max(220, el.clientWidth);
  el.innerHTML = liveChart(w, Live.series[key] || [], { unit, color, threshold, d, height: 170 });
}
