# TASK-ITER-0009-002 SQLite 查询仓库基础能力

- ID: `TASK-ITER-0009-002`
- Status: `Todo`
- Iteration: `ITER-0009`
- Requirement: `REQ-0006`
- ADR: `ADR-0008`

## Objective

为实验平台实现 SQLite 查询仓库基础能力，替代当前 WinForms 列表页逐条详情扫描。

## Implementation Notes

- 新增 `IRunQueryRepository` 与过滤 DTO
- 首期支持 task/mode/backend/device/tag/latest metric/time range 过滤
- 保持 `IRunRepository` 兼容

## Test Plan

- SQLite 查询单元/集成测试
- 查询结果与现有详情数据一致性抽查

## Done Definition

- WinForms 列表查询可切换到查询仓库驱动
