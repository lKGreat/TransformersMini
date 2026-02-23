# REQ-0007 ML.NET 并行主线范围定义与交付

- ID: `REQ-0007`
- Status: `Draft`
- Owner: `pending`
- Related ADRs: `ADR-0004`, `ADR-0010`
- Related Iteration: `ITER-0006+`

## Background

当前项目在架构上保留了 ML.NET 与 TorchSharp 双后端能力，但实际主链路主要由 TorchSharp 承担。后续 6 个月计划中希望 ML.NET 作为并行主线的一部分，但需先明确边界与完成定义。

## Goal

定义并执行 ML.NET 在本周期内的“并行主线”范围，避免与 TorchSharp 主训练链路发生资源冲突或目标模糊。

## Success Criteria

- 通过 ADR 固化 ML.NET 的交付边界
- 至少一条 ML.NET 真实可运行路径交付（训练、推理或分析辅助）
- 能与现有运行记录/报告/WinForms 对齐

## In Scope

- ML.NET 范围边界定义
- 能力矩阵更新
- 至少一个真实路径的文档与验收

## Out of Scope

- 与 TorchSharp 完全同等覆盖的多任务深度训练能力（本周期默认不强制）

## Constraints

- 不得阻塞检测/OCR TorchSharp 主线稳定性
- 文档先行，范围变更必须先更新 ADR

## Acceptance Criteria

- `REQ/ADR/ITER/TASK/AC` 链完整
- ML.NET 路径有运行证据与测试/手工验收说明

## Risks

- 范围膨胀导致主线延误
- 与 TorchSharp 能力重叠过大导致重复建设

## Change Log

- `2026-02-23`：初版需求草案创建
