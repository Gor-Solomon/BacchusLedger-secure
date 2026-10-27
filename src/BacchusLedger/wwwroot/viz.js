// Bacchus Ledger — visualizations. Inline SVG drawn by our own code (no chart libraries).
// Every chart is drawn at its container's real width, so text stays ≥ 12 px on a phone and nothing overflows.
"use strict";

const C = { wine: "#6B1E3F", wine2: "#9C3D63", wine3: "#C77A98", wine4: "#EBD3DD", ink: "#1F1A22", muted: "#6B6770", line: "#E7E4E0",
  amber: "#C98A2B", green: "#2F7A55", red: "#C0392B", blue: "#2F6FB0", purple: "#7A4FC2", bg: "#F6F5F2" };
const FARMER_COLORS = [C.wine, C.wine2, C.amber, "#8A6A9E", "#B5655A", C.green];

// ---------------------------------------------------------------- painting registry (redraw on resize)
const VIZ = [];
function paint(el, fn) {
  if (typeof el === "string") el = $(el);
  if (!el) return;
  const draw = () => { const w = Math.max(220, Math.floor(el.clientWidth || el.parentElement?.clientWidth || 600)); el.innerHTML = fn(w); };
  VIZ.push({ el, draw });
  draw();
}
let _rz;
window.addEventListener("resize", () => { clearTimeout(_rz); _rz = setTimeout(() => VIZ.forEach((v) => document.body.contains(v.el) && v.draw()), 200); });
function clearViz() { VIZ.length = 0; }
const svg = (w, h, inner, label, cls = "") => `<svg class="viz ${cls}" width="${w}" height="${h}" viewBox="0 0 ${w} ${h}" role="img" aria-label="${esc(label)}">${inner}</svg>`;
const txt = (x, y, s, o = {}) => `<text x="${x}" y="${y}" font-size="${o.size || 12}" ${o.anchor ? `text-anchor="${o.anchor}"` : ""} fill="${o.fill || C.ink}" ${o.weight ? `font-weight="${o.weight}"` : ""} ${o.cls ? `class="${o.cls}"` : ""}>${esc(s)}</text>`;
const niceStep = (span, n = 4) => { const raw = span / n, p = Math.pow(10, Math.floor(Math.log10(raw))); const m = raw / p; return (m < 1.5 ? 1 : m < 3 ? 2 : m < 7 ? 5 : 10) * p; };

// ---------------------------------------------------------------- sparkline + KPI tile
function sparkline(values, { w = 120, h = 34, color = C.wine, bars = false } = {}) {
  const v = values.filter((x) => x != null && !Number.isNaN(x));
  if (v.length < 2) return "";
  const lo = Math.min(...v), hi = Math.max(...v), span = hi - lo || 1;
  const x = (i) => 2 + (i / (v.length - 1)) * (w - 4), y = (val) => h - 3 - ((val - lo) / span) * (h - 8);
  if (bars) {
    const bw = Math.max(3, (w - 4) / v.length - 3);
    return svg(w, h, v.map((val, i) => `<rect class="grow-y" style="animation-delay:${i * 40}ms" x="${2 + i * ((w - 4) / v.length)}" y="${y(val) - 2}" width="${bw}" height="${h - y(val) + 2}" rx="2" fill="${color}" opacity="${0.45 + 0.55 * (i / (v.length - 1))}"/>`).join(""), "trend", "spark");
  }
  const pts = v.map((val, i) => `${x(i)},${y(val)}`).join(" ");
  return svg(w, h, `<polyline class="draw" points="${pts}" fill="none" stroke="${color}" stroke-width="2" stroke-linejoin="round" stroke-linecap="round" pathLength="1"/>
    <circle cx="${x(v.length - 1)}" cy="${y(v[v.length - 1])}" r="3" fill="${color}"/>`, "trend", "spark");
}

function kpiTile({ icon, label, value, sub = "", spark = "", src = "", tone = "", id = "" }) {
  return `<div class="tile ${tone}" ${id ? `id="${id}"` : ""}>
    <div class="tile-top"><span class="tile-ic">${ico(icon)}</span><span class="tile-label">${esc(label)}</span></div>
    <div class="tile-mid"><div class="tile-value">${value}</div><span class="tile-spark">${spark}</span></div>
    <div class="tile-bottom"><span class="tile-sub">${sub}</span>${src}</div></div>`;
}

