# Bacchus Ledger — Update 3: modern design, Jury mode, visuals, live simulation + notifications

Extend the existing app in this folder. Keep all features and data rules from BACCHUS_LEDGER_CLI_PROMPT.md and BACCHUS_LEDGER_UPDATE_2.md
(source chips, no invented dataset numbers, offline, Georgian/English, append-only). `/api/selftest` must keep passing all 50 checks + the new ones.

**Feedback from the team:** "The design looks like 1999. It is pure data, not digestible for a jury. We need graphs, a simple mode, live data and notifications."

---

## 1. New design system (replace the current CSS completely)

Goal: clean, modern, symmetric, calm — like a good 2026 SaaS dashboard, with a wine identity.

**Tokens (CSS variables):**
- Colors: background `#F6F5F2`, surface `#FFFFFF`, ink `#1F1A22`, muted `#6B6770`, line `#E7E4E0`,
  wine `#6B1E3F` (primary), wine-dark `#2B1420` (sidebar), amber `#C98A2B` (qvevri accent / demo chips),
  green `#2F7A55` (ok), red `#C0392B` (alerts only), blue `#2F6FB0` (calculated chips).
  Dark-mode not needed.
- Font: vendor **Inter** (OFL license) as woff2 files in `wwwroot/fonts/` (download once at build time is fine; nothing loads from the internet at runtime). Fallback: system-ui. Headings 600, numbers use `font-variant-numeric: tabular-nums`.
- Type scale: 12 / 14 / 16 / 20 / 28 / 40 / 56 px. Body 15–16 px.
- Spacing scale: 4 / 8 / 12 / 16 / 24 / 32 / 48 px only.
- Radius: cards 16 px, buttons/inputs 10 px, chips 999 px. One soft shadow token.
- Icons: vendor a small set of **Lucide** SVG icons (ISC license) into `wwwroot/icons/` (grape, wine, barrel, thermometer, droplet, flask, bell, alert-triangle, check-circle, activity, map, file-text, user, play, pause, zap). No icon fonts, no CDN.

**Layout rules:**
- Left sidebar (240 px, wine-dark) with icon + label nav; top bar with: page title, **Jury/Expert switch**, **Live switch**, **notification bell with counter**, language switch.
- Content max-width 1280 px, 12-column CSS grid, 24 px gutters. Cards in a row always have **equal heights** and align to the grid. No ragged layouts.
- KPI tiles: big number (40 px), small label, tiny sparkline, source chip — always 4 per row on desktop, 2 on tablet, 1 on phone.
- Responsive: 1440×900 laptop, 1280×720 projector, 390×844 phone. No horizontal scroll.
- Motion: only subtle (150–250 ms) for toasts, gauge fill, tank level. Respect `prefers-reduced-motion`.

---

## 2. Jury mode vs Expert mode (switch in the top bar, remembered in localStorage)

- **Jury mode (default):** big visuals, short sentences, max 4 numbers per screen, plain words ("Wine lost", not "racking_first loss"). Tables hidden behind "Show details".
- **Expert mode:** everything that exists today (tables, journals, all fields, source chips visible).
- Source chips are always available: in Jury mode they appear on hover/tap as a small "i".

---

## 3. Visualizations (inline SVG, our own code, no chart libraries)

Add these, all animated on first load:

1. **Cellar map** (new page "Cellar", also on Overview): every vessel drawn as its shape — tank = rounded rectangle, qvevri = egg shape with neck (use the A5 profile for the outline) — with the **liquid level filled** (litres / capacity), the **cellar label big** (F.1, Q.3…), and a colored status ring: green ok / amber attention / red alert. Click a vessel → side panel with its batch, last readings, mini charts.
2. **Batch journey flow** (Sankey-style, SVG): farmers (width = kg, Dataset A2 + DEMO split) → batch → vessels in date order (width = litres) → bottles. Losses peel off as thin red streams with labels ("−280 L racking"). This is the hero visual of the batch page.
3. **Litre waterfall**: keep, restyle (rounded bars, red loss segments, labels).
4. **Loss per step W01 vs W03**: grouped bars with the "normal" line drawn as a dashed threshold.
5. **Fermentation gauge**: radial gauge "X% of potential alcohol" (from Update 2), plus a sugar-drop curve (A3) with the current day marker.
6. **Origin donut**: farmer shares of the batch (DEMO split, labelled).
7. **Money tiles**: "Wine lost: 450 L ≈ $1,345.50" and "What 1% of loss costs per year" as a horizontal bar chart (W04 … W01) — existing calculated numbers only.
8. **Timeline strip**: the batch's life from 18.09.2025 to 17.04.2026 as a horizontal timeline with icons per event.
9. **Journals coverage**: 8 small tiles (one per Kakheti journal) that turn green as the demo fills them.

---

## 4. LIVE mode: simulated real-time data + notifications

