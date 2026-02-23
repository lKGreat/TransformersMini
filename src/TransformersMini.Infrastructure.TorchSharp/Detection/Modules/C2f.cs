using TorchSharp;
using TorchSharp.Modules;
using static TorchSharp.torch;
using static TorchSharp.torch.nn;

namespace TransformersMini.Infrastructure.TorchSharp.Detection.Modules;

/// <summary>
/// C2f（CSP Bottleneck with 2 convolutions）：YOLOv8 核心特征提取模块。
/// 结构：cv1(1×1) → chunk(2) → [n 个 Bottleneck 串联] → cat → cv2(1×1)
/// </summary>
internal sealed class C2f : Module<Tensor, Tensor>
{
    private readonly long _hiddenChannels;
    private readonly ConvBnAct _cv1;
    private readonly ConvBnAct _cv2;
    private readonly ModuleList<Bottleneck> _bottlenecks;

    /// <param name="inChannels">输入通道数</param>
    /// <param name="outChannels">输出通道数</param>
    /// <param name="n">Bottleneck 块数量</param>
    /// <param name="shortcut">是否使用残差连接</param>
    /// <param name="groups">分组卷积数</param>
    /// <param name="expansion">隐藏通道膨胀比例</param>
    public C2f(
        long inChannels,
        long outChannels,
        int n = 1,
        bool shortcut = false,
        long groups = 1,
        float expansion = 0.5f)
        : base(nameof(C2f))
    {
        _hiddenChannels = (long)(outChannels * expansion);
        // cv1 将 inChannels 映射到 2 * hiddenChannels，之后 chunk(2) 拆成两半
        _cv1 = new ConvBnAct(inChannels, 2 * _hiddenChannels, 1);
        // cv2 将 (2 + n) * hiddenChannels 拼接后映射到 outChannels
        _cv2 = new ConvBnAct((2 + n) * _hiddenChannels, outChannels, 1);
        _bottlenecks = new ModuleList<Bottleneck>(
            Enumerable.Range(0, n)
                .Select(_ => new Bottleneck(_hiddenChannels, _hiddenChannels, shortcut, groups))
                .ToArray());
        RegisterComponents();
    }

    public override Tensor forward(Tensor input)
    {
        // cv1 → 分成 [y0, y1]，y0 直接保留，y1 依次经过每个 Bottleneck
        using var afterCv1 = _cv1.forward(input);
        var chunks = afterCv1.chunk(2, dim: 1);
        // chunks[0] 和 chunks[1] 都是新张量，需要追踪以便释放
        var parts = new List<Tensor> { chunks[0], chunks[1] };

        try
        {
            foreach (var block in _bottlenecks)
            {
                // 每个 block 的输出追加到 parts，前一个 block 的输出已在 parts 中
                var next = block.forward(parts[^1]);
                parts.Add(next);
            }

            // cat 所有 parts 沿 channel 维度
            using var catResult = torch.cat(parts.ToArray(), dim: 1);
            return _cv2.forward(catResult);
        }
        finally
        {
            // 释放中间 tensor（排除最后一个，因为 cat 已经使用完毕）
            foreach (var p in parts)
                p.Dispose();
        }
    }
}
