# Build "Bacchus Ledger" — a Dockerized demo for the WINECUBATOR wine hackathon (Georgia)

You are building a working software prototype that runs with ONE command on Docker Desktop:
`docker compose up --build` → open http://localhost:8080

Read this whole file first. Follow it exactly. **Do not invent any number, name or fact that is not listed here.**
Where this file says "DEMO ASSUMPTION", the value is invented on purpose and must be labelled as such in the UI.

---

## 0. The product in one paragraph (show this on the Overview page)

**Problem (sourced):** A Kakheti winery buys grapes from many farmers (~44 per company: 22,000 farmers sold to 500+ companies in 2025 — source CBW/National Wine Agency) and moves wine through many tanks and qvevri, but records everything on paper and by eye. Challenge Book 3.1: *"Vineyard and grape intake records are not automatically linked to the wine batch."* / *"Primary records are often moved by hand from paper to Excel."* Challenge Book 3.2: *"No winery has a sanitary flow meter."* Result (Dataset A6.2): winery W01 put 10,100 L in the tank and bottled 9,630 L — 450 L lost + 20 L left in the tank — and nobody can say which step lost too much.

**Solution:** One record from farmer to bottle. The cellar master scans a QR (vessel / delivery note) and types kg, dip reading (cm) or bottle count on a phone. The app links every kg and every litre (farmer → plot → delivery → batch → tank/qvevri → bottle), shows litres and loss at every step, and fills the 8 journals the Kakheti PDO rules require (Kakheti PDO specification §12).

**UVP:** "The only cellar record that works with qvevri, in Georgian, for Kakheti law, with no sensors and no internet needed."

---

## 1. Hard rules

1. **Every number shown in the UI carries a small source tag**, one of:
   `Dataset A1/A2/A3/A4.1/A4.2/A5/A6.1/A6.2/B2/B3.1` · `Challenge Book` · `Kakheti PDO spec` · `CBW 2025` · `Hackathon slides` · `TBC Capital 2024` · `Calculated` · `DEMO ASSUMPTION`.
   Tags are short colored chips (Dataset = green, Calculated = blue, DEMO ASSUMPTION = amber).
2. Never add data that is not in section 4. If something is needed and missing, use a clearly labelled DEMO ASSUMPTION and list it on the "Data sources" page.
3. Works fully **offline**: no CDN, no external fonts, no external API calls. All JS/CSS is local.
4. UI language: **English and Georgian** with a toggle (strings in section 9). Default English.
5. Records are **append-only**: operations are never edited or deleted; a correction is a new operation with a note. (Reset demo data is the only exception.)
6. Mobile-first layout that also looks good on a laptop/projector.

---

## 2. Tech stack (keep it simple)

- **.NET 8**, ASP.NET Core **minimal API**, one project.
- **SQLite** via `Microsoft.Data.Sqlite` (the ONLY NuGet package). Hand-written SQL. No EF Core.
- Frontend: **plain HTML + CSS + vanilla JavaScript** in `wwwroot/` (no npm, no build step). Charts are inline **SVG drawn by our own JS**.
- QR codes: generate QR as SVG in JS with a small embedded MIT QR library you write or vendor into `wwwroot/vendor/` (no CDN). If that is too heavy, show a printable label with the vessel code in large text instead; QR scanning is NOT required for the demo.
- "PDF": a **print-friendly HTML view** (browser Print → Save as PDF) + **CSV export** (UTF-8 with BOM so Excel opens Georgian).
- Docker: multi-stage `Dockerfile` (`mcr.microsoft.com/dotnet/sdk:8.0` → `mcr.microsoft.com/dotnet/aspnet:8.0`), `docker-compose.yml` with one service, port `8080:8080`, volume `./data:/data`, DB file `/data/bacchus.db`. Env `RESET_ON_START=false`.
- Seed data is created automatically on first start if the DB is empty.

