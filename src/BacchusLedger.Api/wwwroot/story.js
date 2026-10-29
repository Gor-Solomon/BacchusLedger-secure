// Bacchus Ledger — the jury's story pages: Overview, Batches, Batch, Batch history (Update 3 visuals).
"use strict";

async function batchByCode(code) {
  const bs = await api("/api/batches");
  return bs.find((b) => b.code === code);
}

/** Cumulative wine lost over time (W01 + W03 records) — for the KPI sparkline. */
function cumulativeLoss(...details) {
  const st = details.flatMap((d) => d.stages.filter((s) => s.loss_l != null)).sort((a, b) => a.date.localeCompare(b.date));
  let c = 0;
  return [0, ...st.map((s) => (c += s.loss_l))];
}

// ---------------------------------------------------------------- Overview = the jury's 60-second story
async function pageOverview() {
  const [o, bs, cov] = await Promise.all([api("/api/overview"), api("/api/batches"), api("/api/journals/coverage")]);
  const cellarNow = await api("/api/cellar");
  const live = !!Live.state?.on;
  const cellar = live ? cellarNow : await api(`/api/cellar?date=${cellarNow.peak_date}`);
  const w01 = bs.find((b) => b.code === "W01-B1-2025"), w03 = bs.find((b) => b.code === "W03-B3-2025");
  const [d1, d3] = await Promise.all([api(`/api/batches/${w01.id}`), api(`/api/batches/${w03.id}`)]);
  const filled = cov.filter((j) => j.filled).length;
  const k = o.kpi;
  const real = bs.filter((b) => !b.copy);

  app().innerHTML = `<h1>Overview</h1>
  <section class="hero compact">
    <div class="hero-text">
      <div class="eyebrow">${ico("grape")} WINECUBATOR · Kakheti PDO</div>
      <h2 class="hero-title">Every kg. Every litre. From farmer to bottle.</h2>
      <p class="lead">The only cellar record that works with qvevri, in Georgian, for Kakheti law, with no sensors and no internet needed.</p>
    </div>
    <div class="hero-cta">
      <button class="btn lg" id="demoW01">${ico("play")} A→Z demo</button>
      <button class="btn lg amber" id="demoW03">${ico("play")} Qvevri demo</button>
      <button class="btn lg live ${live ? "on" : ""}" id="goLive"><span class="dot"></span>${live ? "Live is on" : "Go live"}</button>
    </div>
  </section>

  <div class="grid k4">
    ${kpiTile({ icon: "barrel", label: "Wine in cellar now", value: `<span id="kv1">${fmt(k.wine_in_cellar_l)}</span><small> L</small>`, sub: "real batches, today",
      spark: sparkline(Object.values(cellarNow.totals), { w: 110 }), src: chip("Calculated") })}
    ${kpiTile({ icon: "layers", label: "Batches tracked", value: `<span id="kv2">${k.batches}</span>`, sub: "vintage 2025 · 5 wineries",
      spark: sparkline(real.map((b) => b.grapes_kg || 0), { w: 110, bars: true, color: C.wine2 }), src: chip("Dataset A2") })}
    ${kpiTile({ icon: "droplet", label: "Wine lost", value: `<span id="kv3">${fmt(k.total_loss_l)}</span><small> L</small>`, sub: `≈ <b>${usd(k.loss_value_usd)}</b> at $${fmt(o.settings.value_per_litre_usd, 2)}/L`,
      spark: sparkline(cumulativeLoss(d1, d3), { w: 110, color: C.red }), src: chip("Calculated"), tone: "red" })}
    ${kpiTile({ icon: "book-open", label: "Journals filled", value: `<span id="kv4">${filled}</span><small>/8</small>`, sub: "Kakheti PDO §12 · tasting = template",
      spark: `<span class="mini-j">${[1, 2, 3, 4, 5, 6, 7, 8].map((n) => `<i class="${cov[n - 1].filled ? "on" : ""}"></i>`).join("")}</span>`, src: chip("Kakheti PDO spec"), tone: "green" })}
  </div>

  <div class="grid">
    <section class="card span-6">
      <div class="card-head"><h3>${ico("map")} Cellar ${live ? "· live" : `on ${dt(cellar.date)}`}</h3>${live ? simChip() : chip("Dataset A6.2")}<a class="link" href="#/cellar${live ? "" : `?date=${cellar.date}`}">Open ${ico("chevron-right")}</a></div>
      <div id="ovMap" class="cmap mini"></div>
      ${live ? "" : `<p class="small muted">Fullest day in the record. Click a vessel for its batch and readings.</p>`}
    </section>
    <section class="card span-6">
      <div class="card-head"><h3>${ico("activity")} What's happening now</h3>${simChip()}<a class="link" href="#/live">Live board ${ico("chevron-right")}</a></div>
      <div id="ovFeed"></div>
    </section>
    <section class="card span-12">
      <div class="card-head"><h3>${ico("wine")} Batch journey · W01-B1-2025 Saperavi</h3>${chips("Dataset A6.2", ["DEMO ASSUMPTION", "Farmer split of the 16,000 kg is a DEMO ASSUMPTION"])}
        <a class="link" href="#/batch/${w01.id}">Open batch ${ico("chevron-right")}</a></div>
      <div id="ovJourney" class="journey-box"></div>
    </section>
  </div>

  <div class="expert-only">
    <div class="grid">
      <section class="card span-6"><div class="card-head"><h3>${ico("zap")} Loss per step · W01 vs W03</h3>${chips("Dataset A6.1", "Dataset A6.2")}</div><div id="ovBars"></div></section>
      <section class="card span-6"><div class="card-head"><h3>${ico("coins")} What 1% of loss costs per year</h3>${chip("Calculated", `yearly t × ${o.settings.litres_per_tonne} L/t × 1% × $${o.settings.value_per_litre_usd}/L`)}</div>
        <div id="ovMoney"></div><p class="small muted">W01: “up to” — not every tonne becomes wine.</p></section>
      <section class="card span-6"><div class="card-head"><h3>${ico("alert-triangle")} Flags</h3></div>${o.flags.filter((f) => !f.copy).map(flagHtml).join("") || `<p class="muted">No flags.</p>`}</section>
      <section class="card span-6">${problemHtml()}</section>
    </div>
  </div>
  <div class="jury-only">${more("Why it matters — the problem, in the wineries' own words", problemHtml())}</div>`;

  $("#demoW01").onclick = () => Player.open("w01");
  $("#demoW03").onclick = () => Player.open("w03");
  $("#goLive").onclick = async () => { if (!Live.state?.on) { await Live.setOn(true); } location.hash = "#/live"; };
  const drawMap = () => {
    $("#ovMap").innerHTML = cellarMap(cellar.vessels.filter((v) => !live || Live.liveMap()[v.code] || v.litres > 0 || v.grapes), { mini: true, liveMap: Live.liveMap() });
    $$(".cm-cell", $("#ovMap")).forEach((b) => (b.onclick = () => (location.hash = `#/cellar${live ? "" : `?date=${cellar.date}`}`)));
  };
  const drawFeed = () => { $("#ovFeed").innerHTML = feedHtml(Live.notes, { limit: 4, empty: `Live is off. Press <b>Go live</b> to see what the winemaker sees when things happen.` }); };
  drawMap(); drawFeed();
  paint("#ovJourney", (w) => journeyFlow(w, d1, { numbers: !isJury(), height: 300 }));
  if (!isJury()) {
    paint("#ovBars", (w) => groupedBars(w, d1.stages, d3.stages));
    paint("#ovMoney", (w) => hbars(w, o.one_percent.map((r) => ({ label: r.winery, value: r.usd, prefix: r.up_to ? "up to " : "" }))));
  }
  countUp($("#kv1"), k.wine_in_cellar_l); countUp($("#kv3"), k.total_loss_l);
  Live.on("tick", () => updateMapLive($("#ovMap"), cellar.vessels));
  Live.on("notes", drawFeed);
  Live.on("state", () => route());
}

