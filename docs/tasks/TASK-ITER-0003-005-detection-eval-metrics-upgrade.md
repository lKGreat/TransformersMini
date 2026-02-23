# TASK-ITER-0003-005 Detection Eval Metrics Upgrade

- ID: `TASK-ITER-0003-005`
- Status: `Todo`
- Iteration: `ITER-0003`
- Requirement: `REQ-0002`
- ADR: `ADR-0002`

## Objective

Replace placeholder detection validation/test score with a simplified but real IoU-based metric set.

## Implementation Notes

- Implement approximate IoU / precision / recall for validation and test
- Persist metric metadata in `reports/validate.json` and `reports/test.json`
- Clearly mark metric type (e.g. `approx-iou-pr`) in report payload

## Test Plan

- Known synthetic boxes produce deterministic IoU result
- Validation report contains metric type and component metrics

## Done Definition

- Placeholder `mAP50` diff-based score removed from detection eval path
