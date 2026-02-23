using TorchSharp;
using static TorchSharp.torch;
using static TorchSharp.torch.nn;

namespace TransformersMini.Infrastructure.TorchSharp.Detection.Loss;

/// <summary>
/// TaskAlignedAssigner：YOLOv8 正负样本分配器。
/// 结合分类分数和定位质量（IoU）选取每个 GT 的 topk 正样本 anchor。
/// 参考 ultralytics/utils/tal.py TaskAlignedAssigner。
/// </summary>
internal sealed class TaskAlignedAssigner
{
    private readonly int _topk;
    private readonly int _numClasses;
    private readonly float _alpha; // 分类分数指数
    private readonly float _beta;  // IoU 指数
    private readonly float _eps;

    public TaskAlignedAssigner(
        int topk = 10,
        int numClasses = 80,
        float alpha = 0.5f,
        float beta = 6.0f,
        float eps = 1e-9f)
    {
        _topk = topk;
        _numClasses = numClasses;
        _alpha = alpha;
        _beta = beta;
        _eps = eps;
    }

    /// <summary>
    /// 计算任务对齐分配结果（no_grad 操作）。
    /// </summary>
    /// <param name="pdScores">[b, total_a, nc] sigmoid 后的分类分数</param>
    /// <param name="pdBboxes">[b, total_a, 4] 解码后 xyxy 框</param>
    /// <param name="anchorPoints">[total_a, 2] anchor 中心点（像素坐标）</param>
    /// <param name="gtLabels">[b, max_gt, 1] GT 类别 id（long）</param>
    /// <param name="gtBboxes">[b, max_gt, 4] GT xyxy 框（像素坐标）</param>
    /// <param name="maskGt">[b, max_gt, 1] 有效 GT 掩码（bool）</param>
    /// <returns>分配结果元组</returns>
    public AssignResult Assign(
        Tensor pdScores,
        Tensor pdBboxes,
        Tensor anchorPoints,
        Tensor gtLabels,
        Tensor gtBboxes,
        Tensor maskGt)
    {
        using var _ = torch.no_grad();

        var bs = pdScores.shape[0];
        var totalA = pdScores.shape[1];
        var nMaxBoxes = gtBboxes.shape[1];
        var device = pdScores.device;

        if (nMaxBoxes == 0)
        {
            return new AssignResult(
                TargetLabels: full([bs, totalA], _numClasses, dtype: ScalarType.Int64, device: device),
                TargetBboxes: zeros([bs, totalA, 4], device: device),
                TargetScores: zeros([bs, totalA, _numClasses], device: device),
                FgMask: zeros([bs, totalA], dtype: ScalarType.Bool, device: device),
                TargetGtIdx: zeros([bs, totalA], dtype: ScalarType.Int64, device: device));
        }

        // ── 1. 计算 anchor 是否在 GT 框内 ──────────────────────────────
        // anchorPoints: [total_a, 2] → [1, 1, total_a, 2]
        using var ap = anchorPoints.unsqueeze(0).unsqueeze(0);
        // gtBboxes: [b, max_gt, 4] → [b, max_gt, 1, 4]
        using var gb = gtBboxes.unsqueeze(2);
        using var gbX1 = gb[.., .., .., 0..1];
        using var gbY1 = gb[.., .., .., 1..2];
        using var gbX2 = gb[.., .., .., 2..3];
        using var gbY2 = gb[.., .., .., 3..4];
        using var apX = ap[.., .., .., 0..1];
        using var apY = ap[.., .., .., 1..2];

        // [b, max_gt, total_a]：anchor 在 GT 内的布尔掩码
        using var inGt = (
            (apX - gbX1) *
            (gbX2 - apX) *
            (apY - gbY1) *
            (gbY2 - apY)
        ).min(dim: -1).values.gt(0f); // [b, max_gt, total_a]

        // ── 2. 计算 IoU（预测框 vs GT）──────────────────────────────────
        // pdBboxes: [b, total_a, 4] → [b, 1, total_a, 4]
        // gtBboxes: [b, max_gt, 4] → [b, max_gt, 1, 4]
        using var pdExp = pdBboxes.unsqueeze(1);   // [b, 1, total_a, 4]
        using var gtExp = gtBboxes.unsqueeze(2);   // [b, max_gt, 1, 4]

        // 逐元素计算 IoU：[b, max_gt, total_a]
        var iou = ComputeIouMatrix(pdExp, gtExp); // [b, max_gt, total_a]

        // ── 3. 对齐指标：score^alpha * iou^beta ─────────────────────────
        // 提取各 GT 对应的预测分类分数
        using var gtLabelsLong = gtLabels.squeeze(-1).@long(); // [b, max_gt]

        // score: [b, max_gt, total_a]（从 pdScores 按 GT 类别 gather）
        var alignScores = GetAlignScores(pdScores, gtLabelsLong, bs, (int)nMaxBoxes, (int)totalA, device);

        using var alignMetric = alignScores.pow(_alpha) * iou.pow(_beta) * inGt.to(ScalarType.Float32);

        // 对无效 GT 置零
        using var maskGtFlat = maskGt.squeeze(-1).unsqueeze(-1); // [b, max_gt, 1]
        using var alignMetricMasked = alignMetric * maskGtFlat.to(ScalarType.Float32);

        // ── 4. 为每个 GT 选 topk Anchor ──────────────────────────────────
        // alignMetricMasked: [b, max_gt, total_a]
        var (topkMetrics, topkIdxs) = torch.topk(alignMetricMasked, Math.Min(_topk, (int)totalA), dim: -1, largest: true);

        // 构建正样本掩码 [b, max_gt, total_a]
        using var isTopk = zeros([bs, nMaxBoxes, totalA], dtype: ScalarType.Float32, device: device);
        isTopk.scatter_(dim: -1, index: topkIdxs, src: ones_like(topkMetrics));
        topkMetrics.Dispose();
        topkIdxs.Dispose();

        using var fgMaskPerGt = isTopk * inGt.to(ScalarType.Float32) * maskGtFlat.to(ScalarType.Float32);

        // ── 5. 解决一个 anchor 被多个 GT 竞争的情况 ─────────────────────
        // 选取 alignMetric 最大的 GT
        using var fgMetric = alignMetricMasked * fgMaskPerGt; // [b, max_gt, total_a]

        // argmax over gt 维度 → [b, total_a]
        var maxResult = fgMetric.max(1L);
        using var fgMetricMax = maxResult.values;
        var targetGtIdx = maxResult.indexes;

        // fg_mask: [b, total_a]（任意 GT 的正样本）
        using var fgMaskAny = fgMaskPerGt.any(1L); // [b, total_a]
        var fgMask = fgMaskAny.clone();

        // ── 6. 构建目标标签/框/分数 ──────────────────────────────────────
        // 目标 GT 索引扩展到框维度
        using var tgtIdxExpBox = targetGtIdx.unsqueeze(-1).expand(-1, -1, 4); // [b, total_a, 4]
        var targetBboxes = gtBboxes.gather(dim: 1, index: tgtIdxExpBox); // [b, total_a, 4]

        using var tgtIdxExpLbl = targetGtIdx.unsqueeze(-1); // [b, total_a, 1]
        using var rawTargetLabels = gtLabels.squeeze(-1).gather(dim: 1, index: targetGtIdx); // [b, total_a]

        // 非前景位置填充 num_classes（背景）
        var targetLabels = rawTargetLabels.clone();
        targetLabels.masked_fill_(~fgMask, _numClasses);

        // one-hot 目标分数 [b, total_a, nc]
        using var fgMaskExp = fgMask.unsqueeze(-1).to(ScalarType.Float32);
        using var ohTmp = torch.nn.functional.one_hot(rawTargetLabels.clamp(0L, _numClasses - 1L), _numClasses).to(ScalarType.Float32);
        var targetScores = ohTmp * fgMaskExp;

        // 用归一化 alignMetric 加权目标分数（软标签，提升训练质量）
        using var amsSum = alignMetricMasked.amax(new long[] { -1 }, keepdim: true) + _eps;
        using var iouSum = iou.amax(new long[] { -1 }, keepdim: true) + _eps;
        using var normAlignMetric = alignMetricMasked / amsSum * (iou / iouSum);
        // 对每个 anchor 取最大 GT 的 normAlignMetric
        using var normAlignPerAnchor = normAlignMetric.amax(new long[] { 1 }); // [b, total_a]
        using var scoreWeight = normAlignPerAnchor.unsqueeze(-1); // [b, total_a, 1]
        using var ts = targetScores;
        targetScores = ts * scoreWeight;

        alignScores.Dispose();
        iou.Dispose();

        return new AssignResult(targetLabels, targetBboxes, targetScores, fgMask, targetGtIdx.clone());
    }