function problemHtml() {
  return `<h3>${ico("file-text")} The problem</h3>
    <p>A Kakheti winery buys grapes from many farmers (~44 per company${chip("Calculated", "22,000 farmers ÷ 500 companies")}: 22,000 farmers sold to 500+ companies in 2025${chip("CBW 2025")}) and records everything on paper and by eye.</p>
    <blockquote>“Vineyard and grape intake records are not automatically linked to the wine batch.” ${chip("Challenge Book")}</blockquote>
    <blockquote>“Primary records are often moved by hand from paper to Excel.” ${chip("Challenge Book")}</blockquote>
    <blockquote>“No winery has a sanitary flow meter.” ${chip("Challenge Book")}</blockquote>
    <p>W01 put <b>10,100 L</b> in the tank and bottled <b>9,630 L</b> — 450 L lost + 20 L left${chip("Dataset A6.2")} — and nobody can say which step lost too much.</p>
    <p class="small muted">1,027 wine companies in Georgia (2023)${chip("TBC Capital 2024")} · Kakheti = 327,000 of 336,000 t processed in 2025 (97%)${chip("CBW 2025")}</p>`;
}

function flagHtml(f) {
  if (f.level === "above" || f.level === "negative")
    return `<div class="flag">${statusBadge(f.level)}<div><a href="#/batch/${f.batch_id}">${esc(f.batch)}</a> · ${esc(plain(f.stage))} ${dt(f.date)}<br>
      <span class="small">${t("Loss")} ${N(f.loss_l, "Calculated", "L", 1)} (${fmt(f.loss_pct, 2)}%) vs normal ${fmt(f.norm_pct, 1)}%${chip("DEMO ASSUMPTION")}${f.excess_l > 0 ? ` · excess ${N(f.excess_l, "Calculated", "L", 1)}` : ""}</span></div></div>`;
  return `<div class="flag">${statusBadge(f.level)}<div><a href="#/batch/${f.batch_id}">${esc(f.batch)}</a> · ${esc(f.text)}${chip(f.source)}<br><span class="small muted">${esc(f.detail || "")}</span></div></div>`;
}

