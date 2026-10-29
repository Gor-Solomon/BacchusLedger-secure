# Refactor review: Bacchus Ledger

This page explains what changed in the branch `refactor/layers`, for a reviewer who did not watch the work.

**In one sentence:** the code was moved from one big `Program.cs` and many static classes into controllers, services,
repositories and pure domain classes in four projects. The app does exactly the same as before.

- Nothing was pushed. `main` was not changed. All work is in local commits on `refactor/layers`.
- Plan: [REFACTOR_PLAN.md](REFACTOR_PLAN.md). Step-by-step notes: [progress.md](progress.md).

## How we know nothing changed

1. **API baseline.** Before any code change, `scripts/check.ps1 -Baseline` saved the answers of 48 GET endpoints
   (including `/api/selftest`) on a fresh database into `docs/refactor/baseline/`. After every feature,
   `scripts/check.ps1` started the new app on a fresh database and compared every answer **byte by byte**
   (status code and body).
2. **Write requests.** The baseline only has GET requests. So a second script (kept outside the repo) sent the same
   ~90 requests (record operations, readings, vessels, demo steps, seal / tamper / verify, live mode, settings, reset)
   to the **original** build and to the new build, and compared the answers. Times, hashes and signatures were masked.
   It matched after every feature.
3. **Tests** (new in step 6) and **Docker**: `docker compose up --build -d` works on port 8081.
4. The Docker volume still holds a database sealed by the **old** app; `/api/verify` on it still says "verified",
   so the hash chain format did not change.

## Final results

| Check | Result |
|---|---|
| `dotnet build BacchusLedger.sln` | 0 errors, 0 warnings |
| `/api/selftest` | pass, **81 / 81** checks |
| `scripts/check.ps1` (and `scripts/check.sh`) | **48 / 48** endpoints identical to the baseline |
| `dotnet test` | **81 / 81** tests passed (Domain 30, Api 51) |
| Write-request comparison with the original build | identical (~90 requests) |
| `docker compose up --build -d` | works on http://localhost:8081 (health 200, selftest 81/81) |

## Before → After (every old file)

