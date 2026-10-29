# Refactor progress

Plan: [REFACTOR_PLAN.md](REFACTOR_PLAN.md). Steps 1–6: branch `refactor/layers` (now merged into `main` by the owner). Step 7: branch `refactor/ef-core` (local commits only, nothing pushed, `main` untouched).

| Step | What | Status |
|------|------|--------|
| 1 | Safety net: solution file, check script, API baseline, remove IDE files | done |
| 2 | Program + Startup + controllers + services (one project) | done |
| 3 | Repositories + remove static infrastructure | done |
| 4 | Domain classes (pure logic) | done |
| 5 | Split into Domain, Application, Infrastructure and Api projects | done |
| 6 | Tests, CI, docs, merge | done (the owner merged it into `main`) |
| 7 | Persistence on EF Core (DbContext, migrations, LINQ repositories) | done |
| 8 | Transactional outbox for sensor and live events (branch `feature/outbox`) | done |

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

## Step 6 notes

- `tests/BacchusLedger.Domain.Tests` (xUnit, 30 tests): qvevri and cylinder calibration, W01 losses (450 L total and the 5 step values),
  the two W03 flagged stages, `potential_abv(22.0) = 12.98`, `brix_to_sg(22.0) ≈ 1.0920`, reading validation, double-entry balance,
  hash-chain verify / tamper detection.
- `tests/BacchusLedger.Api.Tests` (xUnit + `WebApplicationFactory`, 51 tests): overview, batch W01, 404 for an unknown batch,
  selftest returns all pass (81/81), and every baseline endpoint compared byte by byte in process.
  The tests replace `ILocalApiClient` with a client on the in-memory test server, so the selftest's own HTTP checks also run.
  `Program.cs` got `public partial class Program;` so the tests can host it.
- `.github/workflows/ci.yml`: restore, build, test, `scripts/check.sh`, docker build. No secrets. Not run on GitHub yet (nothing pushed).
- README: "Architecture" section at the top (Mermaid diagram of the 4 projects, CI badge, why a modular monolith).
- `docs/adr/0001-modular-monolith.md`.
- `/api/selftest` kept.
- **Not done on purpose:** merge `refactor/layers` into `main` and push. The instructions for this session say never push, never merge,
  never touch `main`; the owner reviews first. See [REVIEW.md](REVIEW.md).

## Final state (all on `refactor/layers`, local commits only)

- `dotnet build BacchusLedger.sln`: 0 errors, 0 warnings.
- `dotnet test`: 81 / 81 passed (Domain 30, Api 51).
- `scripts/check.ps1` and `scripts/check.sh`: 48 / 48 endpoints identical to the baseline, selftest 81 / 81.
- `docker compose up --build -d`: works on http://localhost:8081.
- Reviewer guide: [REVIEW.md](REVIEW.md).

## Step 7 notes (EF Core)

Branch `refactor/ef-core` from `main`. Reviewer guide: [REVIEW-EF.md](REVIEW-EF.md). Decision record: [../adr/0002-ef-core.md](../adr/0002-ef-core.md).

Order of commits (each one: build 0 warnings, `dotnet test`, `scripts/check.ps1`, write-request replay):
1. EF Core packages (`Microsoft.EntityFrameworkCore.Sqlite` / `.Design` 8.0.11, Infrastructure only; `dotnet-ef` 8.0.11 local tool),
   `BacchusLedgerDbContext`, 21 entities, 21 configurations, migration `InitialSchema` (+ triggers), `MigrateAsync` + baseline of old
   databases in `DatabaseInitializer`, `EfUnitOfWork`, scoped lifetimes, live simulator with a DI scope per step. The old SQL repositories ran
   on the DbContext's connection and transaction (a temporary bridge) until each one was converted.
