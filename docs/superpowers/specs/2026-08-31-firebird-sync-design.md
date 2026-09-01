# Firebird Synchronization Design

## Objective

Add a safe, incremental Firebird integration without changing the BI calculation services or allowing the Web application to query the ERP database. PostgreSQL remains the operational database for the API and React SPA.

## Deployment Decision

The design assumes Firebird is reachable only from the ERP network. A Worker Service runs on that network, preferably on the existing VM, and is the only component allowed to connect to Firebird.

The API and SPA can remain deployed in Azure. If Azure is not available for an environment, the existing API can run on the same VM without changing its HTTP contract. The worker is independent from the API host in both cases.

## Components

```text
Firebird (read-only user)
        |
        v
OroBI.Sync.Worker on ERP VM
        |
        v
PostgreSQL
        |
        +--> OroBI.Api (/api/v1/...)
        |          |
        |          v
        |       React SPA
        |
        +--> Sync audit, health and failure records
```

- `OroBI.Sync.Worker` owns scheduling, Firebird connection, retries and checkpoint management.
- `IFirebirdCommercialReader` reads source rows only. Its implementation has no write permission in Firebird.
- `ICommercialSynchronizationService` normalizes rows and performs idempotent writes to the existing PostgreSQL model.
- `ISynchronizationCheckpointStore` persists a per-source watermark and last successful run.
- `ISynchronizationRunStore` records started, completed and failed runs, row counts and a non-sensitive error summary.
- The API reads only PostgreSQL and never contains a Firebird connection string.

## Data Flow

1. A scheduled worker run loads the checkpoint for a source, initially the latest fully completed run.
2. The reader requests changed records after that checkpoint, with a bounded page size and deterministic ordering.
3. The synchronization service maps each row to the current normalized entities used by CSV imports.
4. PostgreSQL upserts use a stable source key plus source type. Re-running a page cannot create duplicate commercial movements.
5. The checkpoint advances only after the entire page transaction succeeds.
6. A completed run records the new checkpoint and counters. A failed run retains the old checkpoint, so the next run safely replays the page.

The initial production cutover remains CSV-first: the CSV parity baseline must be accepted before enabling Firebird synchronization for business use.

## Resilience And Safety

- Firebird credentials use a read-only database user, supplied by environment configuration or a secret store.
- The worker applies a finite command timeout and a bounded retry policy for transient connection failures. Authentication, SQL syntax and mapping errors do not retry automatically.
- A failed run records a correlation identifier, source name and sanitized failure reason. Passwords, connection strings and source row contents are never logged.
- The worker exposes a health result containing the latest successful synchronization timestamp and last failure status. Stale synchronization is unhealthy even when the process is running.
- Firebird calls are made only by the worker; dashboard requests always query PostgreSQL.

## API Compatibility

Existing routes remain available during the migration. New endpoints are introduced below `/api/v1`, beginning with synchronization status for administrators. The SPA uses versioned routes only after a compatibility test proves the versioned contract matches the existing response.

No calculation is reimplemented in the worker or React. The worker writes normalized source data; existing Application calculators remain the sole source of commercial totals and closing formulas.

## Testing And Acceptance

- Unit tests cover checkpoint advancement, idempotent upsert selection and retry classification.
- Integration tests use a fake `IFirebirdCommercialReader`, not a live ERP, to prove a failed page does not advance the checkpoint.
- A contract test verifies the API never asks the Firebird reader for dashboard, trade, margin or closing requests.
- CSV parity tests remain the acceptance gate. Firebird data is accepted only after the same periods, sellers and brands match approved CSV/legacy baselines.
- A deployment smoke test verifies the worker health result, a completed synchronization run and the API reading its resulting PostgreSQL data.

## Out Of Scope

- Direct Firebird access from the browser or API.
- Writes, schema changes or administrative actions in Firebird.
- Replacing CSV imports before parity is approved.
- Moving the complete Azure deployment to a VM without an explicit operational decision.
