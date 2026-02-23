using TorchSharp;
using TorchSharp.Modules;
using static TorchSharp.torch;
using static TorchSharp.torch.nn;

namespace TransformersMini.Infrastructure.TorchSharp.Detection.Modules;

/// <summary>
/// Conv2d + BatchNorm2d + SiLU 标准卷积块，YOLOv8 所有卷积的基础单元。
/// padding 默认为 kernel//2（same 卷积），可通过参数覆盖。
/// </summary>
internal sealed class ConvBnAct : Module<Tensor, Tensor>
{
    private readonly Conv2d _conv;
    private readonly BatchNorm2d _bn;

    public ConvBnAct(
        long inChannels,
        long outChannels,
        long kernelSize = 1,
        long stride = 1,
        long? padding = null,
        long groups = 1)
        : base(nameof(ConvBnAct))
    {
        var pad = padding ?? (kernelSize / 2);
        _conv = Conv2d(inChannels, outChannels, kernelSize,
            stride: stride, padding: pad, groups: groups, bias: false);
        _bn = BatchNorm2d(outChannels);
        RegisterComponents();
    }

    public override Tensor forward(Tensor input)
    {
        using var c = _conv.forward(input);
        using var b = _bn.forward(c);
        return functional.silu(b);
    }
}

/// <summary>
/// 深度可分离卷积：groups = in_channels，用于轻量化 Detect Head 分支。
/// </summary>
internal sealed class DepthwiseConv : Module<Tensor, Tensor>
{
    private readonly ConvBnAct _dw;

    public DepthwiseConv(long channels, long kernelSize = 3, long stride = 1)
        : base(nameof(DepthwiseConv))
    {
        _dw = new ConvBnAct(channels, channels, kernelSize, stride, groups: channels);
        RegisterComponents();
    }

    public override Tensor forward(Tensor input) => _dw.forward(input);
}

/// <summary>
/// Bottleneck：两个 ConvBnAct（1×1 + 3×3），可选残差连接（shortcut）。
/// 当 shortcut=true 且 in==out 时才加残差，与 YOLOv8 逻辑一致。
/// </summary>
internal sealed class Bottleneck : Module<Tensor, Tensor>
{
    private readonly ConvBnAct _cv1;
    private readonly ConvBnAct _cv2;
    private readonly bool _addResidual;

    public Bottleneck(
        long inChannels,
        long outChannels,
        bool shortcut = true,
        long groups = 1,
        float expansion = 1.0f)
        : base(nameof(Bottleneck))
    {
        var hiddenChannels = (long)(outChannels * expansion);
        _cv1 = new ConvBnAct(inChannels, hiddenChannels, 3, padding: 1);
        _cv2 = new ConvBnAct(hiddenChannels, outChannels, 3, padding: 1, groups: groups);
        _addResidual = shortcut && inChannels == outChannels;
        RegisterComponents();
    }

    public override Tensor forward(Tensor input)
    {
        using var h = _cv1.forward(input);
        var out2 = _cv2.forward(h);
        if (!_addResidual)
            return out2;
        using var tmp = out2;
        return tmp + input;
    }
}
