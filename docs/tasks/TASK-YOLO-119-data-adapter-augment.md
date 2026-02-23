# TASK-YOLO-119 CocoDataAdapter 接入增强 Pipeline

- ID: `TASK-YOLO-119`
- Status: `Todo`
- Iteration: `ITER-0010`
- Requirement: `REQ-0008`
- ADR: `ADR-0011`

## Objective

将 MosaicAugmentation 和 RandomFlipAugmentation 接入 CocoDataAdapter 的数据加载 pipeline。

## Implementation Notes

文件：`src/TransformersMini.DataAdapters.Coco/CocoDataAdapter.cs`

- 训练模式下启用增强（通过 config 中 augmentation 开关控制）
- 新增 `useAugmentation` 选项到训练 config（默认 false，兼容现有配置）
- Mosaic 增强需要访问其他样本（传入整个 dataset 列表）

## Done Definition

- 开关控制正常，增强 pipeline 可选启用，build 无 warning
