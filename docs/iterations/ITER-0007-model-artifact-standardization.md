# ITER-0007 模型产物标准化（训练到推理准备）

- ID: `ITER-0007`
- Status: `Planned`
- Start: `pending`
- End: `pending`
- Related Requirements: `REQ-0004`, `REQ-0005`, `REQ-0006`
- Related ADRs: `ADR-0006`, `ADR-0007`

## Objective

统一检测/OCR 训练产物 metadata 结构，为后续批量推理与实验对比建立可复现基础。

## Scope

- 模型产物 metadata 强类型化与字段统一
- 训练产物 schema/契约测试补齐
- 训练 run 对推理兼容性信息落盘

## Non-Goals

- 批量推理实现
- WinForms 推理界面实现

## Tasks

- `TASK-ITER-0007-001-detection-model-metadata-strong-typing.md`
- `TASK-ITER-0007-002-ocr-model-metadata-strong-typing.md`
- `TASK-ITER-0007-003-model-metadata-schema-and-contract-tests.md`

## Risks

- 现有匿名对象字段迁移时破坏 UI 报告读取兼容

## Exit Criteria

- 检测/OCR 训练产物 metadata 结构统一且可用于推理恢复
