# TASK-YOLO-107 DetectHead（多尺度 Anchor-free 检测头）

- ID: `TASK-YOLO-107`
- Status: `Todo`
- Iteration: `ITER-0010`
- Requirement: `REQ-0008`
- ADR: `ADR-0011`

## Objective

实现多尺度 Anchor-free 检测头，输出 boxes（DFL 分布）和 scores（类别分数）。

## Implementation Notes

文件路径：`src/TransformersMini.Infrastructure.TorchSharp/Detection/Head/DetectHead.cs`

每个尺度：
- `cv2[i]`：ConvBnAct(ch, c2, 3) + ConvBnAct(c2, c2, 3) + Conv2d(c2, 4*reg_max, 1)，输出 box 分布
- `cv3[i]`：DWConv + Conv(1) + DWConv + Conv(1) + Conv2d(nc, 1)，输出 cls logits

训练模式：返回 `(boxes_raw, scores_raw, feats)` 字典
推理模式：通过 DflLayer 解码 boxes → 加上 anchor 点 → 转换为 xyxy 格式，再 sigmoid(scores)

Anchor 生成：基于输入特征图尺寸按 stride 生成网格点（`make_anchors`）

## Test Plan

- 3尺度输入，训练模式 forward 返回正确字典键
- 推理模式 forward 输出 `[b, 4+nc, total_anchors]`

## Done Definition

- 训练/推理模式均通过形状测试，build 无 warning
