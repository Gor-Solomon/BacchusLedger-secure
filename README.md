# Bacchus Ledger

Built for the WINECUBATOR wine hackathon (Georgia). It keeps one cellar record from farmer to bottle: every kg and every litre is linked (farmer → plot → delivery → batch → tank/qvevri → bottle). It shows the litres and the loss at every step and fills the 8 Kakheti PDO journals. Works offline, with no sensors.

## Run

Before the event, while you have good Wi-Fi, pull the base images:

```
docker pull mcr.microsoft.com/dotnet/sdk:8.0
docker pull mcr.microsoft.com/dotnet/aspnet:8.0
```

Then:

```
docker compose up --build
```

Open http://localhost:8081. After the image is built the app needs no network: no CDN, no external fonts and no API calls.

- Data is stored in `./data/bacchus.db` (mounted at `/data`). On first start an empty database is seeded with the data from the spec (section 4).
- Self-test: http://localhost:8081/api/selftest returns pass/fail for every acceptance check: calibration, loss engine, and the Update 2 readings checks (formulas, dataset counts, nothing invented, the Initial Brix warning, append-only). The same results appear at the bottom of Settings.
- Health: http://localhost:8081/health

### Reset

- In the app: **Settings → Reset demo data**.
- Or set `RESET_ON_START=true` in `docker-compose.yml` and restart.
- Or stop the app and delete `data/bacchus.db`.

The demo players re-create their own copies (`W01-B1-2025-DEMO`, `W03-B3-2025-DEMO`) every time they start, so you can replay them as often as you like.

### Local run without Docker (.NET 8 SDK)

```
cd src/BacchusLedger.Api
DB_PATH=../../data/bacchus.db dotnet run --urls http://localhost:8080
```

## 2-minute demo script

1. **Overview (20 s).** Read the problem: W01 put 10,100 L in and bottled 9,630 L, so 450 L were lost and 20 L stayed in the tank. Nobody knows which step lost them. Point at the source chips: green = Dataset, blue = Calculated, amber = DEMO ASSUMPTION.
2. **▶ Run the A→Z demo (60 s).** Press **Auto-play**, or use → to step through. Farmers → orders → receiving (origin check ✓) → F-1 → fermentation curve → separation → racking (2.77 % ✓, W03 2.44 %) → filtration (0.51 % ✓, W03 1.00 % flagged) → bottling (12,840 × 0.75 = 9,630 ✓) → result: $1,345.50 and 8/8 journals. On step 13, click **Open batch history (1 click)**.
3. **Batch page (15 s).** Show the litre waterfall (its axis does not start at 0), the stage table with the other winery's value for the same step, and the checks.
4. **Journals (10 s).** Open journal 5 and show **Print** (save as PDF) and **CSV** (opens in Excel with Georgian text).
5. **Record a transfer live (15 s).** Go to **Record operation → "Try: W03 filtration with dip 35.0 cm"**. The live preview shows 1,187.9 L, a loss of about 12 L (1.01 %) and **Above normal**. Press **Save**.

Optional: run the **Qvevri demo (W03 amber)**. It shows the qvevri dip ↔ litres conversion, the two above-normal stages and the qvevri-rule info chip. Deep links for presenting: `#/demo/w01?step=5`, `#/demo/w03`.

## Update 2: fermentation readings

What the winemaker asked for: *"These are the data points that help a winemaker keep track of how their wine is doing, without getting lost in stacks of paper."*

- **Cellar label.** Each vessel has a free-text label, for example `F.2` or `Q.3`, matching the tape label on the real tank or qvevri. The label is shown big and bold everywhere the vessel appears, with the internal dataset code small beside it. You can search and select vessels by label, and the printable vessel label shows it in very large text. Seeded vessels start with an empty label, and labels you type are tagged **Entered**.
- **Optional qvevri measurements.** You can enter depth, neck inner Ø, max inner Ø and known volume, or leave any of them empty.
  - Depth + neck Ø + max Ø: the app builds a dip table using the A5 shape scaled to those numbers, and to the known volume if one is given.
  - Otherwise: the vessel shows **"Volume unknown — enter litres manually"**. Dip → litres is refused and litres are typed directly. The app never guesses.
  - The demo qvevri Q-3 and Q-4 keep the "generalised A5 model (demo)".
