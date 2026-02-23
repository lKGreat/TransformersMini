using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using System.Text.Json;
using TransformersMini.Application.DependencyInjection;
using TransformersMini.Contracts.Abstractions;
using TransformersMini.Contracts.Runtime;
using TransformersMini.Infrastructure.DependencyInjection;
using Xunit;

namespace TransformersMini.Tests.Integration;

public sealed class StubRunPipelineTests
{
    [Fact]
    public async Task DryRun_GeneratesRunDirectoryAndSummary()
    {
        var repoRoot = FindRepoRoot();
        var configPath = Path.Combine(repoRoot, "configs", "detection", "sample.det.train.json");
        var runsRoot = CreateTestRunsRoot(repoRoot);

        try
        {
            using var provider = BuildProvider(runsRoot);

            var orchestrator = provider.GetRequiredService<ITrainingOrchestrator>();
            var result = await orchestrator.ExecuteAsync(new RunTrainingCommand
            {
                ConfigPath = configPath,
                DryRun = true,
                RequestedRunId = $"it-{Guid.NewGuid():N}"[..18]
            }, CancellationToken.None);

            Assert.Equal(TransformersMini.SharedKernel.Core.RunStatus.Succeeded, result.Status);
            Assert.True(Directory.Exists(result.RunDirectory));
            Assert.True(File.Exists(Path.Combine(result.RunDirectory, "resolved-config.json")));
            Assert.True(File.Exists(Path.Combine(result.RunDirectory, "reports", "summary.json")));
        }
        finally
        {
            TryDeleteDirectory(runsRoot);
        }
    }

    [Fact]
    public async Task DryRun_WritesRunMetadataIntoSqlite()
    {
        var repoRoot = FindRepoRoot();
        var configPath = Path.Combine(repoRoot, "configs", "detection", "sample.det.train.json");
        var runsRoot = CreateTestRunsRoot(repoRoot);

        try
        {
            using var provider = BuildProvider(runsRoot);

            var runId = $"it-{Guid.NewGuid():N}"[..18];
            var orchestrator = provider.GetRequiredService<ITrainingOrchestrator>();
            var runRepository = provider.GetRequiredService<IRunRepository>();
            var result = await orchestrator.ExecuteAsync(new RunTrainingCommand
            {
                ConfigPath = configPath,
                DryRun = true,
                RequestedRunId = runId
            }, CancellationToken.None);

            var detail = await runRepository.GetAsync(result.RunId, CancellationToken.None);
            Assert.NotNull(detail);
            Assert.Contains(detail!.Tags, x => x.Key == "system.backend");
            Assert.Contains(detail.Artifacts, x => x.Path.EndsWith("resolved-config.json", StringComparison.OrdinalIgnoreCase));
            Assert.Contains(detail.Artifacts, x => x.Path.Replace('\\', '/').EndsWith("reports/summary.json", StringComparison.OrdinalIgnoreCase));

            var dbPath = Path.Combine(runsRoot, "runs.db");
            Assert.True(File.Exists(dbPath));

            await using var conn = new SqliteConnection($"Data Source={dbPath}");
            await conn.OpenAsync();
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT COUNT(1) FROM runs WHERE run_id = $run_id;";
            cmd.Parameters.AddWithValue("$run_id", result.RunId);
            var count = Convert.ToInt32(await cmd.ExecuteScalarAsync());
            Assert.True(count >= 1);
        }
        finally
        {
            TryDeleteDirectory(runsRoot);
        }
    }

