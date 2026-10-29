// Bacchus Ledger — fermentation readings (Update 2): charts with month slider, one-screen entry form, Readings page.
"use strict";

// ---------------------------------------------------------------- Fermentation readings (Update 2)
const RTYPES = {
  initial_brix: { label: () => t("Initial Brix"), unit: "°Bx", d: 1, inst: () => t("Refractometer").toLowerCase() },
  ph: { label: () => "pH", unit: "pH", d: 2, inst: () => "pH meter / lab" },
  temperature: { label: () => t("Temperature"), unit: "°C", d: 1, inst: () => "thermometer / probe" },
  hydrometer_density: { label: () => `${t("Hydrometer")} · ${t("Density")}`, unit: "SG", d: 3, inst: () => t("Hydrometer").toLowerCase() },
  hydrometer_abv: { label: () => `${t("Hydrometer")} · ${t("Alcohol")}`, unit: "% vol", d: 1, inst: () => t("Hydrometer").toLowerCase() },
  ta: { label: () => `${t("Titratable acidity")} (TA)`, unit: "g/L", d: 1, inst: () => "titration" },
};
const F_POT = "potential ABV ≈ Initial Brix × 0.59 (rule of thumb, approx.)";
const F_PROG = "progress = ABV reached (hydrometer) ÷ potential ABV × 100%";
const F_SG = "SG = 1 + Brix ÷ (258.6 − (Brix ÷ 258.2) × 227.1)";
const rts = (s) => new Date(String(s).slice(0, 16)).getTime();
const DAY = 86400000;

function readingSourceChips(r) {
  return chip(r.source) + (r.time_in_dataset === false ? chip("time not in dataset") : "");
}
function readingCalcHtml(r) {
  const c = r.calc || {};
  if (r.type === "initial_brix" && c.potential_abv != null)
    return `${esc(t("Potential alcohol"))} ≈ <b>${fmt(c.potential_abv, 2)}%</b> (approx.)${chipF("Calculated", F_POT)} · SG ${fmt(c.sg, 4)}${chipF("Calculated", F_SG)}`;
  if (r.type === "hydrometer_abv" && c.progress_pct != null)
    return `Fermentation progress: <b>${fmt(c.progress_pct, 1)}%</b> of potential alcohol (${fmt(c.potential_abv, 2)}%)${chipF("Calculated", F_PROG + " · " + F_POT)}`;
  if (r.type === "hydrometer_abv") return `<span class="small muted">no Initial Brix — progress not available</span>`;
  return "";
}

