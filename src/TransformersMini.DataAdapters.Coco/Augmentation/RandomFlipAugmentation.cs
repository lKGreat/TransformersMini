using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace TransformersMini.DataAdapters.Coco.Augmentation;

/// <summary>
/// GT 框（归一化 xyxy 坐标）。
/// </summary>
public sealed record NormalizedBox(float X1, float Y1, float X2, float Y2, long ClassId);

/// <summary>
/// 随机水平翻转增强。
/// 以概率 p（默认 0.5）翻转图片，同步变换归一化 GT 框坐标。
/// </summary>
public sealed class RandomFlipAugmentation
{
    private readonly float _prob;
    private readonly Random _rng;

    public RandomFlipAugmentation(float prob = 0.5f, Random? rng = null)
    {
        _prob = prob;
        _rng = rng ?? Random.Shared;
    }

    /// <summary>
    /// 对图片和框执行随机水平翻转。
    /// </summary>
    /// <param name="image">要增强的图片（原地修改）</param>
    /// <param name="boxes">归一化 xyxy 框列表</param>
    /// <returns>增强后的框列表（可能为新列表，也可能为原列表）</returns>
    public IReadOnlyList<NormalizedBox> Apply(Image<Rgb24> image, IReadOnlyList<NormalizedBox> boxes)
    {
        if (_rng.NextSingle() > _prob)
            return boxes;

        // 水平翻转图片
        image.Mutate(ctx => ctx.Flip(FlipMode.Horizontal));

        // 变换框坐标：x_new = 1 - x_old（归一化坐标系下）
        var flipped = new List<NormalizedBox>(boxes.Count);
        foreach (var b in boxes)
        {
            var x1New = 1f - b.X2;
            var x2New = 1f - b.X1;
            flipped.Add(new NormalizedBox(
                Math.Clamp(x1New, 0f, 1f),
                b.Y1,
                Math.Clamp(x2New, 0f, 1f),
                b.Y2,
                b.ClassId));
        }

        return flipped;
    }
}
