# TASK-YOLO-113 YoloDetectionModel 完整封装

- ID: `TASK-YOLO-113`
- Status: `Todo`
- Iteration: `ITER-0010`
- Requirement: `REQ-0008`
- ADR: `ADR-0011`

## Objective

将 YoloBackbone + PanNeck + DetectHead 封装为单一 `YoloDetectionModel`，供训练和推理共用。

## Implementation Notes

文件路径：`src/TransformersMini.Infrastructure.TorchSharp/Detection/YoloDetectionModel.cs`

构造参数：`nc`（类别数）、`scale`（nano/small/medium）、`reg_max`（默认 16）
forward 逻辑：
- `p3, p4, p5 = backbone(x)`
- `f3, f4, f5 = neck(p3, p4, p5)`
- `output = head([f3, f4, f5])`
- 返回 head 输出（训练返回字典，推理返回解码后张量）

## Test Plan

- nano 模型，输入 `[1, 3, 640, 640]`，训练模式前向无异常
- 推理模式输出形状 `[1, 4+nc, 8400]`

## Done Definition

- 端到端前向通过，build 无 warning
