// Bacchus Ledger — working pages: record operation, farmers, deliveries, vessels, journals, data sources, settings.
"use strict";

// ---------------------------------------------------------------- Record operation
let VESSELS = null;
async function pageRecord(q) {
  const [bs, vs] = await Promise.all([api("/api/batches"), api("/api/vessels")]);
  VESSELS = vs;
  const st = { batch_id: +(q.get("batch") || bs[0]?.id), type: q.get("type") || "racking", from_vessel: q.get("from") || "", to_vessel: q.get("to") || "",
    mode: q.get("mode") || "dip", dip_cm: q.get("dip") || "", litres: "", bottles: "", bottle_ml: 750, remainder_l: "", kg: "", date: q.get("date") || new Date().toISOString().slice(0, 10), person: "", note: "" };
  const types = ["intake", "separation", "racking", "stock_check", "filtration", "transfer", "bottling", "warehouse"];
  app().innerHTML = `<h1>${t("Record operation")}</h1>
    <div class="btn-row"><button class="btn amber" id="preset">Try: W03 filtration with dip 35.0 cm</button></div>
    <div class="grid k2">
      <div class="card" id="form">
        <label>${t("Batch")}</label><select id="f_batch">${bs.map((b) => `<option value="${b.id}">${esc(b.code)} · ${esc(b.winery)}</option>`).join("")}</select>
        <label>Operation</label><select id="f_type">${types.map((k) => `<option value="${k}">${esc(opLabel(k))}</option>`).join("")}</select>
        <div class="row2"><div><label>Date</label><input type="date" id="f_date"></div><div><label>Person</label><input type="text" id="f_person" placeholder="cellar master"></div></div>
        <div class="row2"><div id="w_from"><label>From vessel</label><select id="f_from"></select></div><div id="w_to"><label>To vessel</label><select id="f_to"></select></div></div>
        <div id="w_mode"><label>Enter</label><div class="seg" id="f_mode"><button data-m="dip">${t("Dip reading (cm)")}</button><button data-m="litres">${t("Litres")}</button></div></div>
        <div id="w_dip"><label id="dipLabel">${t("Dip reading (cm)")}</label><input class="big" type="number" step="0.1" inputmode="decimal" id="f_dip"></div>
        <div id="w_litres"><label>${t("Litres")}</label><input class="big" type="number" step="0.1" inputmode="decimal" id="f_litres"></div>
        <div id="w_bottles" class="row2"><div><label>Bottles</label><input class="big" type="number" step="1" inputmode="numeric" id="f_bottles"></div><div><label>Bottle mL</label><input type="number" id="f_ml" value="750"></div></div>
        <div id="w_kg"><label>Grapes kg (scale)</label><input class="big" type="number" step="1" id="f_kg"></div>
        <div id="w_rem"><label>Remainder left in source vessel (L)</label><input type="number" step="0.1" id="f_rem" placeholder="0"></div>
        <label>Note (corrections are new operations with a note)</label><input type="text" id="f_note">
        <div class="btn-row"><button class="btn" id="save">${t("Save")}</button></div>
        <div id="saveMsg"></div>
      </div>
      <div class="card" id="preview"><h3>Live preview</h3><p class="muted">Fill in the form — the app calculates litres, loss and status before you save.</p></div>
    </div>`;

  const F = (id) => $("#" + id);
  function fillVessels() {
    const b = bs.find((x) => x.id === st.batch_id);
    const opts = vs.filter((v) => !b || v.winery === b.winery);
    const o = (sel) => `<option value="">—</option>` + opts.map((v) => `<option value="${esc(v.code)}" ${v.code === sel ? "selected" : ""}>${esc(vtext(v))} · ${v.type === "qvevri" ? t("Qvevri") : t("Tank")} · ${v.capacity_l != null ? fmt(v.capacity_l) + " L" : t("Volume unknown")}${v.calibrated ? "" : " (no dip)"}${v.litres_now ? ` (now ${fmtA(v.litres_now, 1)} L)` : ""}</option>`).join("");
    F("f_from").innerHTML = o(st.from_vessel); F("f_to").innerHTML = o(st.to_vessel);
    if (!opts.length) F("f_from").innerHTML = F("f_to").innerHTML = `<option value="">no vessels in the dataset for this winery</option>`;
  }
  function layout() {
    const ty = st.type;
    const hasFrom = ty !== "intake" && ty !== "warehouse", hasTo = ["intake", "separation", "racking", "filtration", "transfer"].includes(ty);
    const isB = ty === "bottling" || ty === "warehouse";
    F("w_from").classList.toggle("hidden", !hasFrom); F("w_to").classList.toggle("hidden", !hasTo);
    $("#w_from label").textContent = ty === "stock_check" ? "Vessel" : "From vessel";
    F("w_mode").classList.toggle("hidden", isB || ty === "intake");
    F("w_dip").classList.toggle("hidden", isB || ty === "intake" || st.mode !== "dip");
    F("w_litres").classList.toggle("hidden", isB || ty === "intake" || st.mode !== "litres");
    F("w_bottles").classList.toggle("hidden", !isB);
    F("w_kg").classList.toggle("hidden", ty !== "intake");
    F("w_rem").classList.toggle("hidden", !["racking", "filtration", "transfer", "bottling"].includes(ty));
    F("dipLabel").textContent = `${t("Dip reading (cm)")} — ${ty === "stock_check" ? "in the vessel" : "in the destination vessel"}`;
    $$("#f_mode button").forEach((b) => b.classList.toggle("on", b.dataset.m === st.mode));
  }
  function load() {
    F("f_batch").value = st.batch_id; F("f_type").value = st.type; F("f_date").value = st.date; F("f_dip").value = st.dip_cm; F("f_litres").value = st.litres;
    F("f_bottles").value = st.bottles; F("f_ml").value = st.bottle_ml; F("f_rem").value = st.remainder_l; F("f_kg").value = st.kg; F("f_person").value = st.person; F("f_note").value = st.note;
    fillVessels(); layout();
  }
  function read() {
    st.batch_id = +F("f_batch").value; st.type = F("f_type").value; st.date = F("f_date").value; st.from_vessel = F("f_from").value; st.to_vessel = F("f_to").value;
    st.dip_cm = F("f_dip").value; st.litres = F("f_litres").value; st.bottles = F("f_bottles").value; st.bottle_ml = F("f_ml").value; st.remainder_l = F("f_rem").value;
    st.kg = F("f_kg").value; st.person = F("f_person").value; st.note = F("f_note").value;
  }
  const body = () => {
    const b = { batch_id: st.batch_id, type: st.type, date: st.date, mode: st.mode, person: st.person, note: st.note,
      from_vessel: F("w_from").classList.contains("hidden") ? "" : st.from_vessel, to_vessel: F("w_to").classList.contains("hidden") ? "" : st.to_vessel };
    for (const k of ["dip_cm", "litres", "bottles", "bottle_ml", "remainder_l", "kg"]) if (st[k] !== "" && st[k] != null) b[k] = Number(st[k]);
    return b;
  };
  let timer = null, seq = 0;
  function schedulePreview() { clearTimeout(timer); timer = setTimeout(preview, 200); }
  async function preview() {
    const my = ++seq;
    const box = F("preview");
    try {
      const p = await post("/api/operations/preview", body());
      if (my !== seq) return;
      const s = p.stage;
      box.innerHTML = `<h3>Live preview</h3>
        ${p.dip ? `<p>Dip ${fmtA(p.dip.dip_cm, 1)} cm in ${esc(p.dip.dip_vessel)} → <span class="num" style="font-size:1.3rem">${fmtA(p.dip.litres_in_vessel, 1)} L</span>${chip("Calculated")}
          <span class="small muted">(vessel size ${esc(p.dip.source)})</span>${p.dip.already_in_vessel ? `<br><span class="small">minus ${fmtA(p.dip.already_in_vessel, 1)} L already in the vessel</span>` : ""}</p>` : ""}
        <div class="grid k3" style="gap:8px">
          <div class="kpi"><div class="label">${t("Litres")}${s.type === "bottling" ? " bottled" : ""}</div><div class="value">${p.litres != null ? fmtA(p.litres, 1) : "—"}</div>${chip("Calculated")}</div>
          <div class="kpi"><div class="label">Before (in source)</div><div class="value">${s.before_l != null ? fmtA(s.before_l, 1) : "—"}</div>${chip("Calculated")}</div>
          <div class="kpi ${s.status === "above" ? "warn" : ""}"><div class="label">${t("Loss")}</div><div class="value">${s.loss_l != null ? fmtA(s.loss_l, 1) + " L" : "—"}</div>${s.loss_pct != null ? `${fmt(s.loss_pct, 2)}%` : ""}${chip("Calculated")}</div>
        </div>
        <p>${s.status !== "n/a" ? statusBadge(s.status) : ""} ${s.norm_pct != null ? `norm ${fmt(s.norm_pct, 1)}% = ${fmtA(s.norm_l, 1)} L${chip("DEMO ASSUMPTION")}` : ""}
          ${s.excess_l > 0 ? ` · excess <b>${fmtA(s.excess_l, 1)} L</b>${chip("Calculated")}` : ""}</p>
        ${s.other ? `<p>Same step at ${esc(s.other.winery)}: ${fmt(s.other.loss_pct, 2)}%${chip(s.other.source)}</p>` : ""}
        ${checksList(p.checks)}
        <p class="small muted">Nothing is saved until you press ${t("Save")}.</p>`;
    } catch (e) {
      if (my !== seq) return;
      box.innerHTML = `<h3>Live preview</h3><p class="muted">${esc(e.message)}</p>`;
    }
  }
  F("form").addEventListener("input", (e) => { read(); if (e.target.id === "f_batch") fillVessels(); layout(); schedulePreview(); });
  F("form").addEventListener("change", (e) => { read(); if (e.target.id === "f_batch") { st.from_vessel = st.to_vessel = ""; fillVessels(); } layout(); schedulePreview(); });
  $$("#f_mode button").forEach((b) => (b.onclick = (e) => { e.preventDefault(); st.mode = b.dataset.m; layout(); schedulePreview(); }));
  F("save").onclick = async (e) => {
    e.preventDefault(); read();
    try {
      const r = await post("/api/operations", body());
      F("saveMsg").innerHTML = `<p class="status ok">Saved (operation #${r.id})</p> <a href="#/batch/${st.batch_id}">Open batch page →</a>`;
      toast("Saved — journals updated");
      VESSELS = await api("/api/vessels");
    } catch (err) { F("saveMsg").innerHTML = `<p class="err">${esc(err.message)}</p>`; }
  };
  F("preset").onclick = async () => {
    const r = await post("/api/try/w03");
    location.hash = `#/record?batch=${r.batch_id}&type=filtration&from=T3-2&to=T3-3&mode=dip&dip=35.0&date=2026-06-10`;
    toast(`Fresh copy ${r.code} created (W03 batch 3 up to the stock check)`);
  };
  load();
  if (q.get("batch")) schedulePreview();
}