function onePercentTable(rows, s) {
  return `<div class="table-wrap"><table><thead><tr><th>Winery</th><th class="n">t / year</th><th class="n">1% of litres${chip("Calculated")}</th><th class="n">1% value / year${chip("Calculated")}</th></tr></thead><tbody>
    ${rows.map((r) => `<tr><td>${esc(r.winery)} <span class="small muted">${esc(r.size)}</span></td><td class="n">${N(r.yearly_t, "Challenge Book")}</td>
      <td class="n">${fmt(r.litres_1pct, 0)} L</td><td class="n"><b>${r.up_to ? "up to " : ""}$${fmt(r.usd, 0)}</b></td></tr>`).join("")}</tbody></table></div>`;
}

async function openW03Readings() {
  const b = await batchByCode("W03-B3-2025");
  if (b) location.hash = `#/batch/${b.id}?readings=1`;
}

// ---------------------------------------------------------------- Batches
async function pageBatches() {
  const bs = await api("/api/batches");
  const card = (b) => `<a class="card bcard ${b.above ? "warn" : ""}" href="#/batch/${b.id}">
      <div class="bcard-top"><span class="bcard-ic ${b.technology === "qvevri" ? "q" : ""}">${ico(b.technology === "qvevri" ? "barrel" : "wine")}</span>
        <div><div class="bcard-code">${esc(b.code)}</div><div class="small muted">${esc(b.wine_type || "")} · ${esc(b.winery)}</div></div>
        ${b.copy ? `<span class="status info">copy</span>` : b.above ? statusBadge("above") : b.first_litres != null ? statusBadge("ok") : ""}</div>
      <div class="bcard-stats"><div><span>Grapes</span><b>${b.grapes_kg != null ? fmt(b.grapes_kg) + " kg" : "—"}</b></div>
        <div><span>Wine lost</span><b>${b.first_litres != null ? fmtA(b.total_loss_l, 1) + " L" : "—"}</b></div></div>
      <div class="small muted">${b.first_litres != null ? `${fmt(b.first_litres)} L → ${b.bottled_l ? fmt(b.bottled_l) + " L bottled" : "in cellar"}` : "Only the grape intake is in the dataset"}</div></a>`;
  const row = (b) => `<tr class="${b.above ? "above" : ""}">
    <td><a href="#/batch/${b.id}"><b>${esc(b.code)}</b></a>${b.copy ? ` <span class="status info">copy</span>` : ""}</td>
    <td>${esc(b.winery)}</td><td>${esc(b.wine_type || "")}${chip("Dataset A2")}<div class="small muted">${esc(b.variety || "")}${b.variety ? chip("DEMO ASSUMPTION") : ""}</div></td>
    <td class="n">${b.grapes_kg != null ? N(b.grapes_kg, "Dataset A2", "kg") : "—"}</td><td>${dt(b.intake_date)}</td>
    <td class="n">${b.first_litres != null ? fmt(b.first_litres) + " L" : "—"}</td><td class="n">${b.bottled_l ? fmt(b.bottled_l) + " L" : "—"}</td>
    <td class="n">${b.first_litres != null ? N(b.total_loss_l, "Calculated", "L", 1) : "—"}</td>
    <td>${b.above ? statusBadge("above") : b.negative ? statusBadge("negative") : b.first_litres != null ? statusBadge("ok") : `<span class="muted small">${esc(b.status)}</span>`}</td></tr>`;
  app().innerHTML = `<h1>${t("Batches")}</h1>
    <div class="grid k3">${bs.filter((b) => !b.copy).map(card).join("")}</div>
    ${more(`All batches including demo copies (${bs.length})`, `<div class="table-wrap"><table><thead><tr><th>${t("Batch")}</th><th>Winery</th><th>Wine</th><th class="n">Grapes</th><th>Intake</th>
      <th class="n">After pressing</th><th class="n">Bottled</th><th class="n">${t("Loss")}</th><th>Status</th></tr></thead><tbody>${bs.map(row).join("")}</tbody></table></div>
      <p class="small muted">Litres come from Dataset A6.1 (W03) and A6.2 (W01); for W02, W04, W05 the dataset gives only the grape intake (A2).</p>`)}`;
}

