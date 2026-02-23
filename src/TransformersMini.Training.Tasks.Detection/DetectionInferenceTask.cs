using System.Globalization;
using System.Text.Json;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Processing.Processors.Transforms;
using TorchSharp;
using TransformersMini.Contracts.Abstractions;
using TransformersMini.Contracts.Data;
using TransformersMini.Contracts.ModelMetadata;
using TransformersMini.Contracts.Runtime;
using TransformersMini.SharedKernel.Core;
using static TorchSharp.torch;
using static TorchSharp.torch.nn;
using CoreDeviceType = TransformersMini.SharedKernel.Core.DeviceType;

namespace TransformersMini.Training.Tasks.Detection;

/// <summary>
/// 检测批量推理任务实现。
/// 逐样本推理，输出框/分数/类别，落盘 inference.json 与 inference-samples.jsonl。
/// </summary>
public sealed class DetectionInferenceTask : IInferenceTask
{
    private const int DetectionTargetBoxValueCount = 6;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public TaskType TaskType => TaskType.Detection;

    public async Task<RunResult> ExecuteAsync(InferenceExecutionContext context, CancellationToken ct)
    {
        await context.RunRepository.AppendEventAsync(
            context.RunId,
            new RunEvent("Information", "DetectionInferenceStart", "检测批量推理开始。", DateTimeOffset.UtcNow),
            ct);

        try
        {
            var modelMetadata = LoadModelMetadata(context.ModelRunDirectory);
            var torchDevice = ResolveTorchDevice(context.Config.Device);
            var inputSize = modelMetadata?.InputSize ?? 640;
            var topK = modelMetadata?.TargetEncoding?.TopK ?? 8;
            var normalizeMean = modelMetadata?.Preprocessing?.NormalizeMean ?? [0f, 0f, 0f];
            var normalizeStd = modelMetadata?.Preprocessing?.NormalizeStd ?? [1f, 1f, 1f];
            var resamplerName = modelMetadata?.Preprocessing?.ResizeSampler ?? "bicubic";
            var resampler = ResolveResampler(resamplerName);

            using var model = CreateDetectorModel(topK).to(torchDevice);
            var modelWeightsPath = Path.Combine(context.ModelRunDirectory, "artifacts", "model-weights.bin");
            if (File.Exists(modelWeightsPath))
            {
                if (TryLoadModelWeights(model, modelWeightsPath))
                {
                    await context.RunRepository.AppendEventAsync(
                        context.RunId,
                        new RunEvent("Information", "ModelWeightsLoaded", $"已加载模型权重：{modelWeightsPath}", DateTimeOffset.UtcNow),
                        ct);
                }
                else
                {
                    await context.RunRepository.AppendEventAsync(
                        context.RunId,
                        new RunEvent("Warning", "ModelWeightsLoadSkipped", $"检测到权重文件但加载失败，继续使用默认权重：{modelWeightsPath}", DateTimeOffset.UtcNow),
                        ct);
                }
            }
            else
            {
                await context.RunRepository.AppendEventAsync(
                    context.RunId,
                    new RunEvent("Warning", "ModelWeightsMissing", $"未找到模型权重文件，继续使用默认权重：{modelWeightsPath}", DateTimeOffset.UtcNow),
                    ct);
            }
            model.eval();

            // 优先使用 test split，fallback 到 val，再到 train
            var samples = context.Data.Test.Count > 0
                ? context.Data.Test
                : context.Data.Validation.Count > 0
                    ? context.Data.Validation
                    : context.Data.Train;

            if (context.MaxSamples > 0 && samples.Count > context.MaxSamples)
            {
                samples = samples.Take(context.MaxSamples).ToList();
            }

            var sampleCount = samples.Count;
            var sampleResults = new List<DetectionInferenceSampleResult>(sampleCount);
            var totalBoxCount = 0;

            for (var i = 0; i < sampleCount; i++)
            {
                ct.ThrowIfCancellationRequested();
                var sample = samples[i];

                float[] imageData;
                try
                {
                    imageData = BuildImageTensor(sample.SourcePath, inputSize, normalizeMean, normalizeStd, resampler);
                }
                catch
                {
                    imageData = new float[3 * inputSize * inputSize];
                }

                using var inputTensor = torch.tensor(imageData, [1, 3, inputSize, inputSize], dtype: ScalarType.Float32, device: torchDevice);
                using var predictionTensor = model.forward(inputTensor);
                using var predictionCpu = predictionTensor.detach().to(CPU);
                var predValues = predictionCpu.data<float>().ToArray();

                var boxes = DecodeBoxes(predValues, topK);
                totalBoxCount += boxes.Count;

                sampleResults.Add(new DetectionInferenceSampleResult(
                    i,
                    sample.Id,
                    sample.SourcePath ?? string.Empty,
                    boxes));

                await context.ArtifactStore.AppendLineAsync(
                    context.RunId,
                    "reports/inference-samples.jsonl",
                    JsonSerializer.Serialize(new
                    {
                        sampleIndex = i,
                        sampleId = sample.Id,
                        sourcePath = sample.SourcePath,
                        detectedBoxCount = boxes.Count,
                        boxes = boxes.Select(b => new
                        {
                            cx = b.Cx, cy = b.Cy, bw = b.Bw, bh = b.Bh,
                            categoryId = b.CategoryId, score = b.Score
                        })
                    }),
                    ct);
            }

            var summary = new
            {
                task = "detection",
                mode = "infer",
                backend = context.Config.Backend.ToString(),
                device = context.Config.Device.ToString(),
                sampleCount,
                totalDetectedBoxes = totalBoxCount,
                averageBoxesPerSample = sampleCount > 0 ? (double)totalBoxCount / sampleCount : 0d,
                inputSize,
                topK,
                status = "detection-inference-complete"
            };

            await context.ArtifactStore.WriteTextAsync(
                context.RunId,
                "reports/inference.json",
                JsonSerializer.Serialize(summary, JsonOptions),
                ct);

            await context.RunRepository.AppendMetricAsync(
                context.RunId,
                new MetricPoint("infer_sample_count", 1, sampleCount, DateTimeOffset.UtcNow),
                ct);

            await context.RunRepository.AppendEventAsync(
                context.RunId,
                new RunEvent("Information", "DetectionInferenceComplete",
                    $"检测批量推理完成，共 {sampleCount} 个样本，总框数 {totalBoxCount}。",
                    DateTimeOffset.UtcNow),
                ct);

            return new RunResult(context.RunId, RunStatus.Succeeded, "检测批量推理完成。", context.RunDirectory);
        }
        catch (DllNotFoundException ex)
        {
            throw new InvalidOperationException("TorchSharp 运行时不可用。", ex);
        }
    }

