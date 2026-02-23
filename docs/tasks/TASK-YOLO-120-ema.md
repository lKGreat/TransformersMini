# TASK-YOLO-120 ModelEma（指数移动平均）

- ID: `TASK-YOLO-120`
- Status: `Todo`
- Iteration: `ITER-0010`
- Requirement: `REQ-0008`
- ADR: `ADR-0011`

## Objective

实现 EMA，在训练中维护模型参数的指数移动平均副本，提升推理稳定性。

## Implementation Notes

文件：`src/TransformersMini.Infrastructure.TorchSharp/Detection/Training/ModelEma.cs`

逻辑：
- decay = 0.9999，tau = 2000（warmup 阶段 decay 较小）
- `d = decay * (1 - exp(-steps / tau))`
- 每步 update：`ema_param = d * ema_param + (1-d) * model_param`
- 提供 `ApplyToModel(model)` 将 EMA 权重覆盖到推理模型

## Done Definition

- EMA 参数在 warmup 阶段 decay 正确增长，build 无 warning