    /// <summary>从 pdScores 按 GT 类别提取对应分类分数。</summary>
    private static Tensor GetAlignScores(
        Tensor pdScores, Tensor gtLabels, long bs, int nMaxBoxes, int totalA, Device device)
    {
        // pdScores: [b, total_a, nc]
        // gtLabels: [b, max_gt]（long）
        // 输出：[b, max_gt, total_a]
        var clampedLabels = gtLabels.clamp(0);
        using var lblExp = clampedLabels.unsqueeze(-1).expand(bs, nMaxBoxes, totalA); // [b, max_gt, total_a]
        // pdScores [b, total_a, nc] → permute → [b, nc, total_a] → expand → gather
        using var pdPerm = pdScores.permute(0, 2, 1).contiguous(); // [b, nc, total_a]
        using var pdExp = pdPerm.unsqueeze(1).expand(bs, nMaxBoxes, -1, totalA); // [b, max_gt, nc, total_a]
        // gather along nc 维度，index: [b, max_gt, 1, total_a]
        using var idx = lblExp.unsqueeze(2); // [b, max_gt, 1, total_a]
        using var gathered = pdExp.gather(2, idx); // [b, max_gt, 1, total_a]
        return gathered.squeeze(2); // [b, max_gt, total_a]
    }

    /// <summary>广播计算 IoU 矩阵。</summary>
    private static Tensor ComputeIouMatrix(Tensor pred, Tensor target)
    {
        // pred: [b, 1, total_a, 4]，target: [b, max_gt, 1, 4]
        using var px1 = pred[.., .., .., 0..1];
        using var py1 = pred[.., .., .., 1..2];
        using var px2 = pred[.., .., .., 2..3];
        using var py2 = pred[.., .., .., 3..4];

        using var tx1 = target[.., .., .., 0..1];
        using var ty1 = target[.., .., .., 1..2];
        using var tx2 = target[.., .., .., 2..3];
        using var ty2 = target[.., .., .., 3..4];

        using var ix1 = torch.maximum(px1, tx1);
        using var iy1 = torch.maximum(py1, ty1);
        using var ix2 = torch.minimum(px2, tx2);
        using var iy2 = torch.minimum(py2, ty2);

        using var iw = (ix2 - ix1).clamp_min(0f);
        using var ih = (iy2 - iy1).clamp_min(0f);
        using var inter = iw * ih;

        using var areaP = (px2 - px1).clamp_min(0f) * (py2 - py1).clamp_min(0f);
        using var areaT = (tx2 - tx1).clamp_min(0f) * (ty2 - ty1).clamp_min(0f);
        using var unionArea = areaP + areaT - inter + 1e-7f;

        // 结果 [b, max_gt, total_a]（从最后两维 squeeze）
        return (inter / unionArea).squeeze(-1); // [b, max_gt, total_a]
    }
}

/// <summary>TaskAlignedAssigner 的分配结果。</summary>
internal sealed record AssignResult(
    Tensor TargetLabels,   // [b, total_a]（long，背景=num_classes）
    Tensor TargetBboxes,   // [b, total_a, 4]（xyxy）
    Tensor TargetScores,   // [b, total_a, nc]（软标签）
    Tensor FgMask,         // [b, total_a]（bool，true=正样本）
    Tensor TargetGtIdx);   // [b, total_a]（long，所属 GT 索引）