// ---------------------------------------------------------------- Farmers & plots, Deliveries
async function pageFarmers() {
  const [fs, ps, ws] = await Promise.all([api("/api/farmers"), api("/api/plots"), api("/api/wineries")]);
  app().innerHTML = `<h1>${t("Farmers & plots")}</h1>
    <div class="note">The dataset has no farmers. Demo Farmers 1–5 and plots K-101…K-104, P-301 are DEMO ASSUMPTIONS. Terroir is regional model data (ERA5, SoilGrids), not an on-site measurement.</div>
    <div class="card table-wrap"><h3>${t("Farmer")}s</h3><table><thead><tr><th>Code</th><th>Name</th><th>Phone</th><th class="n">Plots</th><th class="n">kg delivered</th><th>Source</th></tr></thead><tbody>
      ${fs.map((f) => `<tr><td>${esc(f.code)}</td><td>${esc(f.name)}</td><td>${esc(f.phone || "—")}</td><td class="n">${f.plots}</td><td class="n">${fmt(f.kg_delivered)}</td><td>${chip(f.is_demo ? "DEMO ASSUMPTION" : "Entered")}</td></tr>`).join("")}
    </tbody></table></div>
    <div class="card table-wrap"><h3>${t("Plot")}s</h3><table><thead><tr><th>${t("Plot")}</th><th>${t("Farmer")}</th><th>Cadastre</th><th>Municipality</th><th>PDO zone</th><th class="n">ha</th><th>Variety</th><th>Terroir</th><th>Source</th></tr></thead><tbody>
      ${ps.map((p) => `<tr><td><b>${esc(p.code)}</b> <span class="small muted">${esc(p.winery || "")}</span></td><td>${esc(p.farmer)}</td><td>${esc(p.cadastre_no || "—")}</td><td>${esc(p.municipality || "—")}</td>
        <td>${p.in_pdo_zone ? '<span class="tick">✓</span>' : '<span class="cross">✗</span>'}${chip("Kakheti PDO spec")}</td><td class="n">${fmtA(p.area_ha)}</td><td>${esc(p.variety || "—")}</td>
        <td class="small">${p.soil_ph != null ? `pH ${p.soil_ph}${chip("Dataset B2")} · ${p.rain_mm} mm · ${p.summer_temp_c} °C${chip("Dataset A1")}` : "—"}</td><td>${chip(p.is_demo ? "DEMO ASSUMPTION" : "Entered")}</td></tr>`).join("")}
    </tbody></table></div>
    <div class="grid k2 no-print">
      <form class="card" id="addF"><h3>Add farmer</h3><label>Name</label><input type="text" name="name" required><label>Phone</label><input type="text" name="phone"><div class="btn-row"><button class="btn">${t("Save")}</button></div></form>
      <form class="card" id="addP"><h3>Add plot</h3>
        <label>${t("Farmer")}</label><select name="farmer_id">${fs.map((f) => `<option value="${f.id}">${esc(f.name)}</option>`).join("")}</select>
        <div class="row2"><div><label>Plot code</label><input type="text" name="code" required></div><div><label>Cadastre no.</label><input type="text" name="cadastre_no"></div></div>
        <div class="row2"><div><label>Municipality</label><select name="municipality">${["Akhmeta", "Telavi", "Gurjaani", "Sighnaghi", "Dedoplistskaro", "Kvareli", "Lagodekhi", "Sagarejo", "Other (outside PDO zone)"].map((m) => `<option>${m}</option>`).join("")}</select></div>
          <div><label>Village</label><input type="text" name="village"></div></div>
        <div class="row2"><div><label>Area ha</label><input type="number" step="0.01" name="area_ha"></div><div><label>Variety</label><input type="text" name="variety"></div></div>
        <div class="row2"><div><label>Winery</label><select name="winery">${ws.map((w) => `<option>${esc(w.code)}</option>`).join("")}</select></div><div><label>Licence no.</label><input type="text" name="licence_no"></div></div>
        <div class="btn-row"><button class="btn">${t("Save")}</button></div></form>
    </div>`;
  const submit = (id, url) => ($("#" + id).onsubmit = async (e) => {
    e.preventDefault();
    const body = Object.fromEntries(new FormData(e.target).entries());
    if (body.farmer_id) body.farmer_id = +body.farmer_id;
    try { await post(url, body); toast("Saved"); pageFarmers(); } catch (err) { toast(err.message); }
  });
  submit("addF", "/api/farmers"); submit("addP", "/api/plots");
}

