# Review: persistence moved to EF Core (step 7)

This page explains what changed in the branch `refactor/ef-core`, for a reviewer who did not watch the work.

**In one sentence:** all hand-written SQL in the repositories was replaced by EF Core 8 (LINQ on a `DbContext`), the schema is now an
EF Core migration, and the app answers exactly as before.

- Nothing was pushed. `main` was not changed. All work is in local commits on `refactor/ef-core` (branched from `main`).
- Why EF Core, and where raw SQL is still used: [docs/adr/0002-ef-core.md](../adr/0002-ef-core.md). Step notes: [progress.md](progress.md).

## How we know nothing changed

After every repository: `dotnet build` (0 errors, 0 warnings), `dotnet test`, `scripts/check.ps1` (48 GET endpoints compared byte by byte
with `docs/refactor/baseline/`) and a replay of ~90 write requests compared with the original build.

Old databases:
1. A database file made by the `main` build (writes, a demo replay and 3 sealed batches) was opened by the EF Core build. It was
   recorded as migration `InitialSchema` (baseline), kept all its data, and all 3 seals still say **verified** with the same entry counts.
2. The real Docker volume `bacchus-secure-data` (made by the original app and used by every build since) was upgraded the same way: batches,
   readings and deliveries counts, all 7 seals (**verified**, same final hashes) and the overview are identical before and after.

## Before → after, per repository

| Before (hand-written SQL) | After (EF Core) | Notes |
|---|---|---|
| `SqliteWineryRepository` | `EfWineryRepository` | LINQ projections to `WineryDto`. |
| `SqliteSettingsRepository` | `EfSettingsRepository` | "INSERT OR REPLACE" = `ExecuteUpdate`, insert when nothing was updated. |
| `SqliteBatchRepository` | `EfBatchRepository` | Batch + winery join; `LIKE '%copy%'` kept as `EF.Functions.Like` (same case rules). |
| `SqliteVesselRepository` | `EfVesselRepository` | Label search keeps SQLite's `lower(trim(…))`; "batches that used the vessel" is one `UNION ALL` + `GROUP BY` query. |
| `SqliteOperationRepository` | `EfOperationRepository` | Left joins for the vessel codes; new `ListForBatchesAsync` (many batches, one query). |
| `SqliteOriginRepository` | `EfOriginRepository` | Sums and batch lists as one grouped query each, joined in memory, so "no deliveries" stays `null` as before; new `ListOriginChecksAsync(ids)`. |
| `SqliteLabRepository` | `EfLabRepository` | Initial sugar keeps the correlated `MIN(date)` subquery. |
| `SqliteReadingRepository` | `EfReadingRepository` | "First fermentation reading" = two `MIN` queries instead of one `UNION`. |
| `SqliteLiveEventRepository` | `EfLiveEventRepository` | Trim of old readings = read the newest id, then `ExecuteDelete`. |
| `SqliteDemoRepository` | `EfDemoRepository` | `EnsureTablesAsync` removed (the migration creates the tables). |
| `SqliteMaintenanceRepository` | `EfMaintenanceRepository` | Still a `maintenance` row inside the demo-reset transaction (the triggers allow the delete). |
| `SqliteSealRepository` | `EfSealRepository` | `EnsureTablesAsync` removed. A sealed row is read as an entity and turned into the same column → value map (see "Seal" below). |
| `SqliteJournalRepository` | `EfJournalRepository` | Rows built in the same column order; the readings note uses SQLite's own number text (`CAST(value AS TEXT)`, `22.0` stays `"22.0"`). |
| `SqliteDiagnosticsRepository` | `EfDiagnosticsRepository` | Counts with LINQ; the snapshot and the append-only probe keep raw SQL on purpose (below). |
| `SqliteUnitOfWork` + `SqliteConnectionFactory` (`AsyncLocal`) | `EfUnitOfWork` | DbContext transaction; same single write lock (`SqliteWriteGate`); probes roll back and clear the change tracker. |
| `DatabaseInitializer` (CREATE TABLE text + ALTER TABLE list + DROP TABLE reset) | `DatabaseInitializer` with `MigrateAsync` | Baseline of old databases; reset = migrate down to nothing and up again. |
| `SqliteSession`, `SqliteRow`, `RecordFilter`, `ISqliteConnectionFactory` | deleted | Not needed with EF Core. |

New in Infrastructure: `BacchusLedgerDbContext`, 21 entities (`Persistence/Entities`), 21 configurations (`Persistence/Configurations`),
the migration `InitialSchema` (`Persistence/Migrations`), `DesignTimeDbContextFactory` (for `dotnet ef`), `DbContextExtensions`.

## One example, before and after

Before (`SqliteOperationRepository.ListForBatchAsync`): SQL text and hand mapping by column name.

```csharp
await using var db = await connections.OpenAsync();
return await db.QueryAsync("""
    SELECT o.*, vf.code AS from_code, vt.code AS to_code, vf.cellar_label AS from_label, vt.cellar_label AS to_label FROM operations o
    LEFT JOIN vessels vf ON vf.id=o.from_vessel_id LEFT JOIN vessels vt ON vt.id=o.to_vessel_id
    WHERE o.batch_id=$1 ORDER BY o.date, o.id
    """, r => new CellarOperation
{
    Id = r.Long("id"), Date = r.Str("date")!, Type = r.Str("type")!,
    FromCode = r.Str("from_code"), ToCode = r.Str("to_code"), ...
}, batchId);
```