2. One commit per repository: Winery, Settings, Batch, Vessel, Operation, Lab, Reading, LiveEvent, Origin, Demo, Maintenance, Seal, Journal, Diagnostics.
3. No N+1: `AnalyzeManyAsync` + "many batches" repository methods (overview, batch list, vessels, cellar, journals 5/6, assistant, simulator).
4. Cleanup: `SqliteSession`, `SqliteRow`, `RecordFilter`, `ISqliteConnectionFactory`, `SqliteConnectionFactory`, `SqliteUnitOfWork`, all
   `EnsureTablesAsync` deleted.
5. Tests: `WritePathTests` (API: operation, reading, append-only update/delete blocked, demo run + reset, seal / tamper / verify) and the new
   `tests/BacchusLedger.Infrastructure.Tests` (migration on an empty database, initializer seed, old-schema database baseline, reset, seal compatibility).
6. EF Core logs at Warning; ADR 0002, README "Architecture" (EF Core, migrations, how to add a migration), REVIEW-EF.md.

Nothing had to be undone; no repository was skipped.

Proofs of compatibility:
- Schema of a fresh migration compared with a database made by `main`: same columns (type, NOT NULL, default, key), unique keys, foreign keys, CHECKs, triggers.
- A database made by the `main` build (with writes and 3 seals): opened, baselined, all data kept, 3 / 3 seals verified, same selftest result as `main`.
- The Docker volume `bacchus-secure-data`: counts, overview and all 7 seals (same final hashes) identical before and after the upgrade.

Final state: build 0 errors / 0 warnings; tests 98 / 98 (Domain 30, Infrastructure 8, Api 60); `check.ps1` 48 / 48 identical, selftest 81 / 81;
Docker on http://localhost:8081.

## Step 8 notes (transactional outbox)

Branch `feature/outbox` from `main` (local commits only, nothing pushed, `main` untouched). Reviewer guide: [REVIEW-OUTBOX.md](REVIEW-OUTBOX.md).
Decision record: [../adr/0003-transactional-outbox.md](../adr/0003-transactional-outbox.md).

Order of commits (each one: build 0 warnings, `dotnet test`, `scripts/check.ps1` 48 / 48 and selftest 81 / 81):
1. Events, `IOutbox` / `EfOutbox` (same transaction), migration `AddOutbox` (`outbox_messages`, `processed_messages`, index on status + next_attempt_at).
2. Producer: `POST /api/sensors/reading` saves the reading and `SensorReadingReceived` together (the direct SSE push was kept until step 3).
3. Worker + dispatcher + handlers: `OutboxProcessor`, `OutboxBatchProcessor`, `EventDispatcher`, `RetryPolicy`, `InProcessMessagePublisher`,
   `RangeRulesHandler`, `LiveStreamHandler`; simulator rules split from the readings (`ProduceAsync` / `ApplyRules`).
4. Simulator through the outbox (`LiveReadingSimulated` per vessel per step, `RangeAlertRaised` for notifications).
5. Visibility: `/api/outbox/stats`, `/api/outbox/failed`, `/api/outbox/failed/{id}/retry`, Expert-only "Event pipeline" card, `OUTBOX_FAIL_RATE`.
6. Tests: 6 unit tests (Infrastructure) and 5 integration tests (Api).
7. Docs: ADR 0003, README "Event pipeline", REVIEW-OUTBOX.md, this file.

Follow-up commit: `RangeRulesHandler` also handles `SensorReadingReceived` (range rules on sensor readings, own alert state, tagged SENSOR);
test (a) now shows a real notification after 3 readings above the range.

Nothing had to be undone. Final checks: build 0 errors / 0 warnings; tests 109 / 109 (Domain 30, Infrastructure 14, Api 65);
`check.ps1` 48 / 48 identical, selftest 81 / 81; Docker on http://localhost:8081 with the existing volume (data and 7 / 7 seals identical,
migration applied); browser: Live alerts as before, `OUTBOX_FAIL_RATE=0.5` shows retries and failed messages, set back to 0.
