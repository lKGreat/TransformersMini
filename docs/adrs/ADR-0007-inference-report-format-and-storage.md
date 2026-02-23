# ADR-0007 推理报告格式与存储策略

- ID: `ADR-0007`
- Status: `Proposed`
- Date: `2026-02-23`
- Related Requirements: `REQ-0004`, `REQ-0005`, `REQ-0006`

## Context

训练/验证/测试已使用 `reports/*.json` 和 `metrics.jsonl`。推理主线需要定义报告格式与样本级明细文件策略，并接入 SQLite 与 WinForms。

## Decision

- 推理报告沿用 `runs/<runId>/reports/` 目录。
- 至少包含：
  - `reports/inference.json`（汇总）
  - `reports/inference-samples.jsonl`（样本明细，便于大数据量）
- 关键指标写入 `metrics.jsonl` 与 `run_metrics_latest`
- 推理结果文件登记到 `run_artifacts`

## Consequences

- WinForms 详情和对比功能需兼容推理报告
- 需要定义检测/OCR 推理样本明细结构

## Alternatives Considered

- 全部推理结果只写单一 JSON
  - 缺点：大批量样本会导致文件过大、UI 读取慢

## Follow-up

- 定义 detection/ocr inference report schema
- 增加样本明细回放与错误摘要逻辑
