using System.Globalization;
using System.Text.Json;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Processing.Processors.Transforms;
using TorchSharp;
using TransformersMini.Contracts.Abstractions;
using TransformersMini.Contracts.Data;
using TransformersMini.Contracts.Runtime;
using TransformersMini.SharedKernel.Core;
using static TorchSharp.torch;
using static TorchSharp.torch.nn;
using CoreDeviceType = TransformersMini.SharedKernel.Core.DeviceType;

namespace TransformersMini.Training.Tasks.Detection;

public sealed class DetectionTrainingTask : ITrainingTask
{
    private const int DetectionTargetBoxValueCount = 6; // cx, cy, bw, bh, cls, obj
    private const float EvalIouThreshold = 0.5f;
    private const double DetectionBboxLossWeight = 1.0d;
    private const double DetectionCategoryLossWeight = 0.5d;
    private const double DetectionObjectnessLossWeight = 1.0d;
    private static readonly JsonSerializerOptions TrainArtifactJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public TaskType TaskType => TaskType.Detection;

    public bool Supports(RunMode mode) => true;

    public async Task<RunResult> ExecuteAsync(TrainingExecutionContext context, CancellationToken ct)
    {
        if (context.Config.Backend != BackendType.TorchSharp)
        {
            return await ExecuteNonTorchSharpStubAsync(context, ct);
        }

        return context.Config.Mode switch
        {
            RunMode.Train => await ExecuteTorchSharpTrainAsync(context, ct),
            RunMode.Validate => await ExecuteTorchSharpEvalAsync(context, "validate", context.Data.Validation, ct),
            RunMode.Test => await ExecuteTorchSharpEvalAsync(context, "test", context.Data.Test, ct),
            _ => throw new InvalidOperationException("不支持的运行模式。")
        };
    }

    private static async Task<RunResult> ExecuteTorchSharpTrainAsync(TrainingExecutionContext context, CancellationToken ct)
    {
        var trainSamples = context.Data.Train;
        var sampleCount = Math.Max(1, trainSamples.Count);

        await context.RunRepository.AppendEventAsync(
            context.RunId,
            new RunEvent("Information", "DetectionTaskStart", "TorchSharp 检测训练开始。", DateTimeOffset.UtcNow),
            ct);

        try
        {
            var torchDevice = ResolveTorchDevice(context.Config.Device);
            var tensorOptions = ResolveTensorOptions(context);
            var inputSize = tensorOptions.InputSize;
            var batchSize = Math.Max(1, context.Config.Optimization.BatchSize);
            var epochs = Math.Max(1, context.Config.Optimization.Epochs);
            var stepsPerEpoch = Math.Max(1, (int)Math.Ceiling(sampleCount / (double)batchSize));

            await WritePreprocessingTagsAsync(context, tensorOptions, ct);
            await AppendPreprocessingEventAsync(context, tensorOptions, ct);

            torch.random.manual_seed(Math.Max(1, context.Config.Optimization.Seed));

            using var model = CreateDetectorModel(tensorOptions.TargetTopK).to(torchDevice);
            using var optimizer = torch.optim.Adam(model.parameters(), context.Config.Optimization.LearningRate);
            model.train();
            double trainLossSum = 0d;
            double trainBboxLossSum = 0d;
            double trainCategoryLossSum = 0d;
            double trainObjectnessLossSum = 0d;
            var trainStepCount = 0;

            for (var epoch = 1; epoch <= epochs; epoch++)
            {
                ct.ThrowIfCancellationRequested();
                double epochLoss = 0;
                double epochBboxLoss = 0d;
                double epochCategoryLoss = 0d;
                double epochObjectnessLoss = 0d;

                for (var step = 1; step <= stepsPerEpoch; step++)
                {
                    ct.ThrowIfCancellationRequested();
                    var globalStep = ((epoch - 1) * stepsPerEpoch) + step;

                    using var inputs = BuildInputTensor(
                        trainSamples,
                        batchSize,
                        globalStep,
                        tensorOptions,
                        torchDevice,
                        context.Config.Dataset.SkipInvalidSamples,
                        isTraining: true);
                    using var targets = BuildTargetTensor(trainSamples, batchSize, globalStep, tensorOptions, torchDevice);

                    optimizer.zero_grad();
                    using var prediction = model.forward(inputs);
                    using var lossBreakdown = ComputeDetectionTrainLoss(prediction, targets, batchSize, tensorOptions.TargetTopK);
                    using var loss = lossBreakdown.TotalLoss;
                    loss.backward();
                    optimizer.step();

                    var lossValue = loss.ToDouble();
                    var bboxLossValue = lossBreakdown.BboxLoss.ToDouble();
                    var categoryLossValue = lossBreakdown.CategoryLoss.ToDouble();
                    var objectnessLossValue = lossBreakdown.ObjectnessLoss.ToDouble();
                    epochLoss += lossValue;
                    epochBboxLoss += bboxLossValue;
                    epochCategoryLoss += categoryLossValue;
                    epochObjectnessLoss += objectnessLossValue;
                    trainLossSum += lossValue;
                    trainBboxLossSum += bboxLossValue;
                    trainCategoryLossSum += categoryLossValue;
                    trainObjectnessLossSum += objectnessLossValue;
                    trainStepCount++;
                    var metricTimestamp = DateTimeOffset.UtcNow;

                    var trainMetrics = new[]
                    {
                        new MetricPoint("loss", globalStep, lossValue, metricTimestamp),
                        new MetricPoint("loss_bbox", globalStep, bboxLossValue, metricTimestamp),
                        new MetricPoint("loss_category", globalStep, categoryLossValue, metricTimestamp),
                        new MetricPoint("loss_objectness", globalStep, objectnessLossValue, metricTimestamp)
                    };

                    foreach (var metric in trainMetrics)
                    {
                        await context.RunRepository.AppendMetricAsync(context.RunId, metric, ct);
                    }

                    await context.ArtifactStore.AppendLineAsync(
                        context.RunId,
                        "metrics.jsonl",
                        JsonSerializer.Serialize(new DetectionTrainMetricStreamEntry(
                            globalStep,
                            epoch,
                            lossValue,
                            bboxLossValue,
                            categoryLossValue,
                            objectnessLossValue)),
                        ct);
                }

                var avgLoss = epochLoss / stepsPerEpoch;
                var avgEpochBboxLoss = epochBboxLoss / stepsPerEpoch;
                var avgEpochCategoryLoss = epochCategoryLoss / stepsPerEpoch;
                var avgEpochObjectnessLoss = epochObjectnessLoss / stepsPerEpoch;
                await context.RunRepository.AppendEventAsync(
                    context.RunId,
                    new RunEvent(
                        "Information",
                        "EpochCompleted",
                        $"第 {epoch} 轮完成，平均损失 {avgLoss:F6}（bbox={avgEpochBboxLoss:F6}, cls={avgEpochCategoryLoss:F6}, obj={avgEpochObjectnessLoss:F6}）",
                        DateTimeOffset.UtcNow),
                    ct);
            }

            var denominator = Math.Max(1, trainStepCount);
            var trainLossSummary = new DetectionTrainLossSummarySnapshot(
                trainLossSum / denominator,
                trainBboxLossSum / denominator,
                trainCategoryLossSum / denominator,
                trainObjectnessLossSum / denominator,
                trainStepCount);
            var lossWeights = new DetectionLossWeightsSnapshot(
                DetectionBboxLossWeight,
                DetectionCategoryLossWeight,
                DetectionObjectnessLossWeight);

            await context.ArtifactStore.WriteTextAsync(
                context.RunId,
                "artifacts/model-metadata.json",
                JsonSerializer.Serialize(new DetectionTrainModelMetadataReport(
                    "TorchSharp",
                    "detection",
                    context.Config.Device.ToString(),
                    string.IsNullOrWhiteSpace(context.Config.Model.Architecture) ? "tiny-cnn-detector-multihead" : context.Config.Model.Architecture,
                    inputSize,
                    DetectionPreprocessingSnapshot.FromTensorOptions(tensorOptions),
                    DetectionTargetEncodingSnapshot.FromTopK(tensorOptions.TargetTopK),
                    lossWeights,
                    "multi-branch",
                    "trained"), TrainArtifactJsonOptions),
                ct);

            await context.ArtifactStore.WriteTextAsync(
                context.RunId,
                "reports/summary.json",
                JsonSerializer.Serialize(new DetectionTrainSummaryReport(
                    "detection",
                    "Train",
                    context.Config.Backend.ToString(),
                    context.Config.Device.ToString(),
                    sampleCount,
                    epochs,
                    stepsPerEpoch,
                    inputSize,
                    DetectionPreprocessingSnapshot.FromTensorOptions(tensorOptions),
                    DetectionTargetEncodingSnapshot.FromTopK(tensorOptions.TargetTopK),
                    lossWeights,
                    trainLossSummary,
                    "multi-branch",
                    "torchsharp-train-complete"), TrainArtifactJsonOptions),
                ct);

            return new RunResult(context.RunId, RunStatus.Succeeded, "Detection TorchSharp 训练完成。", context.RunDirectory);
        }
        catch (DllNotFoundException ex)
        {
            throw new InvalidOperationException("TorchSharp 运行时不可用，请安装对应 CPU/CUDA 运行时依赖。", ex);
        }
    }

