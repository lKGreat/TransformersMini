# ITER-0010 YOLO 检测架构升级

- ID: `ITER-0010`
- Status: `InProgress`
- Start: 2026-02-23
- End:
- Related Requirements: `REQ-0008`
- Related ADRs: `ADR-0011`

## Objective

将目标检测链路从轻量 CNN 原型升级为 YOLOv8 风格完整检测架构，提供可训练、可推理的高质量检测能力基线。

## Scope

- Phase 0：文档（ADR/REQ/ITER/TASK）
- Phase 1：基础模块（ConvBnAct、Bottleneck、C2f、Sppf）
- Phase 2：Backbone（YoloBackbone，P3/P4/P5 三尺度输出）
- Phase 3：Neck（PanNeck，FPN+PANet 特征融合）
- Phase 4：Head（DflLayer、DetectHead，多尺度 anchor-free）
- Phase 5：损失函数（CIoULoss、DflLoss、TaskAlignedAssigner、YoloDetectionLoss）
- Phase 6：后处理（NmsProcessor）
- Phase 7：完整模型集成（YoloDetectionModel，替换训练/推理任务中旧模型，更新契约）
- Phase 8：数据增强（Mosaic4、RandomFlip，接入 CocoDataAdapter）
- Phase 9：训练策略（ModelEma、余弦学习率调度器）
- Phase 10：测试（单元测试 + 集成测试）

## Non-Goals

- ONNX 导出
- WinForms 可视化升级
- 多 GPU 支持

## Tasks

| 任务 ID | 描述 | 状态 |
|---------|------|------|
| TASK-YOLO-101 | ConvBnAct / DepthwiseConv / Bottleneck | Todo |
| TASK-YOLO-102 | C2f 模块 | Todo |
| TASK-YOLO-103 | Sppf 模块 | Todo |
| TASK-YOLO-104 | YoloBackbone（P3/P4/P5 三尺度） | Todo |
| TASK-YOLO-105 | PanNeck（FPN+PANet） | Todo |
| TASK-YOLO-106 | DflLayer | Todo |
| TASK-YOLO-107 | DetectHead（多尺度 anchor-free） | Todo |
| TASK-YOLO-108 | CIoULoss 工具函数 | Todo |
| TASK-YOLO-109 | DflLoss | Todo |
| TASK-YOLO-110 | TaskAlignedAssigner | Todo |
| TASK-YOLO-111 | YoloDetectionLoss | Todo |
| TASK-YOLO-112 | NmsProcessor | Todo |
| TASK-YOLO-113 | YoloDetectionModel 封装 | Todo |
| TASK-YOLO-114 | DetectionTrainingTask 替换 | Todo |
| TASK-YOLO-115 | DetectionInferenceTask 集成 NMS | Todo |
| TASK-YOLO-116 | DetectionModelMetadata 契约扩展 | Todo |
| TASK-YOLO-117 | MosaicAugmentation | Todo |
| TASK-YOLO-118 | RandomFlipAugmentation | Todo |
| TASK-YOLO-119 | CocoDataAdapter 增强接入 | Todo |
| TASK-YOLO-120 | ModelEma | Todo |
| TASK-YOLO-121 | 余弦学习率调度器 | Todo |
| TASK-YOLO-130 | 单元测试：C2f/SPPF 形状 | Todo |
| TASK-YOLO-131 | 单元测试：YoloDetectionLoss | Todo |
| TASK-YOLO-132 | 单元测试：NmsProcessor | Todo |
| TASK-YOLO-133 | 集成测试：mini COCO 跑通 | Todo |

## Risks

- TorchSharp 对部分 tensor 操作（chunk/split）API 差异
- 多尺度特征图显存消耗
- TaskAlignedAssigner 实现复杂度

## Exit Criteria

- `dotnet build -c Release` 0 errors / 0 warnings
- mini COCO 数据训练 1 epoch 无异常，loss 可观测下降
- 所有单元测试通过
