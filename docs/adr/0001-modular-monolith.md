# ADR 0001: A modular monolith, not microservices

Status: accepted (refactor branch `refactor/layers`)

## Context

Bacchus Ledger keeps one cellar record per winery, from the farmer's delivery to the bottle. It has to run in a cellar
with no internet, on one small machine, maintained by a small team. All data lives in one SQLite file, and the record is
append-only and sealed with a hash chain over rows of several tables (deliveries, operations, lab, readings).

The code had grown into one project with an 850-line `Program.cs` of minimal-API lambdas and static classes that mixed
SQL, calculations and HTTP.

## Decision

Keep one deployable app (one process, one Docker image, one SQLite database) and split the code into four projects with a
strict dependency rule:

| Project | Holds | References |
|---|---|---|
| `BacchusLedger.Domain` | pure calculations: calibration, loss calculator, fermentation math, ranges, double-entry booking, hash chain | nothing |
| `BacchusLedger.Application` | services, request/response records (`Contracts`), repository and provider interfaces | Domain |
| `BacchusLedger.Infrastructure` | SQLite repositories, unit of work, seeder, AI providers, file providers, live simulator | Application, Domain |
| `BacchusLedger.Api` | `Program`, `Startup`, controllers, static web files | Application, Infrastructure |

Inside the projects the code is grouped by feature (overview, batches, vessels, readings, journals, report, seal, live,
assistant, demo, settings, selftest), with one controller and one service per feature.

## Why not microservices

- **Small team**: one build, one deploy, one log; no service discovery, no network contracts to version.
- **One SQLite database**: the seal chains rows of several tables in one transaction, and the loss calculator, journals and
  report all read the same operations. Splitting the data would need distributed transactions or data copies.
- **Offline use in a cellar**: one container that starts with no network is simpler and more reliable than several.
- **Ready to split later**: feature boundaries already exist (a controller, a service and repositories per feature, behind
  interfaces). If real load ever needs it, a feature (for example the live simulator or the assistant) can move into its
  own service without rewriting the domain.

## Consequences

- The app behaves exactly as before; `scripts/check.ps1` / `check.sh` and the API tests compare every JSON endpoint with a
  recorded baseline.
- New code must respect the dependency rule: no SQL outside repositories, no HTTP or files in the domain.
