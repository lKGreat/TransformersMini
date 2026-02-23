# TASK-YOLO-103 Sppf 模块

- ID: `TASK-YOLO-103`
- Status: `Todo`
- Iteration: `ITER-0010`
- Requirement: `REQ-0008`
- ADR: `ADR-0011`

## Objective

实现快速空间金字塔池化 SPPF，用于 Backbone 末尾扩大感受野。

## Implementation Notes

文件路径：`src/TransformersMini.Infrastructure.TorchSharp/Detection/Modules/Sppf.cs`

逻辑：
1. `cv1`：1×1 ConvBnAct，c1 → c1//2
2. MaxPool2d(k=5, stride=1, padding=2) 连续迭代 3 次，每次以上一个输出作为输入
3. Cat([cv1_out, pool1, pool2, pool3], dim=1) → 4*(c1//2) 通道
4. `cv2`：1×1 ConvBnAct，4*(c1//2) → c2

## Test Plan

- Sppf(1024, 1024) 输入 `[1, 1024, 20, 20]` → 输出 `[1, 1024, 20, 20]`

## Done Definition

- 形状测试通过，build 无 warning