    [Theory]
    [InlineData("largest")]
    [InlineData("average")]
    public async Task Train_WritesLatestLossMetricIntoSqlite(string targetBoxStrategy)
    {
        var repoRoot = FindRepoRoot();
        var tempRoot = Path.Combine(repoRoot, "data", "samples", "integration-train", Guid.NewGuid().ToString("N"));
        var imageDir = Path.Combine(tempRoot, "images");
        var annDir = Path.Combine(tempRoot, "annotations");
        var runDir = Path.Combine(tempRoot, "runs");
        Directory.CreateDirectory(imageDir);
        Directory.CreateDirectory(annDir);
        Directory.CreateDirectory(runDir);

        var imagePath = Path.Combine(imageDir, "sample-1.png");
        using (var image = new Image<Rgb24>(32, 32))
        {
            for (var y = 0; y < 32; y++)
            {
                for (var x = 0; x < 32; x++)
                {
                    image[x, y] = new Rgb24((byte)(x * 4), (byte)(y * 4), 120);
                }
            }

            await image.SaveAsPngAsync(imagePath);
        }

        var annotationPath = Path.Combine(annDir, "train.json");
        await File.WriteAllTextAsync(annotationPath, JsonSerializer.Serialize(new
        {
            images = new[]
            {
                new { id = 1, file_name = Path.Combine("images", "sample-1.png").Replace('\\', '/'), width = 32, height = 32 }
            },
            annotations = new[]
            {
                new { id = 1, image_id = 1, category_id = 0, bbox = new[] { 8, 8, 16, 16 } },
                new { id = 2, image_id = 1, category_id = 0, bbox = new[] { 4, 4, 8, 8 } }
            },
            categories = new[]
            {
                new { id = 0, name = "box" }
            }
        }));

        var configPath = Path.Combine(tempRoot, "train.json");
        await File.WriteAllTextAsync(configPath, JsonSerializer.Serialize(new
        {
            configVersion = "1.0",
            runName = "it-detection-train",
            mode = "Train",
            task = "Detection",
            backend = "TorchSharp",
            device = "Cpu",
            dataset = new
            {
                format = "coco",
                rootPath = tempRoot,
                annotationPath = Path.Combine("annotations", "train.json").Replace('\\', '/'),
                trainSplit = "train",
                valSplit = "val",
                testSplit = "test",
                skipInvalidSamples = false
            },
            model = new
            {
                name = "tiny-cnn",
                architecture = "tiny-cnn-detector",
                parameters = new { inputSize = 32 }
            },
            optimization = new
            {
                epochs = 1,
                batchSize = 1,
                learningRate = 0.001,
                seed = 7
            },
            output = new
            {
                baseRunDirectory = runDir,
                experimentGroup = "integration"
            },
            taskOptions = new
            {
                inputSize = 32,
                normalizeMean = new[] { 0.5, 0.5, 0.5 },
                normalizeStd = new[] { 0.5, 0.5, 0.5 },
                resizeSampler = "bilinear",
                targetBoxStrategy,
                targetTopK = 2
            }
        }));

        try
        {
            using var provider = BuildProvider(runDir);

            var orchestrator = provider.GetRequiredService<ITrainingOrchestrator>();
            var runRepository = provider.GetRequiredService<IRunRepository>();
            var result = await orchestrator.ExecuteAsync(new RunTrainingCommand
            {
                ConfigPath = configPath,
                RequestedRunId = $"it-{Guid.NewGuid():N}"[..18]
            }, CancellationToken.None);

            Assert.Equal(TransformersMini.SharedKernel.Core.RunStatus.Succeeded, result.Status);

            var detail = await runRepository.GetAsync(result.RunId, CancellationToken.None);
            Assert.NotNull(detail);
            Assert.Contains(detail!.LatestMetrics, x => x.Name == "loss");
            Assert.Contains(detail.LatestMetrics, x => x.Name == "loss_bbox");
            Assert.Contains(detail.LatestMetrics, x => x.Name == "loss_category");
            Assert.Contains(detail.LatestMetrics, x => x.Name == "loss_objectness");
            Assert.Contains(detail.Artifacts, x => x.Kind == "report");
            Assert.Contains(detail.Tags, x => x.Key == "det.preprocess.target_box_strategy" && x.Value == targetBoxStrategy);
            Assert.Contains(detail.Tags, x => x.Key == "det.preprocess.resize_sampler" && x.Value == "bilinear");
            Assert.Contains(detail.Tags, x => x.Key == "det.target.top_k" && x.Value == "2");
            Assert.Contains(detail.Events, x => x.EventType == "PreprocessingConfig");

            var summaryPath = Path.Combine(result.RunDirectory, "reports", "summary.json");
            Assert.True(File.Exists(summaryPath));
            using var summaryDoc = JsonDocument.Parse(await File.ReadAllTextAsync(summaryPath));
            Assert.Equal("torchsharp-train-complete", summaryDoc.RootElement.GetProperty("status").GetString());
            var preprocessing = summaryDoc.RootElement.GetProperty("preprocessing");
            Assert.Equal(32, GetPropertyIgnoreCase(preprocessing, "InputSize").GetInt32());
            Assert.Equal(targetBoxStrategy, preprocessing.GetProperty("targetBoxStrategy").GetString());
            Assert.Equal("bilinear", preprocessing.GetProperty("resizeSampler").GetString());
            var targetEncoding = summaryDoc.RootElement.GetProperty("targetEncoding");
            Assert.Equal(2, targetEncoding.GetProperty("topK").GetInt32());
            Assert.Equal(6, targetEncoding.GetProperty("valuesPerBox").GetInt32());
            Assert.Equal(12, targetEncoding.GetProperty("flattenedSize").GetInt32());
            var lossWeights = summaryDoc.RootElement.GetProperty("lossWeights");
            Assert.Equal(1d, GetPropertyIgnoreCase(lossWeights, "Bbox").GetDouble());
            Assert.Equal(0.5d, GetPropertyIgnoreCase(lossWeights, "Category").GetDouble());
            Assert.Equal(1d, GetPropertyIgnoreCase(lossWeights, "Objectness").GetDouble());
            var lossSummary = summaryDoc.RootElement.GetProperty("lossSummary");
            Assert.True(GetPropertyIgnoreCase(lossSummary, "AverageTotalLoss").GetDouble() >= 0d);
            Assert.True(GetPropertyIgnoreCase(lossSummary, "AverageBboxLoss").GetDouble() >= 0d);
            Assert.True(GetPropertyIgnoreCase(lossSummary, "AverageCategoryLoss").GetDouble() >= 0d);
            Assert.True(GetPropertyIgnoreCase(lossSummary, "AverageObjectnessLoss").GetDouble() >= 0d);
            Assert.True(GetPropertyIgnoreCase(lossSummary, "TrainStepCount").GetInt32() >= 1);
            Assert.Equal("multi-branch", summaryDoc.RootElement.GetProperty("headType").GetString());

            var modelMetadataPath = Path.Combine(result.RunDirectory, "artifacts", "model-metadata.json");
            Assert.True(File.Exists(modelMetadataPath));
            using var modelMetadataDoc = JsonDocument.Parse(await File.ReadAllTextAsync(modelMetadataPath));
            Assert.Equal("TorchSharp", GetPropertyIgnoreCase(modelMetadataDoc.RootElement, "Framework").GetString());
            Assert.Equal("detection", GetPropertyIgnoreCase(modelMetadataDoc.RootElement, "Task").GetString());
            Assert.True(GetPropertyIgnoreCase(modelMetadataDoc.RootElement, "Preprocessing").ValueKind == JsonValueKind.Object);
            Assert.True(GetPropertyIgnoreCase(GetPropertyIgnoreCase(modelMetadataDoc.RootElement, "Preprocessing"), "InputSize").ValueKind != JsonValueKind.Undefined);
            Assert.True(GetPropertyIgnoreCase(modelMetadataDoc.RootElement, "TargetEncoding").ValueKind == JsonValueKind.Object);
            Assert.True(GetPropertyIgnoreCase(GetPropertyIgnoreCase(modelMetadataDoc.RootElement, "TargetEncoding"), "TopK").ValueKind != JsonValueKind.Undefined);
        }
        finally
        {
            TryDeleteDirectory(tempRoot);
        }
    }

