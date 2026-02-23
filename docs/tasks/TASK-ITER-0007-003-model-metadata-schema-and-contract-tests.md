# TASK-ITER-0007-003 模型产物 metadata Schema 与契约测试

- ID: `TASK-ITER-0007-003`
- Status: `Todo`
- Iteration: `ITER-0007`
- Requirement: `REQ-0004`, `REQ-0005`, `REQ-0006`
- ADR: `ADR-0006`, `ADR-0007`

## Objective

为检测/OCR 模型产物 metadata 增加 schema 与契约测试，确保训练到推理的字段稳定性。

## Implementation Notes

- 新增 schema（检测/OCR metadata）
- 增加契约测试覆盖样例与无效样例

## Test Plan

- `Contracts` 测试项目覆盖 metadata schema 校验

## Done Definition

- metadata 结构变更可被测试及时发现
