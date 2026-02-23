using TorchSharp;
using static TorchSharp.torch;
using static TorchSharp.torch.nn;

namespace TransformersMini.Infrastructure.TorchSharp.Detection.Head;

/// <summary>
/// DFL 解码层（Distribution Focal Loss Decoder）：
/// 将 (b, anchors, 4*reg_max) 的分布预测转换为 (b, anchors, 4) ltrb 偏移。
/// 推理时使用，训练时损失函数直接操作分布预测。
/// </summary>
public sealed class DflLayer : Module<Tensor, Tensor>
{
    private readonly int _regMax;

    public DflLayer(int regMax = 16) : base(nameof(DflLayer))
    {
        _regMax = regMax;
        // 注册 proj buffer：[0, 1, 2, ..., reg_max-1]，用于期望值计算
        register_buffer("proj", arange(regMax, dtype: ScalarType.Float32));
        RegisterComponents();
    }

    public override Tensor forward(Tensor input)
    {
        // input: [b, anchors, 4 * reg_max]
        var b = input.shape[0];
        var a = input.shape[1];

        var proj = get_buffer("proj")!;

        // reshape → [b, anchors, 4, reg_max] → softmax → matmul(proj) → [b, anchors, 4]
        using var reshaped = input.view(b, a, 4, _regMax);
        using var softmaxed = functional.softmax(reshaped, dim: -1);
        return torch.matmul(softmaxed, proj.to(input.dtype));
    }
}