    [Fact]
    public async Task Validate_WritesApproxEvalMetricsReport()
    {
        var repoRoot = FindRepoRoot();
        var tempRoot = Path.Combine(repoRoot, "data", "samples", "integration-validate", Guid.NewGuid().ToString("N"));
        var imageDir = Path.Combine(tempRoot, "images");
        var annDir = Path.Combine(tempRoot, "annotations");
        var runDir = Path.Combine(tempRoot, "runs");
        Directory.CreateDirectory(imageDir);
        Directory.CreateDirectory(annDir);
        Directory.CreateDirectory(runDir);

        var imagePath = Path.Combine(imageDir, "sample-1.png");
        using (var image = new Image<Rgb24>(32, 32))
        {
            for (var y = 0; y < 32; y++)
            {
                for (var x = 0; x < 32; x++)
                {
                    image[x, y] = new Rgb24((byte)(x * 3), (byte)(y * 3), 100);
                }
            }

            await image.SaveAsPngAsync(imagePath);
        }

        var annotationPath = Path.Combine(annDir, "train.json");
        await File.WriteAllTextAsync(annotationPath, JsonSerializer.Serialize(new
        {
            images = new[]
            {
                new { id = 1, file_name = Path.Combine("images", "sample-1.png").Replace('\\', '/'), width = 32, height = 32 }
            },
            annotations = new[]
            {
                new { id = 1, image_id = 1, category_id = 0, bbox = new[] { 8, 8, 16, 16 } },
                new { id = 2, image_id = 1, category_id = 0, bbox = new[] { 12, 12, 8, 8 } }
            },
            categories = new[]
            {
                new { id = 0, name = "box" }
            }
        }));

        var configPath = Path.Combine(tempRoot, "validate.json");
        await File.WriteAllTextAsync(configPath, JsonSerializer.Serialize(new
        {
            configVersion = "1.0",
            runName = "it-detection-validate",
            mode = "Validate",
            task = "Detection",
            backend = "TorchSharp",
            device = "Cpu",
            dataset = new
            {
                format = "coco",
                rootPath = tempRoot,
                annotationPath = Path.Combine("annotations", "train.json").Replace('\\', '/'),
                trainSplit = "train",
                valSplit = "val",
                testSplit = "test",
                skipInvalidSamples = false
            },
            model = new
            {
                name = "tiny-cnn",
                architecture = "tiny-cnn-detector",
                parameters = new { inputSize = 32 }
            },
            optimization = new
            {
                epochs = 1,
                batchSize = 1,
                learningRate = 0.001,
                seed = 11
            },
            output = new
            {
                baseRunDirectory = runDir,
                experimentGroup = "integration"
            },
            taskOptions = new
            {
                inputSize = 32,
                resizeSampler = "bilinear",
                targetBoxStrategy = "largest",
                targetTopK = 2
            }
        }));

        try
        {
            using var provider = BuildProvider(runDir);
            var orchestrator = provider.GetRequiredService<ITrainingOrchestrator>();
            var runRepository = provider.GetRequiredService<IRunRepository>();

            var result = await orchestrator.ExecuteAsync(new RunTrainingCommand
            {
                ConfigPath = configPath,
                RequestedRunId = $"it-{Guid.NewGuid():N}"[..18]
            }, CancellationToken.None);

            Assert.Equal(TransformersMini.SharedKernel.Core.RunStatus.Succeeded, result.Status);

            var detail = await runRepository.GetAsync(result.RunId, CancellationToken.None);
            Assert.NotNull(detail);
            Assert.Contains(detail!.LatestMetrics, x => x.Name == "precision50");
            Assert.Contains(detail.LatestMetrics, x => x.Name == "recall50");
            Assert.Contains(detail.LatestMetrics, x => x.Name == "meanIoU");

            var reportPath = Path.Combine(result.RunDirectory, "reports", "validate.json");
            Assert.True(File.Exists(reportPath));
            using var reportDoc = JsonDocument.Parse(await File.ReadAllTextAsync(reportPath));
            Assert.Equal("approx-iou-pr", reportDoc.RootElement.GetProperty("MetricType").GetString());
            var metrics = reportDoc.RootElement.GetProperty("Metrics");
            Assert.True(metrics.TryGetProperty("MeanIou", out _));
            Assert.True(metrics.TryGetProperty("PrecisionAtIou50", out _));
            Assert.True(metrics.TryGetProperty("RecallAtIou50", out _));
            var sampleDetails = reportDoc.RootElement.GetProperty("SampleDetails");
            Assert.True(sampleDetails.ValueKind == JsonValueKind.Array);
            Assert.True(sampleDetails.GetArrayLength() >= 1);
            var firstDetail = sampleDetails[0];
            Assert.True(firstDetail.TryGetProperty("SampleId", out _));
            Assert.True(firstDetail.TryGetProperty("TruePositive", out _));
            Assert.True(firstDetail.TryGetProperty("MeanMatchedIou", out _));
        }
        finally
        {
            TryDeleteDirectory(tempRoot);
        }
    }

