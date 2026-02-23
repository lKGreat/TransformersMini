# AC-ITER-0003

- Iteration: `ITER-0003`
- Date: `2026-02-23`
- Reviewer: `pending`

## Acceptance Checklist

- [x] TorchSharp detection train/validate/test path runs through unified orchestrator
- [x] COCO adapter parses images + annotations into runtime metadata
- [x] Detection task loads real image tensors (ImageSharp) instead of synthetic features
- [x] Preprocessing configuration persisted to reports/tags/events
- [ ] Multi-box `topK` target encoding implemented
- [ ] Non-placeholder detection evaluation metrics implemented

## Evidence

- Detection integration tests (CPU) pass, including `largest/average` target strategy
- Run reports include `preprocessing` block and SQLite stores `det.preprocess.*` tags

## Open Issues

- Model is still a tiny CNN baseline
- Target encoding is still single-box vector
- `mAP50` is placeholder metric

## Decision

`Partially Accepted (MVP baseline delivered, advanced metrics/encoding pending)`
