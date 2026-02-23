# TASK-ITER-0004-001 OCR TorchSharp MVP

- ID: `TASK-ITER-0004-001`
- Status: `Todo`
- Iteration: `ITER-0004` 
- Requirement: `REQ-0003`
- ADR: `ADR-0002`, `ADR-0004`

## Objective

Implement real OCR train/validate/test on the unified orchestrator using TorchSharp.

## Implementation Notes

- Replace `OcrTrainingTask` stub with TorchSharp-based minimal training loop
- Use OCR manifest adapter (`ocr-manifest-v1`) as input source
- Persist `CER` / `WER` metrics into SQLite, metrics stream, and reports

## Test Plan

- CPU training smoke test on generated OCR mini dataset
- Validate/test generate `reports/validate.json` and `reports/test.json`
- Run detail shows latest `cer` and/or `wer`

## Done Definition

- CLI and WinForms can launch OCR training successfully (non-stub)
