using TorchSharp;
using Xunit;
using TransformersMini.Infrastructure.TorchSharp.Detection;
using TransformersMini.Infrastructure.TorchSharp.Detection.Backbone;
using TransformersMini.Infrastructure.TorchSharp.Detection.Neck;
using static TorchSharp.torch;

namespace TransformersMini.Tests.Unit.Detection;

/// <summary>
/// YOLO 模块形状断言单元测试（TASK-YOLO-130）。
/// </summary>
public sealed class YoloModuleShapeTests : IDisposable
{
    private bool _disposed;

    [Fact]
    public void YoloBackbone_NanoScale_Input640_OutputsP3P4P5CorrectShapes()
    {
        using var backbone = new YoloBackbone(YoloScale.Nano);
        using var input = randn(1, 3, 640, 640);

        var (p3, p4, p5) = backbone.forward(input);

        Assert.Equal([1, backbone.P3Channels, 80, 80], p3.shape);
        Assert.Equal([1, backbone.P4Channels, 40, 40], p4.shape);
        Assert.Equal([1, backbone.P5Channels, 20, 20], p5.shape);

        p3.Dispose();
        p4.Dispose();
        p5.Dispose();
    }

    [Fact]
    public void PanNeck_NanoScale_OutputsF3F4F5CorrectShapes()
    {
        var backbone = new YoloBackbone(YoloScale.Nano);
        using var input = randn(1, 3, 640, 640);
        var (p3, p4, p5) = backbone.forward(input);

        using var neck = new PanNeck(backbone.P3Channels, backbone.P4Channels, backbone.P5Channels);
        var (f3, f4, f5) = neck.forward((p3, p4, p5));

        Assert.Equal([1, neck.F3Channels, 80, 80], f3.shape);
        Assert.Equal([1, neck.F4Channels, 40, 40], f4.shape);
        Assert.Equal([1, neck.F5Channels, 20, 20], f5.shape);

        backbone.Dispose();
        p3.Dispose();
        p4.Dispose();
        p5.Dispose();
        f3.Dispose();
        f4.Dispose();
        f5.Dispose();
    }

    [Fact]
    public void YoloDetectionModel_TrainingMode_ForwardReturnsValidOutput()
    {
        using var model = new YoloDetectionModel(nc: 80, scale: YoloScale.Nano);
        model.train();
        using var input = randn(2, 3, 640, 640);

        var output = model.forward(input);

        Assert.True(output.IsTraining);
        Assert.NotNull(output.Primary);
        Assert.NotNull(output.Scores);
        Assert.NotNull(output.Feats);
        // boxes_distri: [b, 4*reg_max, total_a]
        var totalA = 80 * 80 + 40 * 40 + 20 * 20;
        Assert.Equal([2, 64, totalA], output.Primary.shape);
        Assert.Equal([2, 80, totalA], output.Scores!.shape);
    }

    [Fact]
    public void YoloDetectionModel_EvalMode_ForwardReturnsDecodedOutput()
    {
        using var model = new YoloDetectionModel(nc: 80, scale: YoloScale.Nano);
        model.eval();
        using var input = randn(1, 3, 640, 640);

        using (no_grad())
        {
            var output = model.forward(input);
            Assert.False(output.IsTraining);
            Assert.NotNull(output.Primary);
            // [b, 4+nc, total_a]
            var totalA = 80 * 80 + 40 * 40 + 20 * 20;
            Assert.Equal([1, 84, totalA], output.Primary.shape);
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
    }
}
