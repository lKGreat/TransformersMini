using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace TransformersMini.DataAdapters.Coco.Augmentation;

/// <summary>
/// Mosaic 4拼数据增强（对应 ultralytics Mosaic._mosaic4）。
/// 随机选取 4 张图片，拼贴到 2×targetSize 的画布上，随机中心点后裁剪到 targetSize。
/// 同步变换并过滤 GT 框。
/// </summary>
public sealed class MosaicAugmentation
{
    private readonly int _targetSize;
    private readonly Random _rng;
    private readonly float _minBoxSize; // 增强后保留的最小框边长（归一化）

    public MosaicAugmentation(int targetSize = 640, float minBoxSize = 0.01f, Random? rng = null)
    {
        _targetSize = targetSize;
        _minBoxSize = minBoxSize;
        _rng = rng ?? Random.Shared;
    }

    /// <summary>
    /// 对当前样本应用 Mosaic 增强。
    /// </summary>
    /// <param name="anchorImage">主图（图0）</param>
    /// <param name="anchorBoxes">主图的归一化 xyxy 框</param>
    /// <param name="otherImages">其他 3 张图片</param>
    /// <param name="otherBoxes">其他 3 张图片的归一化 xyxy 框</param>
    /// <returns>拼贴后的 targetSize×targetSize 图片和所有 GT 框</returns>
    public (Image<Rgb24> Image, IReadOnlyList<NormalizedBox> Boxes) Apply(
        Image<Rgb24> anchorImage,
        IReadOnlyList<NormalizedBox> anchorBoxes,
        IReadOnlyList<Image<Rgb24>> otherImages,
        IReadOnlyList<IReadOnlyList<NormalizedBox>> otherBoxes)
    {
        if (otherImages.Count < 3 || otherBoxes.Count < 3)
        {
            // 图片不足时退化为只返回主图（缩放到目标尺寸）
            var fallback = anchorImage.Clone(ctx => ctx.Resize(_targetSize, _targetSize));
            return (fallback, anchorBoxes);
        }

        var ts = _targetSize;
        var ts2 = ts * 2;

        // 随机中心点（在 [ts/2, 3*ts/2] 范围内）
        var cx = (int)(_rng.NextSingle() * ts + ts / 2f);
        var cy = (int)(_rng.NextSingle() * ts + ts / 2f);

        // 创建 2×targetSize 画布（黑色）
        using var canvas = new Image<Rgb24>(ts2, ts2);

        var images = new[] { anchorImage, otherImages[0], otherImages[1], otherImages[2] };
        var boxes = new[] { anchorBoxes, otherBoxes[0], otherBoxes[1], otherBoxes[2] };

        // 各象限的目标区域（在 canvas 上）和源图缩放策略
        var placements = new[]
        {
            // 左上象限：图0，区域右下角对齐 (cx, cy)
            (dstX1: cx - ts, dstY1: cy - ts, dstX2: cx, dstY2: cy),
            // 右上象限：图1，区域左下角对齐 (cx, cy)
            (dstX1: cx,      dstY1: cy - ts, dstX2: cx + ts, dstY2: cy),
            // 左下象限：图2，区域右上角对齐 (cx, cy)
            (dstX1: cx - ts, dstY1: cy,      dstX2: cx, dstY2: cy + ts),
            // 右下象限：图3，区域左上角对齐 (cx, cy)
            (dstX1: cx,      dstY1: cy,      dstX2: cx + ts, dstY2: cy + ts)
        };

        var allBoxes = new List<NormalizedBox>();

        for (var idx = 0; idx < 4; idx++)
        {
            var (dstX1, dstY1, dstX2, dstY2) = placements[idx];
            var dstW = Math.Max(1, dstX2 - dstX1);
            var dstH = Math.Max(1, dstY2 - dstY1);

            // 裁剪目标区域到 canvas 范围
            var clipX1 = Math.Max(0, dstX1);
            var clipY1 = Math.Max(0, dstY1);
            var clipX2 = Math.Min(ts2, dstX2);
            var clipY2 = Math.Min(ts2, dstY2);
            if (clipX2 <= clipX1 || clipY2 <= clipY1) continue;

            // 源图缩放到 dstW × dstH
            using var resized = images[idx].Clone(ctx => ctx.Resize(dstW, dstH));

            // 计算从 resized 中取哪块（相对于 resized 内坐标）
            var srcOffX = Math.Max(0, clipX1 - dstX1);
            var srcOffY = Math.Max(0, clipY1 - dstY1);
            var srcW = clipX2 - clipX1;
            var srcH = clipY2 - clipY1;

            // 粘贴到 canvas
            for (var y = 0; y < srcH; y++)
            {
                for (var x = 0; x < srcW; x++)
                {
                    canvas[clipX1 + x, clipY1 + y] = resized[srcOffX + x, srcOffY + y];
                }
            }

            // 变换 GT 框到 canvas 坐标（再到归一化坐标）
            foreach (var b in boxes[idx])
            {
                // 原始归一化 → 在 dstW×dstH 区域内的像素坐标
                var bx1 = b.X1 * dstW + dstX1;
                var by1 = b.Y1 * dstH + dstY1;
                var bx2 = b.X2 * dstW + dstX1;
                var by2 = b.Y2 * dstH + dstY1;

                // 裁剪到 canvas
                bx1 = Math.Clamp(bx1, 0, ts2);
                by1 = Math.Clamp(by1, 0, ts2);
                bx2 = Math.Clamp(bx2, 0, ts2);
                by2 = Math.Clamp(by2, 0, ts2);

                if (bx2 <= bx1 || by2 <= by1) continue;
                allBoxes.Add(new NormalizedBox(bx1, by1, bx2, by2, b.ClassId));
            }
        }

        // 裁剪 canvas 到 [0, 0, targetSize, targetSize]（从左上角取）
        var cropX = 0;
        var cropY = 0;

        var output = canvas.Clone(ctx => ctx.Crop(new Rectangle(cropX, cropY, ts, ts)));

        // 框变换到裁剪后坐标 → 归一化
        var finalBoxes = new List<NormalizedBox>(allBoxes.Count);
        foreach (var b in allBoxes)
        {
            var fx1 = (b.X1 - cropX) / ts;
            var fy1 = (b.Y1 - cropY) / ts;
            var fx2 = (b.X2 - cropX) / ts;
            var fy2 = (b.Y2 - cropY) / ts;

            fx1 = Math.Clamp(fx1, 0f, 1f);
            fy1 = Math.Clamp(fy1, 0f, 1f);
            fx2 = Math.Clamp(fx2, 0f, 1f);
            fy2 = Math.Clamp(fy2, 0f, 1f);

            if (fx2 - fx1 < _minBoxSize || fy2 - fy1 < _minBoxSize) continue;

            finalBoxes.Add(new NormalizedBox(fx1, fy1, fx2, fy2, b.ClassId));
        }

        return (output, finalBoxes);
    }
}