const ReadingsView = {
  tips: [],
  tipEl: null,

  mount(el, data, opts = {}) {
    const rows = data.rows.map((r) => ({ ...r, ts: rts(r.taken_at) })).filter((r) => !Number.isNaN(r.ts));
    const st = { from: 0, to: 0, zoom: "all", months: [] };
    if (rows.length) {
      const first = new Date(Math.min(...rows.map((r) => r.ts))), last = new Date(Math.max(...rows.map((r) => r.ts)));
      for (let d = new Date(first.getFullYear(), first.getMonth(), 1); d <= last; d = new Date(d.getFullYear(), d.getMonth() + 1, 1)) st.months.push(d);
      st.to = st.months.length - 1;
    }
    const mlabel = (d) => d.toLocaleString("en-GB", { month: "short", year: "numeric" });
    const summary = this.summaryHtml(data, rows);
    el.innerHTML = `${summary}
      ${rows.length ? `<div class="rd-controls no-print">
        <div class="slider"><div class="range-labels"><span>From <b id="rdFromL"></b></span><span>to <b id="rdToL"></b></span></div>
          <input type="range" id="rdFrom" min="0" max="${st.months.length - 1}" value="0" step="1" aria-label="From month" ${st.months.length < 2 ? "disabled" : ""}>
          <input type="range" id="rdTo" min="0" max="${st.months.length - 1}" value="${st.months.length - 1}" step="1" aria-label="To month" ${st.months.length < 2 ? "disabled" : ""}>
          <div class="range-labels"><span>${esc(mlabel(st.months[0]))}</span><span>month by month</span><span>${esc(mlabel(st.months[st.months.length - 1]))}</span></div></div>
        <div><div class="small muted">Zoom (latest inside the range)</div><div class="zoom">${["day", "week", "month", "all"].map((z) => `<button type="button" data-z="${z}" class="${z === "all" ? "on" : ""}">${z[0].toUpperCase() + z.slice(1)}</button>`).join("")}</div></div>
      </div>` : ""}
      <div class="rd-charts"></div>
      <div class="rd-table" style="margin-top:12px"></div>
      ${opts.addHref ? `<div class="btn-row no-print"><a class="btn" href="${opts.addHref}">+ Add a reading</a></div>` : ""}`;
    const draw = () => {
      // chart width = the real column width (text stays ≥ 12 px)
      const cw = el.clientWidth || 700, cols = Math.max(1, Math.min(2, Math.floor((cw + 16) / 360)));
      this.cw = Math.floor((cw - 16 * (cols - 1)) / cols) - 26;
      $(".rd-charts", el).style.gridTemplateColumns = `repeat(${cols}, minmax(0, 1fr))`;
      let t0, t1;
      if (rows.length) {
        t0 = st.months[st.from].getTime();
        const end = st.months[st.to];
        t1 = new Date(end.getFullYear(), end.getMonth() + 1, 1).getTime() - 1;
        if (st.zoom !== "all") {
          const inRange = rows.filter((r) => r.ts >= t0 && r.ts <= t1);
          const last = inRange.length ? Math.max(...inRange.map((r) => r.ts)) : t1;
          const span = { day: DAY, week: 7 * DAY, month: 30 * DAY }[st.zoom];
          t0 = last - span; t1 = last + span * 0.04;
        } else {
          // tighten "All" to the readings themselves so the curve fills the chart
          const inRange = rows.filter((r) => r.ts >= t0 && r.ts <= t1);
          if (inRange.length) { const a = Math.min(...inRange.map((r) => r.ts)), b = Math.max(...inRange.map((r) => r.ts)); const pad = Math.max((b - a) * 0.04, 6 * 3600000); t0 = a - pad; t1 = b + pad; }
        }
        $("#rdFromL", el).textContent = mlabel(st.months[st.from]);
        $("#rdToL", el).textContent = mlabel(st.months[st.to]);
      }
      const win = rows.filter((r) => t0 == null || (r.ts >= t0 && r.ts <= t1));
      this.tips = [];
      const by = (ty) => win.filter((r) => r.type === ty);
      $(".rd-charts", el).innerHTML = [
        this.chart(`${t("Temperature")} (°C)`, [{ pts: by("temperature"), color: "#C98A2B", axis: "L", d: 1 }], t0, t1, opts),
        this.chart("pH", [{ pts: by("ph"), color: "#5B1A3A", axis: "L", d: 2 }], t0, t1, opts),
        this.hydroChart(win, data, t0, t1, opts),
        this.chart(`TA (g/L) — ${t("Titratable acidity")}`, [{ pts: by("ta"), color: "#2F6B4F", axis: "L", d: 1 }], t0, t1, opts),
      ].join("");
      $(".rd-table", el).innerHTML = this.table(win);
    };
    if (rows.length) {
      $("#rdFrom", el).oninput = (e) => { st.from = +e.target.value; if (st.from > st.to) { st.to = st.from; $("#rdTo", el).value = st.to; } st.zoom = "all"; this.setZoom(el, "all"); draw(); };
      $("#rdTo", el).oninput = (e) => { st.to = +e.target.value; if (st.to < st.from) { st.from = st.to; $("#rdFrom", el).value = st.from; } st.zoom = "all"; this.setZoom(el, "all"); draw(); };
      $$(".zoom button", el).forEach((b) => (b.onclick = () => {
        st.zoom = b.dataset.z;
        if (st.zoom === "all") { st.from = 0; st.to = st.months.length - 1; $("#rdFrom", el).value = 0; $("#rdTo", el).value = st.to; }
        this.setZoom(el, st.zoom); draw();
      }));
    }
    draw();
    this.bindTips(el);
  },

  setZoom(el, z) { $$(".zoom button", el).forEach((b) => b.classList.toggle("on", b.dataset.z === z)); },

  summaryHtml(data, rows) {
    const parts = [];
    for (const b of data.initial_brix || [])
      parts.push(`<div class="kpi"><div class="label">${esc(t("Initial Brix"))} · ${vname(b.vessel_code)}</div><div class="value">${fmt(b.initial_brix, 1)} °Bx</div>${chip(b.source)}
        <div class="small">${esc(t("Potential alcohol"))} ≈ <b>${fmt(b.potential_abv, 2)}%</b> (approx.)${chipF("Calculated", F_POT)}<br>start SG ${fmt(b.start_sg, 4)}${chipF("Calculated", F_SG)}</div></div>`);
    const abv = rows.filter((r) => r.type === "hydrometer_abv" && r.calc && r.calc.progress_pct != null);
    if (abv.length) {
      const l = abv[abv.length - 1];
      parts.push(`<div class="kpi"><div class="label">Fermentation progress (latest ${esc(t("Hydrometer").toLowerCase())})</div><div class="value">${fmt(l.calc.progress_pct, 1)}%</div>${chipF("Calculated", F_PROG)}
        <div class="small">of potential alcohol · ${fmt(l.value, 1)}% vol reached ${dtt(l.taken_at)}</div></div>`);
    }
    for (const s of data.initial_sugar || [])
      parts.push(`<div class="kpi"><div class="label">Initial sugar (lab, g/L) · ${esc(s.batch_code)}</div><div class="value">${fmt(s.sugar_gl, 0)} g/L</div>${chip(s.source)}
        <div class="small muted">${dt(s.date)} · given in g/L — not converted to Brix (the dataset has no Brix)</div></div>`);
    if (!(data.initial_brix || []).length)
      parts.push(`<div class="kpi amber"><div class="label">${esc(t("Initial Brix"))}</div><div class="value">—</div><div class="small muted">Not recorded. Take it with a ${esc(t("Refractometer").toLowerCase())} before fermentation starts.</div></div>`);
    return `<div class="rd-summary">${parts.join("")}<div class="kpi"><div class="label">${esc(t("Readings"))}</div><div class="value">${rows.length}</div><div class="small muted">${["temperature", "ph", "ta", "hydrometer_density", "hydrometer_abv", "initial_brix"].map((ty) => `${esc(RTYPES[ty].label())}: ${rows.filter((r) => r.type === ty).length}`).join(" · ")}</div></div></div>`;
  },

  tip(r) {
    const k = this.tips.length;
    this.tips.push(`<b>${fmt(r.value, RTYPES[r.type].d)} ${esc(r.unit)}</b> · ${esc(RTYPES[r.type].label())}<br>${dtt(r.taken_at)}${r.time_in_dataset === false ? " (time not in dataset)" : ""}<br>
      ${t("Vessel")}: ${r.vessel_code ? vname(r.vessel_code, r.winery) : "—"} · ${esc(r.batch_code)}<br>
      Instrument: ${esc(r.instrument || "not in dataset")}<br>Person: ${esc(r.person || "—")}<br>${readingCalcHtml(r)}${r.calc && (r.calc.potential_abv != null) ? "<br>" : ""}${readingSourceChips(r)}`);
    return k;
  },

  axis(vals, d, pad = 0.1) {
    let lo = Math.min(...vals), hi = Math.max(...vals);
    if (hi - lo < Math.pow(10, -d)) { lo -= Math.max(Math.abs(lo) * 0.02, Math.pow(10, -d) * 5); hi += Math.max(Math.abs(hi) * 0.02, Math.pow(10, -d) * 5); }
    const p = (hi - lo) * pad; return [lo - p, hi + p];
  },

  frame(W, H, L, R, T, B, t0, t1) {
    if (t1 - t0 < 3600000) { t0 -= 12 * 3600000; t1 += 12 * 3600000; }
    const x = (ts) => L + ((ts - t0) / (t1 - t0)) * (W - L - R);
    let g = "";
    const span = t1 - t0, n = W < 460 ? 3 : 5;
    for (let i = 0; i <= n; i++) {
      const ts = t0 + (span * i) / n, d = new Date(ts);
      const lab = span < 2 * DAY ? `${String(d.getDate()).padStart(2, "0")}.${String(d.getMonth() + 1).padStart(2, "0")} ${String(d.getHours()).padStart(2, "0")}:${String(d.getMinutes()).padStart(2, "0")}`
        : `${String(d.getDate()).padStart(2, "0")}.${String(d.getMonth() + 1).padStart(2, "0")}.${String(d.getFullYear()).slice(2)}`;
      g += `<line x1="${x(ts)}" x2="${x(ts)}" y1="${T}" y2="${H - B}" stroke="#f0ecee"/><text x="${x(ts)}" y="${H - B + 15}" font-size="12" text-anchor="middle" fill="#6b5f6e">${lab}</text>`;
    }
    return { x, g, t0, t1 };
  },

  yGrid(y, lo, hi, d, side, W, L, R, color) {
    let g = "";
    d = Math.min(4, Math.max(d, Math.ceil(-Math.log10((hi - lo) / 4)))); // enough decimals that ticks never repeat
    for (let i = 0; i <= 4; i++) {
      const v = lo + ((hi - lo) * i) / 4;
      if (side === "L") g += `<line x1="${L}" x2="${W - R}" y1="${y(v)}" y2="${y(v)}" stroke="#ece8ea"/><text x="${L - 6}" y="${y(v) + 4}" font-size="12" text-anchor="end" fill="${color}">${fmt(v, d)}</text>`;
      else g += `<text x="${W - R + 6}" y="${y(v) + 4}" font-size="12" fill="${color}">${fmt(v, d)}</text>`;
    }
    return g;
  },

  line(pts, x, y, color, k, dash) {
    if (!pts.length) return "";
    const s = pts.slice().sort((a, b) => a.ts - b.ts);
    let g = s.length > 1 ? `<polyline points="${s.map((p) => `${x(p.ts)},${y(p.value)}`).join(" ")}" fill="none" stroke="${color}" stroke-width="2.5" ${dash ? 'stroke-dasharray="6 4"' : ""}/>` : "";
    for (const p of s) g += `<circle class="rd-dot" data-k="${this.tip(p)}" cx="${x(p.ts)}" cy="${y(p.value)}" r="5" fill="${color}" stroke="#fff" stroke-width="1.5"/>`;
    return g;
  },

  empty(title, opts) {
    return `<div class="rd-chart"><h4>${esc(title)}</h4><div class="rd-empty">No readings yet — ${opts.addHref ? `<a href="${opts.addHref}">add one</a>` : "add one"}</div></div>`;
  },

  chart(title, series, t0, t1, opts) {
    const s = series[0];
    if (!s.pts.length) return this.empty(title, opts);
    const W = this.cw || 420, H = 214, L = 56, R = 14, T = 12, B = 28;
    const f = this.frame(W, H, L, R, T, B, t0, t1);
    const [lo, hi] = this.axis(s.pts.map((p) => p.value), s.d);
    const y = (v) => T + (H - T - B) * (1 - (v - lo) / (hi - lo));
    const g = f.g + this.yGrid(y, lo, hi, s.d, "L", W, L, R, "#6b5f6e") + this.line(s.pts, f.x, y, s.color);
    return `<div class="rd-chart"><h4>${esc(title)}</h4>${svgEl(W, H, g, title)}<div class="legend">${[...new Set(s.pts.map((p) => p.source))].map((x) => chip(x)).join("")}</div></div>`;
  },

  hydroChart(win, data, t0, t1, opts) {
    const title = `${t("Hydrometer")}: ${t("Density").toLowerCase()} (SG) · ${t("Alcohol").toLowerCase()} % vol`;
    const dens = win.filter((r) => r.type === "hydrometer_density"), abv = win.filter((r) => r.type === "hydrometer_abv");
    const brix = win.filter((r) => r.type === "initial_brix" && r.calc && r.calc.sg != null);
    if (!dens.length && !abv.length && !brix.length) return this.empty(title, opts);
    const W = this.cw || 420, H = 214, L = 60, R = 46, T = 12, B = 28;
    const f = this.frame(W, H, L, R, T, B, t0, t1);
    const sgVals = dens.map((p) => p.value).concat(brix.map((b) => b.calc.sg));
    const [slo, shi] = sgVals.length ? this.axis(sgVals, 3) : [0.99, 1.1];
    const ys = (v) => T + (H - T - B) * (1 - (v - slo) / (shi - slo));
    // potential ABV line: latest Initial Brix of the batch/vessel (also when the Brix reading itself is outside the window)
    const allBrix = (data.initial_brix || []);
    const pot = allBrix.length ? allBrix[allBrix.length - 1].potential_abv : null;
    const ahi = Math.max(1, ...abv.map((p) => p.value), pot || 0) * 1.12;
    const ya = (v) => T + (H - T - B) * (1 - v / ahi);
    let g = f.g + this.yGrid(ys, slo, shi, 3, "L", W, L, R, "#5B1A3A") + this.yGrid(ya, 0, ahi, 1, "R", W, L, R, "#C98A2B");
    if (pot != null) g += `<line x1="${L}" x2="${W - R}" y1="${ya(pot)}" y2="${ya(pot)}" stroke="#C98A2B" stroke-dasharray="5 4" stroke-width="1.5"/><text x="${W - R - 4}" y="${ya(pot) - 4}" font-size="12" text-anchor="end" fill="#855a14">potential ≈ ${fmt(pot, 2)}% (approx.)</text>`;
    g += this.line(dens, f.x, ys, "#5B1A3A") + this.line(abv, f.x, ya, "#C98A2B", null, false);
    for (const b of brix) {
      const k = this.tip({ ...b, value: b.value });
      const cx = f.x(b.ts), cy = ys(b.calc.sg);
      g += `<rect class="rd-dot" data-k="${k}" x="${cx - 6}" y="${cy - 6}" width="12" height="12" transform="rotate(45 ${cx} ${cy})" fill="#fff" stroke="#5B1A3A" stroke-width="2.5"/>`;
      g += `<text x="${cx + 9}" y="${cy - 8}" font-size="12" fill="#5B1A3A">start ${fmt(b.calc.sg, 3)} (${fmt(b.value, 1)} °Bx)</text>`;
    }
    return `<div class="rd-chart"><h4>${esc(title)}</h4>${svgEl(W, H, g, title)}
      <div class="legend"><span><i style="background:#5B1A3A"></i>SG (left)</span><span><i style="background:#C98A2B"></i>% vol (right)</span><span>◇ ${esc(t("Initial Brix"))} as SG${chipF("Calculated", F_SG)}</span><span>--- ${esc(t("Potential alcohol").toLowerCase())}${chipF("Calculated", F_POT)}</span></div></div>`;
  },

  table(win) {
    if (!win.length) return "";
    return `<details ${win.length <= 12 ? "open" : ""}><summary class="small">All ${win.length} readings in view</summary><div class="table-wrap"><table><thead><tr><th>Time</th><th>${t("Vessel")}</th><th>Type</th><th class="n">Value</th><th>Calculated</th><th>Instrument</th><th>Person</th><th>Source</th></tr></thead><tbody>
      ${win.slice().sort((a, b) => b.ts - a.ts).map((r) => `<tr><td class="nowrap">${dtt(r.taken_at)}</td><td>${r.vessel_code ? vname(r.vessel_code, r.winery) : "—"}</td><td>${esc(RTYPES[r.type].label())}</td>
        <td class="n"><b>${fmt(r.value, RTYPES[r.type].d)}</b> ${esc(r.unit)}</td><td class="small">${readingCalcHtml(r)}</td><td class="small">${esc(r.instrument || "not in dataset")}</td><td class="small">${esc(r.person || "—")}</td><td>${readingSourceChips(r)}</td></tr>`).join("")}
    </tbody></table></div></details>`;
  },

  bindTips(el) {
    if (!this.tipEl) { this.tipEl = document.createElement("div"); this.tipEl.className = "tip hidden"; document.body.appendChild(this.tipEl); }
    const tip = this.tipEl;
    const show = (e, target) => {
      tip.innerHTML = this.tips[+target.dataset.k] || ""; tip.classList.remove("hidden");
      const x = Math.min(e.clientX + 14, window.innerWidth - 300), y = Math.min(e.clientY + 14, window.innerHeight - tip.offsetHeight - 10);
      tip.style.left = x + "px"; tip.style.top = y + "px";
    };
    el.addEventListener("mouseover", (e) => { const d = e.target.closest(".rd-dot"); if (d) show(e, d); });
    el.addEventListener("mousemove", (e) => { const d = e.target.closest(".rd-dot"); if (d) show(e, d); });
    el.addEventListener("mouseout", (e) => { if (e.target.closest(".rd-dot")) tip.classList.add("hidden"); });
    el.addEventListener("click", (e) => { const d = e.target.closest(".rd-dot"); if (d) show(e, d); else tip.classList.add("hidden"); });
  },
};

