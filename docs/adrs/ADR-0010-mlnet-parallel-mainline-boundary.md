# ADR-0010 ML.NET 并行主线边界

- ID: `ADR-0010`
- Status: `Proposed`
- Date: `2026-02-23`
- Related Requirements: `REQ-0007`

## Context

6个月计划要求 ML.NET 作为并行主线参与，但若不先定义边界，会挤占检测/OCR TorchSharp 主线和实验平台能力建设。

## Decision

- 本周期将 ML.NET 作为“并行主线受控范围”推进，必须先通过任务卡明确交付边界。
- 优先交付 1 条真实可运行路径（训练、推理或分析辅助其一），再评估是否扩容。
- TorchSharp 保持检测/OCR 深度训练主链路地位，ML.NET 不得阻塞主线迭代。
- ML.NET 范围变更必须先更新 ADR 与迭代计划。

## Consequences

- ML.NET 的完成定义需要单独验收文档证据
- 若并行主线范围扩大，需同步调整迭代节奏与人力分配

## Alternatives Considered

- 让 ML.NET 与 TorchSharp 完全同范围并进
  - 缺点：范围过大、周期风险高

## Follow-up

- 在后续 `ITER-0013+` 中明确 ML.NET 路径任务卡
- 为 ML.NET 路径补充能力矩阵与报告契约