    private static async Task<RunResult> ExecuteTorchSharpEvalAsync(
        TrainingExecutionContext context,
        string stage,
        IReadOnlyList<DataSample> samples,
        CancellationToken ct)
    {
        var sampleCount = Math.Max(1, samples.Count);

        try
        {
            var torchDevice = ResolveTorchDevice(context.Config.Device);
            var tensorOptions = ResolveTensorOptions(context);
            var inputSize = tensorOptions.InputSize;
            await WritePreprocessingTagsAsync(context, tensorOptions, ct);
            await AppendPreprocessingEventAsync(context, tensorOptions, ct);
            using var model = CreateDetectorModel(tensorOptions.TargetTopK).to(torchDevice);
            model.eval();

            using var inputs = BuildInputTensor(
                samples,
                sampleCount,
                1,
                tensorOptions,
                torchDevice,
                allowFallbackOnImageError: true,
                isTraining: false);
            using var target = BuildTargetTensor(samples, sampleCount, 1, tensorOptions, torchDevice);
            using var prediction = model.forward(inputs);
            var evalResult = ComputeApproxEvalMetrics(prediction, target, samples, sampleCount, tensorOptions.TargetTopK);
            var evalMetrics = evalResult.Metrics;
            var primaryMetricName = stage == "test" ? "mAP50_test" : "mAP50";

            var metricPoints = new[]
            {
                new MetricPoint(primaryMetricName, 1, evalMetrics.PrecisionAtIou50, DateTimeOffset.UtcNow),
                new MetricPoint(stage == "test" ? "precision50_test" : "precision50", 1, evalMetrics.PrecisionAtIou50, DateTimeOffset.UtcNow),
                new MetricPoint(stage == "test" ? "recall50_test" : "recall50", 1, evalMetrics.RecallAtIou50, DateTimeOffset.UtcNow),
                new MetricPoint(stage == "test" ? "meanIoU_test" : "meanIoU", 1, evalMetrics.MeanIou, DateTimeOffset.UtcNow)
            };

            foreach (var metric in metricPoints)
            {
                await context.RunRepository.AppendMetricAsync(context.RunId, metric, ct);
                await context.ArtifactStore.AppendLineAsync(
                    context.RunId,
                    "metrics.jsonl",
                    JsonSerializer.Serialize(new DetectionMetricStreamEntry(metric.Name, metric.Step, metric.Value)),
                    ct);
            }

            await context.ArtifactStore.WriteTextAsync(
                context.RunId,
                $"reports/{stage}.json",
                JsonSerializer.Serialize(new DetectionEvalReport(
                    "detection",
                    stage,
                    "TorchSharp",
                    context.Config.Device.ToString(),
                    sampleCount,
                    inputSize,
                    DetectionPreprocessingSnapshot.FromTensorOptions(tensorOptions),
                    DetectionTargetEncodingSnapshot.FromTopK(tensorOptions.TargetTopK),
                    "approx-iou-pr",
                    evalMetrics,
                    evalResult.SampleDetails,
                    "torchsharp-eval-complete")),
                ct);

            return new RunResult(context.RunId, RunStatus.Succeeded, $"Detection {stage} 完成。", context.RunDirectory);
        }
        catch (DllNotFoundException ex)
        {
            throw new InvalidOperationException("TorchSharp 运行时不可用，请安装对应 CPU/CUDA 运行时依赖。", ex);
        }
    }

