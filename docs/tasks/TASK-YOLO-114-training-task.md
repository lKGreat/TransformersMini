# TASK-YOLO-114 DetectionTrainingTask 替换为 YoloDetectionModel

- ID: `TASK-YOLO-114`
- Status: `Todo`
- Iteration: `ITER-0010`
- Requirement: `REQ-0008`
- ADR: `ADR-0011`

## Objective

将 DetectionTrainingTask 中的 TinyMultiHeadDetectorModel 替换为 YoloDetectionModel，同时集成 YoloDetectionLoss。

## Implementation Notes

文件：`src/TransformersMini.Training.Tasks.Detection/DetectionTrainingTask.cs`

变更：
1. 引用 `YoloDetectionModel`（从 Infrastructure.TorchSharp.Detection）
2. 替换 loss 计算为 `YoloDetectionLoss`
3. 更新数据批次格式（需提供 batch_idx、cls、bboxes 三个张量）
4. 保留现有训练报告/快照写入逻辑
5. 保留 EMA 占位（Phase 9 完善）

## Test Plan

- dry-run 命令行能正常加载 config 并完成 model 初始化

## Done Definition

- CLI dry-run 通过，build 无 warning
