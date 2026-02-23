# REQ-0008 YOLO 风格检测架构升级

- ID: `REQ-0008`
- Status: `InProgress`
- Owner: AI
- Related ADRs: `ADR-0011`
- Related Iteration: `ITER-0010`

## Background

TransformersMini 目标检测训练链路（REQ-0002）已完成 MVP，但底层模型为原型级轻量 CNN，精度不足以支撑真实场景。需参考 ultralytics YOLOv8 架构，在 C#/TorchSharp 框架下实现完整检测能力。

## Goal

将目标检测模型从轻量 CNN 原型升级为 YOLOv8 风格架构，涵盖 Backbone（C2f+SPPF）、Neck（PAN）、Head（anchor-free DFL）、Loss（TAL+CIoU+DFL）、NMS 后处理及数据增强，使其能在 COCO 格式数据集上取得合理训练收敛。

## Success Criteria

1. YoloBackbone 输出 P3/P4/P5 三尺度特征，形状符合预期（输入 640×640 时输出 80×80/40×40/20×20）
2. PanNeck 正确融合三尺度特征并保持形状
3. DetectHead 输出 `(boxes, scores)` 格式，支持训练与推理两种模式
4. YoloDetectionLoss 在 mini COCO 数据上可以正常下降（总 loss 第 1 epoch 后可观测下降趋势）
5. NmsProcessor 能正确过滤重叠框，单张图片推理输出有效检测结果
6. `dotnet build -c Release` 无 error、无 warning
7. 单元测试覆盖：C2f/SPPF 形状测试、Loss 下降测试、NMS 正确性测试

## In Scope

- `src/TransformersMini.Infrastructure.TorchSharp/Detection/` 下所有新模块
- `DetectionModelMetadata` 契约扩展（新增 backbone_scale、reg_max 字段）
- `DetectionTrainingTask` 替换为新模型
- `DetectionInferenceTask` 集成 NmsProcessor
- Mosaic4 + RandomFlip 数据增强接入 CocoDataAdapter
- EMA + 余弦学习率调度器
- 相关单元/集成测试

## Out of Scope

- ONNX 导出
- 分割/关键点头（Segment/Pose）
- 多 GPU 训练
- 模型量化/剪枝
- WinForms 可视化升级（可在后续迭代处理）

## Constraints

- 使用 C#/.NET 10 + TorchSharp
- 不引入新的第三方 NuGet 包（除非已在 `Directory.Packages.props` 中）
- 保持与现有 COCO DataAdapter / Orchestrator 接口兼容
- 所有注释为中文 UTF-8

## Acceptance Criteria

参见 `docs/acceptance/AC-ITER-0010.md`

## Risks

- TorchSharp C# 对部分动态操作（如 `chunk`、`split` 等）支持情况需验证
- CUDA 显存消耗增加，可能影响小 GPU 场景
- TaskAlignedAssigner 实现复杂度高，需充分测试

## Change Log

| 日期 | 变更 |
|------|------|
| 2026-02-23 | 创建初稿 |