    private static async Task<RunResult> ExecuteNonTorchSharpStubAsync(TrainingExecutionContext context, CancellationToken ct)
    {
        var sampleCount = context.Config.Mode switch
        {
            RunMode.Train => context.Data.Train.Count,
            RunMode.Validate => context.Data.Validation.Count,
            RunMode.Test => context.Data.Test.Count,
            _ => 0
        };

        await context.RunRepository.AppendEventAsync(
            context.RunId,
            new RunEvent("Warning", "BackendFallback", $"当前后端 {context.Config.Backend} 未实现检测真实训练，执行占位流程。", DateTimeOffset.UtcNow),
            ct);
        await context.RunRepository.AppendMetricAsync(context.RunId, new MetricPoint("loss", 1, 1.0, DateTimeOffset.UtcNow), ct);
        await context.ArtifactStore.AppendLineAsync(context.RunId, "metrics.jsonl", JsonSerializer.Serialize(new { metric = "loss", step = 1, value = 1.0 }), ct);
        await context.ArtifactStore.WriteTextAsync(
            context.RunId,
            context.Config.Mode == RunMode.Train ? "reports/summary.json" : $"reports/{context.Config.Mode.ToString().ToLowerInvariant()}.json",
            JsonSerializer.Serialize(new
            {
                task = "detection",
                mode = context.Config.Mode.ToString(),
                backend = context.Config.Backend.ToString(),
                sampleCount,
                status = "non-torchsharp-stub"
            }),
            ct);
        return new RunResult(context.RunId, RunStatus.Succeeded, "非 TorchSharp 检测占位流程完成。", context.RunDirectory);
    }

    private static Module<Tensor, Tensor> CreateDetectorModel(int targetTopK) => new TinyMultiHeadDetectorModel(Math.Max(1, targetTopK));

    private static Device ResolveTorchDevice(CoreDeviceType configuredDevice)
    {
        if (configuredDevice == CoreDeviceType.Cuda)
        {
            if (!cuda.is_available())
            {
                throw new InvalidOperationException("配置要求 CUDA，但 TorchSharp 未检测到可用 CUDA。");
            }

            return CUDA;
        }

        if (configuredDevice == CoreDeviceType.Auto && cuda.is_available())
        {
            return CUDA;
        }

        return CPU;
    }

    private static DetectionTensorOptions ResolveTensorOptions(TrainingExecutionContext context)
    {
        var inputSize = 640;
        if (context.Config.Model.Parameters.TryGetValue("inputSize", out var modelInputSize) &&
            modelInputSize.ValueKind == JsonValueKind.Number &&
            modelInputSize.TryGetInt32(out var modelSize) &&
            modelSize > 0)
        {
            inputSize = modelSize;
        }
        else if (context.Config.TaskOptions.TryGetValue("inputSize", out var taskInputSize) &&
                 taskInputSize.ValueKind == JsonValueKind.Number &&
                 taskInputSize.TryGetInt32(out var taskSize) &&
                 taskSize > 0)
        {
            inputSize = taskSize;
        }

        var mean = ReadFloatArray(context, "normalizeMean", [0f, 0f, 0f]);
        var std = ReadFloatArray(context, "normalizeStd", [1f, 1f, 1f]);
        for (var i = 0; i < std.Length; i++)
        {
            if (std[i] <= 0f)
            {
                std[i] = 1f;
            }
        }

        var (resampler, resamplerName) = ResolveResampler(context);
        return new DetectionTensorOptions(
            inputSize,
            mean,
            std,
            resampler,
            resamplerName,
            ResolveTargetBoxStrategy(context),
            ResolveTargetTopK(context));
    }

    private static Tensor BuildInputTensor(
        IReadOnlyList<DataSample> samples,
        int batchSize,
        int globalStep,
        DetectionTensorOptions options,
        Device device,
        bool allowFallbackOnImageError,
        bool isTraining)
    {
        var inputSize = options.InputSize;
        var chw = 3 * inputSize * inputSize;
        var data = new float[batchSize * chw];

        for (var i = 0; i < batchSize; i++)
        {
            var sample = PickSample(samples, globalStep + i);
            var offset = i * chw;
            try
            {
                var imageTensor = BuildImageTensor(sample, options);
                Buffer.BlockCopy(imageTensor, 0, data, offset * sizeof(float), chw * sizeof(float));
            }
            catch (Exception) when (allowFallbackOnImageError || !isTraining)
            {
                // 中文说明：图像解码失败时允许回退为零张量，保证流程可继续。
                Array.Clear(data, offset, chw);
            }
            catch (Exception)
            {
                if (isTraining)
                {
                    throw;
                }

                Array.Clear(data, offset, chw);
            }
        }

        return tensor(data, [batchSize, 3, inputSize, inputSize], dtype: ScalarType.Float32, device: device);
    }

