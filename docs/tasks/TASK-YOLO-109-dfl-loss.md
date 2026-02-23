# TASK-YOLO-109 DflLoss

- ID: `TASK-YOLO-109`
- Status: `Todo`
- Iteration: `ITER-0010`
- Requirement: `REQ-0008`
- ADR: `ADR-0011`

## Objective

实现 Distribution Focal Loss，用于边界框分布预测的监督。

## Implementation Notes

文件路径：`src/TransformersMini.Infrastructure.TorchSharp/Detection/Loss/DflLoss.cs`

逻辑（参考 ultralytics DFLoss）：
1. target 裁剪到 [0, reg_max - 1 - 0.01]
2. tl = floor(target)，tr = tl + 1
3. wl = tr - target，wr = 1 - wl
4. loss = CE(pred, tl) * wl + CE(pred, tr) * wr，在最后维度 mean

## Test Plan

- 随机 pred/target 前向传播无 NaN，输出标量

## Done Definition

- 单元测试通过，build 无 warning