async function pageDeliveries() {
  const [ds, ps, bs] = await Promise.all([api("/api/deliveries"), api("/api/plots"), api("/api/batches")]);
  app().innerHTML = `<h1>${t("Deliveries")}</h1>
    <div class="card table-wrap"><table><thead><tr><th>Date</th><th>${t("Farmer")}</th><th>${t("Plot")}</th><th class="n">kg</th><th class="n">Sugar g/L</th><th>Accepted</th><th>${t("Batch")}</th><th>Source</th></tr></thead><tbody>
      ${ds.map((d) => `<tr><td>${dt(d.date)}</td><td>${esc(d.farmer)}</td><td>${esc(d.plot)}</td><td class="n"><b>${fmt(d.kg)}</b></td><td class="n">${d.sugar_gl != null ? fmtA(d.sugar_gl, 1) + chip("Dataset A3") : "—"}</td>
        <td>${d.accepted ? '<span class="tick">✓</span>' : '<span class="cross">✗</span>'}</td><td class="small">${esc(d.batches || "—")}</td><td>${chip(d.source)}</td></tr>`).join("")}
    </tbody></table>
    <p class="small muted">kg per delivery is a DEMO split of the Dataset A2 batch total (W01: 16,000 kg; W03: 1,850 kg). Sugar 233 g/L = Dataset A3 day 1.</p></div>
    <form class="card no-print" id="addD"><h3>Receive grapes</h3>
      <div class="row2"><div><label>${t("Plot")}</label><select name="plot_id">${ps.map((p) => `<option value="${p.id}">${esc(p.code)} · ${esc(p.farmer)}</option>`).join("")}</select></div>
        <div><label>Date</label><input type="date" name="date" value="${new Date().toISOString().slice(0, 10)}"></div></div>
      <div class="row2"><div><label>kg (scale)</label><input class="big" type="number" name="kg" required></div><div><label>Sugar g/L</label><input class="big" type="number" step="0.1" name="sugar_gl"></div></div>
      <div class="row2"><div><label>Link to ${t("Batch").toLowerCase()} (optional)</label><select name="batch_id"><option value="">—</option>${bs.map((b) => `<option value="${b.id}">${esc(b.code)}</option>`).join("")}</select></div>
        <div><label>Accepted</label><select name="accepted"><option value="1">yes</option><option value="0">no</option></select></div></div>
      <label>Quality note</label><input type="text" name="quality_note">
      <div class="btn-row"><button class="btn">${t("Save")}</button></div></form>`;
  $("#addD").onsubmit = async (e) => {
    e.preventDefault();
    const f = Object.fromEntries(new FormData(e.target).entries());
    const body = { plot_id: +f.plot_id, date: f.date, kg: +f.kg, accepted: +f.accepted, quality_note: f.quality_note };
    if (f.sugar_gl) body.sugar_gl = +f.sugar_gl;
    if (f.batch_id) body.batch_id = +f.batch_id;
    try { await post("/api/deliveries", body); toast("Saved — journals 2 and 4 updated"); pageDeliveries(); } catch (err) { toast(err.message); }
  };
}

