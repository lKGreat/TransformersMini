# TASK-ITER-0005-001 ML.NET Capability Matrix Tightening

- ID: `TASK-ITER-0005-001`
- Status: `Done`
- Iteration: `ITER-0005`
- Requirement: `REQ-0001`
- ADR: `ADR-0004`

## Objective

Block unsupported ML.NET training combinations during capability validation instead of failing later at runtime.

## Implementation Notes

- `MlNetBackendCapability.Supports(...)` returns `false` for current unsupported training combinations
- `BackendCapabilityValidator` returns clearer Chinese error messages and usage suggestion

## Test Plan

- Config with `backend=MLNet` and `task=Detection` fails before task execution

## Done Definition

- Capability validation rejects unsupported ML.NET train/validate/test requests