// ---------------------------------------------------------------- vessel glyph + cellar map
function vesselGlyph(v, { w = 96, h = 132, level = null, live = false, pulse = false, showLitres = true, big = false } = {}) {
  const pad = 6, top = 8, bottom = h - 8;
  const id = "g" + Math.random().toString(36).slice(2, 8);
  const lv = level != null ? level : v.capacity_l ? Math.min(1, (v.litres || 0) / v.capacity_l) : 0;
  const ring = { red: C.red, amber: C.amber, ok: C.green, green: C.green }[v.status || "ok"] || C.green;
  let shape;
  if (v.type === "qvevri" && v.outline && v.outline.length > 2) {
    const H = bottom - top, R = Math.min((w - 2 * pad) / 2, H * 0.42), cx = w / 2;
    const right = v.outline.map(([z, r]) => `${(cx + r * R).toFixed(1)},${(bottom - z * H).toFixed(1)}`);
    const left = v.outline.slice().reverse().map(([z, r]) => `${(cx - r * R).toFixed(1)},${(bottom - z * H).toFixed(1)}`);
    shape = `<polygon points="${right.concat(left).join(" ")}"/>`;
  } else {
    const ratio = v.d_cm && v.h_cm ? v.d_cm / v.h_cm : 0.72;
    const sh = bottom - top, sw = Math.min(w - 2 * pad, sh * ratio);
    shape = `<rect x="${(w - sw) / 2}" y="${top}" width="${sw}" height="${sh}" rx="12"/>`;
  }
  const fillH = bottom - top;
  const grapes = v.grapes && !(v.litres > 0);
  const inner = `<defs><clipPath id="${id}">${shape}</clipPath>
      <pattern id="${id}p" width="8" height="8" patternUnits="userSpaceOnUse"><circle cx="4" cy="4" r="2.2" fill="${C.wine2}" opacity=".55"/></pattern></defs>
    <g clip-path="url(#${id})">
      <rect x="0" y="${top}" width="${w}" height="${fillH}" fill="${C.bg}"/>
      ${grapes ? `<rect x="0" y="${top}" width="${w}" height="${fillH}" fill="url(#${id}p)"/>` :
        `<rect class="liquid ${live ? "live" : ""}" x="0" y="${top}" width="${w}" height="${fillH}" fill="${v.type === "qvevri" ? C.amber : C.wine2}" style="transform:scaleY(${lv.toFixed(4)})"/>`}
    </g>
    <g class="ring ${pulse ? "pulse" : ""}" fill="none" stroke="${ring}" stroke-width="3">${shape}</g>`;
  return `<div class="glyph ${big ? "big" : ""}">${svg(w, h, inner, `${v.cellar_label || v.code}: ${fmt(v.litres || 0)} L`)}
    <div class="glyph-label">${esc(v.cellar_label || v.code)}</div>
    ${v.cellar_label ? `<div class="glyph-code">${esc(v.code)}</div>` : showLitres ? `<div class="glyph-code">&nbsp;</div>` : ""}
    ${showLitres ? `<div class="glyph-litres">${grapes ? `${ico("grape")} grapes` : v.capacity_l == null ? esc(t("Volume unknown")) : `${fmt(v.litres || 0)} L`}</div>` : ""}</div>`;
}

/** Cellar map: every vessel as its shape with the liquid level, cellar label big and a status ring. Buttons with data-key. */
function cellarMap(vessels, { mini = false, liveMap = {} } = {}) {
  const by = {};
  for (const v of vessels) (by[v.winery] = by[v.winery] || []).push(v);
  return Object.entries(by).map(([w, list]) => `<div class="cm-group"><div class="cm-head">${esc(w)}<span>${list.length} vessels</span></div>
    <div class="cm-grid ${mini ? "mini" : ""}">${list.map((v) => {
      const lv = liveMap[v.code];
      const vv = lv ? { ...v, litres: lv.level, status: lv.status === "red" ? "red" : lv.status === "amber" ? "amber" : v.status } : v;
      return `<button type="button" class="cm-cell st-${vv.status || "ok"} ${lv ? "is-live" : ""}" data-key="${esc(v.winery)}/${esc(v.code)}" title="${esc(v.status_reason || "")}">
        ${vesselGlyph(vv, { w: mini ? 40 : 92, h: mini ? 54 : 124, live: !!lv, pulse: lv && lv.status !== "ok", showLitres: !mini })}
        ${lv ? `<span class="cm-live">${ico("activity")}</span>` : ""}</button>`;
    }).join("")}</div></div>`).join("");
}

// ---------------------------------------------------------------- batch journey flow (Sankey-style)
/** farmers (width = kg) → batch → vessels in date order (width = litres) → bottles; losses peel off as thin red streams. */
function journeyChain(d) {
  const b = d.batch, stages = d.stages || [];
  const chain = [];
  const intake = stages.find((s) => s.type === "intake");
  if (intake) chain.push({ label: intake.to ? vshort(intake.to, b.winery) : "Intake", sub: dt(intake.date), kg: b.grapes_kg, stage: "intake" });
  for (const s of stages) {
    if (["separation", "racking", "filtration", "transfer"].includes(s.type))
      chain.push({ label: vshort(s.to, b.winery), sub: dt(s.date), litres: s.after_l, loss: s.loss_l || 0, rem: s.remainder_l || 0, stage: s.stage_key, status: s.status });
    else if (s.type === "bottling")
      chain.push({ label: `${fmt(s.bottles)} bottles`, labelJury: "Bottles", sub: dt(s.date), litres: s.after_l, loss: s.loss_l || 0, rem: s.remainder_l || 0, stage: "bottling", status: s.status, fromV: vshort(s.from, b.winery) });
  }
  return chain;
}

