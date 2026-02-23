using TorchSharp;
using TorchSharp.Modules;
using static TorchSharp.torch;
using static TorchSharp.torch.nn;
using TransformersMini.Infrastructure.TorchSharp.Detection.Modules;

namespace TransformersMini.Infrastructure.TorchSharp.Detection.Head;

/// <summary>
/// 多尺度 Anchor-free 检测头，对应 YOLOv8 Detect 模块。
/// 每个尺度独立处理，输出：
///   训练模式：字典 { "boxes_distri": [b, 4*reg_max, total_a], "scores": [b, nc, total_a], "feats": [...] }
///   推理模式：[b, 4+nc, total_a]（boxes 已由 DflLayer 解码为 ltrb，再转 xyxy）
/// </summary>
public sealed class DetectHead : Module<IReadOnlyList<Tensor>, DetectHeadOutput>
{
    private readonly int _nc;        // 类别数
    private readonly int _nl;        // 检测层数（尺度数）
    private readonly int _regMax;    // DFL reg_max

    // box 回归分支（每尺度独立）：ConvBnAct×2 + Conv2d(4*reg_max)
    private readonly ModuleList<Sequential> _cv2;
    // 分类分支（每尺度独立）：DW+PW×2 + Conv2d(nc)
    private readonly ModuleList<Sequential> _cv3;

    private readonly DflLayer _dfl;

    // 记录各尺度 stride（在 forward 时动态生成 anchors 使用）
    private readonly int[] _strides;

    /// <param name="nc">类别数</param>
    /// <param name="regMax">DFL 最大分布值（默认 16）</param>
    /// <param name="channels">各尺度输入通道数，如 [64, 128, 256]（nano 缩放后）</param>
    /// <param name="strides">各尺度 stride，如 [8, 16, 32]</param>
    public DetectHead(int nc, int regMax, long[] channels, int[] strides)
        : base(nameof(DetectHead))
    {
        _nc = nc;
        _regMax = regMax;
        _nl = channels.Length;
        _strides = strides;

        // c2：box 分支中间通道数 = max(16, ch//4, reg_max*4)
        // c3：cls 分支中间通道数 = max(ch[0], min(nc, 100))
        var c2 = (long)Math.Max(16, Math.Max(channels[0] / 4, regMax * 4L));
        var c3 = Math.Max(channels[0], Math.Min((long)nc, 100L));

        var cv2List = new List<Sequential>();
        var cv3List = new List<Sequential>();

        for (var i = 0; i < _nl; i++)
        {
            var ch = channels[i];

            // box 回归分支
            cv2List.Add(Sequential(
                new ConvBnAct(ch, c2, 3),
                new ConvBnAct(c2, c2, 3),
                Conv2d(c2, 4L * regMax, 1)));

            // 分类分支（深度可分离卷积 + 点卷积）
            cv3List.Add(Sequential(
                Sequential(new DepthwiseConv(ch, 3), new ConvBnAct(ch, c3, 1)),
                Sequential(new DepthwiseConv(c3, 3), new ConvBnAct(c3, c3, 1)),
                Conv2d(c3, nc, 1)));
        }

        _cv2 = new ModuleList<Sequential>(cv2List.ToArray());
        _cv3 = new ModuleList<Sequential>(cv3List.ToArray());
        _dfl = new DflLayer(regMax);

        RegisterComponents();
    }

    public override DetectHeadOutput forward(IReadOnlyList<Tensor> feats)
    {
        var batchSize = (int)feats[0].shape[0];

        // 每个尺度输出 box_distri 和 scores，flatten 后拼接
        var boxList = new List<Tensor>();
        var scoreList = new List<Tensor>();

        for (var i = 0; i < _nl; i++)
        {
            // box: [b, 4*reg_max, h, w]
            var boxRaw = _cv2[i].forward(feats[i]);
            // score: [b, nc, h, w]
            var scoreRaw = _cv3[i].forward(feats[i]);

            var h = (int)boxRaw.shape[2];
            var w = (int)boxRaw.shape[3];
            var totalAnchors = h * w;

            // 展平空间维度：[b, C, h, w] → [b, C, h*w]
            boxList.Add(boxRaw.view(batchSize, 4 * _regMax, totalAnchors));
            scoreList.Add(scoreRaw.view(batchSize, _nc, totalAnchors));
        }

        // 沿最后维度拼接所有尺度
        var boxesDistri = torch.cat(boxList.ToArray(), dim: 2);   // [b, 4*reg_max, total_a]
        var scores = torch.cat(scoreList.ToArray(), dim: 2);      // [b, nc, total_a]

        // 释放中间张量
        foreach (var t in boxList) t.Dispose();
        foreach (var t in scoreList) t.Dispose();

        if (is_grad_enabled())
        {
            // 训练模式：返回原始分布和分数，损失函数自行处理
            return new DetectHeadOutput(boxesDistri, scores, feats, isTraining: true);
        }

        // 推理模式：解码 boxes → xyxy，sigmoid scores
        using var boxesLtrb = _dfl.forward(boxesDistri.permute(0, 2, 1)); // [b, total_a, 4]
        var anchors = MakeAnchors(feats);  // [total_a, 2]（cx, cy）
        var strideTensor = MakeStrideTensor(feats); // [total_a]

        using var anchorsCpu = anchors;
        using var strideTensorCpu = strideTensor;
        using var boxesLtrbStrided = boxesLtrb * strideTensor.unsqueeze(0).unsqueeze(-1); // 乘以各尺度 stride
        var xyxy = LtrbToXyxy(boxesLtrbStrided, anchors * strideTensor.unsqueeze(-1));

        using var scoresSigmoid = torch.sigmoid(scores);
        using var xyxyResult = xyxy;
        // 拼接：[b, 4, total_a] + [b, nc, total_a] → [b, 4+nc, total_a]
        return new DetectHeadOutput(
            torch.cat([xyxyResult.permute(0, 2, 1).contiguous().view(batchSize, 4, -1), scoresSigmoid], dim: 1),
            null,
            null,
            isTraining: false);
    }