Project layout:
```
bacchus-ledger/
  docker-compose.yml
  Dockerfile
  README.md
  src/BacchusLedger/
    BacchusLedger.csproj
    Program.cs            (routes)
    Db.cs                 (schema, helpers)
    Seed.cs               (section 4 data)
    Calibration.cs        (section 5)
    LossEngine.cs         (section 6)
    Journals.cs           (section 7)
    DemoScript.cs         (section 8)
    wwwroot/ index.html, app.css, app.js, i18n.js, vendor/
```

---

## 3. Data model (SQLite tables)

- `wineries(id, code, name, size, yearly_t, uses_qvevri, readiness_level)`
- `farmers(id, code, name, phone, is_demo)`
- `plots(id, farmer_id, winery_id, code, cadastre_no, village, municipality, region, area_ha, variety, lat, lng, licence_no, soil_texture, soil_ph, rain_mm, summer_temp_c, is_demo)`
- `orders(id, winery_id, farmer_id, plot_id, variety, kg_planned, price_gel_per_kg, date, is_demo)`
- `deliveries(id, order_id, plot_id, farmer_id, date, kg, sugar_gl, quality_note, accepted, source)`
- `vessels(id, winery_id, code, type['tank'|'qvevri'], name, calibration_json, capacity_l, source)`
- `batches(id, winery_id, code, name, wine_type, technology['classic'|'qvevri'], variety, vintage, abv, status, source_note)`
- `batch_origins(batch_id, delivery_id, kg)` — the farmer/plot share of each batch
- `operations(id, batch_id, date, type, from_vessel_id, to_vessel_id, litres, bottles, bottle_ml, remainder_l, method['paper log'|'dip'|'bottle count'|'scale'], dip_cm, person, note, source, created_at)`
  - `type` ∈ `intake, separation, racking, stock_check, filtration, transfer, bottling, warehouse`
- `lab(id, batch_id, date, vessel_id, stage, sugar_gl, temp_c, ph, ta_gl, rs_gl, note, source)`
- `settings(key, value)` — value_per_litre_usd, litres_per_tonne, loss norms (section 6)

---

## 4. Seed data (exact — do not change numbers)

### 4.1 Wineries (Challenge Book §2)
| code | size | yearly t | qvevri | level |
|---|---|---|---|---|
| W01 | Large | 2000 | yes | L1 |
| W02 | Medium | 300 | yes | L1 |
| W03 | Small to medium | 100 | yes | L2 |
| W04 | Small | 80 | yes | L1 |
| W05 | Medium | 500 | no | L2 |
Names: "Winery 1" … "Winery 5" (anonymised in the dataset).

### 4.2 Plot / terroir (Dataset A1 + B2, reference point M1, Kvareli)
lat 41.92, lng 45.78, municipality Kvareli, region Kakheti. Soil 0–30 cm: pH 6.7, clay 27.3–30.6%, silt 46.3–48.2%, sand 23.1–24.7% (B2). Climate Dec 2024–Nov 2025 (A1): rainfall total 603.8 mm (99.5+219.4+146.9+138.0), summer mean air temp 24.3 °C.
Show note: "Regional model data (ERA5, SoilGrids), not an on-site measurement."

### 4.3 Farmers and plots — DEMO ASSUMPTION (the dataset has no farmers)
For W01 batch 1 (16,000 kg, Dataset A2), split into 4 demo farmers in Kvareli municipality:
| farmer | plot | ha | variety | kg delivered |
|---|---|---|---|---|
| Demo Farmer 1 | K-101 | 0.8 | Saperavi | 5,200 |
| Demo Farmer 2 | K-102 | 0.6 | Saperavi | 4,300 |
| Demo Farmer 3 | K-103 | 0.5 | Saperavi | 3,800 |
| Demo Farmer 4 | K-104 | 0.4 | Saperavi | 2,700 |
(sum = 16,000 kg). Cadastre numbers like `DEMO-KV-0001`. Licence no. `DEMO`.
For W03 batch 3 (1,850 kg): Demo Farmer 5, plot P-301, 0.3 ha, Rkatsiteli, 1,850 kg.
Varieties are DEMO ASSUMPTIONS (dataset says only "classic red" / "qvevri amber").