/** Narrow screens: the same journey as a vertical funnel (rows instead of columns). */
function journeyFunnel(W, d, { upto = 99, numbers = true } = {}) {
  const b = d.batch, origins = d.origins || [];
  const chain = journeyChain(d).slice(0, Math.max(0, upto + 1));
  const kg = b.grapes_kg || origins.reduce((s, o) => s + o.kg, 0) || 1;
  const firstL = (chain.find((n) => n.litres != null) || {}).litres || 1;
  const L = 104, R = 8, tagW = 72, bw = W - L - R - tagW, rowH = 46;
  const rows = [{ label: origins.length ? `${origins.length} farmer${origins.length > 1 ? "s" : ""}` : "Grapes", farmers: true }, { label: "Batch", kg }, ...chain];
  const H = rows.length * rowH + 8;
  let g = "";
  rows.forEach((r, i) => {
    const y = 4 + i * rowH;
    const w = r.farmers || r.kg != null ? bw : Math.max(4, (r.litres / firstL) * bw * 0.86);
    if (i > 0) g += `<line x1="${L + 12}" x2="${L + 12}" y1="${y - 10}" y2="${y + 4}" stroke="${C.line}" stroke-width="2"/>`;
    if (r.farmers) {
      let x = L;
      (origins.length ? origins : [{ kg }]).forEach((o, j) => { const ww = (o.kg / kg) * bw; g += `<rect class="growx" x="${x}" y="${y + 4}" width="${Math.max(2, ww - 2)}" height="22" rx="5" fill="${FARMER_COLORS[j % FARMER_COLORS.length]}"/>`; x += ww; });
    } else g += `<rect class="growx" style="animation-delay:${i * 60}ms" x="${L}" y="${y + 4}" width="${w}" height="22" rx="6" fill="${r.stage === "bottling" ? C.green : C.wine}" opacity="${r.kg != null ? 0.75 : 1}"/>`;
    const bottles = r.stage === "bottling";
    g += txt(L - 8, y + 16, bottles ? "Bottles" : r.label, { anchor: "end", weight: 700, size: 13 });
    g += txt(L - 8, y + 31, bottles && numbers ? r.label.replace(" bottles", "") : r.sub || (r.kg != null && numbers ? `${fmt(r.kg)} kg` : ""), { anchor: "end", fill: C.muted });
    if (numbers && (r.litres != null || r.kg != null) && !r.farmers) g += txt(L + 8, y + 20, r.litres != null ? `${fmt(r.litres)} L` : `${fmt(r.kg)} kg`, { fill: "#fff", weight: 600 });
    const tags = [];
    if (r.loss > 0) tags.push([`−${fmtA(r.loss, 1)} L`, r.status === "above" ? C.red : "#B5655A"]);
    if (r.rem > 0) tags.push([`${fmtA(r.rem)} L left`, "#8A5A12"]);
    tags.forEach(([t2, col], k) => { g += txt(L + w + 8, y + 20 + k * 14, t2, { fill: col, weight: 700 }); });
  });
  return svg(W, H, g, `Batch journey ${b.code}`, "journey");
}