    private static DetectionModelMetadataDto? LoadModelMetadata(string modelRunDirectory)
    {
        if (string.IsNullOrWhiteSpace(modelRunDirectory))
        {
            return null;
        }

        var metadataPath = Path.Combine(modelRunDirectory, "artifacts", "model-metadata.json");
        if (!File.Exists(metadataPath))
        {
            return null;
        }

        try
        {
            var json = File.ReadAllText(metadataPath);
            return JsonSerializer.Deserialize<DetectionModelMetadataDto>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch
        {
            return null;
        }
    }

    private static Module<Tensor, Tensor> CreateDetectorModel(int topK) => new TinyMultiHeadDetectorInferModel(Math.Max(1, topK));

    private static Device ResolveTorchDevice(CoreDeviceType configuredDevice)
    {
        if (configuredDevice == CoreDeviceType.Cuda && cuda.is_available())
        {
            return CUDA;
        }

        if (configuredDevice == CoreDeviceType.Auto && cuda.is_available())
        {
            return CUDA;
        }

        return CPU;
    }

    private static IResampler ResolveResampler(string name) => name.Trim().ToLowerInvariant() switch
    {
        "nearest" => KnownResamplers.NearestNeighbor,
        "bilinear" => KnownResamplers.Triangle,
        "triangle" => KnownResamplers.Triangle,
        "lanczos3" => KnownResamplers.Lanczos3,
        _ => KnownResamplers.Bicubic
    };

    private static float[] BuildImageTensor(string? sourcePath, int inputSize, float[] mean, float[] std, IResampler resampler)
    {
        if (string.IsNullOrWhiteSpace(sourcePath) || !File.Exists(sourcePath))
        {
            return new float[3 * inputSize * inputSize];
        }

        using var image = Image.Load<Rgb24>(sourcePath);
        image.Mutate(ctx => ctx.Resize(new ResizeOptions
        {
            Size = new SixLabors.ImageSharp.Size(inputSize, inputSize),
            Sampler = resampler
        }));

        var data = new float[3 * inputSize * inputSize];
        for (var y = 0; y < inputSize; y++)
        {
            for (var x = 0; x < inputSize; x++)
            {
                var pixel = image[x, y];
                var baseIndex = (y * inputSize) + x;
                var s0 = std[0] <= 0f ? 1f : std[0];
                var s1 = std[1] <= 0f ? 1f : std[1];
                var s2 = std[2] <= 0f ? 1f : std[2];
                data[baseIndex] = (pixel.R / 255f - mean[0]) / s0;
                data[inputSize * inputSize + baseIndex] = (pixel.G / 255f - mean[1]) / s1;
                data[2 * inputSize * inputSize + baseIndex] = (pixel.B / 255f - mean[2]) / s2;
            }
        }

        return data;
    }

    private static List<DetectionInferenceBox> DecodeBoxes(float[] predValues, int topK)
    {
        var boxes = new List<DetectionInferenceBox>(topK);
        var stride = DetectionTargetBoxValueCount;
        for (var i = 0; i < topK && (i * stride + stride - 1) < predValues.Length; i++)
        {
            var offset = i * stride;
            var cx = Math.Clamp(predValues[offset], 0f, 1f);
            var cy = Math.Clamp(predValues[offset + 1], 0f, 1f);
            var bw = Math.Clamp(Math.Abs(predValues[offset + 2]), 0f, 1f);
            var bh = Math.Clamp(Math.Abs(predValues[offset + 3]), 0f, 1f);
            var categoryId = Math.Max(0f, predValues[offset + 4]);
            var score = Math.Clamp(predValues[offset + 5], 0f, 1f);

            if (score >= 0.1f)
            {
                boxes.Add(new DetectionInferenceBox(cx, cy, bw, bh, (int)Math.Round(categoryId), score));
            }
        }

        return boxes;
    }

    private sealed record DetectionInferenceBox(float Cx, float Cy, float Bw, float Bh, int CategoryId, float Score);

    private sealed record DetectionInferenceSampleResult(
        int SampleIndex,
        string SampleId,
        string SourcePath,
        IReadOnlyList<DetectionInferenceBox> Boxes);

    private static bool TryLoadModelWeights(Module<Tensor, Tensor> model, string inputPath)
    {
        try
        {
            var loadMethod = model.GetType().GetMethod("load", [typeof(string)]);
            if (loadMethod is null)
            {
                return false;
            }

            loadMethod.Invoke(model, [inputPath]);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// 推理专用模型（仅前向推理，结构与训练时保持一致）。
    /// </summary>
    private sealed class TinyMultiHeadDetectorInferModel : Module<Tensor, Tensor>
    {
        private readonly int _targetTopK;
        private readonly Module<Tensor, Tensor> _backbone;
        private readonly Module<Tensor, Tensor> _sharedProjection;
        private readonly Module<Tensor, Tensor> _bboxHead;
        private readonly Module<Tensor, Tensor> _categoryHead;
        private readonly Module<Tensor, Tensor> _objectnessHead;

        public TinyMultiHeadDetectorInferModel(int targetTopK)
            : base(nameof(TinyMultiHeadDetectorInferModel))
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
            using var category = torch.nn.functional.relu(categoryRaw).reshape([batchSize, _targetTopK, 1]);
            using var objectness = torch.sigmoid(objectnessRaw).reshape([batchSize, _targetTopK, 1]);
            using var merged = torch.cat(new[] { bbox, category, objectness }, dim: 2);
            return merged.flatten(1);
        }
    }
}
