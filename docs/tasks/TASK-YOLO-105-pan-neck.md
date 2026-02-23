# TASK-YOLO-105 PanNeck（FPN + PANet 特征融合）

- ID: `TASK-YOLO-105`
- Status: `Todo`
- Iteration: `ITER-0010`
- Requirement: `REQ-0008`
- ADR: `ADR-0011`

## Objective

实现 PAN Neck，将 Backbone 输出的 P3/P4/P5 特征融合为 F3/F4/F5。

## Implementation Notes

文件路径：`src/TransformersMini.Infrastructure.TorchSharp/Detection/Neck/PanNeck.cs`

FPN（自顶向下）：
1. P5 → Upsample(2x) + Cat(P4) → C2f → mid4
2. mid4 → Upsample(2x) + Cat(P3) → C2f → F3（小目标输出）

PANet（自底向上）：
3. F3 → Conv(s=2) + Cat(mid4) → C2f → F4（中目标输出）
4. F4 → Conv(s=2) + Cat(P5) → C2f → F5（大目标输出）

## Test Plan

- nano 缩放输入，输出 F3 `[1, 64, 80, 80]`，F4 `[1, 128, 40, 40]`，F5 `[1, 256, 20, 20]`

## Done Definition

- 三路输出形状正确，build 无 warning