function journeyFlow(W, d, { upto = 99, numbers = true, height } = {}) {
  if (W < 620) return journeyFunnel(W, d, { upto, numbers });
  const b = d.batch, origins = d.origins || [], stages = d.stages || [];
  const H = height || 340, top = 44, bandMax = H - 150;
  const kgTotal = b.grapes_kg || origins.reduce((s, o) => s + o.kg, 0) || 1;
  const hk = bandMax / kgTotal;
  // vessel chain
  const chain = [];
  const intake = stages.find((s) => s.type === "intake");
  if (intake) chain.push({ key: "intake", label: intake.to ? vshort(intake.to, b.winery) : "Intake", sub: dt(intake.date), kg: b.grapes_kg, src: intake.source, icon: "grape" });
  let firstL = null;
  for (const s of stages) {
    if (["separation", "racking", "filtration", "transfer"].includes(s.type)) {
      if (firstL == null) firstL = s.after_l;
      chain.push({ key: s.op_id, label: vshort(s.to, b.winery), sub: dt(s.date), litres: s.after_l, loss: s.loss_l || 0, rem: s.remainder_l || 0, stage: s.stage_key, status: s.status, src: s.source, icon: OP_ICON[s.stage_key] });
    } else if (s.type === "stock_check" && chain.length) {
      const last = chain[chain.length - 1];
      last.check = { loss: s.loss_l || 0, date: s.date, status: s.status };
      if (s.loss_l) last.litresAfterCheck = s.after_l;
    } else if (s.type === "bottling") {
      chain.push({ key: s.op_id, label: `${fmt(s.bottles)} bottles`, labelJury: "Bottles", sub: dt(s.date), litres: s.after_l, loss: s.loss_l || 0, rem: s.remainder_l || 0, stage: "bottling", status: s.status, src: s.source, icon: "wine", bottles: s.bottles, fromV: vshort(s.from, b.winery) });
    }
  }
  const hl = firstL ? (bandMax * 0.8) / firstL : 0;
  const cols = 2 + chain.length;
  const left = W < 560 ? 70 : 120, right = W < 560 ? 60 : 90;
  const colX = (i) => left + (i * (W - left - right)) / Math.max(1, cols - 1);
  const nodeW = 12;
  let g = `<defs><linearGradient id="jf" x1="0" x2="1"><stop offset="0" stop-color="${C.wine2}" stop-opacity=".55"/><stop offset="1" stop-color="${C.wine}" stop-opacity=".55"/></linearGradient></defs>`;
  const band = (x0, y0, h0, x1, y1, h1, fill, cls = "", delay = 0) => {
    const mx = (x0 + x1) / 2;
    return `<path class="flow ${cls}" style="animation-delay:${delay}ms" d="M${x0},${y0} C${mx},${y0} ${mx},${y1} ${x1},${y1} L${x1},${y1 + h1} C${mx},${y1 + h1} ${mx},${y0 + h0} ${x0},${y0 + h0} Z" fill="${fill}"/>`;
  };
  const show = (i) => i <= upto + 1; // column index visible (0 farmers, 1 batch, 2.. chain)
  // farmers
  const fx = colX(0);
  let fy = top;
  const gap = origins.length > 1 ? 6 : 0;
  const fTotalH = kgTotal * hk + gap * (origins.length - 1);
  const bx = colX(1), by = top;
  let byCursor = by;
  (origins.length ? origins : [{ farmer: "Grapes", kg: kgTotal, source: "Dataset A2" }]).forEach((o, i) => {
    const h = o.kg * hk, col = FARMER_COLORS[i % FARMER_COLORS.length];
    g += `<rect class="node" x="${fx}" y="${fy}" width="${nodeW}" height="${h}" rx="3" fill="${col}"/>`;
    if (show(1)) g += band(fx + nodeW, fy, h, bx, byCursor, h, col, "f", 100 + i * 60).replace('fill="' + col + '"', `fill="${col}" fill-opacity=".35"`);
    const name = (o.farmer || "").replace("Demo Farmer", "Farmer");
    g += txt(fx - 8, fy + h / 2 + 4, name, { anchor: "end", size: 12, fill: C.ink });
    if (numbers && h > 26) g += txt(fx - 8, fy + h / 2 + 18, `${fmt(o.kg)} kg`, { anchor: "end", size: 12, fill: C.muted });
    fy += h + gap; byCursor += h;
  });
  g += txt(fx + nodeW / 2, top - 22, origins.length ? `${origins.length} farmer${origins.length > 1 ? "s" : ""}` : "Grapes", { anchor: "middle", size: 12, weight: 600, fill: C.muted });
  // batch node
  const bH = kgTotal * hk;
  if (show(1)) {
    g += `<rect class="node" x="${bx}" y="${by}" width="${nodeW}" height="${bH}" rx="3" fill="${C.wine}"/>`;
    g += txt(bx + nodeW / 2, top - 22, "Batch", { anchor: "middle", size: 12, weight: 600, fill: C.muted });
    if (numbers) g += txt(bx + nodeW / 2, top - 8, `${fmt(kgTotal)} kg`, { anchor: "middle", size: 12, fill: C.ink });
  }
  // chain
  let prev = { x: bx, y: by, h: bH };
  chain.forEach((n, i) => {
    const ci = i + 2;
    if (!show(ci)) return;
    const x = colX(ci);
    const isKg = n.kg != null;
    const h = isKg ? n.kg * hk : Math.max(2, n.litres * hl);
    const delay = 200 + i * 140;
    // main stream from previous node (top-aligned) + peel-offs from its bottom part
    g += band(prev.x + nodeW, prev.y, isKg ? prev.h : Math.min(prev.h, h), x, top, h, "url(#jf)", "", delay);
    if (!isKg && i > 0 && chain[i - 1].litres != null) {
      const lossH = Math.max(2, n.loss * hl), remH = n.rem > 0 ? Math.max(2, n.rem * hl) : 0;
      let y0 = prev.y + h;
      const peel = (hh, color, label, k) => {
        const x0 = prev.x + nodeW, x1 = (prev.x + x) / 2 + 10, y1 = H - 84 + k * 24;
        const p = `<path class="flow peel" style="animation-delay:${delay + 80}ms" d="M${x0},${y0} C${x0 + 30},${y0} ${x1 - 20},${y1} ${x1},${y1} L${x1},${y1 + hh} C${x1 - 20},${y1 + hh} ${x0 + 30},${y0 + hh} ${x0},${y0 + hh} Z" fill="${color}" fill-opacity=".8"/>`;
        y0 += hh;
        return p + txt(x1 + 4, y1 + Math.max(hh, 2) / 2 + 4, label, { size: 12, fill: color, weight: 600 });
      };
      const row = i % 2; // alternate label rows so neighbouring loss labels never collide
      if (n.loss > 0) g += peel(lossH, n.status === "above" ? C.red : "#D9776B", `−${fmtA(n.loss, 1)} L ${plain(n.stage).toLowerCase()}`, row);
      if (remH) g += peel(remH, C.amber, `${fmtA(n.rem)} L left in ${n.fromV || ""}`.trim(), row + 1);
    }
    g += `<rect class="node" x="${x}" y="${top}" width="${nodeW}" height="${h}" rx="3" fill="${n.stage === "bottling" ? C.green : C.wine}"/>`;
    const lab = n.stage === "bottling" && !numbers ? n.labelJury : n.label;
    g += txt(x + nodeW / 2, top - 22, lab, { anchor: "middle", size: 13, weight: 700 });
    g += txt(x + nodeW / 2, top - 8, numbers ? (isKg ? `${fmt(n.kg)} kg` : `${fmt(n.litres)} L`) : n.sub, { anchor: "middle", size: 12, fill: numbers ? C.ink : C.muted });
    if (n.check) g += `<g transform="translate(${x + nodeW + 4},${top + h + 6})"><circle r="9" cx="9" cy="9" fill="${n.check.status === "ok" ? C.green : C.amber}"><title>Stock check ${n.check.date}: ${fmtA(n.check.loss, 1)} L difference</title></circle><text x="9" y="13" font-size="12" text-anchor="middle" fill="#fff" font-weight="700">✓</text></g>`;
    prev = { x, y: top, h };
  });
  if (!chain.length) g += txt(W / 2, H / 2, "No cellar operations in the dataset for this batch — only the grape intake (Dataset A2).", { anchor: "middle", size: 13, fill: C.muted });
  return svg(W, H, g, `Batch journey ${b.code}`, "journey");
}

