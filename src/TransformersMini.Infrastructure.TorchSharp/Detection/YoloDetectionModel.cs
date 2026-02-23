using TorchSharp;
using static TorchSharp.torch;
using static TorchSharp.torch.nn;
using TransformersMini.Infrastructure.TorchSharp.Detection.Backbone;
using TransformersMini.Infrastructure.TorchSharp.Detection.Head;
using TransformersMini.Infrastructure.TorchSharp.Detection.Neck;

namespace TransformersMini.Infrastructure.TorchSharp.Detection;

/// <summary>
/// YOLOv8 风格完整检测模型，封装 Backbone + PanNeck + DetectHead。
/// 支持训练和推理两种前向模式（通过 torch.is_grad_enabled() 区分）。
/// 使用方：
///   训练：model.train() → forward → DetectHeadOutput.IsTraining==true
///   推理：model.eval() → torch.no_grad() → forward → IsTraining==false
/// </summary>
public sealed class YoloDetectionModel : Module<Tensor, DetectHeadOutput>
{
    private readonly YoloBackbone _backbone;
    private readonly PanNeck _neck;
    private readonly DetectHead _head;

    /// <summary>类别数。</summary>
    public int NumClasses { get; }
    /// <summary>缩放规格。</summary>
    public YoloScale Scale { get; }
    /// <summary>DFL reg_max。</summary>
    public int RegMax { get; }
    /// <summary>各尺度 stride。</summary>
    public int[] Strides { get; } = [8, 16, 32];

    /// <param name="nc">类别数（默认 80）</param>
    /// <param name="scale">模型缩放规格（默认 nano）</param>
    /// <param name="regMax">DFL 分布最大值（默认 16）</param>
    public YoloDetectionModel(int nc = 80, YoloScale scale = YoloScale.Nano, int regMax = 16)
        : base(nameof(YoloDetectionModel))
    {
        NumClasses = nc;
        Scale = scale;
        RegMax = regMax;

        _backbone = new YoloBackbone(scale);
        _neck = new PanNeck(_backbone.P3Channels, _backbone.P4Channels, _backbone.P5Channels);

        var channels = new long[] { _neck.F3Channels, _neck.F4Channels, _neck.F5Channels };
        _head = new DetectHead(nc, regMax, channels, Strides);

        RegisterComponents();
    }

    public override DetectHeadOutput forward(Tensor input)
    {
        // Backbone → (P3, P4, P5)
        var (p3, p4, p5) = _backbone.forward(input);
        using var p3D = p3;
        using var p4D = p4;
        using var p5D = p5;

        // PAN Neck → (F3, F4, F5)（所有权转移给 DetectHeadOutput.Feats，由调用方在 loss 计算后负责释放）
        var (f3, f4, f5) = _neck.forward((p3, p4, p5));

        // Detect Head → DetectHeadOutput
        return _head.forward(new List<Tensor> { f3, f4, f5 });
    }
}
