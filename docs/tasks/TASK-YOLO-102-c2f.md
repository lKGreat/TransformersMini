# TASK-YOLO-102 C2f 模块

- ID: `TASK-YOLO-102`
- Status: `Todo`
- Iteration: `ITER-0010`
- Requirement: `REQ-0008`
- ADR: `ADR-0011`

## Objective

实现 YOLOv8 核心特征提取模块 C2f（CSP Bottleneck with 2 convolutions）。

## Implementation Notes

文件路径：`src/TransformersMini.Infrastructure.TorchSharp/Detection/Modules/C2f.cs`

逻辑：
1. `cv1`：1×1 ConvBnAct，将 c1 映射到 2*c（c = c2 * e）
2. 将输出沿 channel 维度 chunk(2) 分为两半
3. 第二半经过 n 个 Bottleneck 顺序处理，每步输出均追加到列表
4. `cv2`：1×1 ConvBnAct，将 cat((2+n)*c 维) 映射到 c2

## Test Plan

- C2f(128, 256, n=3) 输入 `[1, 128, 40, 40]` → 输出 `[1, 256, 40, 40]`
- C2f(256, 256, n=6, shortcut=true) 形状不变

## Done Definition

- 形状测试通过，build 无 warning
