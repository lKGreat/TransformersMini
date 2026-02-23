# TASK-YOLO-108 CIoULoss 工具函数

- ID: `TASK-YOLO-108`
- Status: `Todo`
- Iteration: `ITER-0010`
- Requirement: `REQ-0008`
- ADR: `ADR-0011`

## Objective

实现 Complete IoU 损失工具函数，用于 BboxLoss 中的边界框回归。

## Implementation Notes

文件路径：`src/TransformersMini.Infrastructure.TorchSharp/Detection/Loss/BboxIoU.cs`

静态方法 `BboxIou(pred, target, xywh=false, CIoU=true)`：
1. 计算交集面积和并集面积 → IoU
2. CIoU 额外计算中心距离惩罚项 `ρ²/c²` 和宽高比一致性项 `v`/`alpha`
3. 返回 CIoU 张量（形状与输入框一致）

## Test Plan

- 完全重叠框 CIoU ≈ 1.0
- 完全不重叠框 CIoU < 0

## Done Definition

- 数值测试通过，build 无 warning
