# TASK-YOLO-104 YoloBackbone

- ID: `TASK-YOLO-104`
- Status: `Todo`
- Iteration: `ITER-0010`
- Requirement: `REQ-0008`
- ADR: `ADR-0011`

## Objective

实现 YoloBackbone，输出 P3/P4/P5 三尺度特征图，支持 nano/small/medium 缩放系数。

## Implementation Notes

文件路径：`src/TransformersMini.Infrastructure.TorchSharp/Detection/Backbone/YoloBackbone.cs`

缩放系数（参考 yolov8.yaml）：
- nano：depth=0.33，width=0.25，max_ch=1024
- small：depth=0.33，width=0.50，max_ch=1024
- medium：depth=0.67，width=0.75，max_ch=768

层结构（nano 基础通道 [64,128,256,512,1024] × width）：
```
P1: Conv(3, 16, 3, s=2)
P2: Conv(16, 32, 3, s=2) + C2f(32, 32, n=1)
P3: Conv(32, 64, 3, s=2) + C2f(64, 64, n=2, shortcut=true)   ← 输出 P3
P4: Conv(64, 128, 3, s=2) + C2f(128, 128, n=2, shortcut=true) ← 输出 P4
P5: Conv(128, 256, 3, s=2) + C2f(256, 256, n=1) + SPPF(256, 256) ← 输出 P5
```

`forward` 返回 `(p3, p4, p5)` 三元组。

## Test Plan

- nano 缩放，输入 `[1, 3, 640, 640]` → P3 `[1, 64, 80, 80]`，P4 `[1, 128, 40, 40]`，P5 `[1, 256, 20, 20]`

## Done Definition

- 三尺度形状正确，build 无 warning
