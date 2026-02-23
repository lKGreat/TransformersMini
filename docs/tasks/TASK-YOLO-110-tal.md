# TASK-YOLO-110 TaskAlignedAssigner

- ID: `TASK-YOLO-110`
- Status: `Todo`
- Iteration: `ITER-0010`
- Requirement: `REQ-0008`
- ADR: `ADR-0011`

## Objective

实现任务对齐正负样本分配器，结合分类分数和定位质量选取 topk 正样本。

## Implementation Notes

文件路径：`src/TransformersMini.Infrastructure.TorchSharp/Detection/Loss/TaskAlignedAssigner.cs`

参数：topk=10，num_classes，alpha=0.5，beta=6.0，eps=1e-9

核心逻辑：
1. 计算 anchor 是否在 gt box 内（in_gts mask）
2. 计算对齐指标：`score^alpha * iou^beta`
3. 对每个 gt，选取 topk 最高对齐指标的 anchor 作为正样本
4. 处理多 gt 竞争同一 anchor 的情况（取最高指标的 gt）
5. 输出：target_labels、target_bboxes、target_scores、fg_mask、target_gt_idx

## Test Plan

- batch=2，8400 anchors，3 个 gt 框，输出 fg_mask 形状 `[2, 8400]`，正样本数量 > 0

## Done Definition

- 输出形状正确，正样本分配合理，build 无 warning