- **Readings** (new menu item; table `readings`, append-only — the database blocks updates and deletes):

  | Type | Unit | Instrument |
  |---|---|---|
  | Initial Brix | °Bx | refractometer |
  | pH | pH | pH meter / lab |
  | Temperature | °C | thermometer / probe |
  | Hydrometer density | SG | hydrometer |
  | Hydrometer ABV | % vol | hydrometer |
  | TA | g/L | titration |

  The phone form is one screen: vessel (by cellar label) → type → value → date/time (defaults to now) → Save. A live preview shows the calculated values and any warnings before you save. **Initial Brix** is taken once per vessel/batch, before fermentation. The app warns if fermentation has already started, because refractometers are not accurate once alcohol is present.
- **Calculations** (tag Calculated; hover the chip to see the formula):
  - potential ABV ≈ Initial Brix × 0.59 (approx.)
  - **Fermentation progress: X% of potential alcohol** = hydrometer ABV ÷ potential ABV × 100%
  - Brix → SG = 1 + Brix ÷ (258.6 − (Brix ÷ 258.2) × 227.1), used for the starting point on the density chart
- **Charts.** The batch page, vessel page, batch history and Readings page each show 4 SVG charts: temperature, pH, hydrometer (SG on the left axis, % vol on the right, the Initial Brix start point and a dashed potential-ABV line) and TA. Points show a tooltip on hover or tap. A month-by-month range slider and **Day / Week / Month / All** buttons control all 4 charts together.
- **Seed data: dataset only.**
  - Temperature: Dataset A3 (W01, F-1, 14 days) and A4.2 (W03).
  - pH and TA: A4.1 (W02) and A4.2 (W03).
  - These rows carry dates only, so they are stored at 09:00 and tagged "time not in dataset".
  - The dataset has **no** Brix or hydrometer values, so none are seeded. A3's 233 g/L is still shown as "Initial sugar (lab, g/L)".
- Entered readings also appear in journal 7 (Laboratory analysis).
- An existing `data/bacchus.db` from before Update 2 is upgraded automatically on start: the new columns and table are added, and the readings are derived from the stored dataset lab rows.

### Fermentation readings demo — 1-minute presenter script

Overview → **▶ Fermentation readings demo** (deep link `#/demo/readings`). Nothing is invented: you type the values, and they are tagged **Entered (live demo)**.

1. **(10 s)** Click **Use tank F-2**, or **New empty qvevri** to show "Volume unknown". Type the cellar label `F.2` and click **Save**. It now shows big everywhere.
2. **(10 s)** Enter the **Initial Brix** you read on a real refractometer. The app shows the potential alcohol (≈ Brix × 0.59, approx.) and the starting SG.
3. **(15 s)** Enter a hydrometer reading: density (e.g. 1.020) plus ABV reached. The app shows **"Fermentation progress: X% of potential alcohol"**.
4. **(15 s)** Enter one pH, one temperature and one TA, then click **Save**. The 4 charts update. Click **Day / Week / All**.
5. **(10 s)** Click **Open W03 batch readings** to show real Dataset A4.2 temperature, pH and TA from Oct 2025 to Jun 2026. Drag the month slider.

Starting the demo again clears the previous live-demo batch and labels.

## Update 3: modern design, Jury mode, visuals, live simulation and notifications

The team's feedback was: *"The design looks like 1999. It is pure data, not digestible for a jury. We need graphs, a simple mode, live data and notifications."*

### Design

- **New design system.** The CSS was replaced completely.
  - Colour tokens: background `#F6F5F2`, wine `#6B1E3F`, sidebar `#2B1420`, amber, green, red (alerts only), blue (calculated).
  - Type scale 12/14/16/20/28/40/56 px, spacing scale 4/8/12/16/24/32/48 px, cards 16 px radius, one soft shadow.
- **Fonts and icons, all vendored.** The **Inter** font (SIL OFL, `wwwroot/fonts/`) and **Lucide** icons (ISC, `wwwroot/icons/`) are stored in the app, each with its licence file. Nothing loads from the internet at runtime.
- **Layout.** A sidebar with icons, and a top bar with the page title, **Jury/Expert** switch, **Live** switch, **notification bell** with a counter, and the language switch.
  - The content uses a 12-column grid and cards in a row always have equal heights.
  - KPI tiles show 4 per row on desktop, 2 on tablet and 1 on phone.
  - Motion is subtle (150–250 ms) and is switched off when the system asks for reduced motion.