// ---------------------------------------------------------------- Vessels
function capacityHtml(v) {
  if (v.capacity_l == null) return `<b>${esc(t("Volume unknown"))}</b> — enter litres manually`;
  return `Capacity ${N(v.capacity_l, v.capacity_source || "Calculated", "L")}`;
}

async function pageVessels() {
  const [vs, ws] = await Promise.all([api("/api/vessels"), api("/api/wineries")]);
  app().innerHTML = `<h1>${t("Vessels")}</h1>
    <div class="note">Big text = the ${esc(t("Cellar label").toLowerCase())} written on the tape in the cellar (Entered); small = internal code from the dataset.
      Tank sizes are DEMO ASSUMPTIONS. Demo qvevri use the generalised A5 model (Dataset A5).</div>
    <div class="card no-print"><label>Search by ${esc(t("Cellar label").toLowerCase())} or code</label><input type="text" id="vq" placeholder="e.g. F.2 or Q-3" autocomplete="off"></div>
    <div id="vlist"></div>
    <form class="card no-print" id="addV"><h3>Add a vessel</h3>
      <div class="row2"><div><label>Winery</label><select name="winery">${ws.map((w) => `<option>${esc(w.code)}</option>`).join("")}</select></div>
        <div><label>Type</label><select name="type" id="avType"><option value="qvevri">${t("Qvevri")}</option><option value="tank">${t("Tank")}</option></select></div></div>
      <div class="row2"><div><label>Internal code</label><input type="text" name="code" required placeholder="e.g. Q-7"></div>
        <div><label>${t("Cellar label")} (optional)</label><input type="text" name="cellar_label" placeholder="e.g. Q.7"></div></div>
      <div id="avQ"><p class="small muted">All optional. With depth + neck Ø + max Ø the app builds a dip table (A5 shape scaled to your numbers). Without them: “${esc(t("Volume unknown"))} — enter litres manually”. Nothing is guessed.</p>
        <div class="row2"><div><label>Depth (cm)</label><input type="number" step="0.1" name="depth_cm"></div><div><label>Known volume (L)</label><input type="number" step="1" name="known_volume_l"></div></div>
        <div class="row2"><div><label>Neck inner Ø (cm)</label><input type="number" step="0.1" name="neck_d_cm"></div><div><label>Max inner Ø (cm)</label><input type="number" step="0.1" name="max_d_cm"></div></div></div>
      <div id="avT" class="hidden"><p class="small muted">Optional: inner diameter and height make a cylinder dip table.</p>
        <div class="row2"><div><label>Inner Ø (cm)</label><input type="number" step="0.1" name="d_cm"></div><div><label>Inner height (cm)</label><input type="number" step="0.1" name="h_cm"></div></div></div>
      <div class="btn-row"><button class="btn">${t("Save")}</button></div></form>`;
  const draw = () => {
    const q = ($("#vq").value || "").trim().toLowerCase();
    const list = vs.filter((v) => !q || (v.cellar_label || "").toLowerCase().includes(q) || v.code.toLowerCase().includes(q));
    const by = {};
    list.forEach((v) => (by[v.winery] = by[v.winery] || []).push(v));
    $("#vlist").innerHTML = Object.entries(by).map(([w, l]) => `<h2>${esc(w)}</h2><div class="grid k3">${l.map((v) => `
      <a class="card" style="text-decoration:none;color:inherit;display:block" href="#/vessel/${encodeURIComponent(v.winery)}/${encodeURIComponent(v.code)}">
        <div style="display:flex;justify-content:space-between;align-items:center"><h3 style="margin:0">${vname(v.code, v.winery)}${v.cellar_label ? chip(v.cellar_label_source) : ""}</h3>
          <span class="status ${v.type === "qvevri" ? "info" : "na"}">${v.type === "qvevri" ? t("Qvevri") : t("Tank")}</span></div>
        <div class="small muted">${esc(v.name)}</div>
        <p style="margin:8px 0 0">${capacityHtml(v)}<br><span class="small">${esc(v.calibration_note)}</span><br>
          Now: <b>${fmtA(v.litres_now, 1)} L</b>${chip("Calculated")}</p></a>`).join("")}</div>`).join("") || `<p class="muted">No vessel matches.</p>`;
  };
  $("#vq").oninput = draw;
  draw();
  $("#avType").onchange = () => { $("#avQ").classList.toggle("hidden", $("#avType").value !== "qvevri"); $("#avT").classList.toggle("hidden", $("#avType").value !== "tank"); };
  $("#addV").onsubmit = async (e) => {
    e.preventDefault();
    const f = Object.fromEntries(new FormData(e.target).entries());
    const body = { winery: f.winery, type: f.type, code: f.code, cellar_label: f.cellar_label };
    for (const k of ["depth_cm", "known_volume_l", "neck_d_cm", "max_d_cm", "d_cm", "h_cm"]) if (f[k]) body[k] = +f[k];
    try { await post("/api/vessels", body); toast("Vessel saved"); location.hash = `#/vessel/${encodeURIComponent(f.winery)}/${encodeURIComponent(f.code)}`; }
    catch (err) { toast(err.message); }
  };
}

