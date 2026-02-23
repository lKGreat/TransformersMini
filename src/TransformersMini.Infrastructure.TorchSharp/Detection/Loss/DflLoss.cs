using TorchSharp;
using static TorchSharp.torch;
using static TorchSharp.torch.nn;

namespace TransformersMini.Infrastructure.TorchSharp.Detection.Loss;

/// <summary>
/// Distribution Focal Loss（DFL）：
/// 用于监督 anchor-free 检测头的边界框分布预测。
/// 参考：https://ieeexplore.ieee.org/document/9792391
/// </summary>
internal sealed class DflLoss : Module<Tensor, Tensor, Tensor>
{
    private readonly int _regMax;

    public DflLoss(int regMax = 16) : base(nameof(DflLoss))
    {
        _regMax = regMax;
        RegisterComponents();
    }

    /// <param name="predDist">预测分布 [N, reg_max]（logits，未经 softmax）</param>
    /// <param name="target">目标 ltrb 距离（归一化到 [0, reg_max-1]），形状 [N]</param>
    /// <returns>每个 anchor 的 DFL loss [N, 1]</returns>
    public override Tensor forward(Tensor predDist, Tensor target)
    {
        // 裁剪 target 到 [0, reg_max - 1 - 0.01]，防止越界
        using var tClamped = target.clamp(0f, _regMax - 1 - 0.01f);
        using var tl = tClamped.@long();           // 下界索引
        using var tr = (tl + 1).clamp_max(_regMax - 1); // 上界索引（钳位防越界）

        // 插值权重
        using var wl = tr.to(ScalarType.Float32) - tClamped; // 左权重
        using var wr = 1f - wl;                               // 右权重

        // 交叉熵（CE 要求 target 为 long）
        var n = predDist.shape[0];
        using var tlFlat = tl.view(-1);
        using var trFlat = tr.view(-1);
        using var ceL = functional.cross_entropy(predDist, tlFlat, reduction: nn.Reduction.None).view(tl.shape);
        using var ceR = functional.cross_entropy(predDist, trFlat, reduction: nn.Reduction.None).view(tl.shape);

        // 加权平均，保留最后一维 keepdim=true → [N, 1]
        return (ceL * wl + ceR * wr).mean(new long[] { -1 }, keepdim: true);
    }
}
