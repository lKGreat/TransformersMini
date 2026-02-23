using TorchSharp;
using Xunit;
using TransformersMini.Infrastructure.TorchSharp.Detection;
using TransformersMini.Infrastructure.TorchSharp.Detection.Backbone;
using TransformersMini.Infrastructure.TorchSharp.Detection.Head;
using TransformersMini.Infrastructure.TorchSharp.Detection.Loss;
using static TorchSharp.torch;

namespace TransformersMini.Tests.Unit.Detection;

/// <summary>
/// YOLO 损失函数单元测试（TASK-YOLO-131）。
/// </summary>
public sealed class YoloLossTests : IDisposable
{
    private bool _disposed;

    [Fact]
    public void YoloDetectionLoss_RandomInput_NoNanOrInf()
    {
        var nc = 2;
        var batchSize = 2;
        var maxGt = 3;

        using var model = new YoloDetectionModel(nc, scale: YoloScale.Nano);
        model.train();
        var lossFn = new YoloDetectionLoss(nc, regMax: 16);

        using var input = randn(batchSize, 3, 640, 640);
        var headOut = model.forward(input);

        // 构造 GT：随机框和类别
        using var gtBboxes = randn(batchSize, maxGt, 4).abs_().mul(200); // xyxy 像素坐标
        using var gtLabels = randint(0, nc, [batchSize, maxGt, 1]);
        using var maskGt = ones(batchSize, maxGt, 1).@bool();

        var (total, box, cls, dfl) = lossFn.Compute(headOut, gtBboxes, gtLabels, maskGt);

        Assert.False(float.IsNaN(total.item<float>()));
        Assert.False(float.IsInfinity(total.item<float>()));
        Assert.True(total.item<float>() >= 0f);

        total.Dispose();
        box.Dispose();
        cls.Dispose();
        dfl.Dispose();
    }

    [Fact]
    public void YoloDetectionLoss_AfterBackward_GradientsAreFinite()
    {
        var nc = 2;
        var batchSize = 1;

        using var model = new YoloDetectionModel(nc, scale: YoloScale.Nano);
        model.train();
        var lossFn = new YoloDetectionLoss(nc, regMax: 16);

        using var input = randn(batchSize, 3, 640, 640);
        var headOut = model.forward(input);

        using var gtBboxes = tensor(new[] { 100f, 100f, 200f, 200f }, [1, 1, 4]);
        using var gtLabels = tensor(new long[] { 0 }, [1, 1, 1]);
        using var maskGt = ones(1, 1, 1).@bool();

        var (total, _, _, _) = lossFn.Compute(headOut, gtBboxes, gtLabels, maskGt);
        total.backward();

        foreach (var kv in model.named_parameters())
        {
            var param = kv.parameter;
            var grad = param.grad;
            if (grad is null) continue;
            var arr = grad.cpu().data<float>().ToArray();
            Assert.All(arr, v =>
            {
                Assert.False(float.IsNaN(v));
                Assert.False(float.IsInfinity(v));
            });
        }

        total.Dispose();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
    }
}