    [Fact]
    public async Task Ocr_Train_WritesMetricsAndSummary()
    {
        var repoRoot = FindRepoRoot();
        var tempRoot = Path.Combine(repoRoot, "data", "samples", "integration-ocr-train", Guid.NewGuid().ToString("N"));
        var runDir = Path.Combine(tempRoot, "runs");
        Directory.CreateDirectory(tempRoot);
        Directory.CreateDirectory(runDir);

        var imageTrain1 = Path.Combine(tempRoot, "ocr-train-1.png");
        var imageTrain2 = Path.Combine(tempRoot, "ocr-train-2.png");
        var imageVal1 = Path.Combine(tempRoot, "ocr-val-1.png");
        await CreateOcrImageAsync(imageTrain1, "ab12");
        await CreateOcrImageAsync(imageTrain2, "bc23");
        await CreateOcrImageAsync(imageVal1, "ab23");

        var manifestPath = Path.Combine(tempRoot, "manifest.jsonl");
        var manifestLines = new[]
        {
            JsonSerializer.Serialize(new { id = "ocr-train-1", imagePath = "ocr-train-1.png", text = "ab12", split = "train" }),
            JsonSerializer.Serialize(new { id = "ocr-train-2", imagePath = "ocr-train-2.png", text = "bc23", split = "train" }),
            JsonSerializer.Serialize(new { id = "ocr-val-1", imagePath = "ocr-val-1.png", text = "ab23", split = "val" })
        };
        await File.WriteAllLinesAsync(manifestPath, manifestLines);

        var configPath = Path.Combine(tempRoot, "ocr-train.json");
        await File.WriteAllTextAsync(configPath, JsonSerializer.Serialize(new
        {
            configVersion = "1.0",
            runName = "it-ocr-train",
            mode = "Train",
            task = "Ocr",
            backend = "TorchSharp",
            device = "Cpu",
            dataset = new
            {
                format = "ocr-manifest-v1",
                manifestPath
            },
            model = new
            {
                name = "ocr-mvp",
                architecture = "tiny-ocr-cnn",
                parameters = new { }
            },
            optimization = new
            {
                epochs = 1,
                batchSize = 1,
                learningRate = 0.001,
                seed = 13
            },
            output = new
            {
                baseRunDirectory = runDir,
                experimentGroup = "integration"
            },
            taskOptions = new
            {
                inputHeight = 16,
                inputWidth = 64,
                maxTextLength = 8,
                charset = "abc123",
                resizeSampler = "bilinear"
            }
        }));

        try
        {
            using var provider = BuildProvider(runDir);
            var orchestrator = provider.GetRequiredService<ITrainingOrchestrator>();
            var runRepository = provider.GetRequiredService<IRunRepository>();

            var result = await orchestrator.ExecuteAsync(new RunTrainingCommand
            {
                ConfigPath = configPath,
                RequestedRunId = $"it-{Guid.NewGuid():N}"[..18]
            }, CancellationToken.None);

            Assert.Equal(TransformersMini.SharedKernel.Core.RunStatus.Succeeded, result.Status);
            var detail = await runRepository.GetAsync(result.RunId, CancellationToken.None);
            Assert.NotNull(detail);
            Assert.Contains(detail!.LatestMetrics, x => x.Name == "ocr_loss");
            Assert.Contains(detail.LatestMetrics, x => x.Name == "cer_train");
            Assert.Contains(detail.LatestMetrics, x => x.Name == "wer_train");
            Assert.Contains(detail.Tags, x => x.Key == "ocr.input.height" && x.Value == "16");
            Assert.Contains(detail.Tags, x => x.Key == "ocr.resize_sampler" && x.Value == "bilinear");
            Assert.Contains(detail.Events, x => x.EventType == "OcrEpochCompleted");

            var summaryPath = Path.Combine(result.RunDirectory, "reports", "summary.json");
            Assert.True(File.Exists(summaryPath));
            using var summaryDoc = JsonDocument.Parse(await File.ReadAllTextAsync(summaryPath));
            Assert.Equal("torchsharp-ocr-train-complete", summaryDoc.RootElement.GetProperty("Status").GetString());
            var options = summaryDoc.RootElement.GetProperty("Options");
            Assert.Equal(16, options.GetProperty("InputHeight").GetInt32());
            Assert.Equal(64, options.GetProperty("InputWidth").GetInt32());
        }
        finally
        {
            TryDeleteDirectory(tempRoot);
        }
    }

