# TASK-ITER-0006-003 推理主线后续迭代任务拆分

- ID: `TASK-ITER-0006-003`
- Status: `Todo`
- Iteration: `ITER-0006`
- Requirement: `REQ-0004`, `REQ-0005`, `REQ-0006`
- ADR: `ADR-0005`, `ADR-0007`, `ADR-0008`, `ADR-0009`

## Objective

为后续 2-3 个双周迭代预先生成任务卡骨架，保证“先文档后实施”节奏可持续执行。

## Implementation Notes

- 至少覆盖：
  - 批量推理（检测/OCR）
  - 推理报告与样本明细
  - WinForms 推理 UI
  - SQLite 查询与对比能力
- 每张任务卡必须包含输入/输出/测试/完成定义

## Test Plan

- 人工审阅任务卡是否可直接进入实施
- 检查任务卡与 REQ/ADR/ITER 的引用链完整性

## Done Definition

- 后续迭代具备可执行的任务清单，不需要临时补文档再开工
