# ADR-0011 采用 YOLOv8 风格架构升级目标检测能力

- ID: `ADR-0011`
- Status: `Accepted`
- Date: 2026-02-23
- Related Requirements: `REQ-0008`

## Context

当前检测模型（`TinyMultiHeadDetectorModel`）为原型级轻量 CNN：
- Backbone：3 层 Conv2d + AdaptiveAvgPool，输出单一全局特征向量，无空间分辨率
- Neck：无 FPN/PAN，直接将特征 Flatten 后接全连接层
- Head：FC 多分支（bbox/category/objectness），TopK 固定框编码方式
- Loss：MSE（bbox）+ BCE（objectness），无正负样本分配机制
- 后处理：无 NMS，重叠框无法抑制
- 数据增强：仅 Resize，缺乏多样性

这些限制导致模型在真实 COCO 数据上检测精度极低，无法支撑实际场景。

## Decision

以 ultralytics YOLOv8 为参考，使用 C#/TorchSharp 复刻其核心检测架构：

1. **Backbone**：Conv + C2f（CSP Bottleneck 变体）+ SPPF，输出 P3/P4/P5 三尺度特征图
2. **Neck**：PAN（Path Aggregation Network）：FPN 自顶向下上采样融合 + PANet 自底向上下采样融合
3. **Head**：Anchor-free 多尺度 Detect Head（DFL 边界框分布回归 + 卷积分类分支）
4. **Loss**：TaskAlignedAssigner + CIoU Loss + DFL Loss + BCE Cls Loss
5. **后处理**：Conf 阈值过滤 + IoU NMS
6. **数据增强**：Mosaic 4拼 + 随机水平翻转
7. **训练策略**：EMA（指数移动平均）+ Warmup 余弦学习率

所有代码以 C#/TorchSharp 实现，放置于 `src/TransformersMini.Infrastructure.TorchSharp/Detection/`，保持现有分层架构不变。

## Consequences

**正面：**
- 大幅提升检测精度（anchor-free 多尺度、更强 backbone/neck）
- 损失函数更合理（TAL 正负样本分配、CIoU/DFL）
- 支持 NMS 后处理，输出可用检测框
- 数据增强增加训练多样性

**负面/成本：**
- 实现工作量显著增加（约 10 个 Phase，20+ 文件）
- TorchSharp C# 需手动实现 Python 动态特性（如 YAML 解析模型的动态图构建需静态化）
- 训练资源消耗增加（多尺度特征图占用更多显存）
- 需同步更新 `DetectionModelMetadata` 契约以承载新架构参数

## Alternatives Considered

1. **直接加深当前 CNN**：成本低但改善有限，Neck 缺失问题无法解决
2. **引入预训练 ONNX 权重**：复杂度高，与当前 TorchSharp 训练链路不兼容
3. **MobileNet/EfficientDet**：精度/速度折中，但缺少与当前 COCO 数据流的对齐参考

## Follow-up

- 实现完成后补充 `AC-ITER-0010.md` 验收证据
- 后续可扩展：ONNX 导出、分割头（Segment）、多类别增量学习