### 4.4 Batches (Dataset A2)
| batch | winery | intake date | grapes kg | wine type | demo variety | demo ABV |
|---|---|---|---|---|---|---|
| W01-B1-2025 | W01 | 18.09.2025 | 16,000 | Classic red wine | Saperavi | 13.5 |
| W02-B2-2025 | W02 | 22.09.2025 | 9,800 | Classic white wine | Rkatsiteli | — |
| W03-B3-2025 | W03 | 19.09.2025 | 1,850 | Qvevri amber wine | Rkatsiteli | 12.8 |
| W04-B4-2025 | W04 | 27.09.2025 | 1,500 | Qvevri red wine | Saperavi | — |
| W05-B5-2025 | W05 | 21.09.2025 | 7,000 | Classic white wine | Rkatsiteli | — |
ABV values are DEMO ASSUMPTIONS.

### 4.5 Fermentation — W01 batch 1, vessel F-1 (Dataset A3, one row per day from 18.09.2025)
sugar g/L: 233.0, 230.0, 218.0, 193.0, 159.0, 123.0, 88.0, 60.0, 38.0, 21.0, 10.0, 4.6, 2.6, 2.4
temp °C:   21.8, 22.4, 24.1, 26.0, 27.2, 27.5, 27.0, 26.4, 25.8, 25.1, 24.6, 24.0, 23.5, 23.2

### 4.6 Lab
- W02 batch 2 (Dataset A4.1), fermentation day 1,4,7,10,13,16,19 from 23.09.2025 (dates 23.09, 26.09, 29.09, 02.10, 05.10, 08.10, 11.10.2025): pH 3.18, 3.16, 3.17, 3.20, 3.22, 3.23, 3.23; TA g/L 7.1, 7.3, 7.2, 7.0, 6.9, 6.8, 6.8.
- W03 batch 3 (Dataset A4.2):
  15.10.2025 Q-3 on pomace 17.6 °C pH 3.46 TA 6.0 RS 2.3 ·
  20.11.2025 T3-1 storage 15.2 °C pH 3.47 TA 5.9 RS 2.3 ·
  15.12.2025 T3-2 ageing 12.8 °C pH 3.47 TA 5.8 RS 2.2 ·
  26.03.2026 T3-2 ageing 14.0 °C pH 3.48 TA 5.8 RS 2.2 ·
  09.06.2026 T3-2 before bottling 16.1 °C pH 3.48 TA 5.7 RS 2.2

### 4.7 Cellar operations
**W01 batch 1 (Dataset A6.2, "paper log"):**
| date | op | from → to | litres |
|---|---|---|---|
| 18.09.2025 | intake | → F-1 | 16,000 kg grapes |
| 02.10.2025 | separation | F-1 → T1-1 | 10,100 |
| 12.10.2025 | racking | T1-1 → T1-2 | 9,820 |
| 15.12.2025 | stock_check | T1-2 | 9,820 |
| 12.03.2026 | racking | T1-2 → T1-3 | 9,720 |
| 14.04.2026 | filtration | T1-3 → BT-1 | 9,670 |
| 16.04.2026 | bottling | BT-1 → bottles | 12,840 × 750 mL = 9,630; remainder 20 L in BT-1 |
| 17.04.2026 | warehouse | → finished goods | 12,840 bottles |

**W03 batch 3 (Dataset A6.1):**
| date | op | from → to | litres |
|---|---|---|---|
| 19.09.2025 | intake | → Q-3 | 1,850 kg grapes |
| 15.11.2025 | separation | Q-3 → T3-1 | 1,230 |
| 12.12.2025 | racking | T3-1 → T3-2 | 1,200 |
| 26.03.2026 | stock_check | T3-2 | 1,200 |
| 10.06.2026 | filtration | T3-2 → T3-3 | 1,188 |
| 12.06.2026 | bottling | T3-3 → bottles | 1,576 × 750 mL = 1,182; remainder 0 |
| 13.06.2026 | warehouse | → finished goods | 1,576 bottles |

