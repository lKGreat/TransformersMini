using TorchSharp;
using static TorchSharp.torch;
using static TorchSharp.torch.nn;
using TransformersMini.Infrastructure.TorchSharp.Detection.Modules;

namespace TransformersMini.Infrastructure.TorchSharp.Detection.Backbone;

/// <summary>
/// YOLOv8 风格 Backbone 缩放规格。
/// 参数对应 yolov8.yaml 中的 [depth_multiple, width_multiple, max_channels]。
/// </summary>
public enum YoloScale
{
    /// <summary>nano：depth=0.33，width=0.25，max_ch=1024</summary>
    Nano,
    /// <summary>small：depth=0.33，width=0.50，max_ch=1024</summary>
    Small,
    /// <summary>medium：depth=0.67，width=0.75，max_ch=768</summary>
    Medium
}

/// <summary>
/// YOLOv8 风格 Backbone，输出 P3/P4/P5 三尺度特征图。
/// 结构参考 yolov8.yaml backbone 段，支持 nano/small/medium 缩放。
/// 输入尺寸 640×640 时：P3=80×80，P4=40×40，P5=20×20。
/// </summary>
public sealed class YoloBackbone : Module<Tensor, (Tensor P3, Tensor P4, Tensor P5)>
{
    // P1/P2 阶段（stride 2/4）
    private readonly ConvBnAct _p1Conv;
    private readonly ConvBnAct _p2Conv;
    private readonly C2f _p2C2f;

    // P3 阶段（stride 8）
    private readonly ConvBnAct _p3Conv;
    private readonly C2f _p3C2f;

    // P4 阶段（stride 16）
    private readonly ConvBnAct _p4Conv;
    private readonly C2f _p4C2f;

    // P5 阶段（stride 32）
    private readonly ConvBnAct _p5Conv;
    private readonly C2f _p5C2f;
    private readonly Sppf _p5Sppf;

    /// <summary>P3 输出通道数，供 Neck 使用。</summary>
    public long P3Channels { get; }
    /// <summary>P4 输出通道数，供 Neck 使用。</summary>
    public long P4Channels { get; }
    /// <summary>P5 输出通道数，供 Neck 使用。</summary>
    public long P5Channels { get; }

    public YoloBackbone(YoloScale scale = YoloScale.Nano) : base(nameof(YoloBackbone))
    {
        var (depthMul, widthMul, maxCh) = GetScaleParams(scale);

        // 计算各阶段通道数（基准通道数 × widthMul，向上取整到 8 的倍数，不超过 maxCh）
        long Ch(long base_) => Math.Min(maxCh, MakeDiv8((long)Math.Round(base_ * widthMul)));

        // 计算各阶段 C2f 重复数（基准重复数 × depthMul，最少 1 次）
        int Depth(int base_) => Math.Max(1, (int)Math.Round(base_ * depthMul));

        var ch16 = Ch(16);   // P1 输出通道
        var ch32 = Ch(32);   // P2 输出通道
        var ch64 = Ch(64);   // P3 输出通道
        var ch128 = Ch(128); // P4 输出通道
        var ch256 = Ch(256); // P5 输出通道

        P3Channels = ch64;
        P4Channels = ch128;
        P5Channels = ch256;

        // P1/2: stride 2 → stride 4
        _p1Conv = new ConvBnAct(3, ch16, 3, stride: 2);
        _p2Conv = new ConvBnAct(ch16, ch32, 3, stride: 2);
        _p2C2f = new C2f(ch32, ch32, n: Depth(1), shortcut: true);

        // P3: stride 8
        _p3Conv = new ConvBnAct(ch32, ch64, 3, stride: 2);
        _p3C2f = new C2f(ch64, ch64, n: Depth(2), shortcut: true);

        // P4: stride 16
        _p4Conv = new ConvBnAct(ch64, ch128, 3, stride: 2);
        _p4C2f = new C2f(ch128, ch128, n: Depth(2), shortcut: true);

        // P5: stride 32
        _p5Conv = new ConvBnAct(ch128, ch256, 3, stride: 2);
        _p5C2f = new C2f(ch256, ch256, n: Depth(1), shortcut: true);
        _p5Sppf = new Sppf(ch256, ch256);

        RegisterComponents();
    }

    public override (Tensor P3, Tensor P4, Tensor P5) forward(Tensor input)
    {
        // P1/P2 阶段（不需要保留用于 Neck）
        using var p1 = _p1Conv.forward(input);
        using var p2Conv = _p2Conv.forward(p1);
        using var p2 = _p2C2f.forward(p2Conv);

        // P3：供 FPN 上采样融合使用
        using var p3Conv = _p3Conv.forward(p2);
        var p3 = _p3C2f.forward(p3Conv);  // 保留（返回给 Neck）

        // P4：供 FPN 融合使用
        using var p4Conv = _p4Conv.forward(p3);
        var p4 = _p4C2f.forward(p4Conv);  // 保留

        // P5：SPPF 后输出
        using var p5Conv = _p5Conv.forward(p4);
        using var p5C2f = _p5C2f.forward(p5Conv);
        var p5 = _p5Sppf.forward(p5C2f);  // 保留

        return (p3, p4, p5);
    }

    // ─── 内部工具 ─────────────────────────────────────────────────────

    private static (double depth, double width, long maxCh) GetScaleParams(YoloScale scale) => scale switch
    {
        YoloScale.Nano   => (0.33, 0.25, 1024),
        YoloScale.Small  => (0.33, 0.50, 1024),
        YoloScale.Medium => (0.67, 0.75,  768),
        _ => throw new ArgumentOutOfRangeException(nameof(scale))
    };

    /// <summary>将通道数向上对齐到 8 的倍数（与 ultralytics make_divisible 一致）。</summary>
    private static long MakeDiv8(long x) => (long)Math.Ceiling(x / 8.0) * 8;
}
