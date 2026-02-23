# REQ-0004 检测推理与分析主线

- ID: `REQ-0004`
- Status: `Draft`
- Owner: `pending`
- Related ADRs: `ADR-0005`, `ADR-0006`, `ADR-0007`, `ADR-0008`, `ADR-0009`
- Related Iteration: `ITER-0006+`

## Background

当前检测任务已具备训练/验证/测试主链路，但“训练到推理”的闭环尚未成型，缺少统一推理入口、推理报告、样本级结果归档与可视化分析。

## Goal

建立检测任务的完整推理能力（批量 + 单图），并与训练产物、运行记录、WinForms 展示和实验对比能力对齐。

## Success Criteria

- CLI 与 WinForms 都可发起检测批量推理与单图推理
- 推理使用训练产物（TorchSharp 权重 + metadata）可复现
- 推理报告包含汇总指标与样本明细，写入 `runs/` 与 SQLite
- WinForms 可查看检测推理结果与错误样本摘要

## In Scope

- 检测批量推理命令与配置
- 检测单图交互推理（WinForms）
- 推理报告格式与样本明细
- 推理结果归档与实验对比接入

## Out of Scope

- 生产级在线服务部署
- 多机分布式推理调度
- ONNX/TensorRT 导出与部署（后续可扩展）

## Constraints

- 必须兼容现有 `docs/` 驱动流程
- 默认单机 CPU 可运行，CUDA 可选
- 模型产物优先采用 TorchSharp 原生权重
- 需遵守强类型实现要求

## Acceptance Criteria

- `Infer` 主线文档、配置、任务、验收链完整
- 检测批量推理与单图推理有集成测试/手工验收证据
- 推理 run 可在 WinForms 中筛选、查看、对比

## Risks

- 推理结果结构不稳定会导致 UI 解析脆弱
- 训练产物 metadata 不完整会导致推理不可复现

## Change Log

- `2026-02-23`：初版需求草案创建（6个月长期主线计划）