### 4.8 Vessels — sizes are DEMO ASSUMPTIONS except qvevri shape (Dataset A5)
- W01: F-1, F-2 tank cylinder Ø230 cm × 290 cm; T1-1…T1-5 cylinder Ø220 × 290; BT-1 cylinder Ø210 × 290.
- W03: Q-3, Q-4 qvevri (A5 profile, section 5); T3-1…T3-3 cylinder Ø110 × 160.

### 4.9 Settings / reference numbers
- `value_per_litre_usd = 2.99` — Calculated: 2025 Georgian wine exports $267.9M ÷ 89.7M L (Hackathon slides).
- `litres_per_tonne = 631` — Calculated from W01 batch 1: 10,100 L ÷ 16 t (Dataset A2, A6.2).
- Reference grape price 2025 (state purchase price, Kakheti): Saperavi 1.50 GEL/kg, other permitted varieties 1.20 GEL/kg (CBW / National Wine Agency). Use as the demo order price, tagged "CBW 2025 (state purchase price)".
- Kakheti PDO zone municipalities (Kakheti PDO spec §3): Akhmeta, Telavi, Gurjaani, Sighnaghi, Dedoplistskaro, Kvareli, Lagodekhi, Sagarejo.
- Market facts for the Overview page: 1,027 wine companies in Georgia in 2023 (6 large, 29 medium, 982 small) — TBC Capital 2024. Kakheti = 327,000 of 336,000 t processed in 2025 (97%) — CBW 2025.

---

## 5. Calibration: dip reading (cm) → litres

Dip reading `u` = distance in cm from the vessel's top rim down to the wine surface. Fill height `h = H − u`.

**Cylinder tank:** `litres = π · (D/2)² · h / 1000` (D, H in cm).

**Qvevri (Dataset A5):** inner height H = 180 cm, neck inner Ø 60 cm (straight from z = 165 to 180), max inner Ø 137.46 cm at z = 100, volume to the mouth ≈ 1,500 L.
Radius profile r(z) (cm, z = height above inner bottom):
- 0 ≤ z ≤ 100: `r = 10 + (68.73 − 10) · sin(π/2 · z/100)`
- 100 < z < 165: `r = 30 + (68.73 − 30) · cos(π/2 · (z − 100)/65)`
- 165 ≤ z ≤ 180: `r = 30`
Build a table every 1 cm, integrate volume with frustums `V += π·Δz/3·(r0² + r0·r1 + r1²)`; raw total ≈ 1,490 L; then scale every radius by `sqrt(1500 / rawTotal)` so the full volume = 1,500 L. Store the table in `calibration_json`. Show on the vessel page: "Shape: generalised A5 model. Real qvevri differ — measure each qvevri once (Challenge Book 1.1)."

Vessel page has a **dip calculator** (type cm → litres) and a printable dip chart (every 5 cm).

**Acceptance checks (must pass):** qvevri full volume = 1,500 L ± 1; Ø220×290 tank capacity ≈ 11,024 L; T3-x cylinder: 1,188 L ↔ dip ≈ 35.0 cm.

---

## 6. Loss engine (mass balance)

Replay each batch's operations in date order, tracking litres per vessel.
- `separation`: first litres of the batch (no loss computed).
- `racking / filtration / transfer`: `loss = litres_in_source − litres_out − remainder`; source keeps `remainder`.
- `stock_check`: `loss = litres_before − litres_measured`.
- `bottling`: `litres_out = bottles × bottle_ml / 1000`; `loss = litres_in_source − litres_out − remainder`.
- `loss_pct = loss / litres_before × 100`. The first racking of a batch is stage `racking_first`.
- A negative loss = "more than recorded — check the reading" (amber warning).

**Normal loss per stage — DEMO ASSUMPTION defaults, editable in Settings:**
racking_first 3.0% · racking 1.5% · filtration 0.6% · bottling 0.3% · stock_check 0.3% · transfer 1.0%.
`excess = max(0, loss − norm% × litres_before)`. Flag "Above normal" if excess > 0.5 L.
Also show, next to each stage, the **same stage at the other winery** (W01 vs W03) — this comparison is pure dataset fact.

