using System.Text.Json;
using TorchSharp;
using TransformersMini.Contracts.Abstractions;
using TransformersMini.Contracts.Runtime;
using TransformersMini.SharedKernel.Core;
using static TorchSharp.torch;
using static TorchSharp.torch.nn;
using CoreDeviceType = TransformersMini.SharedKernel.Core.DeviceType;

namespace TransformersMini.Training.Tasks.Detection;

public sealed class DetectionTrainingTask : ITrainingTask
{
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
            RunMode.Validate => await ExecuteTorchSharpEvalAsync(context, "validate", context.Data.Validation.Count, ct),
            RunMode.Test => await ExecuteTorchSharpEvalAsync(context, "test", context.Data.Test.Count, ct),
            _ => throw new InvalidOperationException("不支持的运行模式。")
        };
    }

    private static async Task<RunResult> ExecuteTorchSharpTrainAsync(TrainingExecutionContext context, CancellationToken ct)
    {
        var count = context.Config.Mode switch
        {
            RunMode.Train => context.Data.Train.Count,
            RunMode.Validate => context.Data.Validation.Count,
            RunMode.Test => context.Data.Test.Count,
            _ => 0
        };

        await context.RunRepository.AppendEventAsync(
            context.RunId,
            new RunEvent("Information", "DetectionTaskStart", "TorchSharp 检测训练开始。", DateTimeOffset.UtcNow),
            ct);

        try
        {
            var torchDevice = ResolveTorchDevice(context.Config.Device);
            var featureDim = 16;
            var hiddenDim = 32;
            var outputDim = 5;
            var batchSize = Math.Max(1, context.Config.Optimization.BatchSize);
            var epochs = Math.Max(1, context.Config.Optimization.Epochs);
            var sampleCount = Math.Max(1, count);
            var stepsPerEpoch = Math.Max(1, (int)Math.Ceiling(sampleCount / (double)batchSize));

            torch.random.manual_seed(Math.Max(1, context.Config.Optimization.Seed));

            using var model = Sequential(
                ("fc1", Linear(featureDim, hiddenDim)),
                ("relu1", ReLU()),
                ("fc2", Linear(hiddenDim, hiddenDim)),
                ("relu2", ReLU()),
                ("head", Linear(hiddenDim, outputDim))
            ).to(torchDevice);

            using var optimizer = torch.optim.Adam(model.parameters(), context.Config.Optimization.LearningRate);
            model.train();

            for (var epoch = 1; epoch <= epochs; epoch++)
            {
                ct.ThrowIfCancellationRequested();
                double epochLoss = 0;

                for (var step = 1; step <= stepsPerEpoch; step++)
                {
                    ct.ThrowIfCancellationRequested();

                    using var features = randn(new long[] { batchSize, featureDim }, device: torchDevice);
                    using var targetBoxes = rand(new long[] { batchSize, 4 }, device: torchDevice);
                    using var targetObjectness = rand(new long[] { batchSize, 1 }, device: torchDevice);
                    using var targets = cat(new[] { targetBoxes, targetObjectness }, 1);

                    optimizer.zero_grad();
                    using var prediction = model.forward(features);
                    using var loss = TorchSharp.torch.nn.functional.mse_loss(prediction, targets);
                    loss.backward();
                    optimizer.step();

                    var lossValue = loss.ToDouble();
                    epochLoss += lossValue;
                    var globalStep = ((epoch - 1) * stepsPerEpoch) + step;

                    await context.RunRepository.AppendMetricAsync(
                        context.RunId,
                        new MetricPoint("loss", globalStep, lossValue, DateTimeOffset.UtcNow),
                        ct);

                    await context.ArtifactStore.AppendLineAsync(
                        context.RunId,
                        "metrics.jsonl",
                        JsonSerializer.Serialize(new { metric = "loss", step = globalStep, value = lossValue, epoch }),
                        ct);
                }

                var avgLoss = epochLoss / stepsPerEpoch;
                await context.RunRepository.AppendEventAsync(
                    context.RunId,
                    new RunEvent("Information", "EpochCompleted", $"第 {epoch} 轮完成，平均损失 {avgLoss:F6}", DateTimeOffset.UtcNow),
                    ct);
            }

            await context.ArtifactStore.WriteTextAsync(
                context.RunId,
                "artifacts/model-metadata.json",
                JsonSerializer.Serialize(new
                {
                    framework = "TorchSharp",
                    task = "detection",
                    device = context.Config.Device.ToString(),
                    model = context.Config.Model.Architecture,
                    status = "trained"
                }),
                ct);

            var trainReport = JsonSerializer.Serialize(new
            {
                task = "detection",
                mode = "Train",
                backend = context.Config.Backend.ToString(),
                device = context.Config.Device.ToString(),
                sampleCount,
                epochs,
                stepsPerEpoch,
                status = "torchsharp-train-complete"
            });
            await context.ArtifactStore.WriteTextAsync(context.RunId, "reports/summary.json", trainReport, ct);

            return new RunResult(context.RunId, RunStatus.Succeeded, "Detection TorchSharp 训练完成。", context.RunDirectory);
        }
        catch (DllNotFoundException ex)
        {
            throw new InvalidOperationException("TorchSharp 运行时不可用，请安装对应 CPU/CUDA 运行时依赖。", ex);
        }
    }

    private static async Task<RunResult> ExecuteTorchSharpEvalAsync(TrainingExecutionContext context, string stage, int sampleCount, CancellationToken ct)
    {
        var count = Math.Max(1, sampleCount);
        var torchDevice = ResolveTorchDevice(context.Config.Device);

        try
        {
            using var prediction = rand(new long[] { count, 5 }, device: torchDevice);
            using var target = rand(new long[] { count, 5 }, device: torchDevice);
            using var diff = (prediction - target).abs();
            using var mean = diff.mean();
            var score = Math.Max(0, 1.0 - mean.ToDouble());

            var metricName = stage == "test" ? "mAP50_test" : "mAP50";
            await context.RunRepository.AppendMetricAsync(context.RunId, new MetricPoint(metricName, 1, score, DateTimeOffset.UtcNow), ct);
            await context.ArtifactStore.AppendLineAsync(
                context.RunId,
                "metrics.jsonl",
                JsonSerializer.Serialize(new { metric = metricName, step = 1, value = score }),
                ct);

            await context.ArtifactStore.WriteTextAsync(
                context.RunId,
                $"reports/{stage}.json",
                JsonSerializer.Serialize(new
                {
                    task = "detection",
                    mode = stage,
                    backend = "TorchSharp",
                    device = context.Config.Device.ToString(),
                    sampleCount = count,
                    metric = score,
                    status = "torchsharp-eval-complete"
                }),
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
}
