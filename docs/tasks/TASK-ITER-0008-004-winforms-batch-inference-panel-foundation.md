# TASK-ITER-0008-004 WinForms 批量推理面板基础

- ID: `TASK-ITER-0008-004`
- Status: `Todo`
- Iteration: `ITER-0008`
- Requirement: `REQ-0004`, `REQ-0005`
- ADR: `ADR-0005`, `ADR-0009`

## Objective

为检测/OCR 批量推理提供 WinForms 操作入口与基础结果查看能力。

## Implementation Notes

- 必须遵守 WinForms 三文件结构与设计器可预览要求
- 优先使用独立表单/面板，避免继续膨胀 `MainForm`

## Test Plan

- 手工验收：批量推理参数选择、启动、查看结果
- UI 冒烟测试（不要求完整自动化 UI 测试）

## Done Definition

- WinForms 可发起检测/OCR 批量推理并查看基本结果
