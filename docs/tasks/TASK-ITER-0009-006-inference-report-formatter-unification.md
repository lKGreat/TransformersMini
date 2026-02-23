# TASK-ITER-0009-006 推理报告展示统一化

- ID: `TASK-ITER-0009-006`
- Status: `Todo`
- Iteration: `ITER-0009`
- Requirement: `REQ-0004`, `REQ-0005`
- ADR: `ADR-0007`, `ADR-0009`

## Objective

统一批量推理与单图推理的报告展示逻辑，避免重复解析代码，兼容 `inference.json` 与 `inference-samples.jsonl` 结构。

## Implementation Notes

- 抽取 `InferenceReportFormatter`（或等价静态/服务类），负责从 `RunResult` 与 `RunDirectory` 构建可展示文本
- 批量推理 Form 与单图推理 Form 均调用该 formatter，不再各自解析 JSON
- 支持检测任务（框数、平均框数、输入尺寸等）与 OCR 任务（CER、精确匹配数等）的差异化展示
- 报告字段缺失时容错，不抛异常

## Dependencies

- 推理报告落盘格式稳定（TASK-ITER-0008-002、TASK-ITER-0008-003）

## Test Plan

- 手工验收：批量推理与单图推理结果展示一致、字段正确
- 报告缺失或格式异常时 UI 不崩溃

## Done Definition

- 批量/单图推理共用同一报告格式化逻辑
- 检测/OCR 报告展示正确
