# TASK-ITER-0002-004 Test Runs Root Isolation

- ID: `TASK-ITER-0002-004`
- Status: `Done`
- Iteration: `ITER-0002`
- Requirement: `REQ-0001`
- ADR: `ADR-0003`

## Objective

Allow tests to inject a dedicated `runs` root so integration runs do not pollute the default repository `runs/` folder.

## Implementation Notes

- Added `AddTransformersMiniStorage(..., runsRoot)` and `AddTransformersMiniPlatform(..., runsRoot)` overloads
- Integration tests create per-test `runs` root under `data/samples/integration-runs/<guid>/`

## Test Plan

- Integration tests pass using injected `runs` root
- SQLite database is created under the injected path

## Done Definition

- No integration test relies on the default repository root `runs/` path