| Old file (`src/BacchusLedger/`) | Where the code is now |
|---|---|
| `Program.cs` (846 lines, ~60 lambdas) | `Api/Program.cs` (11 lines), `Api/Startup.cs`, 17 controllers in `Api/Controllers/`, one service per feature in `Application/Services/`, response records in `Application/Contracts/<Feature>/` |
| `Db.cs` (static: connection, schema, settings, query helpers) | `Infrastructure/Persistence/`: `SqliteConnectionFactory`, `SqliteSession`, `SqliteRow`, `SqliteUnitOfWork`, `DatabaseInitializer` (schema, migrations, reset), `SqliteSettingsRepository`; default settings in `Domain/Dataset/SettingDefaults` |
| `Seed.cs` | data: `Domain/Dataset/SeedDataset` (+ `Seed*` records); writing: `Infrastructure/Persistence/DatabaseSeeder` and `Application/Services/DatasetWriter` (shared with the demo players) |
| `Calibration.cs` | `Domain/Vessels/VesselCalibration`, `QvevriProfile`, `CylinderTank`, `DipChartPoint` |
| `LossEngine.cs` (replay, analysis, `Fmt`, selftest) | `Domain/Losses/LossCalculator`, `StageNorms`, `CellarOperation`, `LossStage`, `LossCheck`, `LossResult`, `StageComparison`; data + origin/PDO/qvevri checks in `Application/Services/LossAnalysisService`; `Fmt` → `Domain/Common/DisplayTextExtensions`; selftest → `Application/Services/SelfTest/CoreSelfTest`, `LiveSelfTest` |
| `Readings.cs` | `Domain/Readings/InitialBrix`, `ReadingCalculator`, `ReadingValidator`, `ReadingInput`, `ReadingTypes`; warnings and saving in `Application/Services/ReadingService`; SQL in `SqliteReadingRepository` |
| `Journals.cs` | `Application/Services/JournalService` (columns, journals 5 and 6, CSV); SQL of journals 1, 2, 3, 4, 7, 8 in `Infrastructure/Persistence/SqliteJournalRepository` |
| `Live.cs` | `Infrastructure/Live/LiveSimulator`, `LiveSimulatorHost` (background service), `LiveHub`, `LiveSimulatorFactory`; `Application/Services/LiveBoardService`; `Application/Models/LiveEvent`, `LiveScenarios`; SQL in `SqliteLiveEventRepository` |
| `Ranges.cs` | `Domain/Ranges/RangeConfig`, `RangeRule`, `RangeTracker`; file reading in `Infrastructure/Providers/FileRangeConfigSource`; selftest → `RangeSelfTest` |
| `Report.cs` | `Domain/Ledger/DoubleEntryBooking`, `LedgerEntry`, `LedgerAccount`, `LedgerBalance`, `Ledger`, `HarvestDelivery`; `Application/Services/ReportService`; selftest → `LedgerSelfTest` |
| `Seal.cs` | `Domain/Seal/HashChain`; `Application/Services/SealService`; SQL in `SqliteSealRepository`; key file in `Infrastructure/Providers/PemSigningKeyProvider`; QR code in `QrCoderRenderer`; selftest → `SealSelfTest` |
| `DemoScript.cs` (+ `Money`) | `Application/Services/DemoService`; `SqliteDemoRepository`, `SqliteMaintenanceRepository`; `Money` → `Domain/Money/OnePercentLoss` (+ `Services/Mapping/OnePercentMappingExtensions`) |
| `Assistant.cs` | `Application/Services/AssistantService`; `Application/Services/Assistant/` (`QuickAnswerService`, `AssistantTools`, `AssistantTexts`, `IAiProvider`, …); `Infrastructure/Ai/OpenAiProvider`, `GeminiProvider`, `AiProviderBase` (with `IHttpClientFactory`); `Infrastructure/Providers/FileFactsSource`, `LocalApiClient`; `AssistantConfig` → `Application/Options/AssistantOptions`; `AnswerGuard` → `Domain/Assistant/AnswerTextExtensions`; selftest → `AssistantSelfTest` |
| `BacchusLedger.csproj` | `src/BacchusLedger.Api/BacchusLedger.Api.csproj`, plus new `BacchusLedger.Domain`, `BacchusLedger.Application`, `BacchusLedger.Infrastructure` projects |
| `wwwroot/`, `knowledge/`, `config/`, `Properties/`, `appsettings.json` | `src/BacchusLedger.Api/` (same content; `appsettings.json` has one extra line: HttpClient logs at Warning) |
| `.vs/` (IDE files) | removed from git, ignored |
| `Dockerfile` | same image, port, volume and env variables; builds `BacchusLedger.Api.csproj`, ENTRYPOINT `BacchusLedger.Api.dll` |

New things: `BacchusLedger.sln`, `scripts/check.ps1` + `check.sh` + `endpoints.txt`, `docs/refactor/baseline/`,
`tests/`, `.github/workflows/ci.yml`, `docs/adr/0001-modular-monolith.md`, README "Architecture" section.

## Final folder and project tree

