# REQ-0003 OCR Training MVP

- ID: `REQ-0003`
- Status: `InProgress`

## Goal

Implement OCR train/validate/test on top of the unified orchestration entry.

## Current Implementation Status

- Unified orchestration entry is ready
- OCR manifest adapter is implemented and supports train/val/test splits
- TorchSharp OCR MVP train/validate/test is implemented (lightweight CNN baseline)
- CER/WER metrics are persisted to SQLite and reports
- WinForms can view OCR runs, metrics, tags, artifacts, and report content

## Remaining Scope

- OCR sample-level error details (per-sample CER / prediction-target comparison)
- Stronger OCR decoding/model strategy beyond MVP baseline
- OCR taskOptions schema expansion and stricter validation coverage
- WinForms OCR-focused detail formatting (CER/WER + error samples summary)