    [Fact]
    public async Task Ocr_Validate_WritesCerWerReport()
    {
        var repoRoot = FindRepoRoot();
        var tempRoot = Path.Combine(repoRoot, "data", "samples", "integration-ocr-validate", Guid.NewGuid().ToString("N"));
        var runDir = Path.Combine(tempRoot, "runs");
        Directory.CreateDirectory(tempRoot);
        Directory.CreateDirectory(runDir);

        var imageTrain1 = Path.Combine(tempRoot, "ocr-train-1.png");
        var imageVal1 = Path.Combine(tempRoot, "ocr-val-1.png");
        await CreateOcrImageAsync(imageTrain1, "a1");
        await CreateOcrImageAsync(imageVal1, "b2");

        var manifestPath = Path.Combine(tempRoot, "manifest.jsonl");
        var manifestLines = new[]
        {
            JsonSerializer.Serialize(new { id = "ocr-train-1", imagePath = "ocr-train-1.png", text = "a1", split = "train" }),
            JsonSerializer.Serialize(new { id = "ocr-val-1", imagePath = "ocr-val-1.png", text = "b2", split = "val" }),
            JsonSerializer.Serialize(new { id = "ocr-test-1", imagePath = "ocr-val-1.png", text = "b2", split = "test" })
        };
        await File.WriteAllLinesAsync(manifestPath, manifestLines);

        var configPath = Path.Combine(tempRoot, "ocr-validate.json");
        await File.WriteAllTextAsync(configPath, JsonSerializer.Serialize(new
        {
            configVersion = "1.0",
            runName = "it-ocr-validate",
            mode = "Validate",
            task = "Ocr",
            backend = "TorchSharp",
            device = "Cpu",
            dataset = new
            {
                format = "ocr-manifest-v1",
                manifestPath
            },
            model = new
            {
                name = "ocr-mvp",
                architecture = "tiny-ocr-cnn"
            },
            optimization = new
            {
                epochs = 1,
                batchSize = 1,
                learningRate = 0.001,
                seed = 17
            },
            output = new
            {
                baseRunDirectory = runDir,
                experimentGroup = "integration"
            },
            taskOptions = new
            {
                inputHeight = 16,
                inputWidth = 64,
                maxTextLength = 8,
                charset = "ab12",
                resizeSampler = "nearest"
            }
        }));

        try
        {
            using var provider = BuildProvider(runDir);
            var orchestrator = provider.GetRequiredService<ITrainingOrchestrator>();
            var runRepository = provider.GetRequiredService<IRunRepository>();

            var result = await orchestrator.ExecuteAsync(new RunTrainingCommand
            {
                ConfigPath = configPath,
                RequestedRunId = $"it-{Guid.NewGuid():N}"[..18]
            }, CancellationToken.None);

            Assert.Equal(TransformersMini.SharedKernel.Core.RunStatus.Succeeded, result.Status);
            var detail = await runRepository.GetAsync(result.RunId, CancellationToken.None);
            Assert.NotNull(detail);
            Assert.Contains(detail!.LatestMetrics, x => x.Name == "cer");
            Assert.Contains(detail.LatestMetrics, x => x.Name == "wer");

            var reportPath = Path.Combine(result.RunDirectory, "reports", "validate.json");
            Assert.True(File.Exists(reportPath));
            using var reportDoc = JsonDocument.Parse(await File.ReadAllTextAsync(reportPath));
            Assert.Equal("approx-ocr-char-seq", reportDoc.RootElement.GetProperty("MetricType").GetString());
            var metrics = reportDoc.RootElement.GetProperty("Metrics");
            Assert.True(metrics.TryGetProperty("Cer", out _));
            Assert.True(metrics.TryGetProperty("Wer", out _));
        }
        finally
        {
            TryDeleteDirectory(tempRoot);
        }
    }

