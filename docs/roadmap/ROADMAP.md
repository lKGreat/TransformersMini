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

## M2 Detection MVP (Completed)

- TorchSharp detection training pipeline (real image tensor input + COCO target tensorization)
- COCO adapter expansion (annotation metadata / multi-box strategy support)
- Validation/test reports and artifacts (preprocessing snapshot included)
- Multi-box target encoding (`topK`) with multi-head detector baseline
- Stronger evaluation metrics (approx IoU/PR) and sample-level evaluation details

## M3 OCR Integration (Completed)

- OCR manifest pipeline
- OCR training/validation/test implementation
- CER/WER metrics persisted to SQLite and reports
- OCR TorchSharp MVP integrated into unified CLI + WinForms orchestration

## M4 Experiment Analysis & Comparison (In Progress)

- Detection training loss decomposition (`bbox/category/objectness`) and summary snapshots
- WinForms run details/tags/artifacts/metrics visualization improvements
- Detection validation/test sample-level details ready for UI analysis expansion

## M5 Dual Backend Expansion

- ML.NET backend capability enhancements (currently capability matrix blocks unsupported training combos)
- Shared metrics and experiment comparison
