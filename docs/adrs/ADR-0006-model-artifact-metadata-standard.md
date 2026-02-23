# ADR-0006 模型产物与元数据标准化

- ID: `ADR-0006`
- Status: `Proposed`
- Date: `2026-02-23`
- Related Requirements: `REQ-0004`, `REQ-0005`, `REQ-0006`

## Context

训练产物已包含权重文件和 `model-metadata.json`，但字段结构仍存在匿名对象与任务差异化写法。要支撑可复现推理与实验对比，需要统一且稳定的 metadata 规范。

## Decision

- 统一定义模型产物 metadata 字段（基础信息 + 预处理 + 任务特定块）。
- 训练 run 必须写入可复现推理所需的最小 metadata 集。
- 后续逐步以强类型 DTO 替换匿名对象序列化。
- TorchSharp 原生权重作为本周期优先产物格式。

## Consequences

- 检测/OCR 训练任务需要对齐 metadata 字段
- 推理流程需要消费 metadata 并做兼容性校验
- Schema/契约测试需要同步更新

## Alternatives Considered

- 维持任务各自自定义 metadata
  - 缺点：推理兼容和 UI 展示成本持续增加

## Follow-up

- 定义 metadata schema
- 增加 contracts DTO 与契约测试
