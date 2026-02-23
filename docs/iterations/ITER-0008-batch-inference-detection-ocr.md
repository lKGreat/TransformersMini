# ITER-0008 批量推理主线（检测 + OCR）

- ID: `ITER-0008`
- Status: `Planned`
- Start: `pending`
- End: `pending`
- Related Requirements: `REQ-0004`, `REQ-0005`, `REQ-0006`
- Related ADRs: `ADR-0005`, `ADR-0006`, `ADR-0007`

## Objective

实现检测与 OCR 的批量推理主线（CLI + WinForms 并进），接入统一运行记录与推理报告。

## Scope

- 推理编排入口（应用层）
- 检测批量推理 CLI/WinForms
- OCR 批量推理 CLI/WinForms
- 推理报告与样本明细落盘（`inference.json` + `inference-samples.jsonl`）

## Non-Goals

- 单图交互推理（放到下一迭代）
- 高级实验对比视图

## Tasks

- `TASK-ITER-0008-001-inference-orchestrator-and-cli-entry.md`
- `TASK-ITER-0008-002-detection-batch-inference-reporting.md`
- `TASK-ITER-0008-003-ocr-batch-inference-reporting.md`
- `TASK-ITER-0008-004-winforms-batch-inference-panel-foundation.md`

## Risks

- 推理报告结构与 UI 展示耦合过紧
- 权重与 metadata 不兼容导致推理失败

## Exit Criteria

- 检测/OCR 批量推理 run 可在 CLI/WinForms 发起并生成推理报告
