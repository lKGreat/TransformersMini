# TASK-YOLO-132 单元测试：NmsProcessor

- ID: `TASK-YOLO-132`
- Status: `Todo`
- Iteration: `ITER-0010`
- Requirement: `REQ-0008`

## Objective

验证 NmsProcessor 正确抑制重叠框，保留非重叠框。

## Implementation Notes

文件：`tests/TransformersMini.Tests.Unit/Detection/NmsProcessorTests.cs`

测试方法：
- `NmsProcessor_OverlappingBoxes_KeepsOnlyOne`
- `NmsProcessor_NonOverlappingBoxes_KeepsAll`
- `NmsProcessor_BelowConfThreshold_FiltersOut`

## Done Definition

- 所有测试通过