After (`EfOperationRepository`): typed LINQ, used both for one batch and for many batches at once.

```csharp
public Task<List<CellarOperation>> ListForBatchAsync(long batchId) =>
    WithVessels(db.Operations.AsNoTracking().Where(o => o.BatchId == batchId)).Select(x => x.Op).ToListAsync();

IQueryable<OperationWithBatch> WithVessels(IQueryable<OperationEntity> operations) =>
    from o in operations
    from vf in db.Vessels.Where(v => v.Id == o.FromVesselId).DefaultIfEmpty()
    from vt in db.Vessels.Where(v => v.Id == o.ToVesselId).DefaultIfEmpty()
    orderby o.Date, o.Id
    select new OperationWithBatch { BatchId = o.BatchId ?? 0, Op = new CellarOperation { Id = o.Id, FromCode = vf.Code, ToCode = vt.Code, ... } };
```

## Raw SQL that remains, and why

| Where | Why |
|---|---|
| `InitialSchema` migration: 4 triggers (`operations` and `readings` are append-only) | EF Core has no model for triggers. |
| `EfDiagnosticsRepository.SnapshotAsync`: `sqlite_master` and `COUNT(*)`, `MAX(rowid)` per table | The selftest's "nothing was written" check must cover every table; `rowid` has no model. Table names are checked (letters and `_` only) before going into the SQL. |
| `EfDiagnosticsRepository.TryChangeAppendOnlyAsync`: 4 fixed statements | It must hit the real triggers. |
| `DatabaseInitializer`: EF Core's own `__EFMigrationsHistory` scripts | Generated by EF Core (not hand-written), to baseline an old database. |

## Seal (hash chain)

The seal hashes every column of a sealed row as JSON. With EF Core a row is read as an entity and turned into the same
`column name → value` map, with the same value types as the old reader gave (INTEGER → `long`, REAL → `double`, TEXT → `string`).
`SealCompatibilityTests` writes rows with plain SQL (like the old code), reads them both ways, and checks that every value, every type and the
hash are identical, for all four sealed tables. Old seals on the `main` database and on the Docker volume still verify.

## N+1 queries

The overview, the batch list, the vessels page, the cellar map, journals 5 and 6, the assistant's "above normal" answer and the live simulator
used to run about 3 queries per batch. They now call `AnalyzeManyAsync`, which loads batches, operations and origins with one query each.
Measured SQL commands per request now: overview 6, batch list 5, vessels 6, journal 5 (the same however many batches there are).

## Skipped, different from the request, or worth knowing

1. **Branch:** `refactor/ef-core` was created from `main`, which already contains the layered refactor (it was merged there before this step).
2. **Interfaces:** `EnsureTablesAsync` was removed from `ISealRepository` and `IDemoRepository` (as asked). Three methods were *added* to avoid
   N+1 queries (`ILossAnalysisService.AnalyzeManyAsync`, `IOperationRepository.ListForBatchesAsync`, `IOriginRepository.ListOriginChecksAsync(ids)`);
   no existing method changed.
3. **Lifetimes:** the DbContext, repositories and application services are scoped now (they were singletons). The background live simulator opens
   a DI scope for each step (`LiveSimulator.Use(...)` binds it to that scope's repositories); the selftest's simulator uses the request scope,
   so it still runs inside the rolled-back probe.
4. **SQLite settings:** foreign keys and the 5 s lock wait are set in the connection string (`Foreign Keys=True;Default Timeout=5`) instead of
   `PRAGMA` statements, so no raw SQL is needed for them. WAL was never used and is still not used.
5. **Schema names:** columns, types, defaults, keys, unique constraints, foreign keys, CHECKs and triggers are identical to the old schema (checked
   with `PRAGMA table_info / index_list / foreign_key_list` on both). Only *names* differ: unique indexes are `IX_…` instead of `sqlite_autoindex_…`,
   and constraints are named (`FK_…`, `CK_…`). This changes nothing in behaviour.
6. **Very old databases:** the baseline expects the tables the app had on `main`. A database older than the vessel-column update (cellar labels,
   qvevri measurements) is not upgraded automatically; open it once with the `main` build first.
7. **Reset** keeps the database file: it migrates down to nothing and up again (before: a list of `DROP TABLE`).
8. Repositories were renamed from `Sqlite…Repository` to `Ef…Repository`.

## Final check results

| Check | Result |
|---|---|
| `dotnet build BacchusLedger.sln` | 0 errors, 0 warnings |
| `dotnet test` | **98 / 98** passed (Domain 30, Infrastructure 8, Api 60) |
| `scripts/check.ps1` | **48 / 48** endpoints identical to the baseline, selftest **81 / 81** |
| Write-request replay vs original build | identical (~90 requests) |
| Database made by `main` | opened, baselined, data kept, 3 / 3 seals **verified** |
| `docker compose up --build -d` | works on http://localhost:8081; existing volume upgraded, data identical, 7 / 7 seals **verified** |
