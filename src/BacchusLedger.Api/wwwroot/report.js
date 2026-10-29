// Bacchus Ledger — printable ledger report (report.html?batch=CODE[&vessel=W01/F-2]). Read-only: one GET /api/report.
"use strict";

const esc = (s) => String(s ?? "").replace(/[&<>"']/g, (c) => ({ "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&#39;" }[c]));
const fmt = (v, d = 0) => (v === null || v === undefined || v === "" || Number.isNaN(Number(v)) ? "—"
  : Number(v).toLocaleString("en-US", { minimumFractionDigits: d, maximumFractionDigits: d }));
const fmtA = (v, max = 2) => (v === null || v === undefined || v === "" ? "—" : Number(v).toLocaleString("en-US", { maximumFractionDigits: max }));
const dt = (iso) => { const s = String(iso || ""); return s.length >= 10 ? `${s.slice(8, 10)}.${s.slice(5, 7)}.${s.slice(0, 4)}` : s || "—"; };
const dtt = (iso) => { const s = String(iso || ""); return s.length >= 16 ? `${dt(s)} ${s.slice(11, 16)}` : dt(s); };
const chip = (tag) => (tag ? `<span class="chip ${tag === "SIMULATED" ? "sim" : tag === "Calculated" ? "calc" : tag.startsWith("Dataset") ? "ds" : tag === "DEMO ASSUMPTION" ? "demo" : "entered"}">${esc(tag)}</span>` : "");
const STEP = { intake: "Grapes into vessel", separation: "Pressing / separation", racking_first: "First racking", racking: "Racking", stock_check: "Stock check",
  filtration: "Filtration", transfer: "Transfer", bottling: "Bottling", warehouse: "To warehouse" };
const stepName = (s) => STEP[String(s).replace(/ /g, "_")] || s;
const RTYPE = { initial_brix: "Initial Brix", ph: "pH", temperature: "Temperature", hydrometer_density: "Hydrometer density", hydrometer_abv: "Hydrometer ABV", ta: "TA" };

function verdict(b) {
  if (!b.has_litres) return `<div class="rp-verdict na">No pressing recorded yet — the litre balance starts at separation. Grapes: ${fmt(b.harvested_kg)} kg received.</div>`;
  if (b.balanced) return `<div class="rp-verdict ok">✓ Balanced: ${fmt(b.in_l)} L in = ${fmt(b.bottled_l + b.in_vessels_l)} L out + ${fmt(b.losses_l)} L losses · no unexplained difference</div>`;
  return `<div class="rp-verdict bad">⚠ Unexplained difference: ${fmtA(b.gap_l, 2)} L${b.kg_gap ? ` · ${fmtA(b.kg_gap, 2)} kg` : ""} — see the balance check</div>`;
}

// Hash column: filled from the seal's chain once it is loaded (secure copy). Losses show the hash of the operation they come from.
// Always the last column, same two lines everywhere: "#seq + 12 chars" / "prev: + 12 chars".
const hashCell = (k, title = "", rows = 1) => `<td class="hc" ${rows > 1 ? `rowspan="${rows}"` : ""} data-k="${esc(k || "")}" ${title ? `title="${esc(title)}"` : ""}>${k ? "…" : "—"}</td>`;
// Tables of calculated values get no hash, only the range of chain entries they are computed from.
const calcFrom = (tables) => `<p class="small muted calc-from" data-from="${tables}">Computed from chain entries …</p>`;

function ledgerRows(entries) {
  return entries.map((e) => {
    const k = `k-${e.kind.replace(/ /g, "-")}`;
    const amt = (x) => `${fmtA(x.amount, 2)} ${esc(x.unit)}`;
    return `<tr class="dr ${k}"><td rowspan="2" class="n">${e.no}</td><td rowspan="2" class="nowrap">${dt(e.date)}</td>
        <td class="acct"><b>${esc(e.debit.account)}</b></td><td class="n amt">${amt(e.debit)}</td><td class="n"></td>
        <td rowspan="2"><span class="kind">${esc(e.kind)}</span><br>${esc(e.text)}${e.ratio ? `<br><span class="ratio">Yield ${esc(e.ratio)}</span>` : ""} ${chip(e.source)}</td>${hashCell(e.chain_ref, ["loss", "unexplained"].includes(e.kind) ? "Calculated row: hash of the source entry it comes from" : "", 2)}</tr>
      <tr class="cr ${k}"><td class="acct">${esc(e.credit.account)}</td><td class="n"></td><td class="n amt">${amt(e.credit)}</td></tr>`;
  }).join("");
}

function balanceHtml(b) {
  const row = (label, v, unit, cls = "") => `<div class="eq ${cls}"><span>${label}</span><b>${fmtA(v, 2)} ${unit}</b></div>`;
  const kgGap = b.kg_gap == null ? `<div class="eq"><span>Batch record vs deliveries</span><span class="muted">not comparable (no deliveries recorded)</span></div>`
    : Math.abs(b.kg_gap) < 0.005 ? row("Unexplained difference", 0, "kg", "gap-ok") : row("Unexplained difference", b.kg_gap, "kg", "gap-bad");
  const lGap = !b.has_litres ? `<div class="eq"><span class="muted">No litres yet (no pressing recorded)</span></div>`
    : Math.abs(b.gap_l) < 0.005 ? row("Unexplained difference", 0, "L", "gap-ok") : row("Unexplained difference", b.gap_l, "L", "gap-bad");
  return `<div class="bal">
    <div class="bal-box"><h3>Grapes (kg)</h3>
      ${b.grapes_kg != null ? row("Batch record", b.grapes_kg, "kg") : ""}${row("Delivered from vineyards", b.harvested_kg, "kg")}
      ${row("Pressed into wine", b.pressed_kg, "kg")}${row("Grapes not pressed yet", b.grapes_left_kg, "kg")}${kgGap}</div>
    <div class="bal-box"><h3>Wine (L): total in = total out + losses</h3>
      ${row("Total in (litres after pressing)", b.in_l, "L", "total")}
      ${row("Out: bottled", b.bottled_l, "L")}${row("Out: still in vessels", b.in_vessels_l, "L")}${row("Losses", b.losses_l, "L")}
      ${row("Out + losses", b.bottled_l + b.in_vessels_l + b.losses_l, "L", "total")}${lGap}
      <div class="eq small"><span>Loss engine total loss (cross-check)</span><span>${fmtA(b.engine_total_loss_l, 2)} L ${b.engine_matches ? "✓ matches" : "<b class='err'>does not match</b>"}</span></div></div>
  </div>${calcFrom("deliveries,operations")}
  ${b.checks?.length ? `<div class="tw"><table class="rt" style="margin-top:16px"><thead><tr><th>Record check</th><th>Result</th><th>Detail</th></tr></thead><tbody>
    ${b.checks.map((c) => `<tr><td>${esc(c.label)}</td><td class="${c.status === "fail" ? "lv-red" : c.status === "pass" ? "lv-green" : ""}">${esc(c.status)}</td><td class="memo">${esc(c.detail)}</td></tr>`).join("")}</tbody></table></div>${calcFrom("deliveries,operations")}` : ""}`;
}

function simHtml(s) {
  if (!s) return "";
  return `<section class="sim-box"><h2>Live alerts ${chip("SIMULATED")}</h2>
    <p><b>${esc(s.vessel)}</b> · ${esc(s.note)} ${fmt(s.readings)} simulated readings stored.</p>
    ${s.alerts.length ? `<div class="tw"><table class="rt"><thead><tr><th>Simulated time</th><th>Level</th><th>Rule</th><th>Message</th><th>Source</th></tr></thead><tbody>
      ${s.alerts.map((a) => `<tr><td class="nowrap">${dtt(a.sim_time || a.created_at)}</td><td class="lv-${esc(a.level)}">${esc(a.level)}</td><td>${esc(a.rule)}</td><td>${esc(a.message)}</td><td>${chip(a.source)}</td></tr>`).join("")}
      </tbody></table></div><p class="small muted">SIMULATED live data: not in the hash chain.</p>` : `<p class="muted">No simulated alerts for this vessel yet.</p>`}</section>`;
}

function render(d) {
  const sim = simHtml(d.simulated);
  if (!d.batch) {
    return `<div class="rp-head"><div><h1>Ledger report</h1><div class="muted">${esc(d.vessel || "")}</div></div></div>
      <div class="rp-verdict na">No recorded batch has been in ${esc(d.vessel || "this vessel")}, so there is no ledger to show.</div>${sim}
      <div class="foot">Generated ${dtt(d.generated_at)} UTC · Bacchus Ledger</div>`;
  }
  const b = d.batch, bal = d.balance;
  const meta = [["Batch", b.code], ["Winery", `${b.winery} · ${b.winery_name}`], ["Wine", b.wine_type], ["Variety · vintage", `${b.variety} · ${b.vintage}`],
    ["Technology", b.technology], ["Status", b.status], ["Source", b.source]].filter((x) => x[1]);
  return `<div class="rp-head"><div><h1>Ledger report</h1><div><b>${esc(b.name || b.code)}</b> · vineyard to bottle${d.vessel ? ` · selected vessel ${esc(d.vessel)}` : ""}</div></div>
      <div class="small muted">Generated ${dtt(d.generated_at)} UTC<br>Bacchus Ledger · read-only</div></div>
    <div class="rp-meta">${meta.map(([k, v]) => `<div><span>${esc(k)}</span>${esc(v)}</div>`).join("")}</div>
    ${verdict(bal)}

    <h2><span class="n">1</span>Timeline</h2>
    <div class="tw"><table class="rt"><thead><tr><th>Date</th><th>Step</th><th>Vessels</th><th class="n">Volume</th></tr></thead><tbody>
      ${d.timeline.map((t) => `<tr><td class="nowrap">${dt(t.date)}</td><td>${esc(stepName(t.step))}</td><td>${esc(t.vessels)}</td><td class="n">${esc(t.volume)}</td></tr>`).join("")}
    </tbody></table></div>${calcFrom("deliveries,operations")}

    <h2><span class="n">2</span>Ledger (double entry)</h2>
    <p class="small muted">Each movement has two lines with the same amount: <b>debit</b> = the place that receives, <b>credit</b> = the place that gives.
      Losses are a debit to “Losses”. Pressing turns kg of grapes into litres, so it is one <b>transformation</b> entry with the yield. Transport is not recorded separately in the data: deliveries go from the vineyard straight to winery reception.</p>
    <div class="tw"><table class="rt ledger"><thead><tr><th class="n">#</th><th>Date</th><th>Account</th><th class="n">Debit</th><th class="n">Credit</th><th>Movement</th><th>Hash</th></tr></thead>
      <tbody>${ledgerRows(d.entries)}</tbody></table></div>

    <h3 style="margin-top:24px">Account balances</h3>
    <div class="tw"><table class="rt"><thead><tr><th>Account</th><th class="n">Debit</th><th class="n">Credit</th><th class="n">Balance</th></tr></thead><tbody>
      ${d.accounts.map((a) => `<tr><td>${esc(a.name)}</td><td class="n">${fmtA(a.debit, 2)} ${esc(a.unit)}</td><td class="n">${fmtA(a.credit, 2)} ${esc(a.unit)}</td><td class="n"><b>${fmtA(a.balance, 2)} ${esc(a.unit)}</b></td></tr>`).join("")}
    </tbody></table></div>${calcFrom("deliveries,operations")}

    <h2><span class="n">3</span>Balance check</h2>
    ${balanceHtml(bal)}

    <h2><span class="n">4</span>Lab readings</h2>
    ${d.lab.length ? `<div class="tw"><table class="rt"><thead><tr><th>Date</th><th>Vessel</th><th>Stage</th><th class="n">Sugar g/L</th><th class="n">Temp °C</th><th class="n">pH</th><th class="n">TA g/L</th><th class="n">RS g/L</th><th>Note</th><th>Source</th><th>Hash</th></tr></thead><tbody>
      ${d.lab.map((r) => `<tr><td class="nowrap">${dt(r.date)}</td><td>${esc(r.vessel || "—")}</td><td>${esc(r.stage || "")}</td><td class="n">${fmtA(r.sugar_gl, 1)}</td><td class="n">${fmtA(r.temp_c, 1)}</td><td class="n">${fmtA(r.ph, 2)}</td><td class="n">${fmtA(r.ta_gl, 1)}</td><td class="n">${fmtA(r.rs_gl, 1)}</td><td class="memo">${esc(r.note || "")}</td><td>${chip(r.source)}</td>${hashCell(`lab:${r.id}`)}</tr>`).join("")}
      </tbody></table></div>` : `<p class="muted">No lab sheet rows for this batch.</p>`}
    ${d.readings.length ? `<h3 style="margin-top:16px">Readings</h3><div class="tw"><table class="rt"><thead><tr><th>Taken</th><th>Vessel</th><th>Measurement</th><th class="n">Value</th><th>Instrument · person</th><th>Note</th><th>Source</th><th>Hash</th></tr></thead><tbody>
      ${d.readings.map((r) => `<tr><td class="nowrap">${dtt(r.taken_at)}</td><td>${esc(r.vessel || "—")}</td><td>${esc(RTYPE[r.type] || r.type)}</td><td class="n">${fmtA(r.value, 4)} ${esc(r.unit || "")}</td><td>${esc([r.instrument, r.person].filter(Boolean).join(" · ") || "—")}</td><td class="memo">${esc(r.note || "")}</td><td>${chip(r.source)}</td>${hashCell(`readings:${r.id}`)}</tr>`).join("")}
      </tbody></table></div>` : ""}
    <h2><span class="n">5</span>Seal</h2>
    <div id="rpSeal" class="seal"><p class="muted">Sealing…</p></div>
    ${sim}
    <div class="foot">Ledger from the append-only operations record; losses from the loss engine. ${d.simulated ? "The SIMULATED section is live demo data and is not part of the ledger or the balance." : ""}</div>`;
}

// Seal (secure copy): final hash of the batch's hash chain, DEMO ECDSA signature, QR code to the verify page.
async function loadSeal(code) {
  const el = document.getElementById("rpSeal");
  try {
    const r = await fetch(`/api/seal?batch=${encodeURIComponent(code)}`, { method: "POST" });
    const s = await r.json();
    if (!r.ok) throw new Error(s.error || r.statusText);
    const by = Object.fromEntries(s.chain.map((x) => [`${x.tbl}:${x.row_id}`, x]));
    const hashHtml = (x) => `<code class="h">#${x.seq} ${esc(x.hash.slice(0, 12))}</code><br><code class="h prev">prev: ${esc(x.prev.slice(0, 12))}</code>`;
    const shown = new Set();
    document.querySelectorAll("td.hc[data-k]").forEach((td) => {
      if (!td.dataset.k) return;
      const x = by[td.dataset.k];
      if (x) shown.add(td.dataset.k);
      td.innerHTML = x ? hashHtml(x) : `<span class="small muted">not sealed yet</span>`;
    });
    document.querySelectorAll(".calc-from").forEach((p) => {
      const seqs = s.chain.filter((x) => p.dataset.from.split(",").includes(x.tbl)).map((x) => x.seq);
      p.textContent = seqs.length ? `Computed from chain entries #${Math.min(...seqs)}–#${Math.max(...seqs)}` : "No chain entries";
    });
    // Safety net: any chain entry not shown in a table above gets its own row here.
    const rest = s.chain.filter((x) => !shown.has(`${x.tbl}:${x.row_id}`));
    el.innerHTML = `<div class="seal-grid"><div>
        <div class="eq"><span>Final hash (SHA-256 chain of ${fmt(s.entries)} entries · = hash of entry #${fmt(s.entries)})</span></div><code class="hash">${esc(s.final_hash)}</code>
        <div class="eq"><span>Signature (ECDSA P-256 · key ${esc(s.key_id)} · <b>DEMO KEY</b>)</span></div><code class="hash">${esc(s.signature)}</code>
        <div class="eq"><span>Sealed</span><b>${dtt(s.sealed_at)} UTC</b></div>
        <div class="eq"><span>Chain</span><b>#1 (vineyard delivery) → #${fmt(s.entries)} · all ${fmt(s.entries - rest.length)} of ${fmt(s.entries)} entries shown with their hash</b></div>
        ${rest.length ? `<div class="tw"><table class="rt"><thead><tr><th>Other chain entry</th><th>Hash</th></tr></thead><tbody>${rest.map((x) => `<tr><td>${esc(x.tbl)} #${x.row_id}</td><td class="hc">${hashHtml(x)}</td></tr>`).join("")}</tbody></table></div>` : ""}
        <p class="small muted">Each entry (operation, lab row, reading) is hashed with the previous entry's hash, so changing any one entry breaks the chain from there on.</p>
</div>
      <figure class="qr"><img src="${s.qr_png}" alt="QR code to the verify page" width="168" height="168"><a class="verify-link" href="${esc(s.verify_url)}" target="_blank" rel="noopener">✓ Verify</a><figcaption class="small muted">Click or scan to verify<br>${esc(s.verify_url)}</figcaption></figure></div>`;
  } catch (e) { el.innerHTML = `<p class="err">Seal not available: ${esc(e.message)}</p>`; }
}

(async () => {
  const q = new URLSearchParams(location.search);
  const el = document.getElementById("report");
  try {
    const params = new URLSearchParams();
    if (q.get("batch")) params.set("batch", q.get("batch"));
    if (q.get("vessel")) params.set("vessel", q.get("vessel"));
    const r = await fetch(`/api/report?${params}`);
    const d = await r.json();
    if (!r.ok) throw new Error(d.error || r.statusText);
    el.innerHTML = render(d);
    document.title = `Ledger report · ${d.batch?.code || d.vessel || "Bacchus Ledger"}`;
    const p = document.getElementById("rpPrint");
    p.disabled = false;
    p.onclick = () => window.print();
    if (d.batch) loadSeal(d.batch.code);
  } catch (e) {
    el.innerHTML = `<p class="err">Could not build the report: ${esc(e.message)}</p>`;
  }
})();
