using System.Text;
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

namespace TransformersMini.Training.Tasks.Ocr;

/// <summary>
/// OCR 批量推理任务实现。
/// 逐样本推理，输出预测文本与 CER，落盘 inference.json 与 inference-samples.jsonl。
/// </summary>
public sealed class OcrInferenceTask : IInferenceTask
{
    private const string DefaultCharset = "abcdefghijklmnopqrstuvwxyz0123456789";
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public TaskType TaskType => TaskType.Ocr;

    public async Task<RunResult> ExecuteAsync(InferenceExecutionContext context, CancellationToken ct)
    {
        await context.RunRepository.AppendEventAsync(
            context.RunId,
            new RunEvent("Information", "OcrInferenceStart", "OCR 批量推理开始。", DateTimeOffset.UtcNow),
            ct);

        try
        {
            var modelMetadata = LoadModelMetadata(context.ModelRunDirectory);
            var inputHeight = modelMetadata?.Options?.InputHeight ?? 32;
            var inputWidth = modelMetadata?.Options?.InputWidth ?? 128;
            var maxTextLength = modelMetadata?.Options?.MaxTextLength ?? 16;
            var resamplerName = modelMetadata?.Options?.ResizeSampler ?? "bicubic";
            var resampler = ResolveResampler(resamplerName);
            var charset = DefaultCharset;
            var vocabularySize = charset.Length + 1;

            var torchDevice = ResolveTorchDevice(context.Config.Device);
            var outputDim = maxTextLength * vocabularySize;

            using var model = CreateTinyOcrModel(inputHeight, inputWidth, outputDim).to(torchDevice);
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

            IReadOnlyList<DataSample> samples;
            if (!string.IsNullOrWhiteSpace(context.SingleImagePath))
            {
                var singlePath = Path.GetFullPath(context.SingleImagePath);
                samples =
                [
                    new DataSample("single-image", singlePath, null, "infer-single")
                ];
                await context.RunRepository.AppendEventAsync(
                    context.RunId,
                    new RunEvent("Information", "SingleImageOverride", $"本次推理使用单图输入：{singlePath}", DateTimeOffset.UtcNow),
                    ct);
            }
            else
            {
                // 优先使用 test split，fallback 到 val，再到 train
                samples = context.Data.Test.Count > 0
                    ? context.Data.Test
                    : context.Data.Validation.Count > 0
                        ? context.Data.Validation
                        : context.Data.Train;
            }

            if (context.MaxSamples > 0 && samples.Count > context.MaxSamples)
            {
                samples = samples.Take(context.MaxSamples).ToList();
            }

            var sampleCount = samples.Count;
            var cerList = new List<double>(sampleCount);
            var exactMatchCount = 0;

            for (var i = 0; i < sampleCount; i++)
            {
                ct.ThrowIfCancellationRequested();
                var sample = samples[i];

                float[] imageData;
                try
                {
                    imageData = BuildImageTensor(sample.SourcePath, inputHeight, inputWidth, resampler);
                }
                catch
                {
                    imageData = new float[inputHeight * inputWidth];
                }

                using var inputTensor = torch.tensor(imageData, [1, 1, inputHeight, inputWidth], dtype: ScalarType.Float32, device: torchDevice);
                using var logits = model.forward(inputTensor);
                var predictedText = DecodePrediction(logits, maxTextLength, vocabularySize, charset);
                var targetText = NormalizeText(sample.Label ?? string.Empty);
                var cer = ComputeCer(targetText, predictedText);
                cerList.Add(cer);
                if (string.Equals(targetText, predictedText, StringComparison.Ordinal))
                {
                    exactMatchCount++;
                }

                await context.ArtifactStore.AppendLineAsync(
                    context.RunId,
                    "reports/inference-samples.jsonl",
                    JsonSerializer.Serialize(new
                    {
                        sampleIndex = i,
                        sampleId = sample.Id,
                        sourcePath = sample.SourcePath,
                        predictedText,
                        targetText,
                        cer
                    }),
                    ct);
            }

            var avgCer = cerList.Count > 0 ? cerList.Average() : 1d;
            var summary = new
            {
                task = "ocr",
                mode = "infer",
                backend = context.Config.Backend.ToString(),
                device = context.Config.Device.ToString(),
                sampleCount,
                averageCer = avgCer,
                exactMatchCount,
                exactMatchRate = sampleCount > 0 ? (double)exactMatchCount / sampleCount : 0d,
                inputHeight,
                inputWidth,
                maxTextLength,
                status = "ocr-inference-complete"
            };

            await context.ArtifactStore.WriteTextAsync(
                context.RunId,
                "reports/inference.json",
                JsonSerializer.Serialize(summary, JsonOptions),
                ct);

            await context.RunRepository.AppendMetricAsync(
                context.RunId,
                new MetricPoint("infer_cer", 1, avgCer, DateTimeOffset.UtcNow),
                ct);
            await context.RunRepository.AppendMetricAsync(
                context.RunId,
                new MetricPoint("infer_sample_count", 1, sampleCount, DateTimeOffset.UtcNow),
                ct);

            await context.RunRepository.AppendEventAsync(
                context.RunId,
                new RunEvent("Information", "OcrInferenceComplete",
                    $"OCR 批量推理完成，共 {sampleCount} 个样本，平均 CER={avgCer:F4}，精确匹配 {exactMatchCount}。",
                    DateTimeOffset.UtcNow),
                ct);

            return new RunResult(context.RunId, RunStatus.Succeeded, "OCR 批量推理完成。", context.RunDirectory);
        }
        catch (DllNotFoundException ex)
        {
            throw new InvalidOperationException("TorchSharp 运行时不可用。", ex);
        }
    }

