# Refactor progress

Plan: [REFACTOR_PLAN.md](REFACTOR_PLAN.md). Branch: `refactor/layers` (local commits only, nothing pushed, `main` untouched).

| Step | What | Status |
|------|------|--------|
| 1 | Safety net: solution file, check script, API baseline, remove IDE files | done |
| 2 | Program + Startup + controllers + services (one project) | done |
| 3 | Repositories + remove static infrastructure | in progress |
| 4 | Domain classes (pure logic) | todo |
| 5 | Split into Domain, Application, Infrastructure and Api projects | todo |
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
