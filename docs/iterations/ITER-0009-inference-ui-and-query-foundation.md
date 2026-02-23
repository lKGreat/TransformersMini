# ITER-0009 推理交互界面与查询基础

- ID: `ITER-0009`
- Status: `Planned`
- Start: `pending`
- End: `pending`
- Related Requirements: `REQ-0004`, `REQ-0005`, `REQ-0006`
- Related ADRs: `ADR-0008`, `ADR-0009`

## Objective

补齐单图交互推理与实验查询基础能力，支撑后续错误分析与对比功能。

## Scope

- WinForms 单图推理界面（检测/OCR）
- SQLite 查询仓库基础接口与过滤能力
- WinForms 列表查询改造（替代逐条详情扫描）

## Non-Goals

- 完整对比视图
- ML.NET 并行路径实现

## Tasks

- `TASK-ITER-0009-001-winforms-single-image-inference-ui.md`
- `TASK-ITER-0009-002-run-query-repository-sqlite-foundation.md`
- `TASK-ITER-0009-003-winforms-run-list-query-integration.md`

## Risks

- WinForms 设计器结构改造成本高
- 查询接口设计过度影响后续扩展

## Exit Criteria

- 单图推理基础可用
- 运行列表查询切换到查询仓库接口
