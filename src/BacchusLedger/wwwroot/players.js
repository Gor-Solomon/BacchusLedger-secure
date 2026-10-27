// Bacchus Ledger — demo players (A→Z W01, Qvevri W03). Same step logic as before; each step now shows a visual:
// the journey growing, the tank/qvevri level changing, the gauge filling, journal tiles turning green, the money counter ticking.
"use strict";

const Player = {
  script: "w01", steps: null, n: 0, cache: {}, timer: null, anim: null, busy: false, keyHandler: null,
  seedDetail: null, prevMoney: 0, prevLevel: {},

  async open(script) {
    this.script = script; this.cache = {}; this.n = 0; this.prevMoney = 0; this.prevLevel = {}; this.stopAuto();
    const root = $("#player-root");
    root.innerHTML = `<div class="player" role="dialog" aria-modal="true" aria-label="Demo player"><div class="player-head"><div class="title">Preparing the demo batch…</div></div></div>`;
    await post(`/api/demo/reset?script=${script}`);
    this.steps = await api(`/api/demo/steps?script=${script}`);
    const seed = await batchByCode(script === "w03" ? "W03-B3-2025" : "W01-B1-2025");
    this.seedDetail = seed ? await api(`/api/batches/${seed.id}`) : null;
    this.keyHandler = (e) => {
      if (e.key === "ArrowRight") { e.preventDefault(); this.go(this.n + 1); }
      else if (e.key === "ArrowLeft") { e.preventDefault(); this.go(this.n - 1); }
      else if (e.key === "Escape") this.close();
    };
    document.addEventListener("keydown", this.keyHandler);
    await this.go(0);
  },

  close() {
    this.stopAuto();
    clearInterval(this.anim);
    document.removeEventListener("keydown", this.keyHandler);
    $("#player-root").innerHTML = "";
    if (location.hash.startsWith("#/demo/")) location.hash = "#/overview"; else route();
  },

  async fetchStep(n) {
    if (!this.cache[n]) {
      const c = await post(`/api/demo/step/${n}?script=${this.script}`);
      // the batch as it is right after this step (journey grows, levels change)
      c.detail = c.batch_id ? await api(`/api/batches/${c.batch_id}`) : null;
      this.cache[n] = c;
    }
    return this.cache[n];
  },

  async go(n) {
    if (!this.steps || n < 0 || n >= this.steps.total || this.busy) return;
    this.busy = true;
    try {
      // write every step in order so each card's running totals reflect that moment
      for (let i = 0; i <= n; i++) if (!this.cache[i]) await this.fetchStep(i);
      const prev = this.cache[this.n];
      this.prevMoney = prev && prev !== this.cache[n] ? prev.totals.loss_value_usd : this.prevMoney;
      this.n = n;
      this.render(this.cache[n]);
    } catch (e) { toast(e.message); }
    finally { this.busy = false; }
  },

  toggleAuto() {
    if (this.timer) { this.stopAuto(); return; }
    const tick = async () => {
      if (this.n >= this.steps.total - 1) { this.stopAuto(); return; }
      await this.go(this.n + 1);
      if (this.timer) this.timer = setTimeout(tick, 3000);
    };
    this.timer = setTimeout(tick, 3000);
    this.updateControls();
  },
  stopAuto() { clearTimeout(this.timer); this.timer = null; this.updateControls(); },
  updateControls() { const b = $("#pAuto"); if (b) b.innerHTML = this.timer ? `${ico("pause")} Pause` : `${ico("play")} Auto-play`; },

  journalsSoFar(n) { return new Set(this.steps.steps.slice(0, n + 1).flatMap((s) => s.journals)); },

  /** The current vessel: the last one wine (or grapes) went into. */
  currentVessel(d) {
    if (!d) return null;
    const st = d.stages.filter((s) => s.type !== "warehouse");
    const last = st[st.length - 1];
    if (!last) return null;
    const code = last.type === "bottling" || last.type === "stock_check" ? last.from : last.to;
    const v = vfind(code, d.batch.winery);
    if (!v) return null;
    const litres = d.totals.vessels[code] || 0;
    const grapes = last.type === "intake";
    return { ...v, litres, grapes: grapes ? { kg: d.batch.grapes_kg } : null, outline: null };
  },

  async visual(c) {
    const el = $("#plVisual");
    const ex = c.extra || {};
    const d = c.detail;
    if (c.n === 0 && this.script === "w01") {
      el.innerHTML = `<div class="pl-problem">${["Records not linked to the batch", "Paper → Excel by hand", "No flow meter"].map((s, i) =>
        `<div class="pl-q">${ico(["file-text", "pencil-line", "droplet"][i])}<span>${esc(s)}</span></div>`).join("")}
        <div class="pl-q big">${ico("alert-triangle")}<span><b>470 L</b> difference — which step lost it?</span>${chip("Dataset A6.2")}</div></div>`;
      return;
    }
    if (ex.one_percent) {
      el.innerHTML = `<h4>What 1% of loss costs per year</h4><div id="plBars"></div><p class="small muted">Calculated from the record. Not proven yet: how much a winery can win back — the pilot (Nov–Dec) measures it.</p>`;
      paint("#plBars", (w) => hbars(w, ex.one_percent.map((r) => ({ label: r.winery, value: r.usd, prefix: r.up_to ? "up to " : "" }))));
      return;
    }
    if (ex.chart === "fermentation") {
      el.innerHTML = `<div class="pl-ferm"><div id="plGauge"></div><div id="plSugar" class="grow-1"></div></div>`;
      let k = 0;
      const drawK = () => {
        const used = ((ex.sugar[0] - ex.sugar[k]) / ex.sugar[0]) * 100;
        $("#plGauge").innerHTML = gauge(used, { label: "sugar fermented", sub: `day ${ex.days[k]}`, size: 190 }) + chipF("Calculated", "(sugar day 1 − sugar today) ÷ sugar day 1");
        const w = Math.max(300, $("#plSugar").clientWidth);
        $("#plSugar").innerHTML = sugarCurve(w, ex.days.slice(0, k + 1).concat(), ex.sugar.slice(0, k + 1), { marker: k }) ;
      };
      drawK();
      this.anim = setInterval(() => { k++; if (k >= ex.sugar.length) { clearInterval(this.anim); return; } drawK(); }, reducedMotion() ? 1 : 170);
      return;
    }
    if (ex.dip_cm != null) {
      const v = await api(`/api/vessels/${encodeURIComponent(ex.vessel)}?winery=W03`);
      el.innerHTML = `<div class="pl-split"><div class="pl-vessel">${vesselSvg(v.profile, v.vessel.h_cm, v.vessel.h_cm - ex.dip_cm, ex.vessel)}</div>
        <div class="pl-dip"><div class="big-num">${fmt(ex.litres)} L</div><div>${ico("droplet")} dip <b>${fmtA(ex.dip_cm, 1)} cm</b> from the rim</div>${chip("Calculated", "A5 qvevri calibration table")}
        <p class="small muted">Qvevri ${esc(vshort(ex.vessel, "W03"))} · generalised A5 shape</p></div></div>`;
      return;
    }
    // journey so far (+ the vessel the wine is in now)
    const detail = d || (this.seedDetail && { ...this.seedDetail, stages: [] });
    const upto = d ? 99 : -1;
    const cv = this.currentVessel(d);
    el.innerHTML = `<div class="pl-journey"><div id="plJourney" class="grow-1"></div>${cv ? `<div class="pl-glyph" id="plGlyph"></div>` : ""}</div>`;
    paint("#plJourney", (w) => journeyFlow(w, detail, { upto, numbers: true }));
    if (cv) {
      const key = cv.code, k = cv.capacity_l ? Math.min(1, cv.litres / cv.capacity_l) : 0;
      const from = this.prevLevel[key] ?? 0;
      $("#plGlyph").innerHTML = vesselGlyph({ ...cv, litres: cv.litres }, { w: 120, h: 160, level: from, big: true, showLitres: false }) +
        `<div class="small muted center">${cv.grapes ? "grapes fermenting" : `${fmt(cv.litres)} L now`}</div>`;
      this.prevLevel[key] = k;
      requestAnimationFrame(() => requestAnimationFrame(() => { const liq = $("#plGlyph .liquid"); if (liq) liq.style.transform = `scaleY(${k.toFixed(4)})`; }));
    }
  },

  render(c) {
    clearInterval(this.anim); clearViz();
    const total = c.total, tt = c.totals, ex = c.extra || {};
    const icon = (s) => ({ pass: "check-circle", fail: "alert-triangle", above: "alert-triangle", info: "info" }[s] || "circle-dot");
    const head = this.script === "w01" ? "A→Z demo · W01 Saperavi 2025" : "Qvevri demo · W03 amber";
    const jury = isJury();
    const lim = (arr, n) => (jury ? arr.slice(0, n) : arr);
    const rest = (arr, n, fn) => (jury && arr.length > n ? more(`${arr.length - n} more`, arr.slice(n).map(fn).join("")) : "");
    const fieldHtml = (f) => `<div class="field"><div class="fl">${esc(f.label)}</div><div class="fv">${esc(f.value)}${chip(f.source)}</div></div>`;
    const didHtml = (d) => `<div class="did"><span class="did-ic ${d.status || "none"}">${ico(icon(d.status))}</span><div>${esc(d.text)}${chip(d.source)}</div></div>`;
    const resultLine = c.title === "Result" ? `<div class="result-big">${this.script === "w01" ? "16,000 kg → 10,100 L → 9,630 L bottled" : "1,850 kg → 1,230 L → 1,182 L bottled"}</div>` : "";
    const filled = this.journalsSoFar(c.n);
    $("#player-root").innerHTML = `<div class="player" role="dialog" aria-modal="true" aria-label="Demo player">
      <div class="player-head">${ico("wine")}<div class="title">${esc(head)} <span class="muted">· ${esc(c.batch_code)}</span></div>
        <span class="pl-step">Step ${c.n + 1} / ${total}</span>
        <button class="icon-btn light" id="pBatch" ${c.batch_id ? "" : "disabled"} title="Batch page">${ico("layers")}</button><button class="icon-btn light" id="pClose" aria-label="Close">${ico("x")}</button></div>
      <div class="progress">${Array.from({ length: total }, (_, i) => `<span class="${i < c.n ? "done" : i === c.n ? "cur" : ""}" data-i="${i}" title="${esc(this.steps.steps[i].title)}"></span>`).join("")}</div>
      <div class="player-body">
        <div class="pl-side"><div class="big-n">${c.n}</div><h2>${esc(c.title)}</h2>${c.date ? `<div class="date">${ico("calendar")} ${esc(c.date)}</div>` : ""}
          <div class="pl-journals">${c.journals.length ? c.journals.map((j) => `<span class="jbadge">${ico("book-open")} ${j} · ${esc(JT_SHORT[j])}</span>`).join("") : `<span class="small muted">No journal at this step</span>`}</div></div>
        <div class="pl-visual card" id="plVisual"></div>
        <div class="pl-cards">
          <div class="pcard"><h3>${ico("file-text")} Data received</h3>${lim(c.received, 2).map(fieldHtml).join("")}${rest(c.received, 2, fieldHtml)}</div>
          <div class="pcard"><h3>${ico("zap")} What the app did</h3>${resultLine}${lim(c.did, 2).map(didHtml).join("")}${rest(c.did, 2, didHtml)}
            ${ex.buttons ? `<div class="btn-row"><button class="btn" id="pHist">${ico("file-text")} Open batch history (1 click)</button><button class="btn ghost" id="pJ">${ico("book-open")} Open the 8 journals</button></div>` : ""}</div>
        </div>
      </div>
      <div class="pl-totals">
        <div class="pt"><span>${ico("grape")} kg received</span><b id="ptKg">${fmt(tt.kg_received)}</b>${tt.kg_received ? chip("DEMO ASSUMPTION", "DEMO split of the Dataset A2 total") : ""}</div>
        <div class="pt"><span>${ico("barrel")} ${t("Litres")} in vessels</span><b>${fmtA(tt.litres_now, 1)}</b>${chip("Calculated")}</div>
        <div class="pt red"><span>${ico("droplet")} Wine lost</span><b>${fmtA(tt.total_loss_l, 1)} L</b>${chip("Calculated")}</div>
        <div class="pt red"><span>${ico("coins")} Loss value</span><b id="ptMoney">${usd(this.prevMoney)}</b>${chip("Calculated")}</div>
        <div class="pt jt"><span>${ico("book-open")} ${t("Journals")} ${tt.journals_filled}/${tt.journals_total}</span>${journalTiles(filled)}</div>
      </div>
      <div class="controls">
        <button class="btn ghost" id="pBack" ${c.n === 0 ? "disabled" : ""}>◀ Back</button>
        <button class="btn" id="pNext" ${c.n >= total - 1 ? "disabled" : ""}>Next ▶</button>
        ${c.n >= total - 1 ? `<button class="btn" id="pReport" title="Printable double-entry ledger of ${esc(c.batch_code)} (read-only)">${ico("book-open")} Generate ledger report</button>` : ""}
        <button class="btn amber" id="pAuto"></button>
        <button class="btn ghost" id="pClose2">Close</button>
      </div></div>`;
    this.updateControls();
    countUp($("#ptMoney"), tt.loss_value_usd, { d: 2, prefix: "$", from: this.prevMoney, ms: 900 });
    $("#pBack").onclick = () => this.go(this.n - 1);
    $("#pNext").onclick = () => this.go(this.n + 1);
    $("#pAuto").onclick = () => this.toggleAuto();
    $("#pClose").onclick = $("#pClose2").onclick = () => this.close();
    $("#pBatch").onclick = () => { const id = c.batch_id; this.close(); location.hash = `#/batch/${id}`; };
    if ($("#pReport")) $("#pReport").onclick = () => openReport(c.batch_code);
    $$(".progress span", $("#player-root")).forEach((s) => (s.onclick = () => this.go(+s.dataset.i)));
    if (ex.buttons) {
      $("#pHist").onclick = () => { const id = c.batch_id; this.close(); location.hash = `#/history/${id}`; };
      $("#pJ").onclick = () => { const id = c.batch_id; this.close(); location.hash = `#/journals?batch=${id}`; };
    }
    this.visual(c);
    $("#pNext").focus();
  },
};
// ---------------------------------------------------------------- Demo player 3: fermentation readings (presenter types live values)
const RPlayer = {
  n: 0, ctx: null, keyHandler: null,
  steps: [
    { title: "Fermentation readings — what we will show" },
    { title: `Choose the vessel and type its ${"cellar label"}` },
    { title: "Initial Brix (refractometer)" },
    { title: "Hydrometer: density + alcohol" },
    { title: "pH, temperature, TA → 4 charts" },
    { title: "Real data: W03 amber, Oct 2025 – Jun 2026" },
  ],

  async open() {
    this.n = 0; this.ctx = null;
    this.keyHandler = (e) => {
      if (/INPUT|SELECT|TEXTAREA/.test(document.activeElement?.tagName || "")) return; // typing — arrows move the cursor
      if (e.key === "ArrowRight") this.go(this.n + 1);
      else if (e.key === "ArrowLeft") this.go(this.n - 1);
      else if (e.key === "Escape") this.close();
    };
    document.addEventListener("keydown", this.keyHandler);
    this.render();
  },
  close() {
    document.removeEventListener("keydown", this.keyHandler);
    $("#player-root").innerHTML = "";
    if (ReadingsView.tipEl) ReadingsView.tipEl.classList.add("hidden");
    if (location.hash.startsWith("#/demo/")) location.hash = "#/overview"; else route();
  },
  go(n) { if (n < 0 || n >= this.steps.length) return; if (n >= 2 && n <= 4 && !this.ctx) { toast("Start with step 1: choose the vessel"); n = 1; } this.n = n; this.render(); },

  async start(which) {
    const r = await post(`/api/demo/readings/start${which === "new" ? "?vessel=new" : ""}`);
    await loadVessels();
    this.ctx = { batch_id: r.batch_id, batch_code: r.batch_code, vessel: r.vessel, winery: r.winery, live_demo: true };
    this.render();
  },

  async refreshCharts() {
    const el = $("#rpCharts");
    const rd = this.ctx ? await api(`/api/readings?batch=${this.ctx.batch_id}`) : null;
    if (el && rd) ReadingsView.mount(el, rd, {});
    this.rvisual(rd);
  },

  /** Visual for the step: the vessel with its cellar label, potential alcohol, the progress gauge. */
  async rvisual(rd) {
    const el = $("#rpVisual"); if (!el) return;
    const c = this.ctx, n = this.n;
    if (!c) { el.innerHTML = n === 0 ? `<div class="rp-icons">${["barrel", "flask", "activity", "thermometer"].map((i) => `<span>${ico(i)}</span>`).join("")}</div>` : ""; return; }
    const v = vfind(c.vessel, c.winery) || { code: c.vessel, type: "tank" };
    if (n === 1) { el.innerHTML = `<div class="rp-glyph">${vesselGlyph({ ...v, litres: 0, status: "ok", outline: null }, { w: 110, h: 140, big: true, showLitres: false })}</div>`; return; }
    rd = rd || await api(`/api/readings?batch=${c.batch_id}`);
    const brix = rd.initial_brix[rd.initial_brix.length - 1];
    const prog = rd.rows.filter((r) => r.type === "hydrometer_abv" && r.calc && r.calc.progress_pct != null).slice(-1)[0];
    if (n === 2) el.innerHTML = brix ? `<div class="rp-big"><span>${esc(t("Potential alcohol"))}</span><b>≈ ${fmt(brix.potential_abv, 2)}%</b><span class="small muted">${fmt(brix.initial_brix, 1)} °Bx × 0.59 · start SG ${fmt(brix.start_sg, 4)}</span>${chipF("Calculated", F_POT)}</div>`
      : `<div class="rp-big muted"><span>${esc(t("Potential alcohol"))}</span><b>—</b><span class="small">type the Initial Brix</span></div>`;
    else el.innerHTML = gauge(prog ? prog.calc.progress_pct : null, { label: "of potential alcohol reached", sub: prog ? `${fmt(prog.value, 1)} of ${fmt(prog.calc.potential_abv, 1)}% vol` : "add hydrometer", size: 180 }) +
      (prog ? chipF("Calculated", F_PROG) : "");
  },

  render() {
    const n = this.n, c = this.ctx;
    const total = this.steps.length;
    const v = c ? vfind(c.vessel, c.winery) : null;
    let middle = "", right = "";
    const liveChip = chip("Entered (live demo)");
    if (n === 0) {
      middle = `<p>“These are the data points that help a winemaker keep track of how their wine is doing, without getting lost in stacks of paper.” <span class="small muted">— our team's winemaker</span></p>
        <p>The dataset has <b>no</b> Brix or hydrometer values, so this demo invents nothing: <b>you type real values</b> (e.g. read on a real ${esc(t("Refractometer").toLowerCase())}). Everything entered here is tagged ${liveChip}.</p>`;
      right = `<div class="did"><span class="did-ic none">1</span><div>${esc(t("Cellar label"))} on the vessel</div></div><div class="did"><span class="did-ic none">2</span><div>${esc(t("Initial Brix"))} → ${esc(t("Potential alcohol").toLowerCase())}${chipF("Calculated", F_POT)}</div></div>
        <div class="did"><span class="did-ic none">3</span><div>${esc(t("Hydrometer"))} → % of potential alcohol reached${chipF("Calculated", F_PROG)}</div></div><div class="did"><span class="did-ic none">4</span><div>pH, ${esc(t("Temperature").toLowerCase())}, TA → 4 charts + month slider</div></div>`;
    } else if (n === 1) {
      middle = c ? `<p>Live demo batch <b>${esc(c.batch_code)}</b>${liveChip} in vessel ${vname(c.vessel, c.winery)}.</p>
          <label>${t("Cellar label")} — type what is written on the tape</label><input class="big" type="text" id="rpLabel" value="${esc(v?.cellar_label || "")}" placeholder="${c.vessel === "F-2" ? "F.2" : "Q.1"}">
          <div class="btn-row"><button class="btn" id="rpSaveLabel">${t("Save")}</button></div><div id="rpLabelOut"></div>`
        : `<p>Pick the vessel for the live demo. A fresh live-demo batch is created (no numbers in it).</p>
          <div class="btn-row"><button class="btn" id="rpF2">Use tank F-2 (W01)</button><button class="btn secondary" id="rpNew">New empty ${esc(t("Qvevri").toLowerCase())}</button></div>
          <p class="small muted">The new qvevri has no measurements → “${esc(t("Volume unknown"))} — enter litres manually”. Nothing is guessed.</p>`;
      right = c ? `<div class="did"><span class="did-ic pass">${ico("check-circle")}</span><div>Batch ${esc(c.batch_code)} created${liveChip}</div></div>
          <div class="did"><span class="did-ic ${v?.cellar_label ? "pass" : "none"}">${ico(v?.cellar_label ? "check-circle" : "circle-dot")}</span><div>${v?.cellar_label ? `${esc(t("Cellar label"))} <span class="vlabel" style="font-size:1.6rem">${esc(v.cellar_label)}</span> saved${chip(v.cellar_label_source)} — shown everywhere, searchable` : `Shown big everywhere, the internal code small`}</div></div>
          <div class="did"><span class="did-ic info">${ico("info")}</span><div>${esc(v?.calibration_note || "")}</div></div>` : "";
    } else if (n >= 2 && n <= 4) {
      const ty = n === 2 ? "initial_brix" : n === 3 ? "hydrometer" : "multi";
      middle = `<p>${vname(c.vessel, c.winery)} · ${esc(c.batch_code)}${liveChip}</p>${ReadingForm.html("rpForm", [ty], { noPerson: false })}`;
      right = n === 2 ? `<div class="did"><span class="did-ic none">${ico("zap")}</span><div>${esc(t("Potential alcohol"))} ≈ ${esc(t("Initial Brix"))} × 0.59 (approx.)${chipF("Calculated", F_POT)}</div></div>
          <div class="did"><span class="did-ic none">${ico("zap")}</span><div>Start point on the density chart: ${esc(F_SG)}${chipF("Calculated", F_SG)}</div></div>
          <div class="did"><span class="did-ic info">${ico("info")}</span><div>Taken once per vessel/batch, <b>before</b> fermentation — the app warns if fermentation has already started (refractometers are not accurate once alcohol is present).</div></div>`
        : n === 3 ? `<div class="did"><span class="did-ic none">${ico("zap")}</span><div>Fermentation progress = ABV reached ÷ potential ABV × 100%${chipF("Calculated", F_PROG)}</div></div>
          <div class="did"><span class="did-ic info">${ico("info")}</span><div>This is the comparison the winemaker asked for: ${esc(t("Hydrometer").toLowerCase())} vs ${esc(t("Initial Brix"))}.</div></div>`
        : `<div class="did"><span class="did-ic none">${ico("activity")}</span><div>The 4 charts update below. Move the month slider or press Day / Week / Month / All.</div></div>`;
    } else if (n === 5) {
      middle = `<p>W03 batch 3 has real lab readings from Dataset A4.2: ${esc(t("Temperature").toLowerCase())}, pH and TA from 15.10.2025 to 09.06.2026${chip("Dataset A4.2")}.
        Use the month slider there to walk through 9 months. (Dates only in the dataset — shown at 09:00${chip("time not in dataset")}.)</p>
        <div class="btn-row"><button class="btn" id="rpW03">Open W03 batch readings</button>${c ? `<button class="btn secondary" id="rpLive">Open the live demo batch</button>` : ""}</div>`;
      right = `<div class="did"><span class="did-ic pass">${ico("check-circle")}</span><div>Every value entered in this demo is tagged ${liveChip} and appears in journal 7 (Laboratory analysis).</div></div>
        <div class="did"><span class="did-ic pass">${ico("check-circle")}</span><div>Readings are append-only — a correction is a new reading with a note.</div></div>`;
    }
    $("#player-root").innerHTML = `<div class="player" role="dialog" aria-modal="true" aria-label="Fermentation readings demo">
      <div class="player-head">${ico("thermometer")}<div class="title">Fermentation ${esc(t("Readings").toLowerCase())} demo${c ? ` <span class="muted">· ${esc(c.batch_code)}</span>` : ""}</div>
        <span class="pl-step">Step ${n + 1} / ${total}</span><button class="icon-btn light" id="rpClose" aria-label="Close">${ico("x")}</button></div>
      <div class="progress">${this.steps.map((_, i) => `<span class="${i < n ? "done" : i === n ? "cur" : ""}" data-i="${i}"></span>`).join("")}</div>
      <div class="player-body">
        <div class="pl-side"><div class="big-n">${n}</div><h2>${esc(n === 1 ? `Vessel and ${t("Cellar label").toLowerCase()}` : this.steps[n].title)}</h2></div>
        <div class="pcard rp-input"><h3>${ico("pencil-line")} Presenter types</h3>${middle}</div>
        <div class="pcard"><h3>${ico("zap")} What the app does</h3><div id="rpVisual" class="rp-visual"></div>${right}</div>
        ${c && n >= 2 ? `<div class="pcard" style="grid-column:1/-1"><h3>Fermentation ${esc(t("Readings").toLowerCase())} — ${esc(c.batch_code)}</h3><div id="rpCharts"></div></div>` : ""}
      </div>
      <div class="controls">
        <button class="btn ghost" id="rpBack" ${n === 0 ? "disabled" : ""}>◀ Back</button>
        <button class="btn" id="rpNext" ${n >= total - 1 ? "disabled" : ""}>Next ▶</button>
        <button class="btn ghost" id="rpClose2">Close</button>
      </div></div>`;
    $("#rpBack").onclick = () => this.go(n - 1);
    $("#rpNext").onclick = () => this.go(n + 1);
    $("#rpClose").onclick = $("#rpClose2").onclick = () => this.close();
    $$(".progress span", $("#player-root")).forEach((s) => (s.onclick = () => this.go(+s.dataset.i)));
    if (n === 1 && !c) { $("#rpF2").onclick = () => this.start("f2"); $("#rpNew").onclick = () => this.start("new"); }
    if (n === 1 && c) {
      $("#rpSaveLabel").onclick = async () => {
        try {
          await post(`/api/vessels/${encodeURIComponent(c.vessel)}/details?winery=${c.winery}`, { cellar_label: $("#rpLabel").value, live_demo: true });
          await loadVessels(); this.render();
        } catch (e) { $("#rpLabelOut").innerHTML = `<p class="err">${esc(e.message)}</p>`; }
      };
      $("#rpLabel").focus();
    }
    if (n >= 2 && n <= 4 && c) {
      const ty = n === 2 ? "initial_brix" : n === 3 ? "hydrometer" : "multi";
      ReadingForm.bind($("#rpForm"), () => this.ctx, () => this.refreshCharts(), ty);
      this.refreshCharts();
    }
    if (n < 2 || n > 4) this.rvisual();
    if (n === 5) {
      $("#rpW03").onclick = () => { this.close(); openW03Readings(); };
      if (c) $("#rpLive").onclick = () => { const id = c.batch_id; this.close(); location.hash = `#/batch/${id}?readings=1`; };
    }
  },
};
