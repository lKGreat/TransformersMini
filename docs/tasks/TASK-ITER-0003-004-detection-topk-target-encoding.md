# TASK-ITER-0003-004 Detection TopK Target Encoding

- ID: `TASK-ITER-0003-004`
- Status: `Todo`
- Iteration: `ITER-0003`
- Requirement: `REQ-0002`
- ADR: `ADR-0002`, `ADR-0004`

## Objective

Replace the current single-box target vector with fixed-size `topK` multi-box target encoding for detection training.

## Implementation Notes

- Add `taskOptions.targetTopK` (default `8`)
- Encode detection targets as `[B, K, 6]` => `(cx, cy, bw, bh, cls, obj)`
- Keep current `first/largest/average` strategy as fallback selector for reduced targets only if needed
- Update model head output shape and loss computation to match `topK`

## Test Plan

- Multi-box COCO sample produces expected target tensor shape
- Less-than-`K` boxes are zero-padded
- More-than-`K` boxes are truncated deterministically

## Done Definition

- CPU training integration test passes with multi-box samples
- Reports and metrics still generated
