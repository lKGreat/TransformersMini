# ITER-0005 平台分析能力基础

- ID: `ITER-0005`
- Status: `InProgress`
- Start: `2026-02-23`
- End: `pending`
- Related Requirements: `REQ-0002`, `REQ-0003`, `REQ-0006`
- Related ADRs: `ADR-0003`, `ADR-0004`

## Objective

增强运行记录、报告与 WinForms 详情展示，为后续实验查询/对比/推理主线打基础。

## Scope

- SQLite 扩展表与运行标签/产物/最新指标索引
- 检测/ OCR 报告字段增强（训练损失、样本明细）
- WinForms 详情展示增强（metrics/tags/artifacts/report insights）

## Non-Goals

- 完整推理主线（CLI/WinForms）
- 实验对比视图与高级查询接口

## Tasks

- 已落地任务以代码与验收文档为准（后续补充拆分任务卡）

## Risks

- 报告字段快速演进导致 UI 解析逻辑脆弱

## Exit Criteria

- 运行详情可展示关键报告摘要
- 为推理与对比能力提供稳定数据基线
