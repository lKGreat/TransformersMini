using TorchSharp;
using static TorchSharp.torch;
using static TorchSharp.torch.nn;

namespace TransformersMini.Infrastructure.TorchSharp.Detection.Training;

/// <summary>
/// 模型参数指数移动平均（EMA）。
/// 参考 ultralytics/utils/torch_utils.py ModelEMA。
/// decay 在 warmup 阶段从较小值逐步增大，避免早期 EMA 对随机初始化过度依赖。
/// </summary>
public sealed class ModelEma : IDisposable
{
    // EMA 参数存储（name → clone tensor）
    private readonly Dictionary<string, Tensor> _emaParams = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Tensor> _emaBuffers = new(StringComparer.Ordinal);
    private readonly double _decay;
    private readonly double _tau;
    private int _updateCount;
    private bool _disposed;

    /// <param name="model">待追踪的训练模型（初始权重用于初始化 EMA 副本）</param>
    /// <param name="decay">目标 EMA 衰减系数（默认 0.9999）</param>
    /// <param name="tau">warmup 步数（默认 2000）</param>
    public ModelEma(Module model, double decay = 0.9999, double tau = 2000)
    {
        _decay = decay;
        _tau = tau;
        _updateCount = 0;

        // 初始化 EMA 副本（克隆当前参数，不追踪梯度）
        using var _ = torch.no_grad();
        foreach (var (name, param) in model.named_parameters())
        {
            _emaParams[name] = param.clone().detach_();
        }

        foreach (var (name, buffer) in model.named_buffers())
        {
            _emaBuffers[name] = buffer.clone().detach_();
        }
    }

    /// <summary>
    /// 用当前步数计算实际 decay：d = decay * (1 - exp(-steps / tau))。
    /// </summary>
    public double CurrentDecay => _decay * (1.0 - Math.Exp(-_updateCount / _tau));

    /// <summary>
    /// 用当前模型参数更新 EMA 副本。
    /// 需在 optimizer.step() 之后、下一次 forward 之前调用。
    /// </summary>
    public void Update(Module model)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _updateCount++;
        var d = (float)CurrentDecay;
        var oneMinusD = 1f - d;

        using var _ = torch.no_grad();
        foreach (var (name, param) in model.named_parameters())
        {
            if (!_emaParams.TryGetValue(name, out var emaParam)) continue;
            // ema = d * ema + (1-d) * src
            emaParam.mul_(d).add_(param, alpha: oneMinusD);
        }

        foreach (var (name, buffer) in model.named_buffers())
        {
            if (!_emaBuffers.TryGetValue(name, out var emaBuf)) continue;
            emaBuf.copy_(buffer);
        }
    }

    /// <summary>将 EMA 权重复制到目标模型（推理前调用以使用 EMA 权重）。</summary>
    public void ApplyTo(Module targetModel)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        using var _ = torch.no_grad();
        foreach (var (name, tgtParam) in targetModel.named_parameters())
        {
            if (!_emaParams.TryGetValue(name, out var emaParam)) continue;
            tgtParam.copy_(emaParam);
        }

        foreach (var (name, tgtBuf) in targetModel.named_buffers())
        {
            if (!_emaBuffers.TryGetValue(name, out var emaBuf)) continue;
            tgtBuf.copy_(emaBuf);
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        foreach (var t in _emaParams.Values) t.Dispose();
        foreach (var t in _emaBuffers.Values) t.Dispose();
        _emaParams.Clear();
        _emaBuffers.Clear();
    }
}