// ---------------------------------------------------------------- litre waterfall (restyled)
function waterfallSvg(W, stages) {
  const pts = [];
  for (const s of stages) {
    if (s.type === "separation") pts.push({ label: plain("separation"), v: s.after_l, loss: 0, rem: 0, status: "n/a" });
    else if (["racking", "filtration", "transfer", "stock_check"].includes(s.type)) pts.push({ label: plain(s.stage_key), v: s.after_l, loss: s.loss_l || 0, rem: s.remainder_l || 0, status: s.status });
    else if (s.type === "bottling") pts.push({ label: "Bottled", v: s.after_l, loss: s.loss_l || 0, rem: s.remainder_l || 0, status: s.status });
  }
  if (!pts.length) return `<p class="muted">No litres recorded yet.</p>`;
  const H = 280, L = 56, R = 12, T = 36, B = 44, iw = W - L - R, ih = H - T - B;
  const tops = pts.map((p) => p.v + p.rem + Math.max(0, p.loss));
  const max = Math.max(...tops), min = Math.min(...pts.map((p) => p.v)), span = Math.max(max - min, 50);
  const step = niceStep(span * 1.6);
  const lo = Math.max(0, Math.floor((min - span * 0.6) / step) * step), hi = Math.ceil((max + span * 0.12) / step) * step;
  const y = (v) => T + ih - ((v - lo) / (hi - lo)) * ih;
  const slot = iw / pts.length, bw = Math.min(56, slot * 0.56);
  let g = "";
  for (let v = lo; v <= hi + 1e-9; v += step) g += `<line x1="${L}" x2="${W - R}" y1="${y(v)}" y2="${y(v)}" stroke="${C.line}"/>` + txt(L - 8, y(v) + 4, fmt(v), { anchor: "end", fill: C.muted });
  g += `<g transform="translate(${L},12)">${txt(0, 4, `Axis starts at ${fmt(lo)} L, not 0 — so small losses are visible`, { fill: "#8A5A12", size: 12, weight: 600 })}</g>`;
  pts.forEach((p, i) => {
    const cx = L + (i + 0.5) * slot, x0 = cx - bw / 2;
    g += `<rect class="grow-y" style="animation-delay:${i * 70}ms" x="${x0}" y="${y(p.v)}" width="${bw}" height="${y(lo) - y(p.v)}" rx="8" fill="${C.wine}"/>`;
    if (p.rem > 0) g += `<rect x="${x0}" y="${y(p.v + p.rem)}" width="${bw}" height="${y(p.v) - y(p.v + p.rem)}" fill="${C.amber}"/>`;
    if (p.loss > 0) {
      const t2 = p.v + p.rem + p.loss;
      g += `<rect class="fade" style="animation-delay:${300 + i * 70}ms" x="${x0}" y="${y(t2)}" width="${bw}" height="${Math.max(2, y(p.v + p.rem) - y(t2))}" rx="4" fill="${p.status === "above" ? C.red : "#E39A90"}"/>`;
      g += txt(cx, y(t2) - 20, `−${fmtA(p.loss, 1)} L`, { anchor: "middle", fill: p.status === "above" ? C.red : "#B5655A", weight: 700 });
    }
    g += txt(cx, y(p.v + p.rem + Math.max(0, p.loss)) - 6, fmtA(p.v, 1), { anchor: "middle", weight: 600 });
    const label = slot < 80 ? p.label.split(" ")[0] : p.label;
    g += txt(cx, H - B + 18, label, { anchor: "middle", fill: C.muted });
    if (p.rem > 0) g += txt(cx, H - B + 34, `+${fmtA(p.rem)} L left`, { anchor: "middle", fill: "#8A5A12" });
  });
  return svg(W, H, g, "Litre waterfall");
}

// ---------------------------------------------------------------- loss per step W01 vs W03 (grouped bars + dashed norm)
function groupedBars(W, a, b, labels = ["W01", "W03"]) {
  const keys = ["racking_first", "stock_check", "racking", "filtration", "bottling"];
  const get = (st, k) => st.find((s) => s.stage_key === k && s.loss_pct != null);
  const rows = keys.map((k) => ({ k, a: get(a, k), b: get(b, k) })).filter((r) => r.a || r.b);
  const H = 270, L = 44, R = 12, T = 30, B = 50, iw = W - L - R, ih = H - T - B;
  const max = Math.max(1, ...rows.flatMap((r) => [r.a?.loss_pct || 0, r.b?.loss_pct || 0, r.a?.norm_pct || 0, r.b?.norm_pct || 0])) * 1.15;
  const y = (v) => T + ih - (v / max) * ih;
  const slot = iw / rows.length, bw = Math.min(30, slot * 0.28);
  let g = "";
  const st = niceStep(max);
  for (let v = 0; v <= max; v += st) g += `<line x1="${L}" x2="${W - R}" y1="${y(v)}" y2="${y(v)}" stroke="${C.line}"/>` + txt(L - 6, y(v) + 4, `${fmtA(v, 1)}%`, { anchor: "end", fill: C.muted });
  rows.forEach((r, i) => {
    const cx = L + (i + 0.5) * slot;
    [[r.a, C.wine, -1], [r.b, C.amber, 1]].forEach(([s, col, side], j) => {
      if (!s) return;
      const x0 = cx + (side < 0 ? -bw - 2 : 2);
      const over = s.status === "above";
      g += `<rect class="grow-y" style="animation-delay:${i * 80 + j * 40}ms" x="${x0}" y="${y(s.loss_pct)}" width="${bw}" height="${Math.max(1, y(0) - y(s.loss_pct))}" rx="6" fill="${col}" ${over ? `stroke="${C.red}" stroke-width="2.5"` : ""}/>`;
      if (s.loss_pct > 0) {
        // the label sits above the bar — and above the dashed normal line when the two are close
        const norm = s.norm_pct ?? 0, ly = Math.min(y(s.loss_pct), Math.abs(y(s.loss_pct) - y(norm)) < 16 ? y(norm) : 1e9) - 6;
        g += txt(x0 + bw / 2 + (side < 0 ? 4 : -4), ly, `${fmt(s.loss_pct, 2)}%`, { anchor: side < 0 ? "end" : "start", size: 12, fill: over ? C.red : C.ink, weight: over ? 700 : 500 });
      }
    });
    if (!(r.a?.loss_pct > 0) && !(r.b?.loss_pct > 0)) g += txt(cx, H - B + 34, "0 L lost", { anchor: "middle", fill: C.green, weight: 700 });
    const norm = (r.a || r.b).norm_pct;
    if (norm != null) g += `<line x1="${cx - slot * 0.42}" x2="${cx + slot * 0.42}" y1="${y(norm)}" y2="${y(norm)}" stroke="${C.ink}" stroke-width="1.5" stroke-dasharray="5 4"/>`;
    g += txt(cx, H - B + 18, plain(r.k), { anchor: "middle", fill: C.muted });
  });
  return svg(W, H, g, "Loss per step W01 vs W03") +
    `<div class="legend"><span><i style="background:${C.wine}"></i>${esc(labels[0])}</span><span><i style="background:${C.amber}"></i>${esc(labels[1])}</span><span><i class="dash"></i>normal ${chip("DEMO ASSUMPTION")}</span><span><i style="background:#fff;border:2px solid ${C.red}"></i>${esc(t("Above normal"))}</span></div>`;
}