/** One-screen phone form: vessel (by cellar label) → type → value → date/time → Save. Used by the Readings page and the live demo. */
const ReadingForm = {
  html(id, types, opts = {}) {
    return `<div id="${id}">
      <div class="typegrid" role="group" aria-label="Reading type">${types.map((ty) => `<button type="button" data-t="${ty}">${esc(ty === "hydrometer" ? t("Hydrometer") : ty === "multi" ? `pH · ${t("Temperature")} · TA` : RTYPES[ty].label())}</button>`).join("")}</div>
      <div class="rf-fields"></div>
      <div class="row2"><div><label>Date and time</label><input type="datetime-local" class="rf-time" value="${localNow()}"></div><div><label>Instrument</label><input type="text" class="rf-inst"></div></div>
      ${opts.noPerson ? "" : `<div class="row2"><div><label>Person</label><input type="text" class="rf-person" placeholder="who measured"></div><div><label>Note</label><input type="text" class="rf-note" placeholder="optional — corrections are new readings with a note"></div></div>`}
      <div class="rf-preview"></div>
      <div class="btn-row"><button type="button" class="btn rf-save">${t("Save")}</button></div>
      <div class="rf-result"></div></div>`;
  },

  fieldsFor(ty) {
    const inp = (k, label, step, ph) => `<label>${label}</label><input class="big rf-v" data-type="${k}" type="number" step="${step}" inputmode="decimal" placeholder="${ph}">`;
    if (ty === "hydrometer") return `<div class="row2"><div>${inp("hydrometer_density", `${t("Density")} (SG)`, "0.001", "e.g. 1.090")}</div><div>${inp("hydrometer_abv", `${t("Alcohol")} reached (% vol)`, "0.1", "% vol")}</div></div>
      <p class="small muted">Enter one or both — they are saved with the same time.</p>`;
    if (ty === "initial_brix") return inp("initial_brix", `${t("Initial Brix")} (°Bx) — ${t("Refractometer").toLowerCase()}, before fermentation`, "0.1", "°Bx");
    if (ty === "ph") return inp("ph", "pH", "0.01", "pH");
    if (ty === "temperature") return inp("temperature", `${t("Temperature")} (°C)`, "0.1", "°C");
    if (ty === "ta") return inp("ta", `TA (g/L tartaric) — titration`, "0.1", "g/L");
    // "multi": pH + temperature + TA on one screen (live demo step)
    return `<div class="row2"><div>${inp("ph", "pH", "0.01", "pH")}</div><div>${inp("temperature", `${t("Temperature")} (°C)`, "0.1", "°C")}</div></div>${inp("ta", "TA (g/L tartaric)", "0.1", "g/L")}`;
  },

  /** ctx(): { batch_id, vessel, live_demo }. onSaved(result) after a save. */
  bind(root, ctx, onSaved, initialType) {
    let ty = initialType;
    const instFor = (x) => (x === "hydrometer" ? t("Hydrometer").toLowerCase() : x === "multi" ? "" : RTYPES[x].inst());
    const setType = (x) => {
      ty = x;
      $$(".typegrid button", root).forEach((b) => b.classList.toggle("on", b.dataset.t === x));
      $(".rf-fields", root).innerHTML = this.fieldsFor(x);
      $(".rf-inst", root).value = instFor(x);
      $(".rf-inst", root).placeholder = x === "multi" ? "pH meter / thermometer / titration" : "";
      $(".rf-preview", root).innerHTML = "";
      const first = $(".rf-v", root); if (first) first.focus();
    };
    const body = () => {
      const c = ctx();
      return {
        batch_id: c.batch_id, vessel: c.vessel, live_demo: !!c.live_demo, taken_at: $(".rf-time", root).value,
        instrument: ty === "multi" ? "" : $(".rf-inst", root).value, person: $(".rf-person", root)?.value || "", note: $(".rf-note", root)?.value || "",
        readings: $$(".rf-v", root).map((i) => ({ type: i.dataset.type, value: i.value === "" ? null : Number(i.value) })),
      };
    };
    const itemHtml = (it) => {
      const r = { ...it, time_in_dataset: true };
      return `<div>${esc(RTYPES[it.type].label())}: <b>${fmt(it.value, RTYPES[it.type].d)} ${esc(it.unit)}</b> ${readingCalcHtml(r)}</div>${it.warnings.map((w) => `<div class="warnbox">⚠ ${esc(w)}</div>`).join("")}`;
    };
    let timer = null, seq = 0;
    const preview = async () => {
      const my = ++seq;
      try {
        const p = await post("/api/readings/preview", body());
        if (my === seq) $(".rf-preview", root).innerHTML = `<div class="note">${p.items.map(itemHtml).join("")}<div class="small muted">Preview — not saved yet.</div></div>`;
      } catch (e) { if (my === seq) $(".rf-preview", root).innerHTML = `<p class="small muted">${esc(e.message)}</p>`; }
    };
    root.addEventListener("input", () => { clearTimeout(timer); timer = setTimeout(preview, 200); });
    $$(".typegrid button", root).forEach((b) => (b.onclick = () => setType(b.dataset.t)));
    $(".rf-save", root).onclick = async () => {
      try {
        const r = await post("/api/readings", body());
        $(".rf-result", root).innerHTML = `<div class="okbox">✓ Saved ${r.saved.length} reading(s) ${chip(r.saved[0].source)}</div>${r.saved.map(itemHtml).join("")}`;
        $(".rf-preview", root).innerHTML = "";
        $$(".rf-v", root).forEach((i) => (i.value = ""));
        toast("Reading saved — charts and journal 7 updated");
        if (onSaved) onSaved(r);
      } catch (e) { $(".rf-result", root).innerHTML = `<p class="err">${esc(e.message)}</p>`; }
    };
    setType(ty);
  },
};

