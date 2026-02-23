# TASK-ITER-0006-004 文档先行实施的验收证据模板

- ID: `TASK-ITER-0006-004`
- Status: `Done`
- Iteration: `ITER-0006`
- Requirement: `REQ-0006`
- ADR: `ADR-0007`, `ADR-0008`, `ADR-0009`

## Objective

规范长期主线每个迭代的验收证据结构，确保实现结果可以回溯、复现、审阅。

## Implementation Notes

- 在 `AC-ITER-xxxx` 中统一约定：
  - 构建/测试命令与结果
  - CLI 命令示例
  - WinForms 截图
  - 报告路径与关键字段
  - 已知限制与下一步
- 明确证据最小集合，避免验收文档空泛

## Test Plan

- 以 `AC-ITER-0006` 草案验证模板可用性
- 检查证据字段能覆盖训练/推理/平台 UI 三类任务

## Done Definition

- 验收文档模板可直接用于后续迭代，不需要重复设计结构

## Result

- `acceptance.template.md` 已补充构建/测试、CLI、WinForms、报告、任务关联证据结构
- `AC-ITER-0006` 已按模板思路回填