// ---------------------------------------------------------------- radial gauge
function gauge(pct, { label = "", sub = "", size = 200, color = C.wine } = {}) {
  const r = size * 0.38, cx = size / 2, cy = size / 2 + size * 0.06, a0 = Math.PI * 0.75, a1 = Math.PI * 2.25;
  const arc = (from, to) => { const x0 = cx + r * Math.cos(from), y0 = cy + r * Math.sin(from), x1 = cx + r * Math.cos(to), y1 = cy + r * Math.sin(to); return `M${x0},${y0} A${r},${r} 0 ${to - from > Math.PI ? 1 : 0} 1 ${x1},${y1}`; };
  const k = pct == null ? 0 : Math.max(0, Math.min(1, pct / 100));
  const g = `<path d="${arc(a0, a1)}" fill="none" stroke="${C.line}" stroke-width="${size * 0.08}" stroke-linecap="round"/>
    ${pct == null ? "" : `<path class="gauge-fill" d="${arc(a0, a1)}" fill="none" stroke="${color}" stroke-width="${size * 0.08}" stroke-linecap="round" pathLength="100" style="stroke-dasharray:${(k * 100).toFixed(2)} 100"/>`}
    <text x="${cx}" y="${cy + 2}" text-anchor="middle" font-size="${size * 0.19}" font-weight="700" fill="${C.ink}">${pct == null ? "—" : fmt(pct, pct >= 99.95 || pct < 10 ? 1 : 0) + "%"}</text>
    <text x="${cx}" y="${cy + size * 0.13}" text-anchor="middle" font-size="12" fill="${C.muted}">${esc(sub)}</text>`;
  return `<div class="gauge">${svg(size, size * 0.84, g, label)}<div class="gauge-label">${esc(label)}</div></div>`;
}

// ---------------------------------------------------------------- sugar-drop curve with the current-day marker
function sugarCurve(W, days, sugar, { marker = sugar.length - 1, temp = null } = {}) {
  const H = 220, L = 44, R = temp ? 40 : 14, T = 18, B = 34, iw = W - L - R, ih = H - T - B;
  const x = (i) => L + (i / (days.length - 1)) * iw, y = (v) => T + ih - (v / 250) * ih, yt = (v) => T + ih - ((v - 15) / 15) * ih;
  let g = "";
  for (let v = 0; v <= 250; v += 50) g += `<line x1="${L}" x2="${W - R}" y1="${y(v)}" y2="${y(v)}" stroke="${C.line}"/>` + txt(L - 6, y(v) + 4, String(v), { anchor: "end", fill: C.muted });
  if (temp) for (let v = 15; v <= 30; v += 5) g += txt(W - R + 6, yt(v) + 4, `${v}°`, { fill: C.amber });
  const every = days.length > 10 && iw < 420 ? 2 : 1;
  days.forEach((d, i) => { if (i % every === 0 || i === days.length - 1) g += txt(x(i), H - B + 16, String(d), { anchor: "middle", fill: C.muted }); });
  g += txt(L, H - 4, "day", { fill: C.muted });
  const area = `${x(0)},${y(0)} ` + sugar.map((v, i) => `${x(i)},${y(v)}`).join(" ") + ` ${x(sugar.length - 1)},${y(0)}`;
  g += `<polygon class="fade" points="${area}" fill="${C.wine}" fill-opacity=".08"/>`;
  g += `<polyline class="draw" pathLength="1" points="${sugar.map((v, i) => `${x(i)},${y(v)}`).join(" ")}" fill="none" stroke="${C.wine}" stroke-width="3" stroke-linejoin="round"/>`;
  if (temp) g += `<polyline class="draw" pathLength="1" points="${temp.map((v, i) => `${x(i)},${yt(v)}`).join(" ")}" fill="none" stroke="${C.amber}" stroke-width="2" stroke-dasharray="5 4"/>`;
  const m = Math.max(0, Math.min(marker, sugar.length - 1));
  g += `<line x1="${x(m)}" x2="${x(m)}" y1="${T}" y2="${T + ih}" stroke="${C.ink}" stroke-dasharray="3 3"/>`;
  g += `<circle cx="${x(m)}" cy="${y(sugar[m])}" r="6" fill="#fff" stroke="${C.wine}" stroke-width="3"/>`;
  const near = x(m) > W - R - 130;
  g += txt(near ? x(m) - 10 : x(m) + 10, y(sugar[m]) - 12, `day ${days[m]}: ${fmtA(sugar[m], 1)} g/L`, { weight: 700, anchor: near ? "end" : "start" });
  return svg(W, H, g, "Sugar drop during fermentation");
}