async function pageReadings(q) {
  const [vs, bs] = await Promise.all([api("/api/vessels"), api("/api/batches")]);
  const byBatch = q.get("batch") ? bs.find((b) => String(b.id) === q.get("batch")) : null;
  const st = { vessel: q.get("vessel") || "", winery: q.get("winery") || byBatch?.winery || "", batch_id: byBatch ? byBatch.id : null };
  const sorted = vs.slice().sort((a, b) => (b.cellar_label ? 1 : 0) - (a.cellar_label ? 1 : 0) || a.winery.localeCompare(b.winery) || a.id - b.id);
  app().innerHTML = `<h1>${t("Readings")}</h1>
    <p class="small muted">${esc(t("Initial Brix"))} · pH · ${esc(t("Temperature").toLowerCase())} · ${esc(t("Hydrometer").toLowerCase())} · TA — timestamped, attached to a vessel and its batch. Append-only: a correction is a new reading with a note.</p>
    <div class="grid k2">
      <div class="card">
        <label>${t("Vessel")} — type the ${esc(t("Cellar label").toLowerCase())}</label><input type="text" id="rvq" placeholder="e.g. F.2" autocomplete="off">
        <select id="rv" size="5" style="margin-top:6px"></select>
        <label>${t("Batch")}</label><select id="rb"></select>
        <div id="rbHint" class="small muted"></div>
        <label>Type</label>
        ${ReadingForm.html("rform", ["initial_brix", "hydrometer", "ph", "temperature", "ta"])}
      </div>
      <div class="card"><h3 id="rdTitle">Fermentation ${esc(t("Readings").toLowerCase())}</h3><div id="rdView"><p class="muted">Choose a vessel.</p></div></div>
    </div>`;
  const fillVessels = () => {
    const qq = $("#rvq").value.trim().toLowerCase();
    const list = sorted.filter((v) => (!st.winery || !byBatch || v.winery === st.winery) && (!qq || (v.cellar_label || "").toLowerCase().includes(qq) || v.code.toLowerCase().includes(qq)));
    $("#rv").innerHTML = list.map((v) => `<option value="${esc(v.winery)}/${esc(v.code)}" ${v.code === st.vessel && v.winery === st.winery ? "selected" : ""}>${esc(vtext(v))} · ${esc(v.winery)} · ${v.type === "qvevri" ? t("Qvevri") : t("Tank")}</option>`).join("");
    if (list.length === 1 && !st.vessel) { $("#rv").value = `${list[0].winery}/${list[0].code}`; pick(); }
  };
  const refreshView = async () => {
    if (!st.vessel) return;
    const rd = await api(`/api/readings?vessel=${encodeURIComponent(st.vessel)}&winery=${encodeURIComponent(st.winery)}${st.batch_id ? `&batch=${st.batch_id}` : ""}`);
    $("#rdTitle").innerHTML = `Fermentation ${esc(t("Readings").toLowerCase())} — ${vname(st.vessel, st.winery)}${st.batch_id ? ` · ${esc((bs.find((b) => b.id === st.batch_id) || {}).code || "")}` : ""}`;
    ReadingsView.mount($("#rdView"), rd, {});
  };
  async function pick() {
    const val = $("#rv").value; if (!val) return;
    const [w, c] = val.split("/");
    st.vessel = c; st.winery = w;
    const d = await api(`/api/vessels/${encodeURIComponent(c)}?winery=${encodeURIComponent(w)}`);
    const wb = bs.filter((b) => b.winery === w);
    const def = st.batch_id && wb.some((b) => b.id === st.batch_id) ? st.batch_id : d.batches.length ? d.batches[0].id : wb.length ? wb[0].id : null;
    $("#rb").innerHTML = wb.map((b) => `<option value="${b.id}" ${b.id === def ? "selected" : ""}>${esc(b.code)}</option>`).join("");
    st.batch_id = def;
    $("#rbHint").textContent = d.batches.length ? `Default: the batch last recorded in ${vtext(d.vessel)}.` : `No batch recorded in ${vtext(d.vessel)} yet — choose one.`;
    refreshView();
  }
  $("#rvq").oninput = fillVessels;
  $("#rv").onchange = pick;
  $("#rb").onchange = () => { st.batch_id = +$("#rb").value; refreshView(); };
  ReadingForm.bind($("#rform"), () => ({ batch_id: st.batch_id, vessel: st.vessel }), refreshView, q.get("type") || "initial_brix");
  fillVessels();
  if (st.vessel) pick();
  else if (byBatch) {
    // a batch was given: preselect the vessel it used last
    const b = await api(`/api/batches/${byBatch.id}`);
    const last = b.stages.slice().reverse().find((s) => s.to || s.from);
    if (last) { st.vessel = last.to || last.from; fillVessels(); pick(); }
  }
}