**Automatic checks shown as green ticks / red crosses:**
- Bottle check: `bottles × 0.75 = litres bottled` (W01: 12,840 × 0.75 = 9,630 ✓; W03: 1,576 × 0.75 = 1,182 ✓).
- A vessel cannot give out more than it holds.
- Plot municipality is inside the Kakheti PDO zone (section 4.9).
- Origin completeness: batch kg = sum of its deliveries (W01: 16,000 = 5,200+4,300+3,800+2,700 ✓).
- Qvevri rule (information only, neutral wording): "Challenge Book: by law 'qvevri wine' must be fermented and matured in the qvevri up to 31 December of the year of pressing. This batch was separated on 15.11.2025 — please verify the label category." Show as an amber info chip, not an error.

**Acceptance checks (must pass, expose at `GET /api/selftest` returning JSON pass/fail):**
- W01: racking_first 280 L (2.77%) ok · stock_check 0 · racking 100 L (1.02%) ok · filtration 50 L (0.51%) ok · bottling 20 L (0.21%) ok · remainder 20 L · total loss 450 L · total difference 470 L.
- W03: racking_first 30 L (2.44%) ok · stock_check 0 · filtration 12 L (1.00%) ABOVE (excess 4.8 L) · bottling 6 L (0.51%) ABOVE (excess 2.4 L) · total loss 48 L.
- Money: W01 batch loss 450 L × $2.99 = **$1,345.50** (Calculated).
- "What 1% of loss costs per year" = yearly_t × 631 × 1% × $2.99: W04 $1,509 · W03 $1,887 · W02 $5,660 · W05 $9,433 · W01 $37,734 (label "up to" for W01: not every tonne becomes wine).

---

## 7. The 8 Kakheti journals (Kakheti PDO spec §12)

Generate each journal from the records (no separate typing). Each has a print view and CSV export, filtered by winery and batch:
1. **Vineyard accounting** — farmers, plots, ha, variety, cadastre no.
2. **Grape receiving** — deliveries: date, farmer, plot, kg, sugar, accepted.
3. **Grape processing** — which deliveries went into which vessel/batch.
4. **Vintage** — harvest per plot and kg per ha (calculated).
5. **Storage and movement** — every operation with from/to, litres, loss.
6. **Bottling** — bottling runs, bottles, litres, remainder, bottle check.
7. **Laboratory analysis** — all lab rows.
8. **Tasting commission** — empty template with batch info (no data in the dataset — say so).
Plus **"Batch history in 1 click"**: one printable page with everything for one batch (origin → fermentation curve → operations with losses → lab → bottling).

---

## 8. THE DEMO BUTTON — guided A→Z replay (most important)

On the Overview page: big button **"▶ Run the A→Z demo (W01, Saperavi 2025)"**. It opens a full-screen **step player** that replays the real dataset batch, one step at a time.

Player layout per step:
- Left: step number + title + date.
- Middle: **"Data received"** card — the exact fields with source chips.
- Right: **"What the app did"** card — checks run, numbers calculated, journal(s) updated (with the journal number 1–8).
- Bottom: running totals bar: kg received · litres now · total loss L · loss value $ · journals filled (x/8).
- Controls: ◀ Back · Next ▶ · Auto-play (3 s per step) · Close. Keyboard arrows work.
- Each step really writes the data into the DB as it plays (so the batch page and journals fill live). Before starting, the demo batch is re-created from seed as `W01-B1-2025-DEMO` so the player can be replayed any time.

