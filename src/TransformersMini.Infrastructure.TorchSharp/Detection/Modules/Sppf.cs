using TorchSharp;
using TorchSharp.Modules;
using static TorchSharp.torch;
using static TorchSharp.torch.nn;

namespace TransformersMini.Infrastructure.TorchSharp.Detection.Modules;

/// <summary>
/// SPPF（Spatial Pyramid Pooling - Fast）：Backbone 末尾的快速多尺度池化模块。
/// 结构：cv1(1×1, c1→c_) → MaxPool 迭代 n 次 → cat 所有输出 → cv2(1×1)
/// 等价于 SPP(k=(5,9,13))，但复用同一 MaxPool 层，速度更快。
/// </summary>
internal sealed class Sppf : Module<Tensor, Tensor>
{
    private readonly ConvBnAct _cv1;
    private readonly ConvBnAct _cv2;
    private readonly MaxPool2d _pool;
    private readonly int _n;

    /// <param name="inChannels">输入通道数</param>
    /// <param name="outChannels">输出通道数</param>
    /// <param name="kernelSize">MaxPool2d 核大小（默认 5）</param>
    /// <param name="poolIter">池化迭代次数（默认 3，等价于 SPP(5,9,13)）</param>
    public Sppf(long inChannels, long outChannels, long kernelSize = 5, int poolIter = 3)
        : base(nameof(Sppf))
    {
        var hiddenChannels = inChannels / 2;
        _n = poolIter;
        _cv1 = new ConvBnAct(inChannels, hiddenChannels, 1);
        // cat 后通道数 = hiddenChannels * (1 + poolIter)
        _cv2 = new ConvBnAct(hiddenChannels * (1 + poolIter), outChannels, 1);
        _pool = MaxPool2d(kernelSize, stride: 1, padding: kernelSize / 2);
        RegisterComponents();
    }

    public override Tensor forward(Tensor input)
    {
        using var y = _cv1.forward(input);

        // 依次做 n 次 MaxPool，每次以上一次输出为输入
        var poolOutputs = new List<Tensor>();
        Tensor prev = y;
        for (var i = 0; i < _n; i++)
        {
            var pooled = _pool.forward(prev);
            poolOutputs.Add(pooled);
            prev = pooled;
        }

        try
        {
            // 将原始 y 和所有池化输出拼接
            var allParts = new Tensor[1 + poolOutputs.Count];
            // 保存 y 的引用（不额外持有，y 已在 using 中管理）
            allParts[0] = y;
            for (var i = 0; i < poolOutputs.Count; i++)
                allParts[i + 1] = poolOutputs[i];

            using var catResult = torch.cat(allParts, dim: 1);
            return _cv2.forward(catResult);
        }
        finally
        {
            foreach (var p in poolOutputs)
                p.Dispose();
        }
    }
}
