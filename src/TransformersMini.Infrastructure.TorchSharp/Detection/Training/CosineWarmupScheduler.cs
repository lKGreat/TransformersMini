namespace TransformersMini.Infrastructure.TorchSharp.Detection.Training;

/// <summary>
/// Warmup + 余弦退火学习率调度器。
/// warmup 阶段：lr 从 lrMin 线性增长到 lrMax。
/// 余弦阶段：lr = lrMin + 0.5*(lrMax-lrMin)*(1+cos(π*epoch/totalEpochs))。
/// </summary>
public sealed class CosineWarmupScheduler
{
    private readonly double _lrMax;
    private readonly double _lrMin;
    private readonly int _warmupEpochs;
    private readonly int _totalEpochs;

    /// <param name="lrMax">峰值学习率（Adam 初始学习率）</param>
    /// <param name="lrMin">最小学习率（默认 lrMax * 0.01）</param>
    /// <param name="warmupEpochs">warmup 轮数（默认 3）</param>
    /// <param name="totalEpochs">总训练轮数</param>
    public CosineWarmupScheduler(
        double lrMax,
        double? lrMin = null,
        int warmupEpochs = 3,
        int totalEpochs = 100)
    {
        _lrMax = lrMax;
        _lrMin = lrMin ?? lrMax * 0.01;
        _warmupEpochs = Math.Max(1, warmupEpochs);
        _totalEpochs = Math.Max(1, totalEpochs);
    }

    /// <summary>
    /// 获取指定轮次的学习率。
    /// </summary>
    /// <param name="epoch">当前 epoch（1-based）</param>
    /// <returns>该 epoch 应使用的学习率</returns>
    public double GetLr(int epoch)
    {
        if (epoch <= _warmupEpochs)
        {
            // Warmup：线性增长
            var progress = (double)(epoch - 1) / Math.Max(1, _warmupEpochs - 1);
            return _lrMin + progress * (_lrMax - _lrMin);
        }

        // 余弦退火
        var cosEpoch = epoch - _warmupEpochs;
        var cosTotal = _totalEpochs - _warmupEpochs;
        var cosProgress = (double)cosEpoch / Math.Max(1, cosTotal);
        return _lrMin + 0.5 * (_lrMax - _lrMin) * (1.0 + Math.Cos(Math.PI * cosProgress));
    }
}