// ---------------------------------------------------------------- donut
function donut(items, { size = 180, center = "", sub = "" } = {}) {
  const total = items.reduce((s, i) => s + i.value, 0) || 1;
  const r = size * 0.36, cx = size / 2, cy = size / 2, sw = size * 0.16, circ = 2 * Math.PI * r;
  let off = 0, g = "";
  items.forEach((it, i) => {
    const len = (it.value / total) * circ;
    g += `<circle class="donut-seg" style="animation-delay:${i * 90}ms" cx="${cx}" cy="${cy}" r="${r}" fill="none" stroke="${it.color || FARMER_COLORS[i % FARMER_COLORS.length]}" stroke-width="${sw}"
      stroke-dasharray="${Math.max(0, len - 2)} ${circ}" stroke-dashoffset="${-off}" transform="rotate(-90 ${cx} ${cy})"><title>${esc(it.label)}: ${fmtA(it.value / total * 100, 2)}%</title></circle>`;
    off += len;
  });
  g += `<text x="${cx}" y="${cy + 2}" text-anchor="middle" font-size="${size * 0.13}" font-weight="700" fill="${C.ink}">${esc(center)}</text>
    <text x="${cx}" y="${cy + size * 0.12}" text-anchor="middle" font-size="12" fill="${C.muted}">${esc(sub)}</text>`;
  return `<div class="donut">${svg(size, size, g, "Origin")}<ul class="donut-legend">${items.map((it, i) =>
    `<li><i style="background:${it.color || FARMER_COLORS[i % FARMER_COLORS.length]}"></i><span>${esc(it.label)}</span><b>${fmtA(it.value / total * 100, 1)}%</b></li>`).join("")}</ul></div>`;
}

// ---------------------------------------------------------------- horizontal bars ("What 1% of loss costs per year")
function hbars(W, rows, { fmtv = (v) => "$" + fmt(v), color = C.wine } = {}) {
  const lab = W < 420 ? 44 : 64, valW = W < 420 ? 84 : 110, rowH = 34, H = rows.length * rowH + 8;
  const max = Math.max(...rows.map((r) => r.value)) || 1, bwMax = W - lab - valW - 8;
  let g = "";
  rows.forEach((r, i) => {
    const y = 4 + i * rowH, bw = Math.max(3, (r.value / max) * bwMax);
    g += txt(lab - 8, y + 20, r.label, { anchor: "end", weight: 600 });
    g += `<rect class="growx" style="animation-delay:${i * 70}ms" x="${lab}" y="${y + 6}" width="${bw}" height="20" rx="6" fill="${color}" opacity="${0.5 + 0.5 * (r.value / max)}"/>`;
    g += txt(lab + bw + 8, y + 21, (r.prefix || "") + fmtv(r.value), { weight: 700 });
  });
  return svg(W, H, g, "Bars");
}

// ---------------------------------------------------------------- timeline strip
function timeline(W, events) {
  if (!events.length) return "";
  const ts = events.map((e) => new Date(e.date).getTime());
  const t0 = Math.min(...ts), t1 = Math.max(...ts), span = t1 - t0 || 1;
  const H = 128, L = 24, R = 24, y0 = 62;
  const x = (t) => L + ((t - t0) / span) * (W - L - R);
  let g = `<line x1="${L}" x2="${W - R}" y1="${y0}" y2="${y0}" stroke="${C.line}" stroke-width="4" stroke-linecap="round"/>
    <line class="drawx" x1="${L}" x2="${W - R}" y1="${y0}" y2="${y0}" stroke="${C.wine}" stroke-width="4" stroke-linecap="round" pathLength="1"/>`;
  // months
  const m = new Date(t0); m.setDate(1); m.setMonth(m.getMonth() + 1);
  for (; m.getTime() <= t1; m.setMonth(m.getMonth() + 1)) g += txt(x(m.getTime()), H - 6, m.toLocaleString("en-GB", { month: "short" }), { anchor: "middle", fill: C.muted });
  // events closer than 34 px share one marker; its label lists them
  const clusters = [];
  for (const e of events) {
    const xx = x(new Date(e.date).getTime()), last = clusters[clusters.length - 1];
    if (last && xx - last.x < 34) last.items.push(e); else clusters.push({ x: xx, items: [e] });
  }
  let lastX = -999, row = 1;
  clusters.forEach((c, i) => {
    const e = c.items[0], xx = c.x;
    row = xx - lastX < 96 ? 1 - row : 1; lastX = xx;
    const lx = Math.max(L + 30, Math.min(W - R - 30, xx)), anchor = xx < L + 40 ? "start" : xx > W - R - 40 ? "end" : "middle";
    const ax = anchor === "start" ? xx - 12 : anchor === "end" ? xx + 12 : lx;
    const label = c.items.length > 1 ? `${c.items[0].label} +${c.items.length - 1}` : e.label;
    const ly = row ? y0 - 34 : y0 + 30;
    const col = c.items.some((it) => it.color === C.red) ? C.red : e.color || C.wine;
    g += `<g class="pop" style="animation-delay:${i * 80}ms"><circle cx="${xx}" cy="${y0}" r="13" fill="#fff" stroke="${col}" stroke-width="2.5"><title>${esc(c.items.map((it) => `${it.label} ${dt(it.date)}`).join(" · "))}</title></circle>
      <image href="icons/${e.icon}.svg" x="${xx - 8}" y="${y0 - 8}" width="16" height="16"/>
      ${W >= 520 || i === 0 || i === clusters.length - 1 ? txt(ax, ly, label, { anchor, size: 12, weight: 600 }) + txt(ax, ly + 14, dt(e.date).slice(0, 5) + (c.items.length > 1 ? `–${dt(c.items[c.items.length - 1].date).slice(0, 5)}` : ""), { anchor, size: 12, fill: C.muted }) : ""}</g>`;
  });
  return svg(W, H, g, "Batch timeline");
}