// ---------------------------------------------------------------- Batch page
function stageTable(stages) {
  const rows = stages.filter((s) => s.type !== "intake");
  return `<div class="table-wrap"><table><thead><tr><th>Date</th><th>Step</th><th>From → to</th><th class="n">Before</th><th class="n">After</th><th class="n">${t("Loss")} L${chip("Calculated")}</th>
    <th class="n">%</th><th class="n">Normal${chip("DEMO ASSUMPTION")}</th><th class="n">Other winery</th><th>Status</th></tr></thead><tbody>
    ${rows.map((s) => {
      const o = s.other;
      const after = s.type === "bottling" ? `${fmt(s.after_l)} L<div class="small muted">${fmt(s.bottles)} × ${fmt(s.bottle_ml)} mL</div>` : s.type === "warehouse" ? `${fmt(s.bottles)} bottles` : `${fmt(s.after_l, s.after_l % 1 ? 1 : 0)} L`;
      return `<tr class="${s.status === "above" ? "above" : ""}"><td class="nowrap">${dt(s.date)}</td><td>${esc(opLabel(s.stage_key))}${chip(s.source)}${s.method === "dip" ? `<div class="small muted">dip ${fmtA(s.dip_cm, 1)} cm</div>` : ""}${s.note ? `<div class="small muted">${esc(s.note)}</div>` : ""}</td>
        <td class="nowrap">${vname(s.from)}${s.to ? " → " + vname(s.to) : s.type === "bottling" ? " → bottles" : s.type === "warehouse" ? "→ finished goods" : ""}</td>
        <td class="n">${s.before_l != null ? fmt(s.before_l, s.before_l % 1 ? 1 : 0) + " L" : "—"}</td><td class="n">${after}${s.remainder_l ? `<div class="small muted">remainder ${fmtA(s.remainder_l)} L</div>` : ""}</td>
        <td class="n"><b>${s.loss_l != null ? fmtA(s.loss_l, 1) : "—"}</b></td><td class="n">${s.loss_pct != null ? fmt(s.loss_pct, 2) + "%" : "—"}</td>
        <td class="n">${s.norm_pct != null ? fmt(s.norm_pct, 1) + "%" : "—"}${s.excess_l > 0 ? `<div class="small" style="color:var(--red)">excess ${fmtA(s.excess_l, 1)} L</div>` : ""}</td>
        <td class="n">${o ? `${esc(o.winery)}: ${fmt(o.loss_pct, 2)}%${chip(o.source)}` : "—"}</td><td>${s.status !== "n/a" ? statusBadge(s.status) : ""}</td></tr>`;
    }).join("")}</tbody></table></div>`;
}
function checksList(checks) {
  return `<ul class="checks">${checks.map((c) => `<li>${checkIcon(c.status)}<div><b>${esc(c.label)}</b>${chip(c.source)}<br><span class="small muted">${esc(c.detail)}</span></div></li>`).join("")}</ul>`;
}
function labTable(lab) {
  if (!lab.length) return `<p class="muted">No lab rows.</p>`;
  return `<div class="table-wrap"><table><thead><tr><th>Date</th><th>Vessel</th><th>Stage</th><th class="n">Sugar g/L</th><th class="n">Temp °C</th><th class="n">pH</th><th class="n">TA g/L</th><th class="n">RS g/L</th><th>Source</th></tr></thead><tbody>
    ${lab.map((l) => `<tr><td>${dt(l.date)}</td><td>${l.vessel ? vname(l.vessel) : "—"}</td><td>${esc(l.stage || "")}${l.note ? ` <span class="small muted">${esc(l.note)}</span>` : ""}</td>
      <td class="n">${l.sugar_gl != null ? fmt(l.sugar_gl, 1) : "—"}</td><td class="n">${l.temp_c != null ? fmt(l.temp_c, 1) : "—"}</td><td class="n">${l.ph != null ? fmt(l.ph, 2) : "—"}</td><td class="n">${l.ta_gl != null ? fmt(l.ta_gl, 1) : "—"}</td><td class="n">${l.rs_gl != null ? fmt(l.rs_gl, 1) : "—"}</td><td>${chip(l.source)}</td></tr>`).join("")}
  </tbody></table></div>`;
}
function originTable(origins) {
  if (!origins.length) return `<p class="muted">No farmer deliveries recorded — the dataset has no farmers for this batch.</p>`;
  const o0 = origins[0];
  return `<div class="table-wrap"><table><thead><tr><th>${t("Farmer")}</th><th>${t("Plot")}</th><th class="n">ha</th><th class="n">kg</th><th class="n">Share${chip("Calculated")}</th><th class="n">kg/ha${chip("Calculated")}</th><th>Municipality</th></tr></thead><tbody>
    ${origins.map((o) => `<tr><td>${esc(o.farmer)}${chip(o.source)}</td><td>${esc(o.plot)} <span class="small muted">${esc(o.cadastre_no || "")}</span></td><td class="n">${fmtA(o.area_ha)}</td>
      <td class="n">${fmt(o.kg)}</td><td class="n"><b>${fmtA(o.share_pct, 2)}%</b></td><td class="n">${o.area_ha ? fmt(o.kg / o.area_ha) : "—"}</td><td>${esc(o.municipality || "—")}</td></tr>`).join("")}
  </tbody></table></div>
  ${o0.lat != null ? `<p class="small">Terroir: ${o0.lat}, ${o0.lng}${chip("Dataset A1")} · soil pH ${o0.soil_ph}, ${esc(o0.soil_texture)}${chip("Dataset B2")} · rainfall ${o0.rain_mm} mm, summer mean ${o0.summer_temp_c} °C${chip("Dataset A1")}
    <br><span class="muted">Regional model data (ERA5, SoilGrids), not an on-site measurement.</span></p>` : ""}`;
}
function fermFromLab(lab) {
  const f = lab.filter((l) => l.sugar_gl != null && l.temp_c != null);
  if (f.length < 2) return null;
  return { days: f.map((_, i) => i + 1), sugar: f.map((l) => l.sugar_gl), temp: f.map((l) => l.temp_c), source: f[0].source };
}
function timelineEvents(d) {
  const ev = [];
  const lab = fermFromLab(d.lab);
  for (const s of d.stages) {
    if (s.type === "intake") ev.push({ date: s.date, icon: "grape", label: "Grapes in" });
    else ev.push({ date: s.date, icon: OP_ICON[s.stage_key] || "barrel", label: plain(s.stage_key), color: s.status === "above" ? C.red : s.type === "bottling" || s.type === "warehouse" ? C.green : C.wine });
    if (s.type === "intake" && lab) ev.push({ date: d.lab.find((l) => l.sugar_gl != null).date, icon: "thermometer", label: "Fermenting", color: C.amber, skip: true });
  }
  return ev.filter((e, i, a) => !(e.skip && a.some((x, j) => j !== i && x.date === e.date)));
}

