# TASK-YOLO-112 NmsProcessor

- ID: `TASK-YOLO-112`
- Status: `Todo`
- Iteration: `ITER-0010`
- Requirement: `REQ-0008`
- ADR: `ADR-0011`

## Objective

实现置信度阈值过滤 + IoU NMS 后处理，将 DetectHead 原始输出转为最终检测框列表。

## Implementation Notes

文件路径：`src/TransformersMini.Infrastructure.TorchSharp/Detection/PostProcess/NmsProcessor.cs`

输入：`[b, 4+nc, total_anchors]` 格式的推理输出
步骤：
1. 计算每个 anchor 的最大类别分数和类别 ID
2. conf_thresh 过滤（默认 0.25）
3. 对每张图片独立做 NMS（iou_thresh 默认 0.45）
4. 限制最大检测数（max_det=300）
5. 输出每张图片的 `List<DetectionBox>`（xyxy + conf + cls_id）

`DetectionBox` 定义为 record/struct，放在同文件或 Contracts。

## Test Plan

- 5 个高度重叠框（IoU > 0.6）NMS 后只剩 1 个
- 3 个不重叠框全部保留

## Done Definition

- NMS 正确性测试通过，build 无 warning