    private static OcrModelMetadataDto? LoadModelMetadata(string modelRunDirectory)
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
            return JsonSerializer.Deserialize<OcrModelMetadataDto>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch
        {
            return null;
        }
    }

    private static Module<Tensor, Tensor> CreateTinyOcrModel(int inputHeight, int inputWidth, int outputDim) =>
        Sequential(
            ("conv1", Conv2d(1, 8, 3, stride: 1, padding: 1)),
            ("relu1", ReLU()),
            ("pool1", MaxPool2d(2)),
            ("conv2", Conv2d(8, 16, 3, stride: 1, padding: 1)),
            ("relu2", ReLU()),
            ("pool2", AdaptiveAvgPool2d([1, 1])),
            ("flat", Flatten()),
            ("head", Linear(16, outputDim)));

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

    private static float[] BuildImageTensor(string? sourcePath, int inputHeight, int inputWidth, IResampler resampler)
    {
        if (string.IsNullOrWhiteSpace(sourcePath) || !File.Exists(sourcePath))
        {
            return new float[inputHeight * inputWidth];
        }

        using var image = Image.Load<L8>(sourcePath);
        image.Mutate(ctx => ctx.Resize(new ResizeOptions
        {
            Size = new SixLabors.ImageSharp.Size(inputWidth, inputHeight),
            Sampler = resampler
        }));

        var data = new float[inputHeight * inputWidth];
        for (var y = 0; y < inputHeight; y++)
        {
            for (var x = 0; x < inputWidth; x++)
            {
                data[(y * inputWidth) + x] = image[x, y].PackedValue / 255f;
            }
        }

        return data;
    }

    private static string DecodePrediction(Tensor logits, int maxTextLength, int vocabularySize, string charset)
    {
        using var cpuTensor = logits.detach().to(CPU);
        var values = cpuTensor.data<float>().ToArray();
        var builder = new StringBuilder(maxTextLength);

        for (var charIndex = 0; charIndex < maxTextLength; charIndex++)
        {
            var offset = charIndex * vocabularySize;
            var bestIndex = 0;
            var bestValue = float.NegativeInfinity;
            for (var vocabIndex = 0; vocabIndex < vocabularySize; vocabIndex++)
            {
                var value = (offset + vocabIndex) < values.Length ? values[offset + vocabIndex] : float.NegativeInfinity;
                if (value > bestValue)
                {
                    bestValue = value;
                    bestIndex = vocabIndex;
                }
            }

            if (bestIndex > 0 && bestIndex - 1 < charset.Length)
            {
                builder.Append(charset[bestIndex - 1]);
            }
        }

        return builder.ToString().Trim();
    }

    private static string NormalizeText(string text) => (text ?? string.Empty).Trim().ToLowerInvariant();

    private static double ComputeCer(string target, string predicted)
    {
        if (target.Length == 0)
        {
            return predicted.Length == 0 ? 0d : 1d;
        }

        var prev = new int[predicted.Length + 1];
        var curr = new int[predicted.Length + 1];
        for (var j = 0; j <= predicted.Length; j++) prev[j] = j;

        for (var i = 1; i <= target.Length; i++)
        {
            curr[0] = i;
            for (var j = 1; j <= predicted.Length; j++)
            {
                var cost = target[i - 1] == predicted[j - 1] ? 0 : 1;
                curr[j] = Math.Min(Math.Min(curr[j - 1] + 1, prev[j] + 1), prev[j - 1] + cost);
            }

            (prev, curr) = (curr, prev);
        }

        return prev[predicted.Length] / (double)target.Length;
    }

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
}
