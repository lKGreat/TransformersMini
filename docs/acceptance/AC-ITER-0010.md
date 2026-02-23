# AC-ITER-0010 YOLO 检测架构升级 验收标准

- Iteration: `ITER-0010`
- Status: `Pending`

## 验收检查项

### 构建质量
- [ ] `dotnet build .\TransformersMini.slnx -c Release` 输出 `0 errors, 0 warnings`

### 模块形状正确性（单元测试）
- [ ] C2f 输入 `[1, 128, 40, 40]` → 输出 `[1, 256, 40, 40]` 形状正确
- [ ] SPPF 输入 `[1, 1024, 20, 20]` → 输出 `[1, 1024, 20, 20]` 形状正确
- [ ] YoloBackbone 输入 `[1, 3, 640, 640]` → 输出 P3 `[1, 256, 80, 80]`、P4 `[1, 512, 40, 40]`、P5 `[1, 1024, 20, 20]`（nano 缩放后通道数按比例缩小）
- [ ] PanNeck 输出 F3/F4/F5 形状与输入 P3/P4/P5 空间尺寸一致
- [ ] DetectHead 输出 boxes 和 scores 形状符合预期

### 损失函数（单元测试）
- [ ] YoloDetectionLoss 在随机输入下可以正常前向传播（无 NaN/Inf）
- [ ] 训练 5 steps 后 total loss 可观测下降趋势

### 后处理（单元测试）
- [ ] NmsProcessor 对 5 个重叠框（IoU > 0.6）只保留 1 个
- [ ] NmsProcessor 对 3 个不重叠框均保留

### 集成测试
- [ ] mini COCO（2张图片）训练 1 epoch 无异常退出
- [ ] 推理任务可以加载训练后模型并产出带检测框的推理报告

### 契约兼容性
- [ ] `DetectionModelMetadata` 新字段（BackboneScale、RegMax）可正确序列化/反序列化
- [ ] 现有 config JSON （sample.det.train.json）可正常解析（向后兼容）