async function pageVessel(winery, code) {
  const d = await api(`/api/vessels/${encodeURIComponent(code)}?winery=${encodeURIComponent(winery)}`);
  const v = d.vessel;
  const qv = v.type === "qvevri";
  const calNote = v.kind === "qvevri" && v.model === "A5"
    ? `<div class="note">Shape: generalised A5 model (demo). Real qvevri differ — measure each qvevri once (Challenge Book 1.1).${chip("Dataset A5")}<br>
      Raw volume of the A5 profile ${fmtA(v.raw_total_l, 1)} L${chip("Calculated")}; every radius scaled by ${fmtA(v.scale, 4)} so the full volume = 1,500 L${chip("Dataset A5")}.</div>`
    : v.kind === "qvevri" ? `<div class="note">${esc(v.calibration_note)}${chip("Entered")}${chipF("Calculated", "frustum integration every 1 cm of the A5 radius profile, scaled to the entered depth and diameters")}</div>`
    : v.kind === "cylinder" ? `<div class="note">${esc(v.calibration_note)}${chip(v.source)} litres = π·(D/2)²·h / 1000.</div>`
    : `<div class="warnbox"><b>${esc(t("Volume unknown"))}</b> — ${esc(v.calibration_note)}. Dip → litres works only when the vessel has a calibration; type litres directly in operations.</div>`;
  const big = v.cellar_label || v.code;
  app().innerHTML = `<p class="no-print"><a href="#/vessels">← ${t("Vessels")}</a></p>
    <h1 class="vbig">${vname(v.code, v.winery)} <span class="small muted">${esc(v.name)} · ${esc(v.winery)}</span></h1>
    ${calNote}
    <div class="grid k2 no-print">
      <form class="card" id="vEdit"><h3>${t("Cellar label")} ${qv ? "&amp; measurements" : ""}</h3>
        <label>${t("Cellar label")} — what is written on the tape (e.g. F.2, Q.3)</label><input class="big" type="text" name="cellar_label" value="${esc(v.cellar_label || "")}" placeholder="${esc(v.code.replace("-", "."))}">
        ${v.cellar_label ? `<p class="small">Current: ${chip(v.cellar_label_source)}</p>` : `<p class="small muted">Empty — only the dataset code ${esc(v.code)} is known.</p>`}
        ${qv ? `<p class="small muted">Optional qvevri measurements — any can stay empty. Depth + neck Ø + max Ø → own dip table${v.model === "A5" ? " (replaces the demo A5 model)" : ""}.</p>
          <div class="row2"><div><label>Depth (cm)</label><input type="number" step="0.1" name="depth_cm" value="${v.depth_cm ?? ""}"></div><div><label>Known volume (L)</label><input type="number" step="1" name="known_volume_l" value="${v.known_volume_l ?? ""}"></div></div>
          <div class="row2"><div><label>Neck inner Ø (cm)</label><input type="number" step="0.1" name="neck_d_cm" value="${v.neck_d_cm ?? ""}"></div><div><label>Max inner Ø (cm)</label><input type="number" step="0.1" name="max_d_cm" value="${v.max_d_cm ?? ""}"></div></div>` : ""}
        <div class="btn-row"><button class="btn">${t("Save")}</button></div></form>
      ${v.calibrated ? `<div class="card"><h3>Dip calculator</h3>
        <label>${t("Dip reading (cm)")} — from the top rim down to the wine</label><input class="big" type="number" step="0.1" id="dip" placeholder="e.g. 35.0">
        <div id="dipOut" class="result-big">—</div>
        <label>…or ${t("Litres")} → dip</label><input type="number" step="1" id="lit" placeholder="e.g. 1230">
        <div id="litOut" class="result-big">—</div>
        <p class="small muted">${capacityHtml(v)} · now ${fmtA(v.litres_now, 1)} L${chip("Calculated")}
          ${v.contents.length ? "(" + v.contents.map((c) => `${esc(c.batch)} ${fmtA(c.litres, 1)} L`).join(", ") + ")" : ""}</p>
      </div>` : `<div class="card"><h3>${esc(t("Volume unknown"))}</h3><p>${capacityHtml(v)}</p><p class="small muted">Add depth, neck Ø and max Ø to get a dip chart.</p></div>`}
    </div>
    <div class="grid k2">
      ${v.calibrated ? `<div class="card"><h3>Shape</h3><div id="shape" style="max-width:260px;margin:auto">${vesselSvg(d.profile, v.h_cm, null, v.code)}</div></div>
      <div class="card"><div style="display:flex;justify-content:space-between;align-items:center"><h3>Dip chart (every 5 cm)</h3><button class="btn small no-print" onclick="window.print()">🖨 Print</button></div>
        <div class="table-wrap"><table><thead><tr><th class="n">Dip cm</th><th class="n">Fill height cm</th><th class="n">${t("Litres")}${chip("Calculated")}</th></tr></thead><tbody>
        ${d.chart.map((r) => `<tr><td class="n">${r.dip_cm}</td><td class="n">${fmtA(r.fill_cm, 1)}</td><td class="n"><b>${fmtA(r.litres, 1)}</b></td></tr>`).join("")}</tbody></table></div></div>` : ""}
      <div class="card"><h3>Printable label</h3>
        <div class="label-print"><div class="small">${esc(v.winery)} · ${v.type === "qvevri" ? t("Qvevri") : t("Tank")}</div>
          <div class="code ${big.length > 5 ? "long" : ""}">${esc(big)}</div>
          <div class="sub">${v.cellar_label ? `code ${esc(v.code)} · ` : ""}${v.capacity_l != null ? fmt(v.capacity_l) + " L" : esc(t("Volume unknown"))}</div>
          <div class="small muted">Bacchus Ledger — type the ${esc(t("Cellar label").toLowerCase())} in the app</div></div>
        <div class="btn-row no-print"><button class="btn small" onclick="window.print()">🖨 Print label</button></div>
        <p class="small muted">QR scanning is not part of this demo; the label shows the ${esc(t("Cellar label").toLowerCase())} in very large text.</p></div>
    </div>
    <div class="card"><h3>Fermentation ${esc(t("Readings").toLowerCase())} — ${vname(v.code, v.winery)}</h3><div id="rd-vessel"><p class="muted">Loading…</p></div></div>`;

  $("#vEdit").onsubmit = async (e) => {
    e.preventDefault();
    const f = Object.fromEntries(new FormData(e.target).entries());
    const body = { cellar_label: f.cellar_label || "" };
    if (qv) for (const k of ["depth_cm", "known_volume_l", "neck_d_cm", "max_d_cm"]) body[k] = f[k] ? +f[k] : null;
    try { await post(`/api/vessels/${encodeURIComponent(code)}/details?winery=${encodeURIComponent(winery)}`, body); toast("Saved"); route(); }
    catch (err) { toast(err.message); }
  };
  if (v.calibrated) {
    $("#dip").oninput = async () => {
      const x = $("#dip").value;
      if (x === "") { $("#dipOut").textContent = "—"; return; }
      try {
        const r = await api(`/api/vessels/${encodeURIComponent(code)}/litres?dip=${x}&winery=${encodeURIComponent(winery)}`);
        $("#dipOut").innerHTML = `${fmtA(r.litres, 1)} L${chip("Calculated")}`;
        $("#shape").innerHTML = vesselSvg(d.profile, v.h_cm, r.fill_cm, v.code);
      } catch (err) { $("#dipOut").innerHTML = `<span class="err small">${esc(err.message)}</span>`; }
    };
    $("#lit").oninput = async () => {
      const x = $("#lit").value;
      if (x === "") { $("#litOut").textContent = "—"; return; }
      const r = await api(`/api/vessels/${encodeURIComponent(code)}/litres?litres=${x}&winery=${encodeURIComponent(winery)}`);
      $("#litOut").innerHTML = `dip ${fmtA(r.dip_cm, 1)} cm${chip("Calculated")}`;
      $("#shape").innerHTML = vesselSvg(d.profile, v.h_cm, v.h_cm - r.dip_cm, v.code);
    };
  }
  const rd = await api(`/api/readings?vessel=${encodeURIComponent(code)}&winery=${encodeURIComponent(winery)}`);
  ReadingsView.mount($("#rd-vessel"), rd, { addHref: `#/readings?vessel=${encodeURIComponent(code)}&winery=${encodeURIComponent(winery)}` });
}

