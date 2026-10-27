# Bacchus Ledger — Update 2: fermentation readings (winemaker request)

Extend the existing app in this folder. Keep everything that works. Keep all existing rules from BACCHUS_LEDGER_CLI_PROMPT.md
(source chips on every number, no invented data, offline, Georgian/English, append-only).
`/api/selftest` must still pass all existing checks, plus the new ones below.

## Why (from our team's winemaker)
"These are the data points that help a winemaker keep track of how their wine is doing, without getting lost in stacks of paper."

## 1. Vessel codes = the physical label in the cellar
- Add a field **Cellar label** to every vessel (free text, e.g. `F.1`, `F.2`, `Q.3`). This is what is written on the tape label on the real qvevri/tank.
- Show the cellar label everywhere a vessel is shown (big and bold), with the internal code small next to it.
- Search/select vessels by cellar label. The printable vessel label shows the cellar label in very large text.
- Seed: keep the dataset codes (Q-3, T3-1, F-1 …). Leave cellar label empty unless the user types it (tag "Entered").

## 2. Qvevri measurements are OPTIONAL
- Qvevri vessel form: optional fields **depth (cm), neck inner Ø (cm), max inner Ø (cm), known volume (L)**. Any can be empty.
- If the winemaker knows the volume or the measurements, use them. If not, **do not guess**: show "Volume unknown — enter litres manually" and let litres be typed directly in operations.
- Dip → litres only works when the vessel has a calibration (existing A5 demo qvevri keep their "generalised A5 model (demo)" calibration and stay tagged as such).

## 3. New reading types (manual, with timestamp, attached to a fermentation vessel + its batch)
Add one table `readings(id, batch_id, vessel_id, taken_at (date + time), type, value, unit, instrument, person, note, source)`.
Types:
| type | unit | instrument | rule |
|---|---|---|---|
| `initial_brix` | °Bx | refractometer | **Once per vessel/batch, before fermentation.** Label in UI: **"Initial Brix"**. Warn if entered after fermentation has started (refractometers are not accurate once alcohol is present). |
| `ph` | pH | pH meter / lab | many, timestamped |
| `temperature` | °C | thermometer / probe | many, timestamped |
| `hydrometer_density` | SG (e.g. 1.090) | manual or digital hydrometer | many, timestamped |
| `hydrometer_abv` | % vol | hydrometer reading of alcohol reached at time of test | many, timestamped; can be entered together with density |
| `ta` | g/L (tartaric) | titration | many, timestamped |

Entry form on the phone layout: choose vessel (by cellar label) → type → value → date/time (default now) → Save. Fast: one screen, big inputs.

## 4. Calculations (tag "Calculated", show the formula on hover)
- **Potential alcohol from Initial Brix:** `potential_abv ≈ initial_brix × 0.59` (rule of thumb; show "approx.").
- **Strength reached:** `abv_reached / potential_abv × 100%` → shown as "Fermentation progress: X% of potential alcohol" next to every hydrometer ABV reading. This is the comparison the winemaker asked for (hydrometer vs Initial Brix).
- **Brix → SG for the starting point** (to plot on the density chart): `SG = 1 + brix / (258.6 − (brix / 258.2) × 227.1)`.

## 5. Graphs (per vessel / batch) — inline SVG, no libraries
On the batch page and the vessel page, a **"Fermentation readings"** section with 4 charts:
1. **Temperature** (°C) over time.
2. **pH** over time.
3. **Hydrometer**: density (SG, left axis) and ABV % (right axis) over time; draw the Initial Brix starting point (as SG) and a dashed line at potential ABV.
4. **TA** (g/L) over time.
Each chart: dots + line, hover tooltip (value, time, instrument, person, source chip), empty state "No readings yet — add one".
**Time slider:** a month-by-month range slider above the charts (from the first to the last reading month) + buttons "Day / Week / Month / All". All 4 charts follow the slider.

## 6. Seed data — ONLY what the dataset has (no new invented readings)
- **Temperature:** W01 batch 1, vessel F-1, Dataset A3, 14 daily readings 18.09–01.10.2025 (21.8 … 23.2 °C, already in the app — show them in the new chart). W03 batch 3, Dataset A4.2: 15.10.2025 Q-3 17.6 · 20.11.2025 T3-1 15.2 · 15.12.2025 T3-2 12.8 · 26.03.2026 T3-2 14.0 · 09.06.2026 T3-2 16.1 °C.
- **pH:** W02 batch 2, Dataset A4.1 (7 rows: 3.18, 3.16, 3.17, 3.20, 3.22, 3.23, 3.23). W03 batch 3, Dataset A4.2 (3.46, 3.47, 3.47, 3.48, 3.48).
- **TA:** W02 A4.1 (7.1, 7.3, 7.2, 7.0, 6.9, 6.8, 6.8 g/L). W03 A4.2 (6.0, 5.9, 5.8, 5.8, 5.7 g/L).
- **Initial Brix and hydrometer:** the dataset has **none** (it gives sugar in g/L, A3: 233 g/L on day 1 — keep showing that as "Initial sugar (lab, g/L)"). Do NOT convert or invent Brix/hydrometer values. Charts show the empty state for seeded batches.
- Time stamps: dataset rows have dates only → store time 09:00 and tag "time not in dataset".

## 7. Demo button for the new feature
Add a third demo player: **"▶ Fermentation readings demo"**. It does NOT invent data — it guides the presenter to type live:
1. Select vessel F-2 (or a new empty qvevri), set cellar label `F.2` (presenter types it).
2. Enter Initial Brix (presenter types, e.g. the value read on a real refractometer) → app shows potential alcohol.
3. Enter a hydrometer reading (density + ABV) → app shows "% of potential alcohol reached".
4. Enter one pH, one temperature, one TA → the 4 charts update; move the time slider.
All values entered here are tagged **"Entered (live demo)"**.
Also add buttons to open the W03 batch with its real A4.2 readings (temperature, pH, TA over Oct 2025 – Jun 2026) to show the month slider on real data.

## 8. Georgian strings (mark "verify with a native speaker")
Initial Brix საწყისი ბრიქსი · Refractometer რეფრაქტომეტრი · Hydrometer ჰიდრომეტრი · Density სიმკვრივე · Alcohol ალკოჰოლი · Potential alcohol პოტენციური ალკოჰოლი · Titratable acidity ტიტრული მჟავიანობა · Temperature ტემპერატურა · Readings გაზომვები · Cellar label მარნის ეტიკეტი · Volume unknown მოცულობა უცნობია.

## 9. New selftest checks (add to /api/selftest)
- `potential_abv(22.0) == 12.98` (±0.01).
- `brix_to_sg(22.0)` ≈ 1.0920 (±0.001).
- W01 has 14 temperature readings from A3; W03 has 5 temperature, 5 pH, 5 TA readings from A4.2; W02 has 7 pH and 7 TA readings from A4.1.
- No seeded batch has any `initial_brix`, `hydrometer_density` or `hydrometer_abv` reading (proves nothing was invented).
- Saving `initial_brix` after the first fermentation reading of that vessel returns a warning.
- Readings are append-only (update/delete blocked like operations).

## 10. Finish
1. Run the selftest until everything passes (old + new).
2. Rebuild and restart: `docker compose up --build -d` (this replaces the running container if there is one).
3. Confirm http://localhost:8080 works and the new demo runs end to end.
4. Update README: the new readings, the third demo, and the 1-minute presenter script for it.
5. Commit: `git add . && git commit -m "Update 2: fermentation readings"` (run `git init` first if needed).
