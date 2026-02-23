using System.Globalization;
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

public sealed class OcrTrainingTask : ITrainingTask
{
    private const string DefaultCharset = "abcdefghijklmnopqrstuvwxyz0123456789";
    private const string ModelWeightsRelativePath = "artifacts/model-weights.bin";
    private const int DefaultInputHeight = 32;
    private const int DefaultInputWidth = 128;
    private const int DefaultMaxTextLength = 16;

    public TaskType TaskType => TaskType.Ocr;

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
            RunMode.Validate => await ExecuteTorchSharpEvalAsync(context, context.Data.Validation, "validate", ct),
            RunMode.Test => await ExecuteTorchSharpEvalAsync(context, context.Data.Test, "test", ct),
            _ => throw new InvalidOperationException("不支持的 OCR 运行模式。")
        };
    }

    private static async Task<RunResult> ExecuteTorchSharpTrainAsync(TrainingExecutionContext context, CancellationToken ct)
    {
        var options = ResolveOptions(context);
        await WriteOcrTagsAsync(context, options, ct);
        await context.RunRepository.AppendEventAsync(
            context.RunId,
            new RunEvent("Information", "OcrTaskStart", "TorchSharp OCR 训练开始。", DateTimeOffset.UtcNow),
            ct);

        var samples = context.Data.Train;
        var sampleCount = Math.Max(1, samples.Count);

        try
        {
            var torchDevice = ResolveTorchDevice(context.Config.Device);
            torch.random.manual_seed(Math.Max(1, context.Config.Optimization.Seed));

            using var model = CreateTinyOcrModel(options).to(torchDevice);
            using var optimizer = torch.optim.Adam(model.parameters(), context.Config.Optimization.LearningRate);
            using var lossFunction = BCEWithLogitsLoss();
            model.train();

            var batchSize = Math.Max(1, context.Config.Optimization.BatchSize);
            var epochs = Math.Max(1, context.Config.Optimization.Epochs);
            var stepsPerEpoch = Math.Max(1, (int)Math.Ceiling(sampleCount / (double)batchSize));
            var lr = context.Config.Optimization.LearningRate;
            double trainGradNormSum = 0d;
            var trainStepCount = 0;

            await context.RunRepository.AppendEventAsync(
                context.RunId,
                new RunEvent("Information", "TrainPlan",
                    $"训练计划：epochs={epochs}, stepsPerEpoch={stepsPerEpoch}, batchSize={batchSize}, lr={lr}, samples={sampleCount}, inputH={options.InputHeight}, inputW={options.InputWidth}",
                    DateTimeOffset.UtcNow),
                ct);

            for (var epoch = 1; epoch <= epochs; epoch++)
            {
                ct.ThrowIfCancellationRequested();
                double epochLoss = 0;
                double epochGradNormSum = 0d;
                double epochGradNormMin = double.MaxValue;
                double epochGradNormMax = 0d;

                for (var step = 1; step <= stepsPerEpoch; step++)
                {
                    ct.ThrowIfCancellationRequested();
                    var globalStep = ((epoch - 1) * stepsPerEpoch) + step;

                    using var inputTensor = BuildInputTensor(samples, batchSize, globalStep, options, torchDevice, context.Config.Dataset.SkipInvalidSamples, isTraining: true);
                    using var targetTensor = BuildTargetTensor(samples, batchSize, globalStep, options, torchDevice);

                    optimizer.zero_grad();
                    using var logits = model.forward(inputTensor);
                    using var loss = lossFunction.forward(logits, targetTensor);
                    loss.backward();

                    var gradNorm = ComputeGradientNorm(model);

                    optimizer.step();

                    var lossValue = loss.ToDouble();
                    epochLoss += lossValue;
                    epochGradNormSum += gradNorm;
                    epochGradNormMin = Math.Min(epochGradNormMin, gradNorm);
                    epochGradNormMax = Math.Max(epochGradNormMax, gradNorm);
                    trainGradNormSum += gradNorm;
                    trainStepCount++;
                    var timestamp = DateTimeOffset.UtcNow;

                    await context.RunRepository.AppendMetricAsync(context.RunId, new MetricPoint("ocr_loss", globalStep, lossValue, timestamp), ct);
                    await context.RunRepository.AppendMetricAsync(context.RunId, new MetricPoint("grad_norm", globalStep, gradNorm, timestamp), ct);
                    await context.ArtifactStore.AppendLineAsync(
                        context.RunId,
                        "metrics.jsonl",
                        JsonSerializer.Serialize(new OcrTrainMetricStreamEntry(globalStep, epoch, lossValue, gradNorm)),
                        ct);

                    await context.RunRepository.AppendEventAsync(
                        context.RunId,
                        new RunEvent("Trace", "TrainStep",
                            $"Epoch {epoch}/{epochs} Step {step}/{stepsPerEpoch} (global={globalStep}) | ocr_loss={lossValue:F6} | grad_norm={gradNorm:F6}",
                            DateTimeOffset.UtcNow),
                        ct);
                }

                var avgLoss = epochLoss / stepsPerEpoch;
                var avgGradNorm = epochGradNormSum / stepsPerEpoch;
                if (epochGradNormMin == double.MaxValue) epochGradNormMin = 0d;

                await context.RunRepository.AppendEventAsync(
                    context.RunId,
                    new RunEvent("Information", "OcrEpochCompleted",
                        $"第 {epoch}/{epochs} 轮完成 | avg_loss={avgLoss:F6} | grad_norm: avg={avgGradNorm:F6} min={epochGradNormMin:F6} max={epochGradNormMax:F6}",
                        DateTimeOffset.UtcNow),
                    ct);
            }

            var trainMetrics = await EvaluateDatasetAsync(model, samples, options, torchDevice, ct);
            await AppendEvalMetricsAsync(context, "train", trainMetrics, ct);

            var weightsSaved = TrySaveModelWeights(model, Path.Combine(context.RunDirectory, "artifacts", "model-weights.bin"));
            await context.RunRepository.AppendEventAsync(
                context.RunId,
                new RunEvent(
                    "Information",
                    "ModelWeightsSaved",
                    weightsSaved
                        ? $"模型权重已保存：{ModelWeightsRelativePath}"
                        : "模型未提供可用保存接口，已跳过权重保存。",
                    DateTimeOffset.UtcNow),
                ct);
            if (weightsSaved)
            {
                await context.ArtifactStore.WriteTextAsync(
                    context.RunId,
                    "artifacts/model-weights.manifest.json",
                    JsonSerializer.Serialize(new
                    {
                        path = ModelWeightsRelativePath,
                        format = "torch-module-save",
                        createdAt = DateTimeOffset.UtcNow
                    }),
                    ct);
            }

            await context.ArtifactStore.WriteTextAsync(
                context.RunId,
                "artifacts/model-metadata.json",
                JsonSerializer.Serialize(new OcrModelMetadataDto(
                    "TorchSharp",
                    "ocr",
                    context.Config.Device.ToString(),
                    string.IsNullOrWhiteSpace(context.Config.Model.Architecture) ? "tiny-ocr-cnn" : context.Config.Model.Architecture,
                    BuildOptionsDto(options),
                    "trained")),
                ct);

            await context.ArtifactStore.WriteTextAsync(
                context.RunId,
                "reports/summary.json",
                JsonSerializer.Serialize(new OcrTrainSummaryDto(
                    "ocr",
                    "Train",
                    context.Config.Backend.ToString(),
                    context.Config.Device.ToString(),
                    sampleCount,
                    Math.Max(1, context.Config.Optimization.Epochs),
                    Math.Max(1, (int)Math.Ceiling(sampleCount / (double)Math.Max(1, context.Config.Optimization.BatchSize))),
                    BuildOptionsDto(options),
                    BuildEvalMetricsDto(trainMetrics),
                    "torchsharp-ocr-train-complete")),
                ct);

            return new RunResult(context.RunId, RunStatus.Succeeded, "OCR TorchSharp 训练完成。", context.RunDirectory);
        }
        catch (DllNotFoundException ex)
        {
            throw new InvalidOperationException("TorchSharp 运行时不可用，请安装对应 CPU/CUDA 运行时依赖。", ex);
        }
    }

    private static async Task<RunResult> ExecuteTorchSharpEvalAsync(
        TrainingExecutionContext context,
        IReadOnlyList<DataSample> samples,
        string stage,
        CancellationToken ct)
    {
        var options = ResolveOptions(context);
        await WriteOcrTagsAsync(context, options, ct);

        try
        {
            var torchDevice = ResolveTorchDevice(context.Config.Device);
            using var model = CreateTinyOcrModel(options).to(torchDevice);
            model.eval();

            var evalMetrics = await EvaluateDatasetAsync(model, samples, options, torchDevice, ct);
            await AppendEvalMetricsAsync(context, stage, evalMetrics, ct);

            await context.ArtifactStore.WriteTextAsync(
                context.RunId,
                $"reports/{stage}.json",
                JsonSerializer.Serialize(new OcrEvalReportDto(
                    "ocr",
                    stage,
                    "TorchSharp",
                    context.Config.Device.ToString(),
                    Math.Max(1, samples.Count),
                    BuildOptionsDto(options),
                    "approx-ocr-char-seq",
                    BuildEvalMetricsDto(evalMetrics),
                    "torchsharp-ocr-eval-complete")),
                ct);

            return new RunResult(context.RunId, RunStatus.Succeeded, $"OCR {stage} 完成。", context.RunDirectory);
        }
        catch (DllNotFoundException ex)
        {
            throw new InvalidOperationException("TorchSharp 运行时不可用，请安装对应 CPU/CUDA 运行时依赖。", ex);
        }
    }

    private static async Task<RunResult> ExecuteNonTorchSharpStubAsync(TrainingExecutionContext context, CancellationToken ct)
    {
        await context.RunRepository.AppendEventAsync(
            context.RunId,
            new RunEvent("Warning", "OcrBackendFallback", $"当前后端 {context.Config.Backend} 未实现 OCR 真实训练，执行占位流程。", DateTimeOffset.UtcNow),
            ct);

        var metricTime = DateTimeOffset.UtcNow;
        await context.RunRepository.AppendMetricAsync(context.RunId, new MetricPoint("cer", 1, 0.25, metricTime), ct);
        await context.RunRepository.AppendMetricAsync(context.RunId, new MetricPoint("wer", 1, 0.5, metricTime), ct);

        var reportName = context.Config.Mode == RunMode.Train
            ? "reports/summary.json"
            : $"reports/{context.Config.Mode.ToString().ToLowerInvariant()}.json";
        var stubOptions = ResolveOptions(context);
        await context.ArtifactStore.WriteTextAsync(
            context.RunId,
            reportName,
            JsonSerializer.Serialize(new OcrEvalReportDto(
                "ocr",
                context.Config.Mode.ToString(),
                context.Config.Backend.ToString(),
                context.Config.Device.ToString(),
                0,
                BuildOptionsDto(stubOptions),
                "stub",
                BuildEvalMetricsDto(new OcrEvalMetrics(0.25, 0.5, 0, 0, 0)),
                "stub-complete")),
            ct);

        return new RunResult(context.RunId, RunStatus.Succeeded, "非 TorchSharp OCR 占位流程完成。", context.RunDirectory);
    }

    private static double ComputeGradientNorm(Module<Tensor, Tensor> model)
    {
        double totalNormSq = 0;
        foreach (var (_, param) in model.named_parameters())
        {
            var grad = param.grad;
            if (grad is null) continue;
            // 中文说明：避免 norm(2) 被解析为 dim=2 重载，先展平再求 L2，确保得到标量。
            using var gradFlat = grad.flatten();
            using var normTensor = TorchSharp.torch.linalg.vector_norm(gradFlat, ord: 2.0);
            var n = normTensor.ToDouble();
            totalNormSq += n * n;
        }
        return Math.Sqrt(totalNormSq);
    }

    private static Module<Tensor, Tensor> CreateTinyOcrModel(OcrTensorOptions options)
    {
        var outputDim = options.MaxTextLength * options.VocabularySize;
        return Sequential(
            ("conv1", Conv2d(1, 8, 3, stride: 1, padding: 1)),
            ("relu1", ReLU()),
            ("pool1", MaxPool2d(2)),
            ("conv2", Conv2d(8, 16, 3, stride: 1, padding: 1)),
            ("relu2", ReLU()),
            ("pool2", AdaptiveAvgPool2d([1, 1])),
            ("flat", Flatten()),
            ("head", Linear(16, outputDim))
        );
    }

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

    private static OcrTensorOptions ResolveOptions(TrainingExecutionContext context)
    {
        var inputHeight = ReadPositiveInt(context, "inputHeight", DefaultInputHeight);
        var inputWidth = ReadPositiveInt(context, "inputWidth", DefaultInputWidth);
        var maxTextLength = ReadPositiveInt(context, "maxTextLength", DefaultMaxTextLength);
        var charset = ReadCharset(context);
        var vocabulary = BuildVocabulary(charset);

        return new OcrTensorOptions(
            inputHeight,
            inputWidth,
            maxTextLength,
            charset,
            vocabulary,
            vocabulary.Count,
            ReadOcrResamplerName(context),
            ReadOcrResampler(context));
    }

    private static Tensor BuildInputTensor(
        IReadOnlyList<DataSample> samples,
        int batchSize,
        int globalStep,
        OcrTensorOptions options,
        Device device,
        bool allowFallbackOnImageError,
        bool isTraining)
    {
        var hw = options.InputHeight * options.InputWidth;
        var data = new float[batchSize * hw];

        for (var sampleIndex = 0; sampleIndex < batchSize; sampleIndex++)
        {
            var sample = PickSample(samples, globalStep + sampleIndex);
            var offset = sampleIndex * hw;
            try
            {
                var imageTensor = BuildImageTensor(sample, options);
                Buffer.BlockCopy(imageTensor, 0, data, offset * sizeof(float), hw * sizeof(float));
            }
            catch (Exception) when (allowFallbackOnImageError || !isTraining)
            {
                // 中文说明：OCR 图像解码失败时回退零张量，保证流程可继续。
                Array.Clear(data, offset, hw);
            }
            catch (Exception)
            {
                if (isTraining)
                {
                    throw;
                }

                Array.Clear(data, offset, hw);
            }
        }

        return tensor(data, [batchSize, 1, options.InputHeight, options.InputWidth], dtype: ScalarType.Float32, device: device);
    }

    private static float[] BuildImageTensor(DataSample sample, OcrTensorOptions options)
    {
        if (string.IsNullOrWhiteSpace(sample.SourcePath) || !File.Exists(sample.SourcePath))
        {
            throw new FileNotFoundException("OCR 图像不存在。", sample.SourcePath);
        }

        using var image = Image.Load<L8>(sample.SourcePath);
        image.Mutate(ctx => ctx.Resize(new ResizeOptions
        {
            Size = new SixLabors.ImageSharp.Size(options.InputWidth, options.InputHeight),
            Sampler = options.Resampler
        }));

        var data = new float[options.InputHeight * options.InputWidth];
        for (var y = 0; y < options.InputHeight; y++)
        {
            for (var x = 0; x < options.InputWidth; x++)
            {
                var pixel = image[x, y];
                data[(y * options.InputWidth) + x] = pixel.PackedValue / 255f;
            }
        }

        return data;
    }

    private static Tensor BuildTargetTensor(
        IReadOnlyList<DataSample> samples,
        int batchSize,
        int globalStep,
        OcrTensorOptions options,
        Device device)
    {
        var data = new float[batchSize * options.OutputDimension];
        for (var sampleIndex = 0; sampleIndex < batchSize; sampleIndex++)
        {
            var sample = PickSample(samples, globalStep + sampleIndex);
            var encoded = EncodeTextTarget(sample.Label ?? string.Empty, options);
            Buffer.BlockCopy(encoded, 0, data, sampleIndex * options.OutputDimension * sizeof(float), options.OutputDimension * sizeof(float));
        }

        return tensor(data, [batchSize, options.OutputDimension], dtype: ScalarType.Float32, device: device);
    }

    private static float[] EncodeTextTarget(string text, OcrTensorOptions options)
    {
        var normalizedText = NormalizeText(text);
        var data = new float[options.OutputDimension];
        for (var charIndex = 0; charIndex < options.MaxTextLength; charIndex++)
        {
            var outputOffset = charIndex * options.VocabularySize;
            var vocabIndex = 0;
            if (charIndex < normalizedText.Length)
            {
                var ch = normalizedText[charIndex];
                if (!options.Vocabulary.TryGetValue(ch, out vocabIndex))
                {
                    vocabIndex = 0;
                }
            }

            data[outputOffset + vocabIndex] = 1f;
        }

        return data;
    }

    private static DataSample PickSample(IReadOnlyList<DataSample> samples, int index)
    {
        if (samples.Count == 0)
        {
            return new DataSample("ocr-empty", "empty", string.Empty, "train");
        }

        return samples[Math.Abs(index) % samples.Count];
    }

    private static async Task<OcrEvalMetrics> EvaluateDatasetAsync(
        Module<Tensor, Tensor> model,
        IReadOnlyList<DataSample> samples,
        OcrTensorOptions options,
        Device device,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (samples.Count == 0)
        {
            return new OcrEvalMetrics(1.0, 1.0, 0, 0, 0);
        }

        var cerList = new List<double>(samples.Count);
        var werList = new List<double>(samples.Count);
        var exactMatchCount = 0;

        for (var index = 0; index < samples.Count; index++)
        {
            ct.ThrowIfCancellationRequested();
            var sample = samples[index];
            using var inputTensor = BuildInputTensor(samples, 1, index, options, device, allowFallbackOnImageError: true, isTraining: false);
            using var logits = model.forward(inputTensor);
            var predictedText = DecodePrediction(logits, options);
            var targetText = NormalizeText(sample.Label ?? string.Empty);
            var cer = ComputeCharacterErrorRate(targetText, predictedText);
            var wer = ComputeWordErrorRate(targetText, predictedText);
            cerList.Add(cer);
            werList.Add(wer);
            if (string.Equals(targetText, predictedText, StringComparison.Ordinal))
            {
                exactMatchCount++;
            }
        }

        var avgCer = cerList.Count > 0 ? cerList.Average() : 1d;
        var avgWer = werList.Count > 0 ? werList.Average() : 1d;
        return new OcrEvalMetrics(avgCer, avgWer, exactMatchCount, samples.Count, options.MaxTextLength);
    }

    private static string DecodePrediction(Tensor logits, OcrTensorOptions options)
    {
        using var cpuTensor = logits.detach().to(CPU);
        var values = cpuTensor.data<float>().ToArray();
        var builder = new StringBuilder(options.MaxTextLength);

        for (var charIndex = 0; charIndex < options.MaxTextLength; charIndex++)
        {
            var offset = charIndex * options.VocabularySize;
            var bestIndex = 0;
            var bestValue = float.NegativeInfinity;
            for (var vocabIndex = 0; vocabIndex < options.VocabularySize; vocabIndex++)
            {
                var value = values[offset + vocabIndex];
                if (value > bestValue)
                {
                    bestValue = value;
                    bestIndex = vocabIndex;
                }
            }

            if (bestIndex == 0)
            {
                continue;
            }

            builder.Append(options.Charset[bestIndex - 1]);
        }

        return builder.ToString().Trim();
    }

    private static async Task AppendEvalMetricsAsync(TrainingExecutionContext context, string stage, OcrEvalMetrics metrics, CancellationToken ct)
    {
        var suffix = stage switch
        {
            "test" => "_test",
            "train" => "_train",
            _ => string.Empty
        };

        var timestamp = DateTimeOffset.UtcNow;
        var metricPoints = new[]
        {
            new MetricPoint($"cer{suffix}", 1, metrics.Cer, timestamp),
            new MetricPoint($"wer{suffix}", 1, metrics.Wer, timestamp)
        };

        foreach (var metric in metricPoints)
        {
            await context.RunRepository.AppendMetricAsync(context.RunId, metric, ct);
            await context.ArtifactStore.AppendLineAsync(
                context.RunId,
                "metrics.jsonl",
                JsonSerializer.Serialize(new OcrMetricStreamEntry(metric.Name, metric.Step, metric.Value)),
                ct);
        }
    }

    private static async Task WriteOcrTagsAsync(TrainingExecutionContext context, OcrTensorOptions options, CancellationToken ct)
    {
        await context.RunRepository.UpsertTagAsync(context.RunId, "ocr.input.height", options.InputHeight.ToString(CultureInfo.InvariantCulture), ct);
        await context.RunRepository.UpsertTagAsync(context.RunId, "ocr.input.width", options.InputWidth.ToString(CultureInfo.InvariantCulture), ct);
        await context.RunRepository.UpsertTagAsync(context.RunId, "ocr.text.max_length", options.MaxTextLength.ToString(CultureInfo.InvariantCulture), ct);
        await context.RunRepository.UpsertTagAsync(context.RunId, "ocr.charset.size", options.Charset.Length.ToString(CultureInfo.InvariantCulture), ct);
        await context.RunRepository.UpsertTagAsync(context.RunId, "ocr.resize_sampler", options.ResamplerName, ct);
    }

    private static int ReadPositiveInt(TrainingExecutionContext context, string key, int defaultValue)
    {
        if (context.Config.TaskOptions.TryGetValue(key, out var taskElement) && taskElement.ValueKind == JsonValueKind.Number && taskElement.TryGetInt32(out var taskValue) && taskValue > 0)
        {
            return taskValue;
        }

        if (context.Config.Model.Parameters.TryGetValue(key, out var modelElement) && modelElement.ValueKind == JsonValueKind.Number && modelElement.TryGetInt32(out var modelValue) && modelValue > 0)
        {
            return modelValue;
        }

        return defaultValue;
    }

    private static string ReadCharset(TrainingExecutionContext context)
    {
        if (context.Config.TaskOptions.TryGetValue("charset", out var element) && element.ValueKind == JsonValueKind.String)
        {
            var value = element.GetString();
            if (!string.IsNullOrWhiteSpace(value))
            {
                return NormalizeCharset(value);
            }
        }

        return DefaultCharset;
    }

    private static string NormalizeCharset(string value)
    {
        var normalized = NormalizeText(value);
        var builder = new StringBuilder();
        var seen = new HashSet<char>();
        foreach (var ch in normalized)
        {
            if (seen.Add(ch))
            {
                builder.Append(ch);
            }
        }

        return builder.Length > 0 ? builder.ToString() : DefaultCharset;
    }

    private static Dictionary<char, int> BuildVocabulary(string charset)
    {
        var vocabulary = new Dictionary<char, int>(charset.Length);
        for (var index = 0; index < charset.Length; index++)
        {
            vocabulary[charset[index]] = index + 1; // 0 保留为空白符
        }

        return vocabulary;
    }

    private static string ReadOcrResamplerName(TrainingExecutionContext context)
    {
        if (context.Config.TaskOptions.TryGetValue("resizeSampler", out var element) && element.ValueKind == JsonValueKind.String)
        {
            var value = (element.GetString() ?? string.Empty).Trim().ToLowerInvariant();
            return value switch
            {
                "nearest" => "nearest",
                "bilinear" => "bilinear",
                "triangle" => "triangle",
                "bicubic" => "bicubic",
                "lanczos3" => "lanczos3",
                _ => "bicubic"
            };
        }

        return "bicubic";
    }

    private static IResampler ReadOcrResampler(TrainingExecutionContext context)
    {
        var name = ReadOcrResamplerName(context);
        return name switch
        {
            "nearest" => KnownResamplers.NearestNeighbor,
            "bilinear" => KnownResamplers.Triangle,
            "triangle" => KnownResamplers.Triangle,
            "lanczos3" => KnownResamplers.Lanczos3,
            _ => KnownResamplers.Bicubic
        };
    }

    private static string NormalizeText(string text)
    {
        var lower = (text ?? string.Empty).Trim().ToLowerInvariant();
        return lower;
    }

    private static double ComputeCharacterErrorRate(string target, string predicted)
    {
        if (target.Length == 0)
        {
            return predicted.Length == 0 ? 0d : 1d;
        }

        return ComputeLevenshteinDistance(target.AsSpan(), predicted.AsSpan()) / (double)target.Length;
    }

    private static double ComputeWordErrorRate(string target, string predicted)
    {
        var targetWords = SplitWords(target);
        var predictedWords = SplitWords(predicted);

        if (targetWords.Count == 0)
        {
            return predictedWords.Count == 0 ? 0d : 1d;
        }

        return ComputeLevenshteinDistance(targetWords, predictedWords) / (double)targetWords.Count;
    }

    private static List<string> SplitWords(string text)
    {
        return text.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
    }

    private static int ComputeLevenshteinDistance(ReadOnlySpan<char> source, ReadOnlySpan<char> target)
    {
        var previous = new int[target.Length + 1];
        var current = new int[target.Length + 1];

        for (var j = 0; j <= target.Length; j++)
        {
            previous[j] = j;
        }

        for (var i = 1; i <= source.Length; i++)
        {
            current[0] = i;
            for (var j = 1; j <= target.Length; j++)
            {
                var cost = source[i - 1] == target[j - 1] ? 0 : 1;
                current[j] = Math.Min(
                    Math.Min(current[j - 1] + 1, previous[j] + 1),
                    previous[j - 1] + cost);
            }

            (previous, current) = (current, previous);
        }

        return previous[target.Length];
    }

    private static int ComputeLevenshteinDistance(IReadOnlyList<string> source, IReadOnlyList<string> target)
    {
        var previous = new int[target.Count + 1];
        var current = new int[target.Count + 1];

        for (var j = 0; j <= target.Count; j++)
        {
            previous[j] = j;
        }

        for (var i = 1; i <= source.Count; i++)
        {
            current[0] = i;
            for (var j = 1; j <= target.Count; j++)
            {
                var cost = string.Equals(source[i - 1], target[j - 1], StringComparison.Ordinal) ? 0 : 1;
                current[j] = Math.Min(
                    Math.Min(current[j - 1] + 1, previous[j] + 1),
                    previous[j - 1] + cost);
            }

            (previous, current) = (current, previous);
        }

        return previous[target.Count];
    }

    private sealed record OcrTensorOptions(
        int InputHeight,
        int InputWidth,
        int MaxTextLength,
        string Charset,
        IReadOnlyDictionary<char, int> Vocabulary,
        int VocabularySize,
        string ResamplerName,
        IResampler Resampler)
    {
        public int OutputDimension => MaxTextLength * VocabularySize;
    }

    private sealed record OcrMetricStreamEntry(string Metric, long Step, double Value);
    private sealed record OcrTrainMetricStreamEntry(long Step, int Epoch, double Loss, double GradNorm);

    private static OcrTensorOptionsDto BuildOptionsDto(OcrTensorOptions options) =>
        new(options.InputHeight, options.InputWidth, options.MaxTextLength, options.Charset.Length, options.ResamplerName);

    private static OcrEvalMetricsDto BuildEvalMetricsDto(OcrEvalMetrics metrics) =>
        new(metrics.Cer, metrics.Wer, metrics.ExactMatchCount, metrics.SampleCount, metrics.MaxTextLength);

    private sealed record OcrEvalMetrics(double Cer, double Wer, int ExactMatchCount, int SampleCount, int MaxTextLength);

    private static bool TrySaveModelWeights(Module<Tensor, Tensor> model, string outputPath)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
            var saveMethod = model.GetType().GetMethod("save", [typeof(string)]);
            if (saveMethod is null)
            {
                return false;
            }

            saveMethod.Invoke(model, [outputPath]);
            return File.Exists(outputPath);
        }
        catch
        {
            return false;
        }
    }
}
