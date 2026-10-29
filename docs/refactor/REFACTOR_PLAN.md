# Bacchus Ledger: refactor plan (clean OOP, controllers, DI, layers)

This file is the plan for the Claude CLI. Do ONE step per session. Each step ends with a commit.
The app must behave exactly the same after every step. This is a restructuring, not a rewrite.

---

## RULES (apply to every step)

### Safety
1. Work on branch `refactor/layers`. Never commit directly to `main` until step 6 is done.
2. Behaviour must not change: same routes, same JSON field names and values, same UI, same database schema, same seed data, same Docker port.
3. After every step (and after every feature inside a step):
   - `dotnet build BacchusLedger.sln` → 0 errors, no new warnings
   - start the app, run `scripts/check.ps1` (created in step 1) → selftest identical to baseline AND API snapshots identical to baseline
   - `docker compose up --build -d` → app works on the same port as before
   - commit
4. If a step becomes too big for this session: finish the current feature, commit, then STOP and write in `docs/refactor/progress.md` what is done and what is left. The next session continues from there.
5. No new frameworks: no MediatR, AutoMapper, EF Core, Dapper, Kafka. Plain ASP.NET Core 8 + Microsoft.Data.Sqlite.
6. Never put API keys in code, logs, docs or git. `.env` stays gitignored.

### Code style (the target)
7. **Program.cs** only creates the builder, calls `Startup`, builds and runs. Under 20 lines.
8. **Startup.cs** has `ConfigureServices(IServiceCollection)` (all DI registrations, grouped by feature through extension methods like `services.AddReadingsFeature()`) and `Configure(WebApplication)` (middleware, static files, `MapControllers`).
9. **Controllers**: one per feature, `[ApiController]`, `[Route(...)]`, constructor injection only.
   An action is max ~10 lines: validate input → call ONE service method → return a result.
   No SQL, no calculations, no loops, no anonymous objects in controllers.
10. **Services**: interface + class (`IReadingService` / `ReadingService`), constructor injection, no static state, never `new` a dependency.
11. **Repositories**: all SQL lives only in repository classes (`IBatchRepository` / `SqliteBatchRepository`, etc.). They get connections from an injected `ISqliteConnectionFactory`. Use async for DB and HTTP.
12. **DTOs**: every response is a named `record` in a `Contracts` folder. No anonymous objects and no `Dictionary<string, object>` in responses. Use `[JsonPropertyName("...")]` where needed so the JSON stays byte-for-byte the same as the baseline (for example snake_case fields like `winery_code`).
13. **Static classes**: allowed only for extension methods and constants. Pure calculations become small classes in the domain (for example `QvevriCalibration`, `LossCalculator`, `FermentationMath`, `DoubleEntryBooking`, `HashChain`).
14. Settings and env variables use the Options pattern (`AssistantOptions`, `LiveOptions`, `StorageOptions`), bound in Startup.
15. Logging with `ILogger<T>`, no `Console.WriteLine`.
16. One class per file, file name = class name, namespace = folder path.
17. No banner comments like `// ------- overview -------`. Use XML doc comments on public interfaces only where they add meaning.

---

## STEP 1: safety net (no C# changes)

1. Create and switch to branch `refactor/layers`.
2. Remove IDE files from git: `git rm -r --cached src/BacchusLedger/.vs`. Add to `.gitignore`: `.vs/`, `*.user`, `*.suo`, `TestResults/`.
3. Create `BacchusLedger.sln` in the repo root (classic .sln format, works with the .NET 8 SDK and Visual Studio). Add the existing project.
4. Create `scripts/check.ps1` (and `scripts/check.sh` with the same logic):
   - calls `/api/selftest` and saves the result
   - calls every GET endpoint that returns JSON and does not depend on the clock or the live simulator (find them in Program.cs; skip SSE streams, live events and anything with timestamps of "now"); for endpoints with an id or code, use the seeded values (for example batch W01-B1-2025)
   - with `-Baseline`: saves everything into `docs/refactor/baseline/`
   - without it: compares with the baseline and prints PASS or a clear diff per endpoint, and exits with code 1 on any difference
   - use a fresh database (`RESET_ON_START=true` or a temporary DB path) so the data is always the same
5. Run `scripts/check.ps1 -Baseline` and commit the baseline files.
6. README: the "Run" section says port 8080, but `docker-compose.yml` maps 8081. Make the README match `docker-compose.yml`.
7. Create `docs/refactor/progress.md` with the step list and mark step 1 as done.
8. Commit: `Step 1: solution file, check script, API baseline, remove IDE files`.

---

## STEP 2: Program + Startup + controllers + services (one project)

Goal: replace the minimal-API lambdas with controllers and injected services. Still one project. Work feature by feature.

1. Create `Startup.cs` and reduce `Program.cs` to the builder / Startup / run lines (rules 7–8). Register controllers (`AddControllers` with the same JSON options the app uses today, so the casing stays the same).
2. Create folders: `Controllers/`, `Services/`, `Contracts/`.
3. For each feature, in this order: Overview → Batches → Vessels → Readings → Journals → Report/PDF → Seal/verify → Live (including the SSE stream) → Assistant → Demo → Settings → SelfTest:
   - create `XxxController` with the same routes
   - move the handler logic into `IXxxService` / `XxxService`
   - replace anonymous response objects with records in `Contracts/` (rule 12)
   - for now the services may still call the existing static classes (`Db`, `LossEngine`, …). That is fixed in step 3.
   - delete the old lambda from Program.cs
   - run `scripts/check.ps1` → must PASS, then commit `Step 2: <Feature> controller and service`