Purpose: show the jury what the winemaker sees when things happen. **All live data is SIMULATED and must be labelled so** (purple chip "SIMULATED"). It is stored in its own table `live_events` and **never mixes** with dataset data, journals or ROI numbers.

**Engine:** server-side simulator (background service) + **Server-Sent Events** endpoint `GET /api/live/stream`. When Live is ON, every 3 s it emits readings for 2–3 active demo vessels:
- temperature (°C), level (litres from a simulated dip), and — for fermenting vessels — density (SG) trending down.
- Base values start from the vessel's last real/entered value; noise ±0.3 °C, ±2 L.

**Scenario buttons** (Live panel, for the presenter):
1. **"Hot fermentation"** — F.2 temperature climbs 27 → 32 °C over ~30 s.
2. **"Unrecorded wine loss"** — Q.3 level drops 25 L with no operation recorded.
3. **"Stuck fermentation"** — F.2 density stays flat for the simulated 48 h while sugar is still high.
4. **"Missing data"** — a vessel with fermentation readings but no Initial Brix.
5. **"All good"** — back to normal.

**Rules → notifications** (thresholds are DEMO ASSUMPTIONS, editable in Settings, shown with an amber chip):
- Temperature > 30 °C → 🔴 "F.2 is too hot: 31.4 °C. Risk of stuck fermentation." 
- Level drop > 20 L in < 1 h with no operation → 🔴 "Q.3 lost 25 L with no operation recorded. Check for a leak or an unrecorded transfer." (+ value in $ at $2.99/L, Calculated)
- Density change < 0.001 in 48 h while SG > 1.010 → 🟠 "F.2 fermentation may be stuck."
- Fermentation readings but no Initial Brix → 🟠 "F.2 has no Initial Brix — potential alcohol can't be calculated."
- Back in range → 🟢 "F.2 back to normal."

**Notification UI:**
- Toast (top-right, auto-hide 6 s, red/amber/green), bell counter, and a **notification center** drawer with the feed: time, vessel, message, "Acknowledge" button.
- **"What's happening now" feed** on Overview and on the Cellar page (newest first, icons).
- Cellar map: the affected vessel pulses its status ring and its level animates live.
- A **"Live board"** page (TV/projector view): cellar map + feed + 3 live mini charts, big fonts, auto-refresh via SSE.
- Optional: POST `/api/sensors/reading` (already a stub) feeds the same pipeline, so a real ESP32 or WINEGRID could replace the simulator later. Show this sentence on the Live board: "Live data is simulated today. Real sensors plug into the same endpoint."

---

## 5. Overview page = the jury's 60-second story (Jury mode)

Top to bottom, symmetric, one screen on a 1280×720 projector:
1. Hero line: "Every kg. Every litre. From farmer to bottle." + UVP sentence + 3 big buttons: **▶ A→Z demo**, **▶ Qvevri demo**, **● Go live**.
2. 4 KPI tiles: Wine in cellar now · Batches tracked · Wine lost (L and $) · Journals filled (x/8).
3. Two big cards side by side: **Cellar map** (mini) | **What's happening now** (live feed).
4. One wide card: **Batch journey flow** for W01-B1-2025.

---

## 6. Demo players — restyle

The 3 existing players (A→Z, Qvevri, Fermentation readings) keep their logic, but each step shows a **visual** instead of text-only cards: the journey flow growing step by step, the tank/qvevri level changing, the gauge filling, journal tiles turning green, money counter ticking up. Text max 2 lines per card in Jury mode.

---

## 7. New selftest checks
- Live/simulated events are stored only in `live_events`; journals, batch loss totals and ROI numbers are identical with Live ON and OFF.
- Every live event has `source = 'SIMULATED'`.
- Each scenario produces its expected notification type (hot → red temp, loss → red level, stuck → amber, missing → amber, all good → green).
- SSE endpoint responds with `text/event-stream`.
- All 50 previous checks still pass.

---

## 8. Design QA (do this yourself before finishing)
1. Take screenshots with headless Edge at **1440×900, 1280×720 and 390×844** of: Overview (Jury + Expert), Cellar map, Live board with a red notification, batch page (journey flow), each demo player at the last step.
2. Review them critically: equal card heights, aligned grid, no overflow, no text smaller than 12 px, enough contrast, nothing cramped, not more than 4 numbers per Jury screen. Fix and re-screenshot until it looks like a modern product.
3. Save the final screenshots to `docs/screenshots/` and list them in the README.

## 9. Finish
1. Selftest passes (old + new).
2. `docker compose up --build -d`, confirm http://localhost:8080.
3. README: Jury vs Expert, Live mode, the 5 scenarios, and a **2-minute jury script**: Overview → A→Z demo (fast) → Go live → "Unrecorded wine loss" → notification → Cellar map.
4. `git add . && git commit -m "Update 3: design, jury mode, visuals, live notifications"`.
