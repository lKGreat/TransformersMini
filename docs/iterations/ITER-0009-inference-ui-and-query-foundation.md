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

- WinForms 主窗体壳层化与面板拆分（RunListAndFilterPanel、InferencePanel）
- WinForms 单图推理界面（检测/OCR）
- WinForms 增强标注工作台（COCO/YOLO 双格式、推理结果导入）
- 推理报告展示统一化
- SQLite 查询仓库基础接口与过滤能力
- WinForms 列表查询改造（替代逐条详情扫描）

## Non-Goals

- 完整对比视图
- ML.NET 并行路径实现

## Tasks

- `TASK-ITER-0009-001-winforms-single-image-inference-ui.md`
- `TASK-ITER-0009-002-run-query-repository-sqlite-foundation.md`
- `TASK-ITER-0009-003-winforms-run-list-query-integration.md`
- `TASK-ITER-0009-004-winforms-mainform-shell-and-panels.md`
- `TASK-ITER-0009-005-winforms-annotation-workspace.md`
- `TASK-ITER-0009-006-inference-report-formatter-unification.md`

## Risks

- WinForms 设计器结构改造成本高
- 查询接口设计过度影响后续扩展

## Exit Criteria

- MainForm 壳层化完成，运行/推理/标注分区清晰
- 单图推理基础可用
- 增强标注工作台可用，COCO/YOLO 双格式读写
- 推理报告展示统一
- 运行列表查询切换到查询仓库接口
