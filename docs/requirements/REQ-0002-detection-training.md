# REQ-0002 Detection Training MVP

- ID: `REQ-0002`
- Status: `InProgress`

## Goal

Implement a real TorchSharp-based detection training loop with COCO input.

## Current Implementation Status

- Unified CLI/WinForms orchestration path is implemented
- TorchSharp detection training loop is implemented with real image tensor input
- COCO annotation metadata and target tensor generation are implemented
- Validation/test reports and runtime tracking are implemented

## Remaining Acceptance Gaps

- Multi-box target encoding (`topK`) for multi-object samples
- Stronger detection evaluation metrics (replace placeholder score)
- Model architecture upgrade from baseline tiny CNN to richer detection head
