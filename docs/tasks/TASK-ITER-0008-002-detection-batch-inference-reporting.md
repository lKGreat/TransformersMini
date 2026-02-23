# TASK-ITER-0008-002 检测批量推理与报告落盘

- ID: `TASK-ITER-0008-002`
- Status: `Todo`
- Iteration: `ITER-0008`
- Requirement: `REQ-0004`
- ADR: `ADR-0006`, `ADR-0007`

## Objective

实现检测批量推理、推理汇总报告与样本明细文件落盘，并接入 SQLite 产物索引。

## Implementation Notes

- 检测推理输出至少包含框、分数、类别
- 生成 `reports/inference.json` 与 `reports/inference-samples.jsonl`
- 样本明细需支持后续错误分析摘要

## Test Plan

- 检测小样本批量推理集成测试
- 报告字段与 artifact 索引断言

## Done Definition

- 检测批量推理 run 可复现且报告结构稳定
