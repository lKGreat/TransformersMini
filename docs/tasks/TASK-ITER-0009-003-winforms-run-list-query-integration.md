# TASK-ITER-0009-003 WinForms 运行列表查询接入

- ID: `TASK-ITER-0009-003`
- Status: `Todo`
- Iteration: `ITER-0009`
- Requirement: `REQ-0006`
- ADR: `ADR-0008`, `ADR-0009`

## Objective

将 WinForms 运行列表和筛选逻辑接入查询仓库，提升大规模 run 场景下的性能与可用性。

## Implementation Notes

- 保留当前自由标签筛选体验
- 优先实现基础分页/排序/筛选
- 处理报告字段缺失时的容错展示

## Test Plan

- 手工验证筛选速度与结果正确性
- 集成测试覆盖查询筛选关键路径（如可行）

## Done Definition

- WinForms 列表页不再依赖逐条 `GetRunAsync` 扫描