// ---------------------------------------------------------------- journal coverage tiles
const JT_SHORT = ["", "Vineyard", "Receiving", "Process", "Vintage", "Storage", "Bottling", "Lab", "Tasting"];
function journalTiles(filled, { template8 = true } = {}) {
  return `<div class="jtiles">${[1, 2, 3, 4, 5, 6, 7, 8].map((n) => {
    const on = filled.has(n);
    return `<div class="jtile ${on ? "on" : ""}" title="Journal ${n}: ${esc(JTITLES[n])}${n === 8 && template8 ? " (template — no tasting data in the dataset)" : ""}">
      <span class="jn">${n}</span><span class="jt">${esc(JT_SHORT[n])}</span>${on ? ico("check-circle") : ""}</div>`;
  }).join("")}</div>`;
}

// ---------------------------------------------------------------- live mini chart
function liveChart(W, pts, { unit = "", color = C.wine, threshold = null, d = 1, height = 150 } = {}) {
  const H = height, L = 52, R = 10, T = 12, B = 22, iw = W - L - R, ih = H - T - B;
  if (pts.length < 2) return svg(W, H, txt(W / 2, H / 2, "Waiting for live data…", { anchor: "middle", fill: C.muted }), "live chart");
  const vals = pts.map((p) => p.v).concat(threshold != null ? [threshold] : []);
  let lo = Math.min(...vals), hi = Math.max(...vals);
  const pad = (hi - lo || Math.abs(hi) * 0.01 || 1) * 0.15; lo -= pad; hi += pad;
  const x = (i) => L + (i / (pts.length - 1)) * iw, y = (v) => T + ih - ((v - lo) / (hi - lo)) * ih;
  let g = "";
  const dd = Math.min(4, Math.max(d, Math.ceil(-Math.log10((hi - lo) / 3))));
  for (let k = 0; k <= 3; k++) { const v = lo + ((hi - lo) * k) / 3; g += `<line x1="${L}" x2="${W - R}" y1="${y(v)}" y2="${y(v)}" stroke="${C.line}"/>` + txt(L - 6, y(v) + 4, fmt(v, dd), { anchor: "end", fill: C.muted }); }
  if (threshold != null) g += `<line x1="${L}" x2="${W - R}" y1="${y(threshold)}" y2="${y(threshold)}" stroke="${C.red}" stroke-dasharray="5 4"/>` + txt(W - R - 4, y(threshold) - 5, `limit ${fmt(threshold, d)} ${unit}`, { anchor: "end", fill: C.red });
  const area = `${x(0)},${T + ih} ` + pts.map((p, i) => `${x(i)},${y(p.v)}`).join(" ") + ` ${x(pts.length - 1)},${T + ih}`;
  g += `<polygon points="${area}" fill="${color}" opacity=".08"/><polyline points="${pts.map((p, i) => `${x(i)},${y(p.v)}`).join(" ")}" fill="none" stroke="${color}" stroke-width="2.5" stroke-linejoin="round"/>`;
  const last = pts[pts.length - 1];
  g += `<circle cx="${x(pts.length - 1)}" cy="${y(last.v)}" r="5" fill="${color}"/>`;
  g += txt(L + 4, T + 12, `${fmt(last.v, d)} ${unit}`, { size: 14, weight: 700 });
  return svg(W, H, g, "live chart");
}

// ---------------------------------------------------------------- legacy charts kept for the vessel page and the readings view
function fermentationSvg(days, sugar, temp, upto) {
  const W = 760;
  return svgEl(W, 240, sugarCurve(W, days, sugar.slice(0, upto ?? sugar.length).concat(), { marker: (upto ?? sugar.length) - 1, temp }).replace(/^<svg[^>]*>|<\/svg>$/g, ""), "Fermentation");
}

/** Vessel cross-section from its inner profile [[z, r], ...]; optional fill height (cm). */
function vesselSvg(profile, Hcm, fillCm, label) {
  const maxR = Math.max(...profile.map((p) => p[1]));
  const W = 260, H = 300, pad = 16;
  const sc = Math.min((H - 2 * pad) / Hcm, (W / 2 - pad) / maxR);
  const cx = W / 2, yb = H - pad;
  const Y = (z) => yb - z * sc;
  const right = profile.map((p) => `${cx + p[1] * sc},${Y(p[0])}`);
  const left = profile.slice().reverse().map((p) => `${cx - p[1] * sc},${Y(p[0])}`);
  let g = "";
  if (fillCm != null && fillCm > 0) {
    const f = Math.min(fillCm, Hcm);
    const rAt = (z) => { for (let i = 1; i < profile.length; i++) if (profile[i][0] >= z) { const [z0, r0] = profile[i - 1], [z1, r1] = profile[i]; return r0 + (r1 - r0) * ((z - z0) / (z1 - z0 || 1)); } return profile[profile.length - 1][1]; };
    const sub = profile.filter((p) => p[0] < f).concat([[f, rAt(f)]]);
    const fr = sub.map((p) => `${cx + p[1] * sc},${Y(p[0])}`), fl = sub.slice().reverse().map((p) => `${cx - p[1] * sc},${Y(p[0])}`);
    g += `<polygon points="${fr.concat(fl).join(" ")}" fill="${C.amber}" opacity=".55"/>`;
    g += `<line x1="${cx - rAt(f) * sc - 10}" x2="${cx + rAt(f) * sc + 10}" y1="${Y(f)}" y2="${Y(f)}" stroke="#855a14" stroke-width="2"/>`;
    g += `<text x="${W - 6}" y="${Y(f) - 6}" font-size="12" text-anchor="end" fill="${C.ink}" font-weight="700">dip ${fmtA(Hcm - f, 1)} cm</text>`;
  }
  g += `<polygon points="${right.concat(left).join(" ")}" fill="none" stroke="${C.wine}" stroke-width="3"/>`;
  return svgEl(W, H, g, label || "Vessel shape");
}