### Jury mode vs Expert mode

The switch is in the top bar and is remembered on this device.

- **Jury mode (default):** big visuals, short plain words ("Wine lost", not "racking_first loss"), at most 4 headline numbers per screen. Tables are collapsed behind "Show details". Source chips stay available as a small **i** that shows the chip on hover or tap.
- **Expert mode:** everything from before — tables, journals, every field, with source chips visible.

### Visuals

All visuals are inline SVG drawn by our own code (no chart libraries) and animate on first load.

| Visual | Where |
|---|---|
| **Cellar map**: every vessel drawn as its shape (tank = rounded rectangle, qvevri = the A5 egg outline), filled to its level, with the cellar label big and a green/amber/red status ring. A date slider replays the record; click a vessel for a side panel with its batch, last readings and mini charts. | Cellar page, Overview (mini) |
| **Batch journey flow** (Sankey style): farmers (width = kg) → batch → vessels in date order (width = litres) → bottles, with losses peeling off as thin red streams ("−280 L first racking"). On a phone it becomes a vertical funnel. | Batch page (hero), Overview, A→Z player |
| **Litre waterfall** (restyled) and **Loss per step W01 vs W03** (grouped bars with the dashed normal line) | Batch page, Expert overview |
| **Fermentation gauge** ("X% of potential alcohol") plus the A3 **sugar-drop curve** with a current-day marker | Batch page, players |
| **Origin donut** (farmer shares — a DEMO split, labelled) and **timeline strip** (18.09.2025 → 17.04.2026) | Batch page |
| **Money tiles** and "What 1% of loss costs per year" as horizontal bars — calculated numbers only | Batch page, Expert overview, A→Z player |
| **Journal coverage**: 8 tiles that turn green as the record (or the demo) fills them | Batch page, players |

### Live mode (SIMULATED)

All live data is **simulated** and is always labelled with a purple **SIMULATED** chip.

- **Isolation.** Live events are stored only in the `live_events` table. Journals, loss totals and ROI numbers never read it, and the selftest proves this.
- **Simulator.** A background service streams over Server-Sent Events (`GET /api/live/stream`). While Live is on, every 3 s it emits temperature, level (litres from a simulated dip) and — for fermenting vessels — density for F-2, Q-3 and T1-2. Each vessel starts from its last recorded value, with noise of ±0.3 °C and ±2 L. When a vessel has no recorded value, a simulated default is used (listed on the Data sources page).
- **Notifications:**
  - a toast at the top right that hides after 6 s
  - the bell counter
  - a notification drawer with an **Acknowledge** button
  - a **"What's happening now"** feed on the Overview, Cellar and Live board pages
  - on the cellar map, the affected vessel's ring pulses and its level animates
- **Live board** (TV/projector view): the live vessels, the feed, 3 live mini charts and the scenario panel. It also shows: *"Live data is simulated today. Real sensors plug into the same endpoint."* (`POST /api/sensors/reading` feeds the same stream; its events are tagged SENSOR.)

**The 5 scenarios** (Live board or Cellar page, "Live scenarios" panel). The rule thresholds are DEMO ASSUMPTIONS and can be edited in Settings.

| Scenario | What happens | Notification |
|---|---|---|
| Hot fermentation | F-2 temperature climbs from 27 to 32 °C over about 30 s | 🔴 "F-2 (…): temperature too high — 31 °C (allowed 20–30 °C, 3 readings in a row)." (range alert, see Update 5) |
| Unrecorded wine loss | The Q-3 level drops 25 L with no operation recorded | 🔴 "Q-3 lost ≈25 L with no operation recorded. Check for a leak or an unrecorded transfer." + value at $2.99/L (Calculated) (drop above 20 L within 1 h) |
| Stuck fermentation | F-2 density stays flat for a simulated 48 h while the sugar is still high | 🟠 "F-2 fermentation may be stuck." (density change below 0.001 in 48 h while SG is above 1.010) |
| Missing data | A fermenting vessel streams readings but has no Initial Brix | 🟠 "F-2 has no Initial Brix — potential alcohol can't be calculated." |
| All good | Everything goes back to normal | 🟢 "F-2 back to normal." |