async function pageBatch(id, q) {
  const d = await api(`/api/batches/${id}`);
  const b = d.batch, tt = d.totals;
  const live = /live demo/.test(b.source_note || "");
  const btag = live ? "Entered (live demo)" : "Dataset A2";
  const sepSrc = (d.stages.find((s) => s.type === "separation") || {}).source;
  const ferm = fermFromLab(d.lab);
  const [rd, cov] = await Promise.all([api(`/api/readings?batch=${id}`), api(`/api/journals/coverage?batch=${id}`)]);
  const prog = rd.rows.filter((r) => r.type === "hydrometer_abv" && r.calc && r.calc.progress_pct != null).slice(-1)[0];
  const pair = /^W01/.test(b.code) ? ["W03-B3-2025", "W01", "W03"] : /^W03/.test(b.code) ? ["W01-B1-2025", "W03", "W01"] : null;
  const other = pair && d.stages.some((s) => s.loss_pct != null) ? await batchByCode(pair[0]).then((x) => (x ? api(`/api/batches/${x.id}`) : null)) : null;
  const hasOps = tt.first_litres != null;
  const filled = new Set(cov.filter((j) => j.filled).map((j) => j.n));

  app().innerHTML = `
    <p class="no-print crumb"><a href="#/batches">${t("Batches")}</a> ${ico("chevron-right")} ${esc(b.code)}</p>
    <h1>${esc(b.code)}</h1>
    <div class="subhead">
      <span class="pill">${ico(b.technology === "qvevri" ? "barrel" : "wine")} ${esc(b.wine_type || "")}${chip(btag)}</span>
      ${b.variety ? `<span class="pill">${ico("grape")} ${esc(b.variety)}${chip("DEMO ASSUMPTION")}</span>` : ""}
      <span class="pill">${ico("map-pin")} ${esc(b.winery)}</span>${b.copy ? `<span class="status info">copy</span>` : ""}
      <span class="grow"></span>
      <a class="btn ghost small" href="#/history/${id}">${ico("file-text")} Batch history</a>
      <a class="btn ghost small" href="#/journals?batch=${id}">${ico("book-open")} Journals</a>
      <a class="btn ghost small expert-only" href="#/record?batch=${id}">${ico("pencil-line")} Record</a>
      <a class="btn ghost small expert-only" href="#/readings?batch=${id}">${ico("thermometer")} Reading</a>
    </div>

    <div class="grid k4">
      ${kpiTile({ icon: "grape", label: "Grapes", value: b.grapes_kg != null ? `${fmt(b.grapes_kg)}<small> kg</small>` : "—", sub: "intake", src: chip(btag) })}
      ${kpiTile({ icon: "wine", label: "Wine after pressing", value: hasOps ? `${fmt(tt.first_litres)}<small> L</small>` : "—", sub: hasOps ? `${fmt(tt.first_litres / (b.grapes_kg / 1000))} L per tonne` : "not in the dataset", src: hasOps ? chip(sepSrc) : "" })}
      ${kpiTile({ icon: "wine", label: "Bottled", value: tt.bottled_l ? `${fmt(tt.bottled_l)}<small> L</small>` : "—", sub: tt.bottled_l ? `${fmt(tt.bottles)} bottles` : "not bottled yet", src: tt.bottled_l ? chip("Calculated") : "", tone: "green" })}
      ${kpiTile({ icon: "droplet", label: "Wine lost", value: hasOps ? `<span id="bLoss">${fmtA(tt.total_loss_l, 1)}</span><small> L</small>` : "—", sub: hasOps ? `≈ <b>${usd(tt.loss_value_usd)}</b>${tt.remainder_l ? ` · +${fmtA(tt.remainder_l)} L left in tank` : ""}` : "", src: hasOps ? chip("Calculated") : "", tone: "red" })}
    </div>

    <div class="grid">
      <section class="card span-12"><div class="card-head"><h3>${ico("wine")} Journey — farmer to bottle</h3>${chip(sepSrc || "Dataset A2")}${d.origins.length ? chip("DEMO ASSUMPTION", "Farmers and the kg split are DEMO ASSUMPTIONS") : ""}</div>
        <div id="bJourney" class="journey-box"></div></section>
      <section class="card span-12"><div class="card-head"><h3>${ico("calendar")} Timeline</h3></div><div id="bTimeline"></div></section>

      <section class="card span-4"><div class="card-head"><h3>${ico("users")} Origin</h3>${d.origins.length ? chip("DEMO ASSUMPTION") : ""}</div>
        <div id="bDonut">${d.origins.length ? donut(d.origins.map((o) => ({ label: o.farmer.replace("Demo ", ""), value: o.kg })), { center: `${d.origins.length}`, sub: d.origins.length > 1 ? "farmers" : "farmer" })
          : `<div class="feed-empty">${ico("users")}<p>No farmers in the dataset for this batch.</p></div>`}</div></section>
      <section class="card span-8"><div class="card-head"><h3>${ico("thermometer")} ${t("Fermentation")}</h3>${ferm ? chip(ferm.source) : ""}</div>
        <div class="ferm-row">
          <div>${gauge(prog ? prog.calc.progress_pct : null, { label: "of potential alcohol", sub: prog ? "vs Initial Brix" : "no readings yet" })}
            ${prog ? chipF("Calculated", "ABV reached ÷ (Initial Brix × 0.59) × 100%") : `<a class="small" href="#/readings?batch=${id}">Add readings ${ico("chevron-right")}</a>`}</div>
          <div id="bSugar" class="grow-1">${ferm ? "" : `<div class="feed-empty">${ico("thermometer")}<p>No fermentation log for this batch in the dataset.</p></div>`}</div>
        </div></section>

      ${hasOps ? `<section class="card ${other ? "span-6" : "span-12"}"><div class="card-head"><h3>${ico("droplet")} Litres after each step</h3>${chip("Calculated")}</div><div id="bWaterfall"></div></section>` : ""}
      ${other ? `<section class="card span-6"><div class="card-head"><h3>${ico("zap")} Loss per step · ${pair[1]} vs ${pair[2]}</h3></div><div id="bBars"></div></section>` : ""}

      <section class="card span-4 money"><div class="card-head"><h3>${ico("coins")} Money</h3>${chip("Calculated", `litres lost × $${fmt(d.settings.value_per_litre_usd, 2)}/L`)}</div>
        ${hasOps ? `<div class="money-big"><span>Wine lost</span><b>${fmtA(tt.total_loss_l, 1)} L ≈ <span id="bMoney">$0.00</span></b></div>
          ${tt.excess_l > 0.5 ? `<div class="callout red">${ico("alert-triangle")} ${fmtA(tt.excess_l, 1)} L above the normal loss ≈ ${usd(tt.excess_l * d.settings.value_per_litre_usd)}</div>` : `<div class="callout green">${ico("check-circle")} Every step within the normal loss</div>`}`
          : `<p class="muted">No litres recorded for this batch.</p>`}</section>
      <section class="card span-8"><div class="card-head"><h3>${ico("book-open")} The 8 Kakheti journals</h3>${chip("Kakheti PDO spec")}</div>${journalTiles(filled)}
        <p class="small muted">Filled from the records — no separate typing. Tasting commission is a template (no tasting data in the dataset).</p></section>
    </div>

    <div class="stack">
      ${more("Stages — mass balance table", stageTable(d.stages) + `<p class="small muted">loss = litres in source − litres out − remainder; % of litres before. Normal loss per step is a DEMO ASSUMPTION (Settings). Records are append-only: a correction is a new operation with a note.</p>`)}
      ${more("Checks", checksList(d.checks))}
      ${more("Origin — farmers and plots", originTable(d.origins))}
      ${more("Laboratory", labTable(d.lab))}
      <div class="card" id="rd-anchor"><div class="card-head"><h3>${ico("activity")} Fermentation ${esc(t("Readings").toLowerCase())}</h3></div><div id="rd-batch"></div></div>
    </div>`;

  paint("#bJourney", (w) => journeyFlow(w, d, { numbers: !isJury() }));
  paint("#bTimeline", (w) => timeline(w, timelineEvents(d)));
  if (ferm) paint("#bSugar", (w) => sugarCurve(w, ferm.days, ferm.sugar, { temp: isJury() ? null : ferm.temp }));
  if (hasOps) paint("#bWaterfall", (w) => waterfallSvg(w, d.stages));
  if (other) paint("#bBars", (w) => groupedBars(w, /^W01/.test(b.code) ? d.stages : other.stages, /^W01/.test(b.code) ? other.stages : d.stages));
  if (hasOps) countUp($("#bMoney"), tt.loss_value_usd, { d: 2, prefix: "$", ms: 1100 });
  ReadingsView.mount($("#rd-batch"), rd, { addHref: `#/readings?batch=${id}` });
  if (q && q.get("readings")) $("#rd-anchor").scrollIntoView();
}