```
BacchusLedger.sln
src/
├── BacchusLedger.Domain/            (no references)
│   ├── Assistant/   AnswerTextExtensions
│   ├── Common/      DisplayTextExtensions
│   ├── Dataset/     SeedDataset, Seed* records, SettingDefaults
│   ├── Ledger/      DoubleEntryBooking, Ledger, LedgerEntry, LedgerAccount, LedgerBalance, HarvestDelivery
│   ├── Losses/      LossCalculator, StageNorms, CellarOperation, LossStage, LossCheck, LossResult, StageComparison
│   ├── Money/       OnePercentLoss
│   ├── Ranges/      RangeConfig, RangeRule, RangeTracker
│   ├── Readings/    InitialBrix, ReadingCalculator, ReadingValidator, ReadingInput, ReadingTypes, ReadingCalculation
│   ├── Seal/        HashChain
│   └── Vessels/     VesselCalibration, QvevriProfile, CylinderTank, DipChartPoint
├── BacchusLedger.Application/       (→ Domain)
│   ├── Contracts/   96 response/request records, one folder per feature
│   ├── Extensions/  JsonElementExtensions, SettingsExtensions
│   ├── Models/      row records used between services and repositories (BatchRecord, VesselRecord, NewOperation, LiveEvent, …)
│   ├── Options/     StorageOptions, AssistantOptions, LiveOptions
│   ├── Providers/   ISigningKeyProvider, IRangeConfigSource, IFactsSource, IQrCodeRenderer, ILocalApiClient
│   ├── Repositories/ IUnitOfWork, I…Repository (14), IDatabaseInitializer, RepositoryException
│   ├── Services/    I…Service + …Service per feature; Assistant/, SelfTest/, Mapping/
│   └── DependencyInjection.cs   AddApplication() → AddOverviewFeature(), AddReadingsFeature(), …
├── BacchusLedger.Infrastructure/    (→ Application, Domain)
│   ├── Ai/          OpenAiProvider, GeminiProvider, AiProviderBase
│   ├── Live/        LiveSimulator, LiveSimulatorHost, LiveSimulatorFactory, LiveHub
│   ├── Persistence/ SqliteConnectionFactory, SqliteSession, SqliteRow, SqliteUnitOfWork, RecordFilter,
│   │                DatabaseInitializer, DatabaseSeeder, Sqlite…Repository (14)
│   ├── Providers/   PemSigningKeyProvider, FileRangeConfigSource, FileFactsSource, QrCoderRenderer, LocalApiClient
│   └── DependencyInjection.cs   AddInfrastructure()
└── BacchusLedger.Api/               (→ Application, Infrastructure)
    ├── Controllers/ 16 controllers + ServiceResultExtensions
    ├── wwwroot/  knowledge/  config/  Properties/
    ├── Program.cs   (11 lines)
    └── Startup.cs   (ConfigureServices / Configure)
tests/
├── BacchusLedger.Domain.Tests/   calibration, losses, fermentation, double-entry ledger, hash chain (30 tests)
└── BacchusLedger.Api.Tests/      WebApplicationFactory: endpoints, selftest, every baseline endpoint (51 tests)
```

## Examples

**Controller** (`Api/Controllers/ReadingsController.cs`): read the input, call one service method, return the result.

```csharp
[ApiController]
[Route("api/readings")]
public sealed class ReadingsController(IReadingService readings) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<ReadingListResponse>> List(long? batch, string? vessel, string? winery) =>
        this.ToActionResult(await readings.ListAsync(batch, vessel, winery));

    [HttpPost("preview")]
    public async Task<ActionResult<ReadingPreviewResponse>> Preview([FromBody] JsonElement body) => this.ToActionResult(await readings.PreviewAsync(body));

    [HttpPost]
    public async Task<ActionResult<ReadingsSavedResponse>> Save([FromBody] JsonElement body) => this.ToActionResult(await readings.SaveAsync(body));
}
```

**Service** (`Application/Services/OverviewService.cs`): loads data through repositories, uses the domain, returns a named record.

```csharp
public sealed class OverviewService(IBatchRepository batches, IWineryRepository wineries, ISettingsRepository settings, ILossAnalysisService losses) : IOverviewService
{
    public async Task<OverviewResponse> GetAsync()
    {
        var s = await settings.GetAsync();
        ...
        foreach (var b in await batches.ListAsync())
        {
            var r = await losses.AnalyzeAsync(b.Id, s, compare: false);
            ...
        }
        var onePercent = new OnePercentLoss(s.Number("value_per_litre_usd"), s.Number("litres_per_tonne")).ToRows(await wineries.ListBySizeAsync());
        return new OverviewResponse(kpi, flags, onePercent, s);
    }
}
```

**Repository** (`Infrastructure/Persistence/SqliteOperationRepository.cs`): the only place with SQL; the connection comes from the
injected factory (inside a unit of work it is the shared transaction).