Messages use the cellar label when one is set (for example "F.2").

### New selftest checks

The selftest now has 59 checks: the 50 existing ones plus 9 new.

- Live events are stored only in `live_events`. Journals, batch loss totals, ROI numbers and every record table are identical before and after a full simulator run of all 5 scenarios (run in a rolled-back transaction).
- Every simulated event has `source = 'SIMULATED'`, and no stored event is untagged.
- Each scenario produces its expected notification: hot → red temperature, loss → red level, stuck → amber, missing → amber, all good → green.
- `/api/live/stream` responds with `text/event-stream`.

### 2-minute jury script

1. **Overview (20 s), Jury mode.** "Every kg. Every litre. From farmer to bottle." Point at the 4 tiles: wine lost, 498 L ≈ $1,489, and 8/8 journals. Then the batch journey: the red streams are the losses.
2. **▶ A→Z demo, fast (40 s).** Press **Auto-play**, or tap → a few times. The journey grows, the tank level changes, the gauge fills, the journal tiles turn green and the loss counter ticks up to $1,345.50. Close.
3. **● Go live (15 s).** You land on the Live board. Point at the purple SIMULATED label and the sentence about real sensors.
4. **"Unrecorded wine loss" (25 s).** Click the scenario. Within a few seconds a red toast says *"Q-3 lost ≈25 L with no operation recorded… ≈ $74.75"*, the bell counter goes up and Q-3's ring turns red and pulses.
5. **Notification → Cellar map (20 s).** Open the bell, show the feed and click **Acknowledge**. Open **Cellar**, click Q-3, and the side panel shows the live value next to its recorded data. Finish with **All good** (green).

### Design QA screenshots

These were taken with headless Edge at 1440×900, 1280×720 and 390×844. They are in `docs/screenshots/`:

| View | Files |
|---|---|
| Overview — Jury | `overview-jury-1440.png`, `overview-jury-1280.png`, `overview-jury-390.png` |
| Overview — Expert | `overview-expert-1440.png`, `overview-expert-1280.png`, `overview-expert-390.png` |
| Cellar map (vessel side panel open) | `cellar-1440.png`, `cellar-1280.png`, `cellar-390.png` |
| Live board with a red notification | `live-board-1440.png`, `live-board-1280.png`, `live-board-390.png` |
| Batch page (journey flow) | `batch-1440.png`, `batch-1280.png`, `batch-390.png`, `batch-expert-1440.png` |
| A→Z player, last step | `player-w01-1440.png`, `player-w01-1280.png`, `player-w01-390.png` (+ result step: `player-w01-journey-1440.png`, `player-w01-journey-390.png`) |
| Qvevri player, last step | `player-w03-1440.png`, `player-w03-1280.png`, `player-w03-390.png` |
| Fermentation readings player | `player-readings-1440.png`, `player-readings-1280.png`, `player-readings-390.png` |

Each screenshot was also checked automatically: no horizontal scroll, no text smaller than 12 px, equal card heights in every grid row, no console errors, and at most 4 headline numbers on Jury screens.

## Update 4: Marani Assistant

A round button with a small qvevri sits at the bottom right. Click it to open the **Marani Assistant** chat (380 px wide, full screen on a phone). It answers **only from the ledger**.

### Two layers

- **Quick questions — no AI, always work, even offline.** Five buttons at the top of the panel. Our own server code answers them from the database. Each answer has short text, source chips and an **Open in app** link to the right page.
  1. *How much wine did W01 lose, and where?* → first racking 280, stock check 0, 2nd racking 100, filtration 50, bottling 20 = **450 L** + 20 L left; **$1,345.50** at $2.99/L.
  2. *Which steps are above normal?* → W03 filtration 1.00 %, W03 bottling 0.51 %.
  3. *Where did W01's grapes come from?* → the 4 demo farmers with kg and % (a **DEMO** split, labelled as such).
  4. *Show W03 readings* → the latest temperature, pH and TA (Dataset A4.2), with a link to the charts.
  5. *Any live alerts?* → the open live notifications (labelled **SIMULATED**), or "No alerts".