async function pageHistory(id) {
  const d = await api(`/api/batches/${id}`);
  const b = d.batch, tt = d.totals;
  const ferm = fermFromLab(d.lab);
  const bott = d.stages.filter((s) => s.type === "bottling" || s.type === "warehouse");
  app().innerHTML = `
    <div class="btn-row no-print"><a class="btn ghost" href="#/batch/${id}">${ico("chevron-right")} Batch page</a><button class="btn" onclick="window.print()">${ico("file-text")} Print / Save as PDF</button></div>
    <h1>Batch history — ${esc(b.code)}</h1>
    <p>${esc(b.wine_type || "")}${chip("Dataset A2")} · ${b.variety ? esc(b.variety) + chip("DEMO ASSUMPTION") + " · " : ""}vintage ${b.vintage ?? "—"} · winery ${esc(b.winery)} · grapes ${b.grapes_kg != null ? N(b.grapes_kg, "Dataset A2", "kg") : "—"} · printed ${dt(new Date().toISOString())}</p>
    <div class="card"><h3>1. Journey</h3><div id="hJourney"></div></div>
    <div class="card"><h3>2. Origin</h3>${originTable(d.origins)}</div>
    <div class="card"><h3>3. ${t("Fermentation")}</h3>${ferm ? `<div id="hSugar"></div>` : '<p class="muted">No fermentation log for this batch.</p>'}</div>
    <div class="card"><h3>4. Operations with losses</h3><div id="hWater"></div>${stageTable(d.stages)}
      <p>Total loss ${N(tt.total_loss_l, "Calculated", "L", 1)} · value ${usd(tt.loss_value_usd)}${chip("Calculated")}</p></div>
    <div class="card"><h3>5. Laboratory</h3>${labTable(d.lab)}</div>
    <div class="card"><h3>6. ${t("Bottling")}</h3>${bott.length ? `<ul>${bott.map((s) => `<li>${dt(s.date)} · ${esc(opLabel(s.type))}: ${fmt(s.bottles)} bottles${s.type === "bottling" ? ` × ${fmt(s.bottle_ml)} mL = ${fmt(s.after_l)} L, remainder ${fmtA(s.remainder_l)} L` : ""}${chip(s.source)}</li>`).join("")}</ul>` : '<p class="muted">Not bottled yet.</p>'}</div>
    <div class="card"><h3>7. Checks</h3>${checksList(d.checks)}</div>
    <div class="card"><h3>8. Fermentation ${esc(t("Readings").toLowerCase())}</h3><div id="hist-rd"></div></div>
    <p class="small muted">Generated by Bacchus Ledger from the append-only cellar record. Journals: Kakheti PDO spec §12.</p>`;
  paint("#hJourney", (w) => journeyFlow(w, d, { numbers: true }));
  if (ferm) paint("#hSugar", (w) => sugarCurve(w, ferm.days, ferm.sugar, { temp: ferm.temp }));
  if (tt.first_litres != null) paint("#hWater", (w) => waterfallSvg(w, d.stages));
  ReadingsView.mount($("#hist-rd"), await api(`/api/readings?batch=${id}`), {});
}
