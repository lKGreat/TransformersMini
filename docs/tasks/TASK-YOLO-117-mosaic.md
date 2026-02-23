# TASK-YOLO-117 MosaicAugmentation

- ID: `TASK-YOLO-117`
- Status: `Todo`
- Iteration: `ITER-0010`
- Requirement: `REQ-0008`
- ADR: `ADR-0011`

## Objective

实现 Mosaic 4拼数据增强，将 4 张图片随机拼贴为 1 张，同步变换 GT 框坐标。

## Implementation Notes

文件：`src/TransformersMini.DataAdapters.Coco/Augmentation/MosaicAugmentation.cs`

逻辑：
1. 随机选取 4 张图片（含当前样本）
2. 创建 2*target_size × 2*target_size 画布，随机放置中心点
3. 四个象限分别缩放粘贴各图片
4. 裁剪到 target_size × target_size
5. 同步变换并裁剪 GT 框，过滤过小框

## Done Definition

- 输出图片尺寸正确，GT 框在图片范围内，build 无 warning
