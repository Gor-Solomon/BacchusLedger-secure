# Refactor progress

Plan: [REFACTOR_PLAN.md](REFACTOR_PLAN.md). Branch: `refactor/layers` (local commits only, nothing pushed, `main` untouched).

| Step | What | Status |
|------|------|--------|
| 1 | Safety net: solution file, check script, API baseline, remove IDE files | done |
| 2 | Program + Startup + controllers + services (one project) | done |
| 3 | Repositories + remove static infrastructure | done |
| 4 | Domain classes (pure logic) | done |
| 5 | Split into Domain, Application, Infrastructure and Api projects | done |
| 6 | Tests, CI, docs, merge | todo |

## Step 1 notes

- `BacchusLedger.sln` (classic format) in the repo root, contains `src/BacchusLedger/BacchusLedger.csproj`.
- `scripts/check.ps1` (Windows PowerShell 5.1+) and `scripts/check.sh` (bash + curl) share `scripts/endpoints.txt`.
  They build the solution, start the app themselves on port 5199 with a fresh temporary DB (`RESET_ON_START=true`),
  a temporary signing key and blank AI keys, then compare status code + raw body of 48 GET endpoints
  (including `/api/selftest`) with `docs/refactor/baseline/`.
  - `scripts/check.ps1 -Baseline` / `scripts/check.sh --baseline` re-records the baseline.
  - `-Project <dir>` / `--project <dir>` points at the web project (needed after step 5: `src/BacchusLedger.Api`).
- Endpoints left out on purpose (clock, key or live simulator) are listed at the top of `scripts/endpoints.txt`.
- The baseline was verified to be stable: recorded once, then compared with two further fresh runs (PowerShell and bash), all identical.
- `.gitattributes` marks the baseline as binary so git never changes its line endings.

## Step 2 notes

- `Program.cs` is 8 lines; `Startup.cs` registers controllers (same snake_case JSON options as before) and one `AddXxxFeature()` per feature.
- Controllers: Health, Overview, Batches, Operations, Origins (farmers/plots/deliveries), Vessels, Cellar, Readings, Journals, Report, Seal,
  Live (incl. SSE stream and sensor stub), Assistant, Demo (incl. practice copy and live-demo batch), Settings (incl. reset), SelfTest.
- Every response is a named record in `Contracts/<Feature>/`. Services return `ServiceResult<T>` (ok / not found / bad request);
  `ServiceResultExtensions.ToActionResult` turns it into 200 / 404 / 400 `{ "error": ... }`.
- MVC is configured to behave like the former minimal API: no ProblemDetails for bare 404s, `null` written as JSON null.
- Extra safety net used during the refactor (not in the repo): a script that replays ~90 write/read requests (operations, readings,
  vessels, demo steps, seal/tamper/verify, live, settings, reset) against the original build and the refactored build and compares
  the responses (timestamps, hashes and signatures masked). It passed after every feature.
- Tiny behaviour differences on malformed input only (documented in REVIEW.md): `POST /api/sensors/reading` without `vessel`/`dip_cm`
  now returns 400 instead of a 500 crash; `POST /api/operations/preview` with `batch_id` sent as a string now works instead of a 500.

## Step 3 notes

- Done as one coordinated change (commit "Step 3: repositories, …"): the static classes called each other
  (`LossEngine.Analyze` was used by journals, demo, live, assistant, report and selftest), so converting one data area
  at a time would have needed a temporary static service locator. Build, `check.ps1` and the write-request replay all passed.
- `ISqliteConnectionFactory` / `SqliteConnectionFactory` (DB path from `StorageOptions`, env `DB_PATH`); `IUnitOfWork` /
  `SqliteUnitOfWork` gives one transaction per write (serialised like the old `Db.Write` lock) and rolled-back probes for the selftest.
  Repositories use the running transaction automatically (an `AsyncLocal` session), otherwise their own pooled connection.
- Repositories (interfaces in `Repositories/`, SQLite classes in `Infrastructure/Persistence/`): Winery, Batch, Vessel, Operation,
  Origin (farmers, plots, orders, deliveries, batch origins), Lab, Reading, Settings, LiveEvent, Demo, Maintenance (append-only
  exception for the demo reset), Seal, Journal, Diagnostics (selftest counts and append-only probes). SQL text was copied unchanged.