- **Free questions — AI, when online and a key is set.** Type anything (max 500 characters, Enter to send).
  - The model uses **tool calling** over our own read-only API: `get_overview`, `list_batches`, `get_batch`, `get_readings`, `get_vessel`, `get_journal`, `get_live_alerts`, `search_facts`. It does not use its own knowledge.
  - `search_facts` is a keyword search over `src/BacchusLedger.Api/knowledge/facts.md`, the problem and market facts from the Overview and Data sources pages, one fact per line with its source.
  - The rules are in the code (`Assistant.SystemPrompt`):
    - use only numbers the tools return, each with its source tag;
    - otherwise say *"I don't have that data in the ledger."*;
    - at most about 6 lines, in the language of the question;
    - read-only.
  - The server also **enforces** the first rule: if an answer contains a number that no tool returned, it is replaced with *"I don't have that data in the ledger."*
  - Under each answer the panel shows the tools used ("Used: batch W01-B1-2025, journal 5").
  - Timeout is 15 s. On an error or timeout the server tries the other provider once. If that also fails, the answer is *"AI is offline right now — use the quick questions above."*
  - With no key, the panel header says **"Quick questions only (AI not configured)"** and the quick questions still work.

### Keys: create `.env`

```
cp .env.example .env
```

Then fill in `OPENAI_API_KEY` and/or `GEMINI_API_KEY`. The optional settings are:

- `ASSISTANT_PROVIDER`: `openai` (default) or `gemini`
- `OPENAI_MODEL`: default `gpt-5.4-mini`
- `GEMINI_MODEL`: default `gemini-3.5-flash-lite`
- `ASSISTANT_ENABLED`: `true` (default) or `false`

`docker compose up --build -d` reads `.env` if it exists. The app also starts without it.

- Keys are read **only** from environment variables, and only the server calls the AI provider. Keys never go into the code, the prompt, the logs (only the provider name and HTTP status are logged), the browser or API responses.
- `.env` is listed in `.gitignore` and `.dockerignore`, so it is never committed or copied into the image.

> **Warning: rotate (revoke and re-create) both API keys after the event.** They were on a demo laptop and were used in front of an audience.

### Three good free questions to ask on stage

1. "Which W01 step lost the most wine, and how does it compare with the normal loss?"
2. "How much does 1% of loss cost each winery per year?"
3. "რამდენი ფერმერი ყიდის ყურძენს საქართველოში?" (*How many farmers sell grapes in Georgia?* — the answer comes back in Georgian.)

### New selftest checks

The selftest now has 67 checks: the 59 existing ones plus 8 new.

- Quick question 1 returns 450 L in total and the 5 step values. Quick question 2 returns exactly the two W03 stages.
- `POST /api/assistant/ask` with no key configured returns HTTP 200 and "AI not configured". The check calls the endpoint code with an empty configuration, so it never calls an AI provider.
- All 8 tools return ledger data.
- Key leak check: no served file in `wwwroot` and no API response contains the two key prefixes or the configured key values. (The unused `class` attribute in `icons/flask.svg` contained one of the prefixes, so it was removed.)
- Read-only: every record table and setting is identical before and after the quick questions, the tools and an ask.

## Update 5: range alerts on the Live board

Plain code, no AI, Live board only. Real records (`readings`, `/api/readings`) are not touched, and the schema is unchanged.

- **Ranges:** `src/BacchusLedger.Api/config/ranges.json` holds the defaults per wine type, plus optional per-vessel overrides under `"vessels"` (for example `"W01/F-2": { "temperature": { "min": 22, "max": 28 } }`). An override wins over the default. Edit the file, then restart the app.

  | Measurement | Default |
  |---|---|
  | Fermentation temperature, red and qvevri amber | 20–30 °C |
  | Fermentation temperature, white | 12–20 °C |
  | Cellar / storage temperature (vessel not fermenting) | 10–18 °C |
  | pH | 3.0–4.0 |
  | Density | 0.990–1.130 SG |
  | Level | 0 L – vessel capacity |
  | TA | 4.0–9.0 g/L |
  | Initial Brix | 18–26 °Bx |
  | Hydrometer ABV | 0–16 % vol |

  The simulator streams temperature, density and level, so only those three are checked today. Humidity is not measured, so it has no range.
