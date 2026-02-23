# REQ-0003 OCR Training MVP

- ID: `REQ-0003`
- Status: `Draft`

## Goal

Implement OCR train/validate/test on top of the unified orchestration entry.

## Current Implementation Status

- Unified orchestration entry is ready
- OCR manifest adapter project exists
- OCR task implementation is still stubbed (train/validate/test not yet real)

## Planned Scope

- TorchSharp-based OCR training/validation/test
- CER/WER metrics persisted to SQLite and reports
- WinForms detail display for OCR metrics and artifacts
