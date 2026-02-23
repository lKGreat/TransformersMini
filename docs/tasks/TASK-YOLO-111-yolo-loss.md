# TASK-YOLO-111 YoloDetectionLoss

- ID: `TASK-YOLO-111`
- Status: `Todo`
- Iteration: `ITER-0010`
- Requirement: `REQ-0008`
- ADR: `ADR-0011`

## Objective

组合 TaskAlignedAssigner、CIoULoss、DflLoss 和 BCE Cls Loss，实现完整的 YOLOv8 检测损失。

## Implementation Notes

文件路径：`src/TransformersMini.Infrastructure.TorchSharp/Detection/Loss/YoloDetectionLoss.cs`

组件：
- assigner：TaskAlignedAssigner
- bbox_loss：BboxLoss（CIoU + DFL）
- bce：BCEWithLogitsLoss
- proj：arange(reg_max)

forward 步骤：
1. 从 DetectHead 输出提取 boxes_distri 和 scores
2. make_anchors 生成 anchor points 和 stride tensor
3. assigner 分配正负样本
4. 计算 BCE cls loss（全 anchor）
5. 对正样本计算 CIoU + DFL loss
6. 按权重组合：loss_box * 7.5、loss_cls * 0.5、loss_dfl * 1.5

## Test Plan

- 随机 batch 前向无 NaN，返回 3 元素 loss 向量

## Done Definition

- loss 有限，可反向传播，build 无 warning
