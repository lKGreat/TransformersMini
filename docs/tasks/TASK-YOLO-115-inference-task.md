# TASK-YOLO-115 DetectionInferenceTask 集成 NmsProcessor

- ID: `TASK-YOLO-115`
- Status: `Todo`
- Iteration: `ITER-0010`
- Requirement: `REQ-0008`
- ADR: `ADR-0011`

## Objective

更新 DetectionInferenceTask，使用 YoloDetectionModel 推理，并通过 NmsProcessor 输出最终检测框。

## Implementation Notes

文件：`src/TransformersMini.Training.Tasks.Detection/DetectionInferenceTask.cs`

变更：
1. 替换 TinyMultiHeadDetectorInferModel 为 YoloDetectionModel（eval 模式）
2. 加载模型权重后，推理输出经 NmsProcessor 处理
3. 将 NmsProcessor 输出的 DetectionBox 列表写入推理报告

## Done Definition

- 推理报告包含有效检测框字段，build 无 warning
