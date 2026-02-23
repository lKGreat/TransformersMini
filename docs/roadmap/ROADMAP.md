# Roadmap

## M0 Foundation (Completed)

- Requirement-driven documentation system
- Solution skeleton and project boundaries
- Unified CLI + WinForms host architecture
- Config schemas and sample configs
- Stub train/validate/test pipeline for OCR
- Initial real TorchSharp detection loop with COCO image/annotation loading (MVP baseline)

## M1 Runnable Skeleton (Completed)

- Improved run tracking (SQLite repository + artifacts/tags/latest metrics)
- Config validation and richer diagnostics (JSON Schema strict validation + device/build checks)
- WinForms workbench UX polish (run details/tags/artifacts/metrics, build mode hints, tag filtering)

## M2 Detection MVP (In Progress)

- TorchSharp detection training pipeline (real image tensor input + COCO target tensorization)
- COCO adapter expansion (annotation metadata / multi-box strategy support)
- Validation/test reports and artifacts (preprocessing snapshot included)
- Remaining: multi-box target encoding (`topK`) and stronger evaluation metrics

## M3 OCR Integration

- OCR manifest pipeline
- OCR training/validation/test implementation

## M4 Dual Backend Expansion

- ML.NET backend capability enhancements (currently capability matrix blocks unsupported training combos)
- Shared metrics and experiment comparison