- **Rules:**
  - Only new live readings are checked, never history.
  - Empty values and impossible values (for example −20 °C or SG 2.0) are skipped: they don't count towards an alert and don't reset the count.
  - An alert fires after **3 readings in a row** out of range, once per vessel and measurement.
  - The alert clears, with a green "back to normal", on the first reading back in range.
  - Each alert shows the vessel, batch, measurement, value, allowed range, time, and **too high** or **too low**.
- **The old "Too hot above (°C)" setting was removed.** The range alert replaced it. The level-drop, stuck-fermentation and missing-Brix rules are unchanged.
- **Live board:**
  - a red card list of active alerts at the top;
  - each value is shown green (in range) or red (out of range), and hovering shows the allowed range;
  - **Simulate spike** runs the existing "Hot fermentation" scenario: F-2 climbs to 32 °C and stays there. It writes only SIMULATED rows to `live_events`.
- **Selftest:** 6 new checks (73 in total).

## Update 6: ledger report

Read-only: no schema change, no change to existing writes, `/api/readings` untouched. The new `GET /api/report?batch=CODE[&vessel=W01/F-2]` only runs SELECTs, and `report.html` shows the result as a printable page.

- **Buttons:**
  - **Generate ledger report** on the last step of the A→Z demo and of the Qvevri demo (report for the demo batch);
  - on the Live board header: pick a vessel, and the report is for the batch in that vessel.
- **Content:**
  - a timeline summary;
  - the double-entry ledger: debit = the place that receives, credit = the place that gives; losses are a debit to "Losses"; pressing is one *transformation* entry from kg to L with the yield (L per tonne);
  - account balances;
  - a balance check: total in = total out + losses, and any gap is shown in red as "Unexplained difference";
  - lab readings.
- **Loss numbers** come from `LossEngine.Analyze` (same totals, cross-checked on the page).
- **Transport** is not recorded separately in the data, so deliveries go from the vineyard straight to winery reception.
- **Live board:** simulated alerts for the vessel appear in a separate dashed box marked SIMULATED. They never enter the ledger or the balance.
- **Print / Save as PDF** uses the browser print dialog (A4 print styles).
- **Selftest:** 5 new checks (78 in total):
  - W01 and W03 balance;
  - the demo copies balance after both players run to the end (rolled back);
  - simulated events leave the ledger unchanged;
  - building the report writes nothing.

## Notes

- Stack: .NET 8 minimal API, SQLite through `Microsoft.Data.Sqlite` (the only package), hand-written SQL. The frontend is vanilla HTML/CSS/JS in `wwwroot/`, with its own SVG charts:
  - `core.js`: helpers, modes, shell, router
  - `viz.js`: visuals
  - `live.js`: the SSE client, notifications, and the Cellar and Live board pages
  - `story.js`: Overview and Batch pages
  - `app.js`: the working pages
  - `readings.js`
  - `players.js`
  - `assistant.js`: the Marani Assistant panel (server side: `Assistant.cs`)
  - `fonts/` (Inter, OFL), `icons/` (Lucide, ISC)
- Operations and readings are append-only: SQLite triggers block updates and deletes. The only exception is a demo reset, which lifts the guard inside its own transaction. To correct a record, add a new operation with a note. Reset is the only exception.
- Two columns were added to the data model from the spec: `batches.grapes_kg` (the Dataset A2 intake, needed for the origin-completeness check) and `orders.source`. Helper tables: `demo_progress`, `demo_rows`, `maintenance` (the append-only guard), `sensor_readings` (stub endpoint `POST /api/sensors/reading`). Update 2 adds `readings` and the vessel columns `cellar_label`, `cellar_label_source`, `depth_cm`, `neck_d_cm`, `max_d_cm`, `known_volume_l`.
- Records typed into the app carry the source tag **Entered**. Live-demo readings are tagged **Entered (live demo)**.
- The QR code is replaced by a printable label that shows the vessel code in large text, as the spec allows.
- Georgian UI strings come from the spec and still need checking by a native speaker.
- Every DEMO ASSUMPTION is listed on the **Data sources** page.