    /// <summary>生成各尺度 anchor 中心点（归一化，stride=1 的网格点 + 0.5 偏移）。</summary>
    private Tensor MakeAnchors(IReadOnlyList<Tensor> feats)
    {
        var parts = new List<Tensor>();
        foreach (var feat in feats)
        {
            var h = feat.shape[2];
            var w = feat.shape[3];
            using var gy = arange(h, dtype: ScalarType.Float32, device: feat.device);
            using var gx = arange(w, dtype: ScalarType.Float32, device: feat.device);
            using var meshY = torch.meshgrid([gy, gx], indexing: "ij")[0];
            using var meshX = torch.meshgrid([gy, gx], indexing: "ij")[1];
            using var cx = (meshX + 0.5f).view(-1);
            using var cy = (meshY + 0.5f).view(-1);
            parts.Add(torch.stack([cx, cy], dim: 1)); // [h*w, 2]
        }
        var result = torch.cat(parts.ToArray(), dim: 0); // [total_a, 2]
        foreach (var p in parts) p.Dispose();
        return result;
    }

    /// <summary>生成各尺度 stride 张量。</summary>
    private Tensor MakeStrideTensor(IReadOnlyList<Tensor> feats)
    {
        var parts = new List<Tensor>();
        for (var i = 0; i < _nl; i++)
        {
            var total = feats[i].shape[2] * feats[i].shape[3];
            parts.Add(full(total, _strides[i], dtype: ScalarType.Float32, device: feats[i].device));
        }
        var result = torch.cat(parts.ToArray(), dim: 0);
        foreach (var p in parts) p.Dispose();
        return result;
    }

    /// <summary>ltrb（left/top/right/bottom 相对 anchor）→ xyxy（绝对坐标）。</summary>
    private static Tensor LtrbToXyxy(Tensor ltrb, Tensor anchorXy)
    {
        // ltrb: [b, total_a, 4]，anchorXy: [total_a, 2]
        using var xy = anchorXy.unsqueeze(0); // [1, total_a, 2]
        using var l = ltrb[.., .., 0..1];
        using var t = ltrb[.., .., 1..2];
        using var r = ltrb[.., .., 2..3];
        using var b = ltrb[.., .., 3..4];
        using var x1 = xy[.., .., 0..1] - l;
        using var y1 = xy[.., .., 1..2] - t;
        using var x2 = xy[.., .., 0..1] + r;
        using var y2 = xy[.., .., 1..2] + b;
        return torch.cat([x1, y1, x2, y2], dim: -1); // [b, total_a, 4]
    }
}

/// <summary>DetectHead 的前向输出，训练/推理模式下字段含义不同。</summary>
public sealed class DetectHeadOutput
{
    /// <summary>
    /// 训练模式：box 分布预测 [b, 4*reg_max, total_a]。
    /// 推理模式：最终检测张量 [b, 4+nc, total_a]。
    /// </summary>
    public Tensor Primary { get; }

    /// <summary>训练模式：分类分数 [b, nc, total_a]。推理模式：null。</summary>
    public Tensor? Scores { get; }

    /// <summary>训练模式：原始特征图列表（供损失函数 make_anchors 使用）。推理模式：null。</summary>
    public IReadOnlyList<Tensor>? Feats { get; }

    /// <summary>是否为训练模式输出。</summary>
    public bool IsTraining { get; }

    public DetectHeadOutput(Tensor primary, Tensor? scores, IReadOnlyList<Tensor>? feats, bool isTraining)
    {
        Primary = primary;
        Scores = scores;
        Feats = feats;
        IsTraining = isTraining;
    }
}