    private static float[] BuildImageTensor(DataSample sample, DetectionTensorOptions options)
    {
        var inputSize = options.InputSize;
        if (string.IsNullOrWhiteSpace(sample.SourcePath) || !File.Exists(sample.SourcePath))
        {
            throw new FileNotFoundException("训练图像不存在。", sample.SourcePath);
        }

        using var image = Image.Load<Rgb24>(sample.SourcePath);
        image.Mutate(ctx => ctx.Resize(new ResizeOptions
        {
            Size = new SixLabors.ImageSharp.Size(inputSize, inputSize),
            Sampler = options.Resampler
        }));

        var data = new float[3 * inputSize * inputSize];
        for (var y = 0; y < inputSize; y++)
        {
            for (var x = 0; x < inputSize; x++)
            {
                var pixel = image[x, y];
                var baseIndex = y * inputSize + x;
                data[baseIndex] = NormalizeChannel(pixel.R / 255f, options.NormalizeMean[0], options.NormalizeStd[0]);
                data[(inputSize * inputSize) + baseIndex] = NormalizeChannel(pixel.G / 255f, options.NormalizeMean[1], options.NormalizeStd[1]);
                data[(2 * inputSize * inputSize) + baseIndex] = NormalizeChannel(pixel.B / 255f, options.NormalizeMean[2], options.NormalizeStd[2]);
            }
        }

        return data;
    }

    private static Tensor BuildTargetTensor(
        IReadOnlyList<DataSample> samples,
        int batchSize,
        int globalStep,
        DetectionTensorOptions options,
        Device device)
    {
        var targetDim = options.TargetTopK * DetectionTargetBoxValueCount;
        var data = new float[batchSize * targetDim];
        for (var i = 0; i < batchSize; i++)
        {
            var sample = PickSample(samples, globalStep + i);
            var target = BuildTargetVector(sample, options);
            Buffer.BlockCopy(target, 0, data, i * targetDim * sizeof(float), targetDim * sizeof(float));
        }

        return tensor(data, [batchSize, targetDim], dtype: ScalarType.Float32, device: device);
    }

    private static DataSample PickSample(IReadOnlyList<DataSample> samples, int index)
    {
        if (samples.Count == 0)
        {
            return new DataSample(
                "det-empty",
                "empty",
                null,
                "train",
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["coco.image_width"] = "640",
                    ["coco.image_height"] = "640",
                    ["coco.annotations_json"] = "[]"
                });
        }