Steps (use exactly these facts):
0. **The problem** — 3 quotes from Challenge Book 3.1/3.2 + "5 of 5 partner wineries report it; #2 priority for W02, W03, W05" (Challenge Book).
1. **Register farmers and plots** — 4 demo farmers, plots K-101…K-104 in Kvareli (DEMO ASSUMPTION). App did: Kakheti PDO zone check ✓ (Kvareli is in §3 list); terroir attached from A1/B2. Journal 1.
2. **Grape orders** — 4 orders, Saperavi, price 1.50 GEL/kg (CBW 2025 state purchase price). App did: expected cost = 16,000 × 1.50 = 24,000 GEL (Calculated). Journal: none (commercial).
3. **Receiving, 18.09.2025** — 4 deliveries (5,200 / 4,300 / 3,800 / 2,700 kg, DEMO split of the Dataset A2 total 16,000 kg); sugar 233 g/L (Dataset A3 day 1). App did: origin check 16,000 = sum ✓; potential alcohol ≈ 233 / 17 ≈ 13.7% (Calculated, rule of thumb ~17 g/L per 1% ABV). Journal 2.
4. **Into tank F-1** — batch created with origin list (Farmer 1: 32.5%, 2: 26.9%, 3: 23.75%, 4: 16.9% — Calculated). Journals 3, 4 (kg per ha: e.g. 5,200 / 0.8 = 6,500 kg/ha).
5. **Fermentation, 14 days (18.09–01.10.2025)** — animate the A3 rows into a sugar + temperature SVG chart. App did: fastest drop −36 g/L/day (day 5→6: 159 → 123); peak temperature 27.5 °C on day 6; fermentation finished at 2.4 g/L. Journal 7.
6. **Separation, 02.10.2025** — F-1 → T1-1, 10,100 L (Dataset A6.2). App did: yield 10,100 / 16 t = 631 L/t (Calculated). Journal 5.
7. **First racking, 12.10.2025** — T1-1 → T1-2, 9,820 L. App did: loss 280 L = 2.77%; normal ≤ 3.0% (DEMO ASSUMPTION norm) ✓; W03 same step: 2.44%. Journal 5.
8. **Stock check, 15.12.2025** — T1-2 = 9,820 L. App did: 0 L difference ✓. Journal 5.
9. **Second racking, 12.03.2026** — T1-2 → T1-3, 9,720 L. App did: loss 100 L = 1.02% ✓. Journal 5.
10. **Filtration, 14.04.2026** — T1-3 → BT-1, 9,670 L. App did: loss 50 L = 0.51% ✓; **comparison: W03 lost 1.00% at the same step (Dataset A6.1) → flagged at W03.** Journal 5.
11. **Bottling, 16.04.2026** — 12,840 bottles × 0.75 L. App did: bottle check 12,840 × 0.75 = 9,630 L ✓; 20 L remainder stays in BT-1; loss 20 L = 0.21% ✓. Journal 6.
12. **Warehouse, 17.04.2026** — 12,840 bottles (Dataset A6.2). Journal 6.
13. **Result** — big summary: 16,000 kg → 10,100 L → 9,630 L bottled; total loss 450 L (+20 L remainder) = 470 L difference (Dataset A6.2); value $1,345.50 at $2.99/L (Calculated); 8 of 8 journals available (tasting = template); button **"Open batch history (1 click)"** and **"Open the 8 journals"**.
14. **What it is worth** — the 1%-loss table per winery (section 6) + "1,027 wine companies in Georgia" (TBC Capital 2024) + UVP line. Clearly show "Not proven yet: how much loss a winery can win back — the pilot (Nov–Dec) measures it."

Second, shorter button: **"▶ Qvevri demo (W03 amber)"** — replays W03 batch 3: intake into Q-3 (1,850 kg) → lab on pomace 17.6 °C (A4.2) → separation 1,230 L (show the qvevri dip: litres 1,230 ↔ dip cm from the A5 table, Calculated) → racking 1,200 → stock check → filtration 1,188 (**ABOVE normal: 1.00% vs norm 0.6%, excess 4.8 L ≈ $14.35**) → bottling 1,576 bottles = 1,182 L (**0.51% vs norm 0.3%, excess 2.4 L**) → qvevri rule info chip → result 48 L total loss (Dataset A6.1).

---

## 9. Pages

