# Refactor progress

Plan: [REFACTOR_PLAN.md](REFACTOR_PLAN.md). Branch: `refactor/layers`.

| Step | What | Status |
|------|------|--------|
| 1 | Safety net: solution file, check script, API baseline, remove IDE files | done |
| 2 | Program + Startup + controllers + services (one project) | todo |
| 3 | Repositories + remove static infrastructure | todo |
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