// ---------------------------------------------------------------- Journals
const JTITLES = ["", "Vineyard accounting", "Grape receiving", "Grape processing", "Vintage", "Storage and movement", "Bottling", "Laboratory analysis", "Tasting commission"];
async function journalFilters(q) {
  const [ws, bs] = await Promise.all([api("/api/wineries"), api("/api/batches")]);
  const w = q.get("winery") || "", b = q.get("batch") || "";
  return {
    html: `<div class="row2 no-print"><div><label>Winery</label><select id="jw"><option value="">all</option>${ws.map((x) => `<option ${x.code === w ? "selected" : ""}>${esc(x.code)}</option>`).join("")}</select></div>
      <div><label>${t("Batch")}</label><select id="jb"><option value="">all</option>${bs.filter((x) => !w || x.winery === w).map((x) => `<option value="${x.id}" ${String(x.id) === b ? "selected" : ""}>${esc(x.code)}</option>`).join("")}</select></div></div>`,
    qs: () => { const p = new URLSearchParams(); if (w) p.set("winery", w); if (b) p.set("batch", b); return p.toString(); },
    bind: (base) => { $("#jw").onchange = () => { location.hash = `${base}?winery=${$("#jw").value}`; }; $("#jb").onchange = () => { location.hash = `${base}?winery=${$("#jw").value}&batch=${$("#jb").value}`; }; },
  };
}

async function pageJournals(q) {
  const f = await journalFilters(q);
  const qs = f.qs();
  app().innerHTML = `<h1>${t("Journals")}</h1>
    <p>The 8 journals the Kakheti PDO rules require${chip("Kakheti PDO spec")} §12 — generated from the records, no separate typing.</p>
    <div class="card">${f.html}</div>
    <div class="grid k2">${JTITLES.slice(1).map((ti, i) => `<div class="card"><h3>${i + 1}. ${esc(ti)}</h3>
      ${i + 1 === 8 ? '<p class="small muted">Empty template — no tasting data in the dataset.</p>' : ""}
      <div class="btn-row"><a class="btn small" href="#/journal/${i + 1}?${qs}">View / print</a><a class="btn small secondary" href="/api/journals/${i + 1}.csv?${qs}">CSV</a></div></div>`).join("")}
    </div>
    ${q.get("batch") ? `<p><a class="btn" href="#/history/${q.get("batch")}">Batch history in 1 click</a></p>` : `<p class="muted">Pick a batch for “Batch history in 1 click”.</p>`}`;
  f.bind("#/journals");
}

