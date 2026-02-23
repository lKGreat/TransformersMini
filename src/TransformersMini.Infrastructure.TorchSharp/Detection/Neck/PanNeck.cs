using TorchSharp;
using TorchSharp.Modules;
using static TorchSharp.torch;
using static TorchSharp.torch.nn;
using TransformersMini.Infrastructure.TorchSharp.Detection.Modules;

namespace TransformersMini.Infrastructure.TorchSharp.Detection.Neck;

/// <summary>
/// PAN Neck（Path Aggregation Network）：
/// FPN 自顶向下：P5 → 上采样 + cat(P4) → mid4，mid4 → 上采样 + cat(P3) → F3
/// PANet 自底向上：F3 → Conv(s=2) + cat(mid4) → F4，F4 → Conv(s=2) + cat(P5) → F5
/// 输出 (F3, F4, F5) 分别对应小目标/中目标/大目标检测尺度。
/// </summary>
public sealed class PanNeck : Module<(Tensor, Tensor, Tensor), (Tensor F3, Tensor F4, Tensor F5)>
{
    private readonly Upsample _upsample;

    // FPN 自顶向下路径
    private readonly C2f _fpnMid4;  // cat(P5_up, P4) → mid4
    private readonly C2f _fpnF3;    // cat(mid4_up, P3) → F3（小目标）

    // PANet 自底向上路径
    private readonly ConvBnAct _panDown3; // F3 下采样（stride=2）
    private readonly C2f _panF4;          // cat(panDown3, mid4) → F4（中目标）
    private readonly ConvBnAct _panDown4; // F4 下采样（stride=2）
    private readonly C2f _panF5;          // cat(panDown4, P5) → F5（大目标）

    /// <summary>F3 输出通道数（小目标尺度）。</summary>
    public long F3Channels { get; }
    /// <summary>F4 输出通道数（中目标尺度）。</summary>
    public long F4Channels { get; }
    /// <summary>F5 输出通道数（大目标尺度）。</summary>
    public long F5Channels { get; }

    /// <param name="p3Ch">Backbone P3 通道数</param>
    /// <param name="p4Ch">Backbone P4 通道数</param>
    /// <param name="p5Ch">Backbone P5 通道数</param>
    /// <param name="n">C2f 内部 Bottleneck 块数（深度）</param>
    public PanNeck(long p3Ch, long p4Ch, long p5Ch, int n = 1) : base(nameof(PanNeck))
    {
        _upsample = Upsample(scale_factor: new double[] { 2, 2 }, mode: UpsampleMode.Nearest);

        // FPN：P5(p5Ch) 上采样后 cat P4(p4Ch) → 2路 → p4Ch 输出
        var mid4Ch = p4Ch;
        _fpnMid4 = new C2f(p5Ch + p4Ch, mid4Ch, n: n);

        // FPN：mid4(mid4Ch) 上采样后 cat P3(p3Ch) → p3Ch 输出 = F3
        F3Channels = p3Ch;
        _fpnF3 = new C2f(mid4Ch + p3Ch, F3Channels, n: n);

        // PANet：F3(F3Ch) stride=2 下采样后 cat mid4(mid4Ch) → mid4Ch 输出 = F4
        _panDown3 = new ConvBnAct(F3Channels, F3Channels, 3, stride: 2);
        F4Channels = mid4Ch;
        _panF4 = new C2f(F3Channels + mid4Ch, F4Channels, n: n);

        // PANet：F4(F4Ch) stride=2 下采样后 cat P5(p5Ch) → p5Ch 输出 = F5
        _panDown4 = new ConvBnAct(F4Channels, F4Channels, 3, stride: 2);
        F5Channels = p5Ch;
        _panF5 = new C2f(F4Channels + p5Ch, F5Channels, n: n);

        RegisterComponents();
    }

    public override (Tensor F3, Tensor F4, Tensor F5) forward((Tensor, Tensor, Tensor) input)
    {
        var (p3, p4, p5) = input;

        // ── FPN 自顶向下 ──────────────────────────────────────────────
        // P5 上采样
        using var p5Up = _upsample.forward(p5);
        // cat(P5_up, P4) → C2f → mid4
        using var catMid4 = torch.cat([p5Up, p4], dim: 1);
        using var mid4 = _fpnMid4.forward(catMid4);

        // mid4 上采样
        using var mid4Up = _upsample.forward(mid4);
        // cat(mid4_up, P3) → C2f → F3
        using var catF3 = torch.cat([mid4Up, p3], dim: 1);
        var f3 = _fpnF3.forward(catF3);  // 返回给调用方

        // ── PANet 自底向上 ─────────────────────────────────────────────
        // F3 下采样 → cat mid4 → C2f → F4
        using var down3 = _panDown3.forward(f3);
        using var catF4 = torch.cat([down3, mid4], dim: 1);
        var f4 = _panF4.forward(catF4);  // 返回

        // F4 下采样 → cat P5 → C2f → F5
        using var down4 = _panDown4.forward(f4);
        using var catF5 = torch.cat([down4, p5], dim: 1);
        var f5 = _panF5.forward(catF5);  // 返回

        return (f3, f4, f5);
    }
}
