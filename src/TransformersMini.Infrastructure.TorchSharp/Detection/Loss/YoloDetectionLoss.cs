using TorchSharp;
using static TorchSharp.torch;
using static TorchSharp.torch.nn;
using TransformersMini.Infrastructure.TorchSharp.Detection.Head;

namespace TransformersMini.Infrastructure.TorchSharp.Detection.Loss;

/// <summary>
/// YOLOv8 检测损失，组合：
///   - BCE 分类损失
///   - CIoU 边界框回归损失
///   - DFL 分布回归损失
/// 通过 TaskAlignedAssigner 进行正负样本分配。
/// </summary>
public sealed class YoloDetectionLoss
{
    private readonly int _nc;
    private readonly int _regMax;
    private readonly int[] _strides;
    private readonly TaskAlignedAssigner _assigner;
    private readonly DflLoss _dflLoss;

    // YOLOv8 默认损失权重
    private const float BoxGain = 7.5f;
    private const float ClsGain = 0.5f;
    private const float DflGain = 1.5f;

    /// <param name="nc">类别数</param>
    /// <param name="regMax">DFL reg_max</param>
    /// <param name="strides">各检测尺度的 stride</param>
    public YoloDetectionLoss(int nc, int regMax = 16, int[]? strides = null)
    {
        _nc = nc;
        _regMax = regMax;
        _strides = strides ?? [8, 16, 32];
        _assigner = new TaskAlignedAssigner(topk: 10, numClasses: nc, alpha: 0.5f, beta: 6.0f);
        _dflLoss = new DflLoss(regMax);
    }

    /// <summary>
    /// 计算检测损失。
    /// </summary>
    /// <param name="headOutput">DetectHead 的训练模式输出</param>
    /// <param name="gtBboxes">[b, max_gt, 4] GT 框（xyxy，像素坐标）</param>
    /// <param name="gtLabels">[b, max_gt, 1] GT 类别 id（long）</param>
    /// <param name="maskGt">[b, max_gt, 1] 有效 GT 掩码</param>
    /// <returns>(totalLoss, boxLoss, clsLoss, dflLoss)</returns>
    public (Tensor Total, Tensor Box, Tensor Cls, Tensor Dfl) Compute(
        DetectHeadOutput headOutput,
        Tensor gtBboxes,
        Tensor gtLabels,
        Tensor maskGt)
    {
        var device = headOutput.Primary.device;
        var bs = (int)headOutput.Primary.shape[0];

        // boxesDistri: [b, 4*reg_max, total_a]
        // scores: [b, nc, total_a]
        var boxesDistri = headOutput.Primary;  // 训练模式
        var scores = headOutput.Scores!;
        var feats = headOutput.Feats!;

        // ── 生成 anchor 点和 stride 张量 ──────────────────────────────────
        var (anchorPoints, strideTensor) = MakeAnchors(feats, device);

        // ── 解码 box（用于 assigner） ──────────────────────────────────────
        // boxesDistri: [b, 4*reg_max, total_a] → permute → [b, total_a, 4*reg_max]
        using var bdPerm = boxesDistri.permute(0, 2, 1).contiguous();
        var totalA = bdPerm.shape[1];

        // DFL 解码
        using var proj = arange(_regMax, dtype: ScalarType.Float32, device: device);
        using var bdReshaped = bdPerm.view(bs, totalA, 4, _regMax);
        using var bdSoftmax = functional.softmax(bdReshaped, dim: -1);
        using var boxesLtrb = torch.matmul(bdSoftmax, proj); // [b, total_a, 4]

        // ltrb → xyxy（乘 stride 转像素坐标）
        using var stEx = strideTensor.unsqueeze(0).unsqueeze(-1); // [1, total_a, 1]
        using var boxesLtrbPx = boxesLtrb * stEx;
        using var anchorsPx = anchorPoints * strideTensor.unsqueeze(-1); // [total_a, 2]
        using var pdBboxes = LtrbToXyxy(boxesLtrbPx, anchorsPx); // [b, total_a, 4]

        // scores 转 sigmoid：[b, nc, total_a] → permute → [b, total_a, nc]
        using var scoresPerm = scores.permute(0, 2, 1).contiguous(); // [b, total_a, nc]
        using var pdScoresSig = torch.sigmoid(scoresPerm);

        // ── TaskAligned 分配 ──────────────────────────────────────────────
        var assign = _assigner.Assign(pdScoresSig, pdBboxes, anchorPoints, gtLabels, gtBboxes, maskGt);

        using var targetLabels = assign.TargetLabels;
        using var targetBboxes = assign.TargetBboxes;
        using var targetScores = assign.TargetScores;
        using var fgMask = assign.FgMask;
        using var targetGtIdx = assign.TargetGtIdx;

        var targetScoresSum = Math.Max(targetScores.sum().item<float>(), 1f);

        // ── 分类损失（BCE，所有 anchor） ─────────────────────────────────
        // scores: [b, nc, total_a] vs targetScores: [b, total_a, nc]
        using var scoresBce = scores.permute(0, 2, 1).contiguous(); // [b, total_a, nc]
        using var clsLossRaw = functional.binary_cross_entropy_with_logits(
            scoresBce, targetScores, reduction: Reduction.None);
        var clsLoss = clsLossRaw.sum() / targetScoresSum;

        // ── 边界框损失（CIoU + DFL，仅正样本） ───────────────────────────
        var boxLoss = zeros(1, device: device);
        var dflLoss = zeros(1, device: device);

        var numFg = fgMask.sum().item<long>();
        if (numFg > 0)
        {
            // 提取正样本
            using var fgExpBox = fgMask.unsqueeze(-1).expand(-1, -1, 4);
            using var predBoxesFg = pdBboxes.masked_select(fgExpBox).view(numFg, 4);
            using var tgtBoxesFg = targetBboxes.masked_select(fgExpBox).view(numFg, 4);

            // 权重 = targetScores 在正样本处的 max（软标签加权）
            using var fgExpNc = fgMask.unsqueeze(-1).expand(-1, -1, _nc);
            using var tgtScoresFg = targetScores.masked_select(fgExpNc).view(numFg, _nc);
            using var weight = tgtScoresFg.sum(dim: -1, keepdim: true);

            // CIoU loss
            using var iou = BboxIoU.Compute(predBoxesFg, tgtBoxesFg, ciou: true);
            using var ciouLossRaw = (1f - iou) * weight.squeeze(-1);
            boxLoss = ciouLossRaw.sum() / targetScoresSum;

            // DFL loss
            // bdPerm[正样本]: [numFg, 4*reg_max] → [numFg, 4, reg_max]
            using var fgExpDfl = fgMask.unsqueeze(-1).expand(-1, -1, 4 * _regMax);
            using var bdFg = bdPerm.masked_select(fgExpDfl).view(numFg, 4 * _regMax);
            using var bdFgReshaped = bdFg.view(numFg * 4, _regMax);

            // 目标 ltrb（归一化到 [0, reg_max-1]）
            using var stridesFg = strideTensor.unsqueeze(0).expand(bs, -1)
                .masked_select(fgMask).unsqueeze(-1); // [numFg, 1]
            using var tgtLtrb = XyxyToLtrb(tgtBoxesFg, anchorsPx.unsqueeze(0).expand(bs, -1, -1)
                .masked_select(fgExpBox).view(numFg, 4)) / stridesFg;
            using var tgtLtrbClamped = tgtLtrb.clamp(0, _regMax - 1 - 0.01f);
            using var tgtLtrbFlat = tgtLtrbClamped.view(numFg * 4);

            using var dflLossRaw = _dflLoss.forward(bdFgReshaped, tgtLtrbFlat)
                .view(numFg, 4).sum(dim: -1, keepdim: true) * weight;
            dflLoss = dflLossRaw.sum() / targetScoresSum;
        }

        // ── 加权合并 ─────────────────────────────────────────────────────
        var totalLoss = boxLoss * BoxGain + clsLoss * ClsGain + dflLoss * DflGain;

        anchorPoints.Dispose();
        strideTensor.Dispose();
        boxLoss.Dispose();
        clsLoss.Dispose();
        dflLoss.Dispose();

        return (totalLoss, boxLoss * BoxGain, clsLoss * ClsGain, dflLoss * DflGain);
    }

