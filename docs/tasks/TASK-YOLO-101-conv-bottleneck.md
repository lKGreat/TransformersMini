# TASK-YOLO-101 基础卷积模块（ConvBnAct / DepthwiseConv / Bottleneck）

- ID: `TASK-YOLO-101`
- Status: `Todo`
- Iteration: `ITER-0010`
- Requirement: `REQ-0008`
- ADR: `ADR-0011`

## Objective

实现 YOLOv8 最基础的卷积构建块，供后续 C2f、Backbone、Neck、Head 复用。

## Implementation Notes

文件路径：`src/TransformersMini.Infrastructure.TorchSharp/Detection/Modules/ConvBnAct.cs`

- `ConvBnAct`：Conv2d + BatchNorm2d + SiLU，支持 padding 自动计算
- `DepthwiseConv`：groups=in_channels 的 ConvBnAct（深度可分离卷积）
- `Bottleneck`：两个 ConvBnAct（1×1 + 3×3）+ 可选残差连接

## Test Plan

- 输入 `[2, 32, 64, 64]`，ConvBnAct(32, 64, 3) → 输出 `[2, 64, 64, 64]`
- Bottleneck shortcut=true 时输入输出形状相同

## Done Definition

- 文件编译通过，0 warnings
- 基础形状断言正确
