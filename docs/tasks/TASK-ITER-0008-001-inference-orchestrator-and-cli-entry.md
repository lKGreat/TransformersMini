# TASK-ITER-0008-001 推理编排入口与 CLI 命令基线

- ID: `TASK-ITER-0008-001`
- Status: `Todo`
- Iteration: `ITER-0008`
- Requirement: `REQ-0004`, `REQ-0005`
- ADR: `ADR-0005`, `ADR-0007`

## Objective

建立统一推理编排入口和 CLI 推理命令，为检测/OCR 批量推理共用。

## Implementation Notes

- 新增 `IInferenceOrchestrator` 与 `RunInferenceCommand`
- CLI 新增 `infer` 命令（或等价命令）
- 推理 run 接入现有运行仓库

## Test Plan

- CLI dry-run / smoke 推理测试
- 推理 run 落盘基础报告与元数据

## Done Definition

- CLI 可以发起统一推理流程（检测/OCR共用入口）