    // ─── 工具方法 ────────────────────────────────────────────────────────

    /// <summary>生成所有尺度的 anchor 中心点和 stride 向量。</summary>
    private (Tensor AnchorPoints, Tensor StrideTensor) MakeAnchors(
        IReadOnlyList<Tensor> feats, Device device)
    {
        var apParts = new List<Tensor>();
        var stParts = new List<Tensor>();

        for (var i = 0; i < feats.Count; i++)
        {
            var h = feats[i].shape[2];
            var w = feats[i].shape[3];
            using var gy = arange(h, dtype: ScalarType.Float32, device: device);
            using var gx = arange(w, dtype: ScalarType.Float32, device: device);
            var meshes = torch.meshgrid([gy, gx], indexing: "ij");
            using var meshY = meshes[0];
            using var meshX = meshes[1];
            using var cx = (meshX + 0.5f).view(-1);
            using var cy = (meshY + 0.5f).view(-1);
            apParts.Add(torch.stack([cx, cy], dim: 1));
            stParts.Add(full(h * w, _strides[i], dtype: ScalarType.Float32, device: device));
        }

        var ap = torch.cat(apParts.ToArray(), dim: 0);
        var st = torch.cat(stParts.ToArray(), dim: 0);
        foreach (var t in apParts) t.Dispose();
        foreach (var t in stParts) t.Dispose();
        return (ap, st);
    }

    private static Tensor LtrbToXyxy(Tensor ltrb, Tensor anchorXy)
    {
        using var ax = anchorXy[.., 0..1];
        using var ay = anchorXy[.., 1..2];
        using var l = ltrb[.., .., 0..1];
        using var t = ltrb[.., .., 1..2];
        using var r = ltrb[.., .., 2..3];
        using var b = ltrb[.., .., 3..4];
        using var x1 = ax.unsqueeze(0) - l;
        using var y1 = ay.unsqueeze(0) - t;
        using var x2 = ax.unsqueeze(0) + r;
        using var y2 = ay.unsqueeze(0) + b;
        return torch.cat([x1, y1, x2, y2], dim: -1); // [b, total_a, 4]
    }

    private static Tensor XyxyToLtrb(Tensor xyxy, Tensor anchorXy)
    {
        // xyxy: [N, 4]，anchorXy: [N, 2] → ltrb: [N, 4]
        using var x1 = xyxy[.., 0..1];
        using var y1 = xyxy[.., 1..2];
        using var x2 = xyxy[.., 2..3];
        using var y2 = xyxy[.., 3..4];
        using var ax = anchorXy[.., 0..1];
        using var ay = anchorXy[.., 1..2];
        using var l = ax - x1;
        using var t = ay - y1;
        using var r = x2 - ax;
        using var b = y2 - ay;
        return torch.cat([l, t, r, b], dim: -1); // [N, 4]
    }
}