```csharp
public sealed class SqliteOperationRepository(ISqliteConnectionFactory connections) : IOperationRepository
{
    ...
    public async Task<string?> FirstDateAsync(long batchId)
    {
        await using var db = await connections.OpenAsync();
        return (await db.QueryAsync("SELECT MIN(date) d FROM operations WHERE batch_id=$1", r => r.Str("d"), batchId))[0];
    }
}
```

**Domain class** (`Domain/Readings/InitialBrix.cs`): plain data in, result out. No SQL, no HTTP, no files.

```csharp
public sealed record InitialBrix(double Brix)
{
    public double PotentialAbv => Math.Round(Brix * 0.59, 2, MidpointRounding.AwayFromZero);
    public double SpecificGravity => 1 + Brix / (258.6 - Brix / 258.2 * 227.1);
    public double ProgressPct(double abvReached) => Math.Round(abvReached / PotentialAbv * 100, 1, MidpointRounding.AwayFromZero);
}
```

## Skipped, unfinished or different from the plan (and why)

1. **No merge into `main`, no push** (plan step 6.7). Your instructions for this session override the plan: you will review and decide.
   Because nothing was pushed, the GitHub Actions workflow has **not run on GitHub yet**; its steps (restore, build, test, `check.sh`, docker build) were run locally.
2. **Step 3 is one commit**, not one commit per data area. The old static classes called each other (`LossEngine.Analyze` was used by
   journals, demo, live, assistant, report and selftest), so a half-converted state would have needed a temporary static service locator.
   All checks passed on that commit; steps 2 and 4 are one commit per feature / per calculator.
3. **Responses that are not one fixed record.** Every response is built from named records, but a few have a varying shape and are declared as `object` / a map:
   - journal rows are `column key → value` maps, because each of the 8 journals has different columns (the CSV export uses the same keys);
   - `/api/settings` (and `settings` inside overview / batch) is a `string → string` map: it is a key/value store;
   - overview `flags` mixes `StageFlag` and `CheckFlag`; the demo card `extra` is one of 4 records; `/api/report` returns `ReportResponse` or `VesselReportResponse`; `/api/vessels/{code}/litres` returns one of 2 records.
   Changing these to one fixed shape would change the JSON, which the plan forbids.
4. **`AddInfrastructure()` has no `configuration` parameter.** Rule 14 says options are bound in Startup, so Startup binds `StorageOptions`,
   `AssistantOptions` and `LiveOptions` from the same environment variables as before, and the infrastructure only registers services.
5. **Tiny behaviour differences, only for broken input:** `POST /api/sensors/reading` without `vessel` or `dip_cm` now answers 400 instead of crashing with 500;
   `POST /api/operations/preview` with `batch_id` sent as a string now works instead of crashing with 500.
   The AI settings are read once at start instead of on every request (environment variables cannot change while the app runs anyway).
6. **Static classes that remain** are extension methods or constants: `SeedDataset`, `SettingDefaults`, `ReadingTypes`, `LiveScenarios` (data),
   `AssistantTexts` (the English/Georgian texts plus two tiny text helpers), and the `…Extensions` classes.
   A few private nested types stay inside their class (`LiveSimulator.VState`, `RangeTracker.Change`, `DemoService.StepDef`).
7. **Seed data lives in `Domain/Dataset`.** It is pure data used by the seeder (infrastructure), the services and the tests, so the innermost project is the only place all of them can reach.
8. **The write-request comparison script is not in the repo.** It needs the original build next to the new one; the plan's safety net (`check.ps1`, GET only) and the new API tests are in the repo.
9. **Live simulator values are random**, so the live endpoints are compared only by status code and shape, not by values (as before, they were excluded from the baseline).

## How to check it yourself

```
dotnet build BacchusLedger.sln
dotnet test BacchusLedger.sln
powershell -File scripts/check.ps1        # or: bash scripts/check.sh
docker compose up --build -d              # then open http://localhost:8081
git log --oneline main..refactor/layers   # ~30 local commits, one per feature / step
```
