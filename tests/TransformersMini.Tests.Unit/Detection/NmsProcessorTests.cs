using TorchSharp;
using Xunit;
using TransformersMini.Infrastructure.TorchSharp.Detection.PostProcess;
using static TorchSharp.torch;

namespace TransformersMini.Tests.Unit.Detection;

/// <summary>
/// NMS 后处理器单元测试（TASK-YOLO-132）。
/// </summary>
public sealed class NmsProcessorTests : IDisposable
{
    private bool _disposed;

    [Fact]
    public void NmsProcessor_OverlappingBoxes_KeepsOnlyOne()
    {
        // 构造 5 个高度重叠的框（同一位置，conf 不同）
        // output: [1, 4+nc, total_a] = [1, 5, 5]，布局 [batch, feat, anchor]
        var data = new float[1 * 5 * 5];
        for (var a = 0; a < 5; a++)
        {
            data[0 * 5 + a] = 100;   // x1
            data[1 * 5 + a] = 100;   // y1
            data[2 * 5 + a] = 200;   // x2
            data[3 * 5 + a] = 200;   // y2
            data[4 * 5 + a] = 0.3f + a * 0.1f; // conf
        }

        using var output = tensor(data, [1, 5, 5]);
        var nms = new NmsProcessor(confThresh: 0.25f, iouThresh: 0.45f);
        var results = nms.Process(output);

        Assert.Single(results);
        Assert.Single(results[0]);
        Assert.True(results[0][0].Confidence >= 0.5f);
    }

    [Fact]
    public void NmsProcessor_NonOverlappingBoxes_KeepsAll()
    {
        // 3 个不重叠框，布局 [batch, feat, anchor]
        var data = new float[1 * 5 * 3];
        data[0 * 3 + 0] = 10; data[0 * 3 + 1] = 100; data[0 * 3 + 2] = 200;   // x1
        data[1 * 3 + 0] = 10; data[1 * 3 + 1] = 100; data[1 * 3 + 2] = 200;   // y1
        data[2 * 3 + 0] = 50; data[2 * 3 + 1] = 150; data[2 * 3 + 2] = 250;   // x2
        data[3 * 3 + 0] = 50; data[3 * 3 + 1] = 150; data[3 * 3 + 2] = 250;   // y2
        data[4 * 3 + 0] = 0.9f; data[4 * 3 + 1] = 0.8f; data[4 * 3 + 2] = 0.7f; // conf

        using var output = tensor(data, [1, 5, 3]);
        var nms = new NmsProcessor(confThresh: 0.25f, iouThresh: 0.45f);
        var results = nms.Process(output);

        Assert.Single(results);
        Assert.Equal(3, results[0].Count);
    }

    [Fact]
    public void NmsProcessor_BelowConfThreshold_FiltersOut()
    {
        var data = new float[1 * 5 * 2];
        data[0 * 2 + 0] = 10; data[0 * 2 + 1] = 100; data[1 * 2 + 0] = 10; data[1 * 2 + 1] = 100;
        data[2 * 2 + 0] = 50; data[2 * 2 + 1] = 150; data[3 * 2 + 0] = 50; data[3 * 2 + 1] = 150;
        data[4 * 2 + 0] = 0.1f; data[4 * 2 + 1] = 0.05f; // 低 conf

        using var output = tensor(data, [1, 5, 2]);
        var nms = new NmsProcessor(confThresh: 0.25f, iouThresh: 0.45f);
        var results = nms.Process(output);

        Assert.Single(results);
        Assert.Empty(results[0]);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
    }
}
