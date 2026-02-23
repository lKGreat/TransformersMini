# TASK-ITER-0009-001 WinForms 单图推理界面（检测 + OCR）

- ID: `TASK-ITER-0009-001`
- Status: `Todo`
- Iteration: `ITER-0009`
- Requirement: `REQ-0004`, `REQ-0005`
- ADR: `ADR-0005`, `ADR-0009`

## Objective

提供 WinForms 单图交互推理界面，用于检测/OCR 调试、演示和错误分析。

## Implementation Notes

- 设计器布局 + 代码逻辑分离 + `.resx` 资源文件
- 检测显示框和分数，OCR 显示识别文本与关键指标

## Test Plan

- 手工验收截图
- 基础功能冒烟（加载图片、推理、保存结果）

## Done Definition

- 单图推理流程在 WinForms 中可完整使用
