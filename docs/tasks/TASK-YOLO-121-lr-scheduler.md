# TASK-YOLO-121 余弦学习率调度器（Warmup + CosineAnnealing）

- ID: `TASK-YOLO-121`
- Status: `Todo`
- Iteration: `ITER-0010`
- Requirement: `REQ-0008`
- ADR: `ADR-0011`

## Objective

实现 Warmup + 余弦退火学习率调度，提升训练稳定性和最终精度。

## Implementation Notes

文件：`src/TransformersMini.Infrastructure.TorchSharp/Detection/Training/CosineWarmupScheduler.cs`

逻辑：
- warmup_epochs（默认 3）：lr 从 lr_min 线性增长到 lr_max
- 之后：`lr = lr_min + 0.5*(lr_max-lr_min)*(1 + cos(pi * epoch / total_epochs))`
- 提供 `GetLr(epoch, step, steps_per_epoch)` 方法

## Done Definition

- warmup 阶段 lr 单调递增，余弦阶段 lr 单调递减，build 无 warning
