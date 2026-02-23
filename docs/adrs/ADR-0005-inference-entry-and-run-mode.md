# ADR-0005 推理入口与运行模式策略

- ID: `ADR-0005`
- Status: `Proposed`
- Date: `2026-02-23`
- Related Requirements: `REQ-0004`, `REQ-0005`

## Context

当前统一编排主要覆盖 `Train/Validate/Test`。长期主线要求打通“训练到推理”闭环，需要明确推理入口如何与现有编排、运行记录和 CLI/WinForms 宿主对齐。

## Decision

- 新增独立推理编排入口（建议 `IInferenceOrchestrator`），不直接膨胀 `ITrainingOrchestrator`。
- CLI 与 WinForms 均通过共享应用层推理编排服务执行推理。
- 推理 run 进入同一运行仓库体系（SQLite + artifacts + reports），与训练 run 共存。
- 推理模式优先作为独立命令/流程实现，避免在训练命令中混入推理语义。

## Consequences

- 需要新增推理命令/DTO/任务抽象
- WinForms 需要新增推理控制入口
- 运行详情展示需兼容训练与推理报告结构

## Alternatives Considered

- 复用 `ITrainingOrchestrator` 并扩展 `RunMode`
  - 优点：少接口
  - 缺点：命令与参数语义混杂，后续维护成本高

## Follow-up

- 补充推理配置 schema 与报告 schema
- 定义 `IInferenceTask` 与推理结果 DTO
