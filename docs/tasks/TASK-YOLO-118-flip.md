# TASK-YOLO-118 RandomFlipAugmentation

- ID: `TASK-YOLO-118`
- Status: `Todo`
- Iteration: `ITER-0010`
- Requirement: `REQ-0008`
- ADR: `ADR-0011`

## Objective

实现随机水平翻转增强，同步更新 GT 框的 x 坐标。

## Implementation Notes

文件：`src/TransformersMini.DataAdapters.Coco/Augmentation/RandomFlipAugmentation.cs`

- 以概率 p（默认 0.5）水平翻转图片
- bbox [x1, y1, x2, y2] 变换：x1_new = W - x2，x2_new = W - x1
- 归一化坐标：x1_new = 1 - x2，x2_new = 1 - x1

## Done Definition

- 翻转后框坐标正确，build 无 warning
