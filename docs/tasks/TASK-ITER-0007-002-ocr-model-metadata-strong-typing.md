# TASK-ITER-0007-002 OCR 模型产物 metadata 强类型化

- ID: `TASK-ITER-0007-002`
- Status: `Todo`
- Iteration: `ITER-0007`
- Requirement: `REQ-0005`
- ADR: `ADR-0006`

## Objective

统一 OCR 训练/验证/测试报告与模型 metadata 的强类型结构，为推理链路消费 metadata 做准备。

## Implementation Notes

- 对齐检测侧的 metadata 基础字段
- 保留 OCR 特有字段（字符集、输入尺寸、最大长度）

## Test Plan

- OCR 训练/验证集成测试
- 报告字段兼容性检查

## Done Definition

- OCR metadata 可被后续推理流程直接消费
