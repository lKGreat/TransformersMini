# TASK-YOLO-133 集成测试：mini COCO 跑通训练 1 epoch

- ID: `TASK-YOLO-133`
- Status: `Todo`
- Iteration: `ITER-0010`
- Requirement: `REQ-0008`

## Objective

使用 mini COCO 数据（2张图片）完整跑通训练 1 epoch，验证端到端链路无异常。

## Implementation Notes

文件：`tests/TransformersMini.Tests.Integration/Detection/YoloDetectionTrainingIntegrationTest.cs`

步骤：
1. 加载 data/samples/detection/ 中的 mini COCO 数据
2. 以 nano 缩放运行 1 epoch 训练
3. 断言 loss 为有限值
4. 断言模型权重文件已写入 runs/

## Done Definition

- 集成测试通过，无未处理异常
