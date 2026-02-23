using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using TransformersMini.Contracts.Abstractions;
using TransformersMini.Contracts.Configurations;
using TransformersMini.Contracts.Runtime;
using TransformersMini.SharedKernel.Core;

namespace TransformersMini.Application.Services;

public sealed class DataTrainingConfigBuilder : IDataTrainingConfigBuilder
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    static DataTrainingConfigBuilder()
    {
        JsonOptions.Converters.Add(new JsonStringEnumConverter());
    }

    public TrainingConfig Build(DataTrainingBuildRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.AnnotationPath))
        {
            throw new InvalidOperationException("标注文件路径不能为空。");
        }

        var isOcr = request.Task == TaskType.Ocr;
        var dataset = BuildDataset(request, isOcr);

        var config = new TrainingConfig
        {
            ConfigVersion = "1.0",
            RunName = string.IsNullOrWhiteSpace(request.RunName) ? string.Empty : request.RunName.Trim(),
            Mode = request.Mode,
            Task = request.Task,
            Backend = BackendType.TorchSharp,
            Device = request.Device,
            Dataset = dataset,
            Model = new ModelConfig
            {
                Name = ResolveModelName(request, isOcr),
                Architecture = ResolveArchitecture(request, isOcr)
            },
            Optimization = new OptimizationConfig
            {
                Epochs = request.Epochs > 0 ? request.Epochs : (isOcr ? 2 : 10),
                BatchSize = request.BatchSize > 0 ? request.BatchSize : (isOcr ? 8 : 2),
                LearningRate = request.LearningRate > 0 ? request.LearningRate : 0.001,
                Seed = 42
            },
            Runtime = new RuntimeConfig
            {
                MaxWorkers = 1,
                Deterministic = true,
                SaveCheckpoints = false,
                CheckpointEveryEpochs = 1
            },
            Output = new OutputConfig
            {
                BaseRunDirectory = "runs",
                ExperimentGroup = string.IsNullOrWhiteSpace(request.ExperimentGroup) ? "user-training" : request.ExperimentGroup.Trim()
            },
            Logging = new LoggingConfig
            {
                Level = "Information",
                WriteJsonLogs = false
            }
        };

        if (isOcr)
        {
            if (request.InputSize > 0)
            {
                config.Model.Parameters["inputSize"] = JsonSerializer.SerializeToElement(request.InputSize);
            }
        }
        else
        {
            config.TaskOptions["numClasses"] = JsonSerializer.SerializeToElement(request.NumClasses > 0 ? request.NumClasses : 2);
            config.Model.Parameters["inputSize"] = JsonSerializer.SerializeToElement(request.InputSize > 0 ? request.InputSize : 640);
        }

        return config;
    }

    public async Task<string> WriteTempConfigAsync(TrainingConfig config, string filePrefix, CancellationToken ct)
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "TransformersMini", "temp-configs");
        Directory.CreateDirectory(tempDir);
        var safePrefix = string.IsNullOrWhiteSpace(filePrefix) ? "train" : filePrefix.Trim().ToLowerInvariant();
        var tempPath = Path.Combine(tempDir, $"{safePrefix}-{DateTime.Now:yyyyMMdd-HHmmss}.json");
        var json = JsonSerializer.Serialize(config, JsonOptions);
        await File.WriteAllTextAsync(tempPath, json, ct);
        return tempPath;
    }

    private static DatasetConfig BuildDataset(DataTrainingBuildRequest request, bool isOcr)
    {
        if (isOcr)
        {
            return new DatasetConfig
            {
                Format = string.IsNullOrWhiteSpace(request.DatasetFormat) ? "ocr-manifest-v1" : request.DatasetFormat.Trim(),
                ManifestPath = request.AnnotationPath,
                RootPath = request.ImageRoot ?? string.Empty,
                AnnotationPath = request.AnnotationPath
            };
        }

        var rootPath = request.ImageRoot ?? string.Empty;
        return new DatasetConfig
        {
            Format = string.IsNullOrWhiteSpace(request.DatasetFormat) ? "coco" : request.DatasetFormat.Trim(),
            RootPath = rootPath,
            AnnotationPath = ToRelativeIfUnderRoot(rootPath, request.AnnotationPath),
            TrainSplit = "train",
            ValSplit = "val",
            TestSplit = "test",
            SkipInvalidSamples = false
        };
    }

    private static string ResolveArchitecture(DataTrainingBuildRequest request, bool isOcr)
    {
        if (!string.IsNullOrWhiteSpace(request.Architecture))
        {
            return request.Architecture.Trim();
        }

        return isOcr ? "crnn-like" : "yolo-like";
    }

    private static string ResolveModelName(DataTrainingBuildRequest request, bool isOcr)
    {
        if (!string.IsNullOrWhiteSpace(request.ModelName))
        {
            return request.ModelName.Trim();
        }

        return isOcr ? "ocr-custom" : "det-custom";
    }

    private static string ToRelativeIfUnderRoot(string rootPath, string fullFilePath)
    {
        if (string.IsNullOrWhiteSpace(rootPath))
        {
            return fullFilePath;
        }

        var normalizedRoot = Path.GetFullPath(rootPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var normalizedFile = Path.GetFullPath(fullFilePath);
        if (!normalizedFile.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase))
        {
            return normalizedFile;
        }

        return Path.GetRelativePath(normalizedRoot, normalizedFile);
    }
}