    private static string FindRepoRoot()
    {
        var current = AppContext.BaseDirectory;
        for (var i = 0; i < 8; i++)
        {
            var candidate = current;
            for (var j = 0; j < i; j++)
            {
                candidate = Path.GetFullPath(Path.Combine(candidate, ".."));
            }

            if (File.Exists(Path.Combine(candidate, "TransformersMini.slnx")))
            {
                return candidate;
            }
        }

        throw new DirectoryNotFoundException("Repository root not found.");
    }

    private static ServiceProvider BuildProvider(string runsRoot)
    {
        var services = new ServiceCollection();
        services.AddTransformersMiniApplication();
        services.AddTransformersMiniPlatform(runsRoot);
        return services.BuildServiceProvider();
    }

    private static string CreateTestRunsRoot(string repoRoot)
    {
        var path = Path.Combine(repoRoot, "data", "samples", "integration-runs", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private static void TryDeleteDirectory(string path)
    {
        if (!Directory.Exists(path))
        {
            return;
        }

        try
        {
            Directory.Delete(path, recursive: true);
        }
        catch
        {
            // 中文说明：测试清理失败不影响断言结果。
        }
    }

    private static async Task CreateOcrImageAsync(string path, string text)
    {
        const int width = 64;
        const int height = 16;
        using var image = new Image<Rgb24>(width, height);
        var seed = Math.Abs(text.GetHashCode(StringComparison.Ordinal));

        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var value = (byte)((seed + (x * 7) + (y * 13)) % 255);
                image[x, y] = new Rgb24(value, (byte)(255 - value), (byte)((value / 2) + 20));
            }
        }

        await image.SaveAsPngAsync(path);
    }

    private static JsonElement GetPropertyIgnoreCase(JsonElement element, string propertyName)
    {
        foreach (var property in element.EnumerateObject())
        {
            if (string.Equals(property.Name, propertyName, StringComparison.OrdinalIgnoreCase))
            {
                return property.Value;
            }
        }

        throw new KeyNotFoundException($"Property not found: {propertyName}");
    }
}
