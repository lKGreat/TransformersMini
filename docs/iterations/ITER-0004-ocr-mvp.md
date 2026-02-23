# ITER-0004 OCR MVP

- ID: `ITER-0004`
- Status: `Completed`
- Start: `2026-02-23`
- End: `2026-02-23`
- Related Requirements: `REQ-0003`
- Related ADRs: `ADR-0002`, `ADR-0004`

## Objective

在统一编排入口上完成 OCR TorchSharp MVP 的训练/验证/测试链路。

## Scope

- `OcrTrainingTask` 非 stub 化
- OCR manifest 数据适配与训练输入
- CER/WER 指标写入 SQLite、`metrics.jsonl` 与报告
- CLI/WinForms 通过统一编排启动 OCR

## Non-Goals

- 高精度 OCR 模型与复杂解码
- OCR 推理单图交互界面

## Tasks

- `TASK-ITER-0004-001-ocr-torchsharp-mvp.md`

## Risks

- TorchSharp 运行时依赖（CPU/CUDA）兼容问题
- OCR 输出格式演进导致 UI 展示逻辑调整

## Exit Criteria

- OCR train/validate/test 真实链路可运行
- CER/WER 报告与 SQLite 指标可查看
