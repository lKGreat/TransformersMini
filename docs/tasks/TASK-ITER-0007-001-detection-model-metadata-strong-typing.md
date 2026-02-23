# TASK-ITER-0007-001 检测模型产物 metadata 强类型化

- ID: `TASK-ITER-0007-001`
- Status: `Todo`
- Iteration: `ITER-0007`
- Requirement: `REQ-0004`
- ADR: `ADR-0006`

## Objective

将检测训练产物 `model-metadata.json` 与 `summary.json` 中的关键 metadata 从匿名对象逐步迁移为强类型 DTO。

## Implementation Notes

- 优先覆盖 `preprocessing`、`targetEncoding`、`lossWeights`、`lossSummary`
- 保持现有 JSON 字段兼容，避免 UI 回归

## Test Plan

- 契约测试校验 JSON 字段结构
- 检测训练集成测试断言不回归

## Done Definition

- 检测训练产物 metadata 使用强类型 DTO 输出