- `Seed` → `DatabaseSeeder` + `DatasetWriter` (shared with the demo players); schema → `DatabaseInitializer` (same `RESET_ON_START`).
  Append-only triggers are unchanged.
- Providers: `ISigningKeyProvider` (PEM file, still generates a DEMO key when missing), `IRangeConfigSource` (config/ranges.json),
  `IFactsSource` (knowledge/facts.md). AI: `IAiProvider` with `OpenAiProvider` / `GeminiProvider` on `IHttpClientFactory`.
- Options: `StorageOptions`, `AssistantOptions`, `LiveOptions`, bound in Startup from the same environment variables.
- `Console.WriteLine` replaced by `ILogger<T>`. `HttpClient` request logging is set to Warning in appsettings.json.
- Database errors reach services as `RepositoryException` (same message text as before), so services do not depend on SQLite.
- Left for step 4 (pure static classes): `LossEngine`, `Calibration`, `Readings`, `Ranges`, `LedgerReport`, `Money`, `Fmt`, `AnswerGuard`.

## Step 4 notes

One commit per calculator, each with build + `check.ps1` + write replay passing. `Domain/` uses nothing outside `Domain/`.

| Old static code | Domain class(es) |
|---|---|
| `Calibration` | `Vessels/VesselCalibration` (dip ↔ litres, dip chart), `Vessels/QvevriProfile` (A5 shape), `Vessels/CylinderTank`, `DipChartPoint` |
| `LossEngine.Replay` + types | `Losses/LossCalculator`, `StageNorms`, `CellarOperation`, `LossStage`, `LossCheck`, `LossResult`, `StageComparison` |
| `Readings` (formulas, validation) | `Readings/InitialBrix` (potential ABV, Brix → SG, progress %), `ReadingCalculator`, `ReadingValidator`, `ReadingInput`, `ReadingTypes` |
| `Ranges`, `RangeTracker` | `Ranges/RangeConfig`, `RangeRule`, `RangeTracker` |
| `LedgerReport.Build` | `Ledger/DoubleEntryBooking`, `LedgerEntry`, `LedgerAccount`, `LedgerBalance`, `Ledger`, `HarvestDelivery` |
| seal hashing (in `Seal`) | `Seal/HashChain` |
| `Money.OnePercentTable` | `Money/OnePercentLoss` |
| `Fmt` | `Common/DisplayTextExtensions` (`ToDisplayDate`, `ToDisplayDateTime`, `WithCellarLabel`) |
| `AnswerGuard` | `Assistant/AnswerTextExtensions` (`UsesOnlyNumbersFrom`, `SourceTags`) |

`LossEngine`, `Calibration`, `Readings`, `Ranges`, `Report`, `Money`, `Fmt` files are gone.

## Step 5 notes

- Projects: `src/BacchusLedger.Domain` (no references), `src/BacchusLedger.Application` (Domain + Microsoft.Extensions abstractions),
  `src/BacchusLedger.Infrastructure` (Application + Domain, ASP.NET Core shared framework, Microsoft.Data.Sqlite, QRCoder),
  `src/BacchusLedger.Api` (Application + Infrastructure; wwwroot, knowledge/, config/ copied to the output).
- Namespaces follow the folders: `BacchusLedger.Domain.*`, `BacchusLedger.Application.*`, `BacchusLedger.Infrastructure.*`, `BacchusLedger.Api.*`.
  The old `Dataset/` (seed data and setting defaults) is now `BacchusLedger.Domain.Dataset`.
- `AddApplication()` (Application/DependencyInjection.cs, one method per feature) and `AddInfrastructure()` (Infrastructure/DependencyInjection.cs)
  are called from Startup; the options are bound in Startup (rule 14), so `AddInfrastructure` takes no configuration parameter.
- Before the split, QR code generation (`IQrCodeRenderer`) and calls to our own API (`ILocalApiClient`) were put behind interfaces,
  so Application needs neither QRCoder nor `IHttpClientFactory`.
- Dockerfile restores/publishes `BacchusLedger.Api.csproj`; ENTRYPOINT is `BacchusLedger.Api.dll`. Same port, volume and env variables.
  `.dockerignore` now keeps `src/BacchusLedger.Api/knowledge/*.md`.
- `scripts/check.ps1` / `check.sh` default to `src/BacchusLedger.Api`. README local-run path updated.