1. **Overview** — problem / solution / UVP (section 0), the two demo buttons, KPI cards (wine in cellar now, batches, total loss, loss value), flags list.
2. **Batches** — list; batch page with: origin table (farmers %), fermentation chart, **litre waterfall SVG** (bars = litres after each step, axis starts near the minimum so small losses are visible, label "axis does not start at 0"), stage table (before, after, loss L, %, norm, other-winery value, status), lab table, checks, buttons: Batch history (print), CSV.
3. **Record operation** (phone layout) — choose type, from vessel, to vessel, enter dip cm OR litres OR bottles; **live preview** before saving: calculated litres, loss, % and status; then Save. Preset button "Try: W03 filtration with dip 35.0 cm" (prefills T3-2 → T3-3 on a fresh copy of the batch).
4. **Farmers & plots**, **Deliveries**, **Vessels** (with dip calculator and printable label with the vessel code / QR).
5. **Journals** — the 8 journals (section 7).
6. **Data sources** — table of every source tag with its full name, and the full list of DEMO ASSUMPTIONS from this file.
7. **Settings** — value per litre, litres per tonne, loss norms, language, **Reset demo data**.

---

## 10. Georgian strings (i18n.js) — mark in code: "verify with a native speaker"

Overview მიმოხილვა · Batches პარტიები · Record operation ოპერაციის ჩაწერა · Vessels ჭურჭელი · Farmers & plots ფერმერები და ნაკვეთები · Deliveries ყურძნის მიღება · Journals ჟურნალები · Settings პარამეტრები · Separation ჭაჭიდან გამოყოფა · Racking ლექიდან მოხსნა · Filtration ფილტრაცია · Transfer გადატანა · Stock check ნაშთის შემოწმება · Bottling ჩამოსხმა · Litres ლიტრი · Loss დანაკარგი · Dip reading (cm) სიღრმის ზომა (სმ) · Save შენახვა · Qvevri ქვევრი · Tank ავზი · Batch პარტია · Farmer ფერმერი · Plot ნაკვეთი · Harvest რთველი · Fermentation დუღილი · Above normal ნორმაზე მეტი · Normal ნორმაშია · Run the A→Z demo დემოს გაშვება.

---

## 11. API (JSON)

`GET /api/overview` · `GET /api/batches` · `GET /api/batches/{id}` (with stages, checks, origins, lab) · `POST /api/operations/preview` · `POST /api/operations` · `GET/POST /api/farmers`, `/api/plots`, `/api/deliveries` · `GET /api/vessels`, `GET /api/vessels/{code}/litres?dip=cm` · `GET /api/journals/{n}?batch=` and `.csv` · `GET /api/demo/steps?script=w01|w03` · `POST /api/demo/step/{n}?script=` (writes that step's data, returns the step card) · `POST /api/demo/reset` · `GET /api/selftest` · `GET /health`.
Optional, later (stub only): `POST /api/sensors/reading {vessel, dip_cm}` → stores a reading and converts with calibration.

---

## 12. Design

Calm cellar palette: deep Saperavi `#2B1420` (sidebar/headers), wine `#5B1A3A` (primary), amber `#C98A2B` (qvevri accent / DEMO ASSUMPTION chips), limestone `#F1F2EF` background, ink `#221A26` text, red `#B3261E` only for "above normal", green `#2F6B4F` for passed checks. System fonts only (serif headings: "Iowan Old Style", Palatino, Georgia, "Noto Serif Georgian", Sylfaen; body: system-ui, "Noto Sans Georgian", Sylfaen). Big readable numbers for the projector. Keyboard focus visible.

---

## 13. Done means

1. `docker compose up --build` works on a clean machine; app at http://localhost:8080 within a minute after the image is built.
2. `GET /api/selftest` returns all checks PASS (sections 5 and 6).
3. Both demo players run start to end, with Back/Next/Auto-play, and the batch page + journals fill live.
4. Every number in the UI has a source chip; the Data sources page lists all DEMO ASSUMPTIONS.
5. Works with the network disconnected after the build.
6. README: how to run, how to reset, a 2-minute demo script (Overview → A→Z demo → batch page → journals → record a transfer live), and a note to run `docker pull mcr.microsoft.com/dotnet/sdk:8.0` and `docker pull mcr.microsoft.com/dotnet/aspnet:8.0` beforehand on good Wi-Fi.

Build it step by step: schema + seed → calibration + loss engine + selftest (make it pass) → API → pages → demo players → journals → Docker → README. Run the selftest and fix until everything passes before you finish.