4. The background live simulator must be registered in Startup as a hosted service and keep working.
5. At the end, Program.cs has no `Map...` calls left (only `MapControllers` in Startup). Update `progress.md`.

---

## STEP 3: repositories + remove static infrastructure

Goal: all SQL moves into repositories; the static `Db`, `Seed`, settings and file access become injected classes.

1. Create `ISqliteConnectionFactory` / `SqliteConnectionFactory` (DB path from `StorageOptions`, same env variable `DB_PATH` as today).
2. Create repositories, one per data area, for example: `IBatchRepository`, `IVesselRepository`, `IOperationRepository`, `IReadingRepository`, `IJournalRepository`, `ILiveEventRepository`, `ISettingsRepository`, `IAuditRepository` (use the real tables you find in `Db.cs` / `Seed.cs`).
3. Move every SQL statement from services and static classes into those repositories. Keep the append-only protections (triggers or checks) exactly as they are.
4. `Seed` becomes `DatabaseSeeder` (an injected class run at startup, same `RESET_ON_START` behaviour). The schema creation becomes `DatabaseInitializer`.
5. File-based things become injected providers: `IFactsSource` (knowledge/facts.md), `IRangeConfigSource` (config/ranges.json), `ISigningKeyProvider` (the PEM file; still generates a demo key when missing).
6. AI calls: `IAiProvider` with `OpenAiProvider` and `GeminiProvider` using `IHttpClientFactory`, options from env variables exactly as today.
7. Work one data area at a time, run `scripts/check.ps1` and commit after each one.
8. At the end: no `static class Db`, no SQL outside repositories. Update `progress.md`.

---

## STEP 4: domain classes (pure logic)

Goal: calculations become small, testable classes with no SQL, no HTTP, no files.

1. Create folder `Domain/` with entities / value objects and calculators, for example:
   `QvevriProfile`, `VesselCalibration` (dip → litres), `LossCalculator` + `StageNorm`, `FermentationMath` (potential ABV, Brix → SG, progress %), `RangeRule`, `DoubleEntryBooking`, `HashChain`, `Money`.
2. These classes receive plain data and return results. Services load data through repositories, call the domain, and map to DTOs.
3. One calculator at a time → `scripts/check.ps1` → commit.
4. The old `LossEngine`, `Calibration`, `Readings` static classes disappear when they are empty. Update `progress.md`.

---

## STEP 5: split into projects

Move the folders into projects. This step should be mostly moving files and fixing namespaces.

```
src/BacchusLedger.Domain          → Domain/ (references nothing)
src/BacchusLedger.Application     → Services/, Contracts/, repository and provider interfaces (references Domain)
src/BacchusLedger.Infrastructure  → Sqlite repositories, connection factory, seeder, initializer, AI providers, file providers, live simulator (references Application + Domain)
src/BacchusLedger.Api             → Program, Startup, Controllers, wwwroot, knowledge/, config/ (references Application + Infrastructure)
```

1. Add all projects to the sln. Each project gets a `DependencyInjection.cs` with an extension method (`AddApplication()`, `AddInfrastructure(configuration)`) that Startup calls.
2. Move package references to the project that uses them (Sqlite and QRCoder → Infrastructure).
3. Keep `knowledge/**` and `config/**` copied to the Api output.
4. Update the Dockerfile and `docker-compose.yml` for the new paths. Same image behaviour, same port, same volume, same env variables. If the DLL name changes, update the ENTRYPOINT.
5. Check the dependency rule: Domain → nothing; Application → Domain; Infrastructure → Application + Domain; Api → Application + Infrastructure.
6. `scripts/check.ps1` → PASS, docker → works, commit `Step 5: split into Domain, Application, Infrastructure and Api projects`.

---

## STEP 6: tests, CI, docs, merge

1. `tests/BacchusLedger.Domain.Tests` (xUnit): qvevri calibration, the W01 losses (450 L total and the 5 step values), the W03 flagged stages, `potential_abv(22.0) = 12.98`, `brix_to_sg(22.0) ≈ 1.0920`, double-entry balance, hash-chain verify.
2. `tests/BacchusLedger.Api.Tests` (xUnit + `WebApplicationFactory`): a few endpoint tests (overview, a batch, selftest returns all pass).
3. `.github/workflows/ci.yml`: restore, build, test, docker build. No secrets.
4. README top: an "Architecture" section with a Mermaid diagram of the 4 projects, the CI badge, and "Why a modular monolith, not microservices": small team, one SQLite DB, offline use in a cellar; feature boundaries are ready to split into services later if real load needs it.
5. `docs/adr/0001-modular-monolith.md` with the same reasoning.
6. Keep `/api/selftest` (the demo uses it).
7. Everything green → merge `refactor/layers` into `main`, push. Mark everything done in `progress.md`.
