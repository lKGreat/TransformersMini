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
- Multi-box target encoding (`topK`) is implemented
- Multi-head detector baseline is implemented (bbox/category/objectness heads)
- Detection train loss decomposition and summary snapshots are implemented
- Approx IoU/Precision/Recall metrics and sample-level eval details are implemented

## Remaining Acceptance Gaps

- Detection evaluation TopN error summaries and richer analysis views
- WinForms detection report visualization for `lossSummary` / sample-level details
- Training strategy upgrades (scheduler / stronger head-backbone baseline beyond MVP)