async function pageJournal(n, q) {
  const f = await journalFilters(q);
  const j = await api(`/api/journals/${n}?${f.qs()}`);
  const cell = (c, r) => {
    const v = r[c.key];
    if (v === null || v === undefined || v === "") return n === 8 ? "&nbsp;" : "—";
    if (typeof v === "number") return fmtA(v, 2);
    if (/^\d{4}-\d{2}-\d{2}$/.test(v)) return dt(v);
    if (c.key === "op") return esc(opLabel(v));
    if (c.key === "status") return v === "n/a" ? "" : statusBadge(v);
    return esc(v);
  };
  app().innerHTML = `<p class="no-print"><a href="#/journals?${f.qs()}">← ${t("Journals")}</a></p>
    <h1>Journal ${n}: ${esc(j.title)}</h1>
    <p class="small">Kakheti PDO spec §12${chip("Kakheti PDO spec")} · ${q.get("winery") ? "winery " + esc(q.get("winery")) : "all wineries"} · printed ${dt(new Date().toISOString())}</p>
    <div class="card">${f.html}<div class="btn-row no-print"><button class="btn" onclick="window.print()">🖨 Print / Save as PDF</button><a class="btn secondary" href="/api/journals/${n}.csv?${f.qs()}">CSV (Excel)</a></div></div>
    <div class="card table-wrap"><table><thead><tr>${j.columns.map((c) => `<th class="${/kg|_l$|pct|ha|bottles|ml|ph|ta|rs|sugar|temp|abv|score/.test(c.key) ? "n" : ""}">${esc(c.label)}${chip(c.source)}</th>`).join("")}<th>Source</th></tr></thead>
      <tbody>${j.rows.length ? j.rows.map((r) => `<tr>${j.columns.map((c) => `<td class="${typeof r[c.key] === "number" ? "n" : ""}">${cell(c, r)}</td>`).join("")}<td>${chip(r.source)}</td></tr>`).join("") : `<tr><td colspan="${j.columns.length + 1}" class="muted">No rows for this filter.</td></tr>`}</tbody></table>
      <p class="small muted">${esc(j.note)}</p></div>`;
  f.bind(`#/journal/${n}`);
}

// ---------------------------------------------------------------- Data sources
function pageSources() {
  const tags = [
    ["Dataset A1", "Climate, reference point M1 Kvareli, Dec 2024–Nov 2025: rainfall 603.8 mm, summer mean 24.3 °C (regional model data, ERA5)"],
    ["Dataset A2", "Batches 2025: winery, intake date, grapes kg, wine type"],
    ["Dataset A3", "Fermentation log W01 batch 1, vessel F-1, daily sugar and temperature from 18.09.2025"],
    ["Dataset A4.1", "Lab W02 batch 2: pH and TA, fermentation days 1–19"],
    ["Dataset A4.2", "Lab W03 batch 3: temperature, pH, TA, RS from pomace to bottling"],
    ["Dataset A5", "Generalised qvevri shape: H 180 cm, neck Ø60 cm, max Ø137.46 cm, ≈1,500 L"],
    ["Dataset A6.1", "Cellar operations W03 batch 3 (separation → bottling)"],
    ["Dataset A6.2", "Cellar operations W01 batch 1 (paper log)"],
    ["Dataset B2", "Soil 0–30 cm at M1: pH 6.7, clay/silt/sand (SoilGrids)"],
    ["Dataset B3.1", "Part of the dataset tag set; no values from B3.1 are used in this demo"],
    ["Challenge Book", "WINECUBATOR Challenge Book: winery profiles (§2), problem quotes (3.1, 3.2), qvevri measurement (1.1), qvevri rule"],
    ["Kakheti PDO spec", "Kakheti PDO specification: zone municipalities (§3), the 8 journals (§12)"],
    ["CBW 2025", "CBW / National Wine Agency 2025: 22,000 farmers, 500+ companies, state purchase price, Kakheti 327,000 of 336,000 t"],
    ["Hackathon slides", "2025 Georgian wine exports $267.9M and 89.7M L"],
    ["TBC Capital 2024", "1,027 wine companies in Georgia in 2023 (6 large, 29 medium, 982 small)"],
    ["Calculated", "Derived by the app from the sources above (formulas shown next to the value)"],
    ["DEMO ASSUMPTION", "Invented on purpose for the demo — listed below"],
    ["SIMULATED", "LIVE mode data from the simulator (Update 3). Stored only in live_events; never used by journals, losses or ROI numbers. Real sensors (POST /api/sensors/reading) are tagged SENSOR."],
    ["Entered", "Typed into this app (live records, cellar labels, vessel measurements)"],
    ["Entered (live demo)", "Typed by the presenter in the fermentation readings demo (Initial Brix, hydrometer, pH, temperature, TA)"],
    ["time not in dataset", "Dataset rows carry a date only — readings are stored at 09:00"],
  ];
  const demo = [
    "Farmers: Demo Farmer 1–5 (the dataset has no farmers).",
    "Plots K-101 (0.8 ha), K-102 (0.6 ha), K-103 (0.5 ha), K-104 (0.4 ha) in Kvareli; P-301 (0.3 ha). P-301 municipality assumed Kvareli (reference point M1).",
    "Split of W01 batch 1 (16,000 kg, Dataset A2) into 5,200 / 4,300 / 3,800 / 2,700 kg; W03 batch 3 1,850 kg all from Demo Farmer 5.",
    "Cadastre numbers DEMO-KV-0001…0005 and licence no. DEMO.",
    "Varieties: Saperavi for W01 and W04, Rkatsiteli for W02, W03 and W05 (dataset says only “classic red” / “qvevri amber” etc.).",
    "ABV: W01 batch 1 13.5%, W03 batch 3 12.8%.",
    "Orders: one per demo delivery, kg planned = kg delivered, dated on the intake day; price = CBW 2025 state purchase price.",
    "Delivery sugar 233 g/L applied to each W01 delivery (Dataset A3 day 1 is the must in F-1).",
    "Tank sizes: F-1, F-2 Ø230 × 290 cm; T1-1…T1-5 Ø220 × 290 cm; BT-1 Ø210 × 290 cm; T3-1…T3-3 Ø110 × 160 cm. Q-4 uses the same A5 profile as Q-3.",
    "Normal loss per stage: first racking 3.0%, racking 1.5%, filtration 0.6%, bottling 0.3%, stock check 0.3%, transfer 1.0% (editable in Settings).",
    "Terroir of A1/B2 reference point M1 is attached to every demo plot (regional model data, not on-site).",
    "LIVE range alerts (config/ranges.json): fermentation temperature 20–30 °C for red and qvevri amber, 12–20 °C for white; storage 10–18 °C; density 0.990–1.130 SG; level 0 L–capacity; alert after 3 readings in a row outside the range.",
    "LIVE rule thresholds (Settings): level drop above 20 L within 1 h with no operation; stuck if density changes less than 0.001 in 48 h while SG is above 1.010.",
    "LIVE simulator start values when a vessel has no recorded value: 25.5 °C fermenting / 14 °C storage, 70% of capacity, SG 1.060 — all SIMULATED.",
  ];
  app().innerHTML = `<h1>Data sources</h1>
    <div class="card table-wrap"><table><thead><tr><th>Tag</th><th>Full name / content</th></tr></thead><tbody>
      ${tags.map(([k, v]) => `<tr><td>${chip(k)}</td><td>${esc(v)}</td></tr>`).join("")}</tbody></table></div>
    <div class="card"><h3>All DEMO ASSUMPTIONS ${chip("DEMO ASSUMPTION")}</h3><ol>${demo.map((d) => `<li>${esc(d)}</li>`).join("")}</ol></div>
    <div class="card"><h3>Calculated values</h3><ul>
      <li>value per litre $2.99 = $267.9M ÷ 89.7M L${chip("Hackathon slides")}</li>
      <li>litres per tonne 631 = 10,100 L ÷ 16 t${chip("Dataset A2")}${chip("Dataset A6.2")}</li>
      <li>~44 farmers per company = 22,000 ÷ 500${chip("CBW 2025")}</li>
      <li>potential alcohol ≈ sugar ÷ 17 (rule of thumb ~17 g/L per 1% ABV)</li>
      <li>qvevri litres: frustum integration of the A5 radius profile every 1 cm, radii scaled so full = 1,500 L</li>
      <li>loss = litres in source − litres out − remainder; excess = max(0, loss − norm% × litres before)</li>
    </ul></div>`;
}

