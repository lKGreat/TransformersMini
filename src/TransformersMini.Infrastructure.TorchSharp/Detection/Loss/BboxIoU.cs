using TorchSharp;
using static TorchSharp.torch;

namespace TransformersMini.Infrastructure.TorchSharp.Detection.Loss;

/// <summary>
/// 边界框 IoU 工具类，支持 GIoU / DIoU / CIoU。
/// 所有输入均为 xyxy 格式（x1, y1, x2, y2）。
/// </summary>
internal static class BboxIoU
{
    /// <summary>
    /// 计算 CIoU（或 IoU）。
    /// </summary>
    /// <param name="pred">预测框 [N, 4]（xyxy）</param>
    /// <param name="target">GT 框 [N, 4]（xyxy）</param>
    /// <param name="ciou">是否使用 CIoU（默认 true）</param>
    /// <param name="eps">数值稳定项</param>
    /// <returns>[N] 每对框的 IoU 或 CIoU 值</returns>
    public static Tensor Compute(Tensor pred, Tensor target, bool ciou = true, float eps = 1e-7f)
    {
        // 预测框坐标
        using var b1x1 = pred[.., 0];
        using var b1y1 = pred[.., 1];
        using var b1x2 = pred[.., 2];
        using var b1y2 = pred[.., 3];

        // 目标框坐标
        using var b2x1 = target[.., 0];
        using var b2y1 = target[.., 1];
        using var b2x2 = target[.., 2];
        using var b2y2 = target[.., 3];

        // 交集
        using var interX1 = torch.maximum(b1x1, b2x1);
        using var interY1 = torch.maximum(b1y1, b2y1);
        using var interX2 = torch.minimum(b1x2, b2x2);
        using var interY2 = torch.minimum(b1y2, b2y2);

        using var interW = (interX2 - interX1).clamp_min(0);
        using var interH = (interY2 - interY1).clamp_min(0);
        using var inter = interW * interH;

        // 各自面积
        using var area1 = (b1x2 - b1x1).clamp_min(0) * (b1y2 - b1y1).clamp_min(0);
        using var area2 = (b2x2 - b2x1).clamp_min(0) * (b2y2 - b2y1).clamp_min(0);

        // 并集
        using var unionArea = area1 + area2 - inter + eps;
        var iou = inter / unionArea;

        if (!ciou) return iou;

        // ── CIoU 附加项 ─────────────────────────────────────────────────
        // 最小外接矩形（enclosing box）
        using var cX1 = torch.minimum(b1x1, b2x1);
        using var cY1 = torch.minimum(b1y1, b2y1);
        using var cX2 = torch.maximum(b1x2, b2x2);
        using var cY2 = torch.maximum(b1y2, b2y2);

        // c² = 外接矩形对角线平方
        using var cW = (cX2 - cX1).clamp_min(0);
        using var cH = (cY2 - cY1).clamp_min(0);
        using var c2 = cW * cW + cH * cH + eps;

        // ρ² = 中心距离平方
        using var b1CX = (b1x1 + b1x2) / 2f;
        using var b1CY = (b1y1 + b1y2) / 2f;
        using var b2CX = (b2x1 + b2x2) / 2f;
        using var b2CY = (b2y1 + b2y2) / 2f;
        using var rho2 = (b2CX - b1CX).pow(2) + (b2CY - b1CY).pow(2);

        // v = 宽高比一致性
        using var w1 = (b1x2 - b1x1).clamp_min(0);
        using var h1 = (b1y2 - b1y1).clamp_min(eps);
        using var w2 = (b2x2 - b2x1).clamp_min(0);
        using var h2 = (b2y2 - b2y1).clamp_min(eps);
        using var atanPred = torch.atan(w1 / h1);
        using var atanTarget = torch.atan(w2 / h2);
        using var v = (4f / (MathF.PI * MathF.PI)) * (atanPred - atanTarget).pow(2);

        // alpha = v / (1 - iou + v)（不参与梯度，防止 v 梯度消失）
        using var iouDetach = iou.detach();
        using var alpha = v / (1f - iouDetach + v + eps);

        using var tmp = iou;
        return tmp - rho2 / c2 - v * alpha;
    }
}