        return samples[Math.Abs(index) % samples.Count];
    }

    private static float[] BuildTargetVector(DataSample sample, DetectionTensorOptions options)
    {
        var boxes = ReadCocoTargetBoxes(sample, options.TargetBoxStrategy, options.TargetTopK);
        var data = new float[options.TargetTopK * DetectionTargetBoxValueCount];
        for (var i = 0; i < boxes.Count && i < options.TargetTopK; i++)
        {
            var offset = i * DetectionTargetBoxValueCount;
            var box = boxes[i];
            data[offset] = box.Cx;
            data[offset + 1] = box.Cy;
            data[offset + 2] = box.Bw;
            data[offset + 3] = box.Bh;
            data[offset + 4] = box.CategoryId;
            data[offset + 5] = box.Objectness;
        }

        return data;
    }

    private static (float width, float height, float annCount, float categoryId, float cx, float cy, float bw, float bh) ReadCocoMetadata(
        DataSample sample,
        string targetBoxStrategy)
    {
        var width = ReadMetadataFloat(sample, "coco.image_width", 640f);
        var height = ReadMetadataFloat(sample, "coco.image_height", 640f);
        var annCount = ReadMetadataFloat(sample, "coco.annotation_count", 0f);
        var categoryId = 0f;
        var cx = 0f;
        var cy = 0f;
        var bw = 0f;
        var bh = 0f;

        if (sample.Metadata is not null &&
            sample.Metadata.TryGetValue("coco.annotations_json", out var annotationsJson) &&
            !string.IsNullOrWhiteSpace(annotationsJson))
        {
            using var doc = JsonDocument.Parse(annotationsJson);
            if (doc.RootElement.ValueKind == JsonValueKind.Array && doc.RootElement.GetArrayLength() > 0)
            {
                var ann = SelectAnnotation(doc.RootElement, targetBoxStrategy);
                if (ann.TryGetProperty("CategoryId", out var categoryElement) && categoryElement.ValueKind == JsonValueKind.Number)
                {
                    categoryId = categoryElement.GetSingle();
                }
                else if (ann.TryGetProperty("categoryId", out var categoryElementCamel) && categoryElementCamel.ValueKind == JsonValueKind.Number)
                {
                    categoryId = categoryElementCamel.GetSingle();
                }

                if (ann.TryGetProperty("Bbox", out var bboxElement) || ann.TryGetProperty("bbox", out bboxElement))
                {
                    var bbox = bboxElement.EnumerateArray().Select(x => x.GetSingle()).ToArray();
                    if (bbox.Length >= 4)
                    {
                        var x = bbox[0];
                        var y = bbox[1];
                        bw = width > 0 ? bbox[2] / width : 0f;
                        bh = height > 0 ? bbox[3] / height : 0f;
                        cx = width > 0 ? (x + (bbox[2] / 2f)) / width : 0f;
                        cy = height > 0 ? (y + (bbox[3] / 2f)) / height : 0f;
                    }
                }
            }
        }

        return (width, height, annCount, categoryId, Clamp01(cx), Clamp01(cy), Clamp01(bw), Clamp01(bh));
    }

    private static JsonElement SelectAnnotation(JsonElement annotations, string strategy)
    {
        if (annotations.GetArrayLength() == 1 || strategy.Equals("first", StringComparison.OrdinalIgnoreCase))
        {
            return annotations[0];
        }

        if (strategy.Equals("largest", StringComparison.OrdinalIgnoreCase))
        {
            var bestIndex = 0;
            var bestArea = -1f;
            for (var i = 0; i < annotations.GetArrayLength(); i++)
            {
                var area = TryReadBboxArea(annotations[i]);
                if (area > bestArea)
                {
                    bestArea = area;
                    bestIndex = i;
                }
            }

            return annotations[bestIndex];
        }

        if (strategy.Equals("average", StringComparison.OrdinalIgnoreCase))
        {
            return BuildAverageAnnotation(annotations);
        }

        return annotations[0];
    }

    private static JsonElement BuildAverageAnnotation(JsonElement annotations)
    {
        float sumX = 0;
        float sumY = 0;
        float sumW = 0;
        float sumH = 0;
        var count = 0;
        float categoryId = 0;

        for (var i = 0; i < annotations.GetArrayLength(); i++)
        {
            var ann = annotations[i];
            if (count == 0)
            {
                if (ann.TryGetProperty("CategoryId", out var categoryElement) && categoryElement.ValueKind == JsonValueKind.Number)
                {
                    categoryId = categoryElement.GetSingle();
                }
                else if (ann.TryGetProperty("categoryId", out var categoryElementCamel) && categoryElementCamel.ValueKind == JsonValueKind.Number)
                {
                    categoryId = categoryElementCamel.GetSingle();
                }
            }

            if (!TryReadBbox(ann, out var bbox))
            {
                continue;
            }

            sumX += bbox[0];
            sumY += bbox[1];
            sumW += bbox[2];
            sumH += bbox[3];
            count++;
        }

        if (count == 0)
        {
            return annotations[0];
        }

        using var doc = JsonDocument.Parse(
            $$"""
            {"categoryId":{{categoryId.ToString(CultureInfo.InvariantCulture)}},"bbox":[{{(sumX / count).ToString(CultureInfo.InvariantCulture)}},{{(sumY / count).ToString(CultureInfo.InvariantCulture)}},{{(sumW / count).ToString(CultureInfo.InvariantCulture)}},{{(sumH / count).ToString(CultureInfo.InvariantCulture)}}]}
            """);
        return doc.RootElement.Clone();
    }

    private static bool TryReadBbox(JsonElement ann, out float[] bbox)
    {
        if (ann.TryGetProperty("Bbox", out var bboxElement) || ann.TryGetProperty("bbox", out bboxElement))
        {
            var values = bboxElement.EnumerateArray().Select(x => x.GetSingle()).ToArray();
            if (values.Length >= 4)
            {
                bbox = [values[0], values[1], values[2], values[3]];
                return true;
            }
        }

        bbox = [];
        return false;
    }

    private static float TryReadBboxArea(JsonElement ann)
    {
        if (!TryReadBbox(ann, out var bbox))
        {
            return -1f;
        }

        return Math.Max(0f, bbox[2]) * Math.Max(0f, bbox[3]);
    }

    private static ApproxDetectionEvalComputationResult ComputeApproxEvalMetrics(
        Tensor prediction,
        Tensor target,
        IReadOnlyList<DataSample> samples,
        int sampleCount,
        int targetTopK)
    {
        using var predictionCpu = prediction.detach().to(CPU);
        using var targetCpu = target.detach().to(CPU);

        var predictionValues = predictionCpu.data<float>().ToArray();
        var targetValues = targetCpu.data<float>().ToArray();
        var boxStride = DetectionTargetBoxValueCount;
        var sampleStride = targetTopK * boxStride;

        double totalMatchedIou = 0;
        var matchedCount = 0;
        var truePositive = 0;
        var falsePositive = 0;
        var falseNegative = 0;
        var sampleDetails = new List<ApproxDetectionEvalSampleDetail>(sampleCount);

        for (var sampleIndex = 0; sampleIndex < sampleCount; sampleIndex++)
        {
            var baseOffset = sampleIndex * sampleStride;
            var predictedBoxes = new List<DetectionTargetBox>(targetTopK);
            var targetBoxes = new List<DetectionTargetBox>(targetTopK);

            for (var boxIndex = 0; boxIndex < targetTopK; boxIndex++)
            {
                var offset = baseOffset + (boxIndex * boxStride);
                predictedBoxes.Add(ReadDetectionTargetBox(predictionValues, offset, normalizePrediction: true));
                targetBoxes.Add(ReadDetectionTargetBox(targetValues, offset, normalizePrediction: false));
            }

            var predictedPositive = predictedBoxes.Where(x => x.Objectness >= 0.5f).ToList();
            var targetPositive = targetBoxes.Where(x => x.Objectness >= 0.5f).ToList();
            var targetMatched = new bool[targetPositive.Count];
            double sampleMatchedIou = 0d;
            var sampleMatchedCount = 0;
            var sampleTruePositive = 0;
            var sampleFalsePositive = 0;

            foreach (var predicted in predictedPositive)
            {
                var bestIndex = -1;
                var bestIou = 0f;
                for (var targetIndex = 0; targetIndex < targetPositive.Count; targetIndex++)
                {
                    if (targetMatched[targetIndex])
                    {
                        continue;
                    }

                    var iou = ComputeIou(predicted, targetPositive[targetIndex]);
                    if (iou > bestIou)
                    {
                        bestIou = iou;
                        bestIndex = targetIndex;
                    }
                }

                if (bestIndex >= 0 && bestIou >= EvalIouThreshold)
                {
                    targetMatched[bestIndex] = true;
                    truePositive++;
                    sampleTruePositive++;
                    totalMatchedIou += bestIou;
                    sampleMatchedIou += bestIou;
                    matchedCount++;
                    sampleMatchedCount++;
                }
                else
                {
                    falsePositive++;
                    sampleFalsePositive++;
                }
            }

            var sampleFalseNegative = 0;
            for (var targetIndex = 0; targetIndex < targetMatched.Length; targetIndex++)
            {
                if (!targetMatched[targetIndex])
                {
                    falseNegative++;
                    sampleFalseNegative++;
                }
            }

            var selectedSample = PickSample(samples, sampleIndex + 1);
            sampleDetails.Add(new ApproxDetectionEvalSampleDetail(
                sampleIndex,
                selectedSample.Id,
                selectedSample.SourcePath,
                predictedPositive.Count,
                targetPositive.Count,
                sampleTruePositive,
                sampleFalsePositive,
                sampleFalseNegative,
                sampleMatchedCount > 0 ? sampleMatchedIou / sampleMatchedCount : 0d));
        }

        var precision = truePositive + falsePositive > 0
            ? truePositive / (double)(truePositive + falsePositive)
            : 0d;
        var recall = truePositive + falseNegative > 0
            ? truePositive / (double)(truePositive + falseNegative)
            : 0d;
        var meanIou = matchedCount > 0
            ? totalMatchedIou / matchedCount
            : 0d;

        var metrics = new ApproxDetectionEvalMetrics(
            MeanIou: meanIou,
            PrecisionAtIou50: precision,
            RecallAtIou50: recall,
            TruePositive: truePositive,
            FalsePositive: falsePositive,
            FalseNegative: falseNegative,
            IouThreshold: EvalIouThreshold);
        return new ApproxDetectionEvalComputationResult(metrics, sampleDetails);
    }

    private static DetectionTrainLossBreakdown ComputeDetectionTrainLoss(Tensor prediction, Tensor target, int batchSize, int targetTopK)
    {
        using var predictionBoxes = prediction.reshape([batchSize, targetTopK, DetectionTargetBoxValueCount]);
        using var targetBoxes = target.reshape([batchSize, targetTopK, DetectionTargetBoxValueCount]);

        using var bboxPrediction = predictionBoxes.narrow(2, 0, 4);
        using var bboxTarget = targetBoxes.narrow(2, 0, 4);
        using var categoryPrediction = predictionBoxes.narrow(2, 4, 1);
        using var categoryTarget = targetBoxes.narrow(2, 4, 1);
        using var objectnessPrediction = predictionBoxes.narrow(2, 5, 1);
        using var objectnessTarget = targetBoxes.narrow(2, 5, 1);

        var bboxLoss = TorchSharp.torch.nn.functional.mse_loss(bboxPrediction, bboxTarget);
        var categoryLoss = TorchSharp.torch.nn.functional.mse_loss(categoryPrediction, categoryTarget);
        var objectnessLoss = TorchSharp.torch.nn.functional.binary_cross_entropy(objectnessPrediction, objectnessTarget);
        var totalLoss =
            (bboxLoss * DetectionBboxLossWeight) +
            (categoryLoss * DetectionCategoryLossWeight) +
            (objectnessLoss * DetectionObjectnessLossWeight);

        return new DetectionTrainLossBreakdown(totalLoss, bboxLoss, categoryLoss, objectnessLoss);
    }

    private static DetectionTargetBox ReadDetectionTargetBox(float[] values, int offset, bool normalizePrediction)
    {
        var cx = values[offset];
        var cy = values[offset + 1];
        var bw = values[offset + 2];
        var bh = values[offset + 3];
        var categoryId = values[offset + 4];
        var objectness = values[offset + 5];

        if (normalizePrediction)
        {
            cx = Clamp01(cx);
            cy = Clamp01(cy);
            bw = Clamp01(Math.Abs(bw));
            bh = Clamp01(Math.Abs(bh));
            categoryId = Math.Max(0f, categoryId);
            objectness = Clamp01(objectness);
        }

        return new DetectionTargetBox(cx, cy, bw, bh, categoryId, objectness);
    }

    private static float ComputeIou(DetectionTargetBox a, DetectionTargetBox b)
    {
        var aX1 = a.Cx - (a.Bw / 2f);
        var aY1 = a.Cy - (a.Bh / 2f);
        var aX2 = a.Cx + (a.Bw / 2f);
        var aY2 = a.Cy + (a.Bh / 2f);

        var bX1 = b.Cx - (b.Bw / 2f);
        var bY1 = b.Cy - (b.Bh / 2f);
        var bX2 = b.Cx + (b.Bw / 2f);
        var bY2 = b.Cy + (b.Bh / 2f);

        var interX1 = Math.Max(aX1, bX1);
        var interY1 = Math.Max(aY1, bY1);
        var interX2 = Math.Min(aX2, bX2);
        var interY2 = Math.Min(aY2, bY2);

        var interW = Math.Max(0f, interX2 - interX1);
        var interH = Math.Max(0f, interY2 - interY1);
        var intersection = interW * interH;

        var areaA = Math.Max(0f, a.Bw) * Math.Max(0f, a.Bh);
        var areaB = Math.Max(0f, b.Bw) * Math.Max(0f, b.Bh);
        var union = areaA + areaB - intersection;
        if (union <= 0f)
        {
            return 0f;
        }

        return Clamp01(intersection / union);
    }

    private static List<DetectionTargetBox> ReadCocoTargetBoxes(DataSample sample, string strategy, int targetTopK)
    {
        var width = ReadMetadataFloat(sample, "coco.image_width", 640f);
        var height = ReadMetadataFloat(sample, "coco.image_height", 640f);

        if (sample.Metadata is null ||
            !sample.Metadata.TryGetValue("coco.annotations_json", out var annotationsJson) ||
            string.IsNullOrWhiteSpace(annotationsJson))
        {
            return [];
        }

        using var doc = JsonDocument.Parse(annotationsJson);
        if (doc.RootElement.ValueKind != JsonValueKind.Array || doc.RootElement.GetArrayLength() == 0)
        {
            return [];
        }

        if (strategy.Equals("average", StringComparison.OrdinalIgnoreCase))
        {
            var averaged = BuildAverageAnnotation(doc.RootElement);
            return [ToDetectionTargetBox(averaged, width, height)];
        }

        var boxes = new List<DetectionTargetBox>();
        foreach (var ann in doc.RootElement.EnumerateArray())
        {
            if (!TryReadBbox(ann, out _))
            {
                continue;
            }

            boxes.Add(ToDetectionTargetBox(ann, width, height));
        }

        if (strategy.Equals("largest", StringComparison.OrdinalIgnoreCase))
        {
            boxes = boxes
                .OrderByDescending(x => x.Bw * x.Bh)
                .ToList();
        }

        return boxes.Take(Math.Max(1, targetTopK)).ToList();
    }

    private static DetectionTargetBox ToDetectionTargetBox(JsonElement ann, float width, float height)
    {
        var categoryId = 0f;
        if (ann.TryGetProperty("CategoryId", out var categoryElement) && categoryElement.ValueKind == JsonValueKind.Number)
        {
            categoryId = categoryElement.GetSingle();
        }
        else if (ann.TryGetProperty("categoryId", out var categoryElementCamel) && categoryElementCamel.ValueKind == JsonValueKind.Number)
        {
            categoryId = categoryElementCamel.GetSingle();
        }

        float cx = 0f;
        float cy = 0f;
        float bw = 0f;
        float bh = 0f;
        if (TryReadBbox(ann, out var bbox))
        {
            var x = bbox[0];
            var y = bbox[1];
            bw = width > 0 ? bbox[2] / width : 0f;
            bh = height > 0 ? bbox[3] / height : 0f;
            cx = width > 0 ? (x + (bbox[2] / 2f)) / width : 0f;
            cy = height > 0 ? (y + (bbox[3] / 2f)) / height : 0f;
        }

        return new DetectionTargetBox(
            Clamp01(cx),
            Clamp01(cy),
            Clamp01(bw),
            Clamp01(bh),
            Math.Max(0f, categoryId),
            1f);
    }

    private static float ReadMetadataFloat(DataSample sample, string key, float defaultValue)
    {
        if (sample.Metadata is null || !sample.Metadata.TryGetValue(key, out var text))
        {
            return defaultValue;
        }

        return float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
            ? value
            : defaultValue;
    }

    private static float[] ReadFloatArray(TrainingExecutionContext context, string key, float[] defaultValue)
    {
        if (!context.Config.TaskOptions.TryGetValue(key, out var element) || element.ValueKind != JsonValueKind.Array)
        {
            return defaultValue;
        }

        var values = element.EnumerateArray()
            .Where(x => x.ValueKind == JsonValueKind.Number)
            .Select(x => x.GetSingle())
            .Take(3)
            .ToArray();

        return values.Length == 3 ? values : defaultValue;
    }

    private static (IResampler Resampler, string Name) ResolveResampler(TrainingExecutionContext context)
    {
        if (!context.Config.TaskOptions.TryGetValue("resizeSampler", out var element) || element.ValueKind != JsonValueKind.String)
        {
            return (KnownResamplers.Bicubic, "bicubic");
        }

        var name = element.GetString() ?? string.Empty;
        return name.Trim().ToLowerInvariant() switch
        {
            "nearest" => (KnownResamplers.NearestNeighbor, "nearest"),
            "bilinear" => (KnownResamplers.Triangle, "bilinear"),
            "triangle" => (KnownResamplers.Triangle, "triangle"),
            "bicubic" => (KnownResamplers.Bicubic, "bicubic"),
            "lanczos3" => (KnownResamplers.Lanczos3, "lanczos3"),
            _ => (KnownResamplers.Bicubic, "bicubic")
        };
    }

    private static string ResolveTargetBoxStrategy(TrainingExecutionContext context)
    {
        if (!context.Config.TaskOptions.TryGetValue("targetBoxStrategy", out var element) || element.ValueKind != JsonValueKind.String)
        {
            return "largest";
        }

        var value = (element.GetString() ?? string.Empty).Trim().ToLowerInvariant();
        return value is "first" or "largest" or "average" ? value : "largest";
    }

    private static int ResolveTargetTopK(TrainingExecutionContext context)
    {
        if (context.Config.TaskOptions.TryGetValue("targetTopK", out var element) &&
            element.ValueKind == JsonValueKind.Number &&
            element.TryGetInt32(out var value) &&
            value > 0)
        {
            return value;
        }

        return 8;
    }

    private static float NormalizeChannel(float value, float mean, float std) => (value - mean) / (std <= 0f ? 1f : std);

    private static float Clamp01(float value) => Math.Clamp(value, 0f, 1f);

    private static async Task WritePreprocessingTagsAsync(
        TrainingExecutionContext context,
        DetectionTensorOptions options,
        CancellationToken ct)
    {
        await context.RunRepository.UpsertTagAsync(context.RunId, "det.preprocess.input_size", options.InputSize.ToString(CultureInfo.InvariantCulture), ct);
        await context.RunRepository.UpsertTagAsync(context.RunId, "det.preprocess.resize_sampler", options.ResamplerName, ct);
        await context.RunRepository.UpsertTagAsync(context.RunId, "det.preprocess.target_box_strategy", options.TargetBoxStrategy, ct);
        await context.RunRepository.UpsertTagAsync(context.RunId, "det.preprocess.normalize_mean", string.Join(",", options.NormalizeMean.Select(x => x.ToString("0.######", CultureInfo.InvariantCulture))), ct);
        await context.RunRepository.UpsertTagAsync(context.RunId, "det.preprocess.normalize_std", string.Join(",", options.NormalizeStd.Select(x => x.ToString("0.######", CultureInfo.InvariantCulture))), ct);
        await context.RunRepository.UpsertTagAsync(context.RunId, "det.target.top_k", options.TargetTopK.ToString(CultureInfo.InvariantCulture), ct);
    }

    private static Task AppendPreprocessingEventAsync(
        TrainingExecutionContext context,
        DetectionTensorOptions options,
        CancellationToken ct)
    {
        var message = JsonSerializer.Serialize(new
        {
            inputSize = options.InputSize,
            normalizeMean = options.NormalizeMean,
            normalizeStd = options.NormalizeStd,
            resizeSampler = options.ResamplerName,
            targetBoxStrategy = options.TargetBoxStrategy,
            targetTopK = options.TargetTopK
        });

        return context.RunRepository.AppendEventAsync(
            context.RunId,
            new RunEvent("Information", "PreprocessingConfig", message, DateTimeOffset.UtcNow),
            ct);
    }

    private sealed record DetectionTensorOptions(
        int InputSize,
        float[] NormalizeMean,
        float[] NormalizeStd,
        IResampler Resampler,
        string ResamplerName,
        string TargetBoxStrategy,
        int TargetTopK);

    private sealed record DetectionTargetBox(
        float Cx,
        float Cy,
        float Bw,
        float Bh,
        float CategoryId,
        float Objectness);

    private sealed record DetectionMetricStreamEntry(string Metric, long Step, double Value);

    private sealed record DetectionTrainMetricStreamEntry(
        long Step,
        int Epoch,
        double Loss,
        double BboxLoss,
        double CategoryLoss,
        double ObjectnessLoss);

    private sealed record DetectionLossWeightsSnapshot(
        double Bbox,
        double Category,
        double Objectness);

    private sealed record DetectionTrainLossSummarySnapshot(
        double AverageTotalLoss,
        double AverageBboxLoss,
        double AverageCategoryLoss,
        double AverageObjectnessLoss,
        int TrainStepCount);

    private sealed record DetectionTrainModelMetadataReport(
        string Framework,
        string Task,
        string Device,
        string Model,
        int InputSize,
        DetectionPreprocessingSnapshot Preprocessing,
        DetectionTargetEncodingSnapshot TargetEncoding,
        DetectionLossWeightsSnapshot LossWeights,
        string HeadType,
        string Status);

    private sealed record DetectionTrainSummaryReport(
        string Task,
        string Mode,
        string Backend,
        string Device,
        int SampleCount,
        int Epochs,
        int StepsPerEpoch,
        int InputSize,
        DetectionPreprocessingSnapshot Preprocessing,
        DetectionTargetEncodingSnapshot TargetEncoding,
        DetectionLossWeightsSnapshot LossWeights,
        DetectionTrainLossSummarySnapshot LossSummary,
        string HeadType,
        string Status);

    private sealed record DetectionPreprocessingSnapshot(
        int InputSize,
        float[] NormalizeMean,
        float[] NormalizeStd,
        string ResizeSampler,
        string TargetBoxStrategy)
    {
        public static DetectionPreprocessingSnapshot FromTensorOptions(DetectionTensorOptions options) =>
            new(
                options.InputSize,
                options.NormalizeMean,
                options.NormalizeStd,
                options.ResamplerName,
                options.TargetBoxStrategy);
    }

    private sealed record DetectionTargetEncodingSnapshot(
        int TopK,
        int ValuesPerBox,
        int FlattenedSize)
    {
        public static DetectionTargetEncodingSnapshot FromTopK(int topK) =>
            new(
                topK,
                DetectionTargetBoxValueCount,
                topK * DetectionTargetBoxValueCount);
    }

    private sealed record ApproxDetectionEvalMetrics(
        double MeanIou,
        double PrecisionAtIou50,
        double RecallAtIou50,
        int TruePositive,
        int FalsePositive,
        int FalseNegative,
        float IouThreshold);

    private sealed record ApproxDetectionEvalSampleDetail(
        int SampleIndex,
        string SampleId,
        string SourcePath,
        int PredictedPositiveCount,
        int TargetPositiveCount,
        int TruePositive,
        int FalsePositive,
        int FalseNegative,
        double MeanMatchedIou);

    private sealed record ApproxDetectionEvalComputationResult(
        ApproxDetectionEvalMetrics Metrics,
        IReadOnlyList<ApproxDetectionEvalSampleDetail> SampleDetails);

    private sealed record DetectionTrainLossBreakdown(
        Tensor TotalLoss,
        Tensor BboxLoss,
        Tensor CategoryLoss,
        Tensor ObjectnessLoss) : IDisposable
    {
        public void Dispose()
        {
            TotalLoss.Dispose();
            BboxLoss.Dispose();
            CategoryLoss.Dispose();
            ObjectnessLoss.Dispose();
        }
    }

    private sealed record DetectionEvalReport(
        string Task,
        string Mode,
        string Backend,
        string Device,
        int SampleCount,
        int InputSize,
        DetectionPreprocessingSnapshot Preprocessing,
        DetectionTargetEncodingSnapshot TargetEncoding,
        string MetricType,
        ApproxDetectionEvalMetrics Metrics,
        IReadOnlyList<ApproxDetectionEvalSampleDetail> SampleDetails,
        string Status);

    private sealed class TinyMultiHeadDetectorModel : Module<Tensor, Tensor>
    {
        private readonly int _targetTopK;
        private readonly Module<Tensor, Tensor> _backbone;
        private readonly Module<Tensor, Tensor> _sharedProjection;
        private readonly Module<Tensor, Tensor> _bboxHead;
        private readonly Module<Tensor, Tensor> _categoryHead;
        private readonly Module<Tensor, Tensor> _objectnessHead;

        public TinyMultiHeadDetectorModel(int targetTopK)
            : base(nameof(TinyMultiHeadDetectorModel))
        {
            _targetTopK = Math.Max(1, targetTopK);
            _backbone = Sequential(
                ("conv1", Conv2d(3, 16, 3, stride: 1, padding: 1)),
                ("relu1", ReLU()),
                ("pool1", MaxPool2d(2)),
                ("conv2", Conv2d(16, 32, 3, stride: 1, padding: 1)),
                ("relu2", ReLU()),
                ("pool2", MaxPool2d(2)),
                ("conv3", Conv2d(32, 32, 3, stride: 1, padding: 1)),
                ("relu3", ReLU()),
                ("pool3", AdaptiveAvgPool2d([1, 1])),
                ("flat", Flatten()));

            _sharedProjection = Sequential(
                ("fc1", Linear(32, 64)),
                ("relu1", ReLU()),
                ("drop1", Dropout(0.1)),
                ("fc2", Linear(64, 64)),
                ("relu2", ReLU()));

            _bboxHead = Linear(64, _targetTopK * 4);
            _categoryHead = Linear(64, _targetTopK);
            _objectnessHead = Linear(64, _targetTopK);

            RegisterComponents();
        }

        public override Tensor forward(Tensor input)
        {
            using var backboneFeatures = _backbone.forward(input);
            using var sharedFeatures = _sharedProjection.forward(backboneFeatures);
            using var bboxRaw = _bboxHead.forward(sharedFeatures);
            using var categoryRaw = _categoryHead.forward(sharedFeatures);
            using var objectnessRaw = _objectnessHead.forward(sharedFeatures);

            var batchSize = input.shape[0];
            using var bbox = torch.sigmoid(bboxRaw).reshape([batchSize, _targetTopK, 4]);
            using var category = TorchSharp.torch.nn.functional.relu(categoryRaw).reshape([batchSize, _targetTopK, 1]);
            using var objectness = torch.sigmoid(objectnessRaw).reshape([batchSize, _targetTopK, 1]);
            using var merged = torch.cat(new[] { bbox, category, objectness }, dim: 2);
            return merged.flatten(1);
        }
    }
}