// ---------------------------------------------------------------- Settings
async function pageSettings() {
  const [s, st] = await Promise.all([api("/api/settings"), api("/api/selftest")]);
  const field = (k, label, tag) => `<div><label>${esc(label)}${chip(tag)}</label><input type="number" step="any" name="${k}" value="${esc(s[k])}"></div>`;
  app().innerHTML = `<h1>${t("Settings")}</h1>
    <form class="card" id="sf"><h3>Reference numbers</h3>
      <div class="row2">${field("value_per_litre_usd", "Value per litre (USD)", "Calculated")}${field("litres_per_tonne", "Litres per tonne", "Calculated")}</div>
      <h3 style="margin-top:14px">Normal loss per stage (%)</h3>
      <div class="row2">${field("norm_racking_first", "First racking", "DEMO ASSUMPTION")}${field("norm_racking", t("Racking"), "DEMO ASSUMPTION")}</div>
      <div class="row2">${field("norm_filtration", t("Filtration"), "DEMO ASSUMPTION")}${field("norm_bottling", t("Bottling"), "DEMO ASSUMPTION")}</div>
      <div class="row2">${field("norm_stock_check", t("Stock check"), "DEMO ASSUMPTION")}${field("norm_transfer", t("Transfer"), "DEMO ASSUMPTION")}</div>
      <h3 style="margin-top:14px">${ico("bell")} Live rules (simulated data only)</h3>
      <p class="small muted">Temperature, density and level ranges (alert after 3 readings in a row outside the range) are set per wine type and vessel in <code>config/ranges.json</code>.</p>
      <div class="row2">${field("live_level_drop_l", "Level drop alert above (L)", "DEMO ASSUMPTION")}${field("live_level_window_h", "… within (hours)", "DEMO ASSUMPTION")}</div>
      <div class="row2">${field("live_stuck_hours", "Stuck: density flat for (hours)", "DEMO ASSUMPTION")}<div></div></div>
      <div class="row2">${field("live_stuck_delta_sg", "Stuck: density change below (SG)", "DEMO ASSUMPTION")}${field("live_stuck_min_sg", "Stuck: only while SG above", "DEMO ASSUMPTION")}</div>
      <label>Language</label><select name="language"><option value="en" ${LANG === "en" ? "selected" : ""}>English</option><option value="ka" ${LANG === "ka" ? "selected" : ""}>ქართული</option></select>
      <div class="btn-row"><button class="btn">${t("Save")}</button></div></form>
    <div class="card"><h3>Reset demo data</h3><p>Drops every record and re-creates the seed data (section 4). This is the only way rows are ever removed.</p>
      <button class="btn danger" id="reset">Reset demo data</button></div>
    <div class="card"><h3>Self-test ${st.pass ? '<span class="status pass">ALL PASS</span>' : '<span class="status fail">FAIL</span>'} <span class="small muted">${st.passed}/${st.total} · <a href="/api/selftest">/api/selftest</a></span></h3>
      <p class="small muted">${esc(st.note)}</p>
      <div class="table-wrap"><table><thead><tr><th></th><th>Group</th><th>Check</th><th>Expected</th><th>Actual</th></tr></thead><tbody>
      ${st.checks.map((c) => `<tr><td>${c.pass ? '<span class="tick">✓</span>' : '<span class="cross">✗</span>'}</td><td>${esc(c.group)}</td><td>${esc(c.name)}</td><td>${esc(c.expected)}</td><td>${esc(c.actual)}</td></tr>`).join("")}
      </tbody></table></div></div>`;
  $("#sf").onsubmit = async (e) => {
    e.preventDefault();
    const body = Object.fromEntries(new FormData(e.target).entries());
    try { await post("/api/settings", body); setLang(body.language); toast("Settings saved"); route(); } catch (err) { toast(err.message); }
  };
  $("#reset").onclick = async () => {
    if (!confirm("Reset all demo data to the seed? Entered records will be removed.")) return;
    await post("/api/reset"); toast("Demo data reset"); location.hash = "#/overview";
  };
}
