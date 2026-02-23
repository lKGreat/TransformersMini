# TASK-YOLO-106 DflLayer

- ID: `TASK-YOLO-106`
- Status: `Todo`
- Iteration: `ITER-0010`
- Requirement: `REQ-0008`
- ADR: `ADR-0011`

## Objective

实现 Distribution Focal Loss 的解码层：将 (b, a, 4*reg_max) 分布预测转换为 (b, a, 4) ltrb 偏移。

## Implementation Notes

文件路径：`src/TransformersMini.Infrastructure.TorchSharp/Detection/Head/DflLayer.cs`

逻辑：
1. 注册 `proj` buffer：`torch.arange(reg_max)` 形状 `[reg_max]`
2. forward：输入 `[b, a, 4*reg_max]` → reshape `[b, a, 4, reg_max]` → softmax(dim=-1) → matmul(proj) → `[b, a, 4]`

## Test Plan

- reg_max=16，输入 `[2, 8400, 64]` → 输出 `[2, 8400, 4]`

## Done Definition

- 形状测试通过，build 无 warning
