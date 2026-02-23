using TorchSharp;
using static TorchSharp.torch;

namespace TransformersMini.Infrastructure.TorchSharp.Detection.PostProcess;

/// <summary>
/// 单个检测结果框。
/// </summary>
public sealed record DetectionBox(
    float X1,
    float Y1,
    float X2,
    float Y2,
    float Confidence,
    int ClassId);

/// <summary>
/// NMS 后处理器：置信度阈值过滤 + IoU NMS。
/// 对应 ultralytics utils/ops.py non_max_suppression。
/// </summary>
public sealed class NmsProcessor
{
    private readonly float _confThresh;
    private readonly float _iouThresh;
    private readonly int _maxDet;

    public NmsProcessor(float confThresh = 0.25f, float iouThresh = 0.45f, int maxDet = 300)
    {
        _confThresh = confThresh;
        _iouThresh = iouThresh;
        _maxDet = maxDet;
    }

    /// <summary>
    /// 对推理输出执行 NMS，返回每张图片的检测框列表。
    /// </summary>
    /// <param name="output">DetectHead 推理输出 [b, 4+nc, total_a]</param>
    /// <returns>长度为 b 的检测框列表（每张图片独立）</returns>
    public IReadOnlyList<IReadOnlyList<DetectionBox>> Process(Tensor output)
    {
        var bs = (int)output.shape[0];
        // output: [b, 4+nc, total_a] → permute → [b, total_a, 4+nc]
        using var perm = output.permute(0, 2, 1).contiguous();

        var results = new List<IReadOnlyList<DetectionBox>>(bs);
        for (var i = 0; i < bs; i++)
        {
            using var pred = perm[i]; // [total_a, 4+nc]
            results.Add(ProcessSingle(pred));
        }
        return results;
    }

    /// <summary>对单张图片的预测结果执行 NMS。</summary>
    private IReadOnlyList<DetectionBox> ProcessSingle(Tensor pred)
    {
        // pred: [total_a, 4+nc]
        var nc = (int)pred.shape[1] - 4;

        // 提取 xyxy 和 class scores
        using var xyxy = pred[.., 0..4];         // [total_a, 4]
        using var classScores = pred[.., 4..];   // [total_a, nc]

        // 计算置信度 = max class score
        var (confValues, classIds) = classScores.max(1);
        using var classIdsD = classIds;

        // 置信度过滤
        using var confMask = confValues.gt(_confThresh); // [total_a]
        var numCandidates = confMask.sum().item<long>();

        if (numCandidates == 0)
        {
            confValues.Dispose();
            return Array.Empty<DetectionBox>();
        }

        using var confFiltered = confValues.masked_select(confMask);          // [m]
        using var classFiltered = classIdsD.masked_select(confMask);          // [m]
        using var maskExp4 = confMask.unsqueeze(-1).expand(-1, 4);
        using var xyxyFiltered = xyxy.masked_select(maskExp4).view(numCandidates, 4); // [m, 4]

        confValues.Dispose();

        // 按置信度降序排序
        using var sortedIdxTensor = confFiltered.argsort(descending: true);
        var totalCands = (int)numCandidates;
        var boxes = new List<DetectionBox>(Math.Min(totalCands, _maxDet));

        // 转为 CPU float 数组方便操作
        var xyxyArr = xyxyFiltered.cpu().data<float>().ToArray();
        var confArr = confFiltered.cpu().data<float>().ToArray();
        var clsArr = classFiltered.cpu().data<long>().ToArray();
        var sortedIdx = sortedIdxTensor.cpu().data<long>().ToArray();

        var suppressed = new bool[totalCands];

        foreach (var si in sortedIdx)
        {
            var i = (int)si;
            if (suppressed[i]) continue;

            var x1 = xyxyArr[i * 4];
            var y1 = xyxyArr[i * 4 + 1];
            var x2 = xyxyArr[i * 4 + 2];
            var y2 = xyxyArr[i * 4 + 3];
            var conf = confArr[i];
            var cls = (int)clsArr[i];

            boxes.Add(new DetectionBox(x1, y1, x2, y2, conf, cls));
            if (boxes.Count >= _maxDet) break;

            // 对后续候选框做 IoU 抑制（同类）
            var area1 = Math.Max(0f, x2 - x1) * Math.Max(0f, y2 - y1);
            foreach (var sj in sortedIdx)
            {
                var j = (int)sj;
                if (suppressed[j] || j == i) continue;
                if (clsArr[j] != clsArr[i]) continue; // 仅同类抑制

                var jx1 = xyxyArr[j * 4];
                var jy1 = xyxyArr[j * 4 + 1];
                var jx2 = xyxyArr[j * 4 + 2];
                var jy2 = xyxyArr[j * 4 + 3];

                var interX1 = Math.Max(x1, jx1);
                var interY1 = Math.Max(y1, jy1);
                var interX2 = Math.Min(x2, jx2);
                var interY2 = Math.Min(y2, jy2);
                var interArea = Math.Max(0f, interX2 - interX1) * Math.Max(0f, interY2 - interY1);
                var area2 = Math.Max(0f, jx2 - jx1) * Math.Max(0f, jy2 - jy1);
                var unionArea = area1 + area2 - interArea + 1e-7f;
                var iou = interArea / unionArea;

                if (iou > _iouThresh)
                    suppressed[j] = true;
            }
        }

        return boxes;
    }
}
