# TASK-ITER-0008-003 OCR 批量推理与报告落盘

- ID: `TASK-ITER-0008-003`
- Status: `Todo`
- Iteration: `ITER-0008`
- Requirement: `REQ-0005`
- ADR: `ADR-0006`, `ADR-0007`

## Objective

实现 OCR 批量推理与推理报告落盘，支持样本级预测文本与错误分析明细。

## Implementation Notes

- 输出预测文本、可选真值对照、CER 样本明细
- 生成 `inference.json` 与 `inference-samples.jsonl`

## Test Plan

- OCR 小样本批量推理集成测试
- CER 汇总与样本明细一致性检查

## Done Definition

- OCR 批量推理 run 可在平台中查看与分析
