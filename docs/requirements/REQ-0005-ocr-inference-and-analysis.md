# REQ-0005 OCR 推理与分析主线

- ID: `REQ-0005`
- Status: `Draft`
- Owner: `pending`
- Related ADRs: `ADR-0005`, `ADR-0006`, `ADR-0007`, `ADR-0008`, `ADR-0009`
- Related Iteration: `ITER-0006+`

## Background

OCR 已具备 TorchSharp MVP 的训练/验证/测试链路，但缺少统一推理能力、样本级错误分析与桌面端交互推理体验。

## Goal

打通 OCR 从训练产物到推理（批量 + 单图）的闭环，并支持 CER/WER 与样本级错误分析。

## Success Criteria

- CLI 与 WinForms 可执行 OCR 批量推理
- WinForms 支持 OCR 单图交互推理
- 推理报告包含预测文本、可选真值对照、CER 样本明细
- OCR 推理结果可进入实验筛选与对比

## In Scope

- OCR 批量推理配置与命令
- OCR 单图推理界面
- OCR 推理报告、样本级错误明细、TopN 错误摘要
- 训练产物 metadata 与推理兼容检查

## Out of Scope

- 生产级 OCR 服务接口
- 多语言大字符集优化（本周期仅做可扩展设计）

## Constraints

- 与统一编排/运行仓库保持一致
- WinForms 需满足三文件结构约束且设计器可预览
- 默认 CPU 可运行，CUDA 为可选加速路径

## Acceptance Criteria

- OCR 推理 run 可在 CLI/WinForms 发起并落盘报告
- CER/WER 与样本级明细可在 WinForms 查看
- OCR 推理结果可复现实验（配置 + 权重 + metadata）

## Risks

- OCR 输出格式变更频繁导致 UI 和报告耦合
- 字符集/预处理参数不一致导致推理偏差

## Change Log

- `2026-02-23`：初版需求草案创建（6个月长期主线计划）
