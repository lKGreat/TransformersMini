# ADR-0008 运行查询仓库与过滤策略

- ID: `ADR-0008`
- Status: `Proposed`
- Date: `2026-02-23`
- Related Requirements: `REQ-0006`

## Context

当前 WinForms 标签过滤依赖列表后逐条读取详情，随着 run 数增长会显著变慢。需要为实验平台能力引入查询仓库能力，同时保持现有 `IRunRepository` 兼容。

## Decision

- 保持 `IRunRepository` 用于写入与详情读取。
- 新增 `IRunQueryRepository` 用于筛选、排序、分页、对比查询。
- 查询维度优先支持：
  - task / mode / backend / device
  - tag key/value
  - latest metric name/value range
  - time range

## Consequences

- SQLite 实现需要新增查询 SQL 与索引评估
- WinForms 列表页需要改为查询接口驱动

## Alternatives Considered

- 扩展 `IRunRepository` 承担全部查询能力
  - 缺点：接口职责膨胀、调用语义混杂

## Follow-up

- 定义查询 DTO / 过滤模型
- 为 WinForms 对比功能提供查询接口
