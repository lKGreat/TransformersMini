using Microsoft.Extensions.DependencyInjection;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using TransformersMini.Contracts.Abstractions;
using TransformersMini.Contracts.Configurations;
using TransformersMini.Contracts.Runtime;
using TransformersMini.Infrastructure.DependencyInjection;
using TransformersMini.SharedKernel.Core;

var services = new ServiceCollection();
services.AddTransformersMiniPlatform();
using var provider = services.BuildServiceProvider();

if (args.Length == 0 || args[0] is "--help" or "-h")
{
    PrintHelp();
    return;
}

var command = args[0].ToLowerInvariant();

// infer 命令单独处理
if (command == "infer")
{
    var inferCommand = ParseInferArgs(args);
    var sysProbe = provider.GetRequiredService<ISystemProbe>();
    ValidateBuildModeAndDeviceForCli(inferCommand.ForcedDevice, sysProbe);
    var inferOrchestrator = provider.GetRequiredService<IInferenceOrchestrator>();
    var inferResult = await inferOrchestrator.ExecuteAsync(inferCommand, CancellationToken.None);
    Console.WriteLine($"RunId: {inferResult.RunId}");
    Console.WriteLine($"Status: {inferResult.Status}");
    Console.WriteLine($"Message: {inferResult.Message}");
    Console.WriteLine($"RunDir: {inferResult.RunDirectory}");
    return;
}

// data-based 训练命令：直接从标注数据构造训练配置
if (command is "train-data" or "validate-data" or "test-data")
{
    var dataTraining = ParseDataTrainingArgs(args);
    var effectiveDevice = NormalizeDeviceForCliBuild(dataTraining.Device);
    dataTraining.Device = effectiveDevice;
    var runMode = command switch
    {
        "validate-data" => RunMode.Validate,
        "test-data" => RunMode.Test,
        _ => RunMode.Train
    };

    ValidateBuildModeAndDeviceForCli(effectiveDevice, provider.GetRequiredService<ISystemProbe>());
    var generatedConfigPath = await WriteTempTrainingConfigAsync(BuildTrainingConfigFromData(dataTraining, runMode));
    var dataOrchestrator = provider.GetRequiredService<ITrainingOrchestrator>();
    var runResult = await dataOrchestrator.ExecuteAsync(new RunTrainingCommand
    {
        ConfigPath = generatedConfigPath,
        RequestedRunName = dataTraining.RunName,
        ForcedDevice = effectiveDevice,
        ForcedMode = runMode
    }, CancellationToken.None);

    Console.WriteLine($"TempConfig: {generatedConfigPath}");
    Console.WriteLine($"RunId: {runResult.RunId}");
    Console.WriteLine($"Status: {runResult.Status}");
    Console.WriteLine($"Message: {runResult.Message}");
    Console.WriteLine($"RunDir: {runResult.RunDirectory}");
    return;
}

var (_, runCommand) = ParseArgs(args);
runCommand = new RunTrainingCommand
{
    ConfigPath = runCommand.ConfigPath,
    OverrideJson = runCommand.OverrideJson,
    DryRun = runCommand.DryRun,
    RequestedRunId = runCommand.RequestedRunId,
    RequestedRunName = runCommand.RequestedRunName,
    ForcedDevice = runCommand.ForcedDevice,
    ForcedMode = command switch
    {
        "train" => RunMode.Train,
        "validate" => RunMode.Validate,
        "test" => RunMode.Test,
        _ => runCommand.ForcedMode
    }
};

var systemProbe = provider.GetRequiredService<ISystemProbe>();
ValidateBuildModeAndDeviceForCli(runCommand.ForcedDevice, systemProbe);

var orchestrator = provider.GetRequiredService<ITrainingOrchestrator>();
var result = await orchestrator.ExecuteAsync(runCommand, CancellationToken.None);
Console.WriteLine($"RunId: {result.RunId}");
Console.WriteLine($"Status: {result.Status}");
Console.WriteLine($"Message: {result.Message}");
Console.WriteLine($"RunDir: {result.RunDirectory}");

static (string Command, RunTrainingCommand RunCommand) ParseArgs(string[] args)
{
    var cmd = args[0].ToLowerInvariant();
    var configPath = string.Empty;
    var dryRun = false;
    string? runName = null;
    DeviceType? device = null;

    for (var i = 1; i < args.Length; i++)
    {
        switch (args[i])
        {
            case "--config":
                configPath = args[++i];
                break;
            case "--dry-run":
                dryRun = true;
                break;
            case "--run-name":
                runName = args[++i];
                break;
            case "--device":
                device = args[++i].ToLowerInvariant() switch
                {
                    "cpu" => DeviceType.Cpu,
                    "cuda" => DeviceType.Cuda,
                    _ => DeviceType.Auto
                };
                break;
        }
    }

    if (string.IsNullOrWhiteSpace(configPath))
    {
        throw new InvalidOperationException("--config <path> is required.");
    }

    return (cmd, new RunTrainingCommand
    {
        ConfigPath = configPath,
        DryRun = dryRun,
        RequestedRunName = runName,
        ForcedDevice = device
    });
}

static RunInferenceCommand ParseInferArgs(string[] args)
{
    var configPath = string.Empty;
    var modelRunDirectory = string.Empty;
    string? runName = null;
    DeviceType? device = null;
    var maxSamples = 0;

    for (var i = 1; i < args.Length; i++)
    {
        switch (args[i])
        {
            case "--config":
                configPath = args[++i];
                break;
            case "--model-run-dir":
                modelRunDirectory = args[++i];
                break;
            case "--run-name":
                runName = args[++i];
                break;
            case "--device":
                device = args[++i].ToLowerInvariant() switch
                {
                    "cpu" => DeviceType.Cpu,
                    "cuda" => DeviceType.Cuda,
                    _ => DeviceType.Auto
                };
                break;
            case "--max-samples":
                if (int.TryParse(args[++i], out var ms))
                {
                    maxSamples = ms;
                }

                break;
        }
    }

    if (string.IsNullOrWhiteSpace(configPath))
    {
        throw new InvalidOperationException("infer --config <path> is required.");
    }

    return new RunInferenceCommand
    {
        ConfigPath = configPath,
        ModelRunDirectory = modelRunDirectory,
        RequestedRunName = runName,
        ForcedDevice = device,
        MaxSamples = maxSamples
    };
}

static void PrintHelp()
{
    Console.WriteLine("TransformersMini CLI");
    Console.WriteLine("Commands:");
    Console.WriteLine("  run --config <path> [--dry-run] [--device cpu|cuda|auto] [--run-name <name>]");
    Console.WriteLine("  train --config <path> [--dry-run]");
    Console.WriteLine("  validate --config <path> [--dry-run]");
    Console.WriteLine("  test --config <path> [--dry-run]");
    Console.WriteLine("  train-data --task detection|ocr --annotation <path> --image-root <dir> [--dataset-format coco|ocr-manifest-v1] [--device cpu|cuda|auto] [--run-name <name>] [--arch <name>] [--input-size <n>] [--num-classes <n>] [--epochs <n>] [--batch-size <n>] [--learning-rate <v>]");
    Console.WriteLine("  validate-data --task detection|ocr --annotation <path> --image-root <dir> [same options as train-data]");
    Console.WriteLine("  test-data --task detection|ocr --annotation <path> --image-root <dir> [same options as train-data]");
    Console.WriteLine("  infer --config <path> [--model-run-dir <dir>] [--device cpu|cuda|auto] [--max-samples <n>] [--run-name <name>]");
}

static void ValidateBuildModeAndDeviceForCli(DeviceType? forcedDevice, ISystemProbe systemProbe)
{
    var requestedDevice = forcedDevice ?? DeviceType.Auto;

    if (requestedDevice == DeviceType.Cuda && !IsTorchSharpCudaBuild())
    {
        throw new InvalidOperationException(
            "当前 CLI 为 CPU 构建模式，但指定了 --device cuda。\n" +
            "请先执行：dotnet build .\\TransformersMini.slnx -c Release -p:UseTorchSharpCuda=true\n" +
            "然后再执行：dotnet run --project .\\src\\TransformersMini.Cli\\TransformersMini.Cli.csproj -c Release -p:UseTorchSharpCuda=true -- train --config <配置文件> --device cuda");
    }

    if (requestedDevice == DeviceType.Auto && !IsTorchSharpCudaBuild())
    {
        Console.WriteLine("提示：当前 CLI 为 CPU 构建模式，--device auto 将按 CPU 运行。");
    }

    if (requestedDevice == DeviceType.Cuda && !systemProbe.IsCudaAvailable())
    {
        throw new InvalidOperationException("当前机器未检测到可用 CUDA GPU/驱动，无法按 --device cuda 运行。");
    }
}

static bool IsTorchSharpCudaBuild()
{
#if TORCHSHARP_CUDA_BUILD
    return true;
#else
    return false;
#endif
}

static DeviceType NormalizeDeviceForCliBuild(DeviceType? requestedDevice)
{
    var requested = requestedDevice ?? DeviceType.Auto;
    if (!IsTorchSharpCudaBuild() && requested == DeviceType.Auto)
    {
        return DeviceType.Cpu;
    }

    return requested;
}

static DataTrainingCliArgs ParseDataTrainingArgs(string[] args)
{
    var result = new DataTrainingCliArgs();
    for (var i = 1; i < args.Length; i++)
    {
        switch (args[i])
        {
            case "--task":
                result.Task = args[++i].ToLowerInvariant();
                break;
            case "--annotation":
                result.AnnotationPath = args[++i];
                break;
            case "--image-root":
                result.ImageRoot = args[++i];
                break;
            case "--dataset-format":
                result.DatasetFormat = args[++i];
                break;
            case "--device":
                result.Device = args[++i].ToLowerInvariant() switch
                {
                    "cpu" => DeviceType.Cpu,
                    "cuda" => DeviceType.Cuda,
                    _ => DeviceType.Auto
                };
                break;
            case "--run-name":
                result.RunName = args[++i];
                break;
            case "--arch":
                result.Architecture = args[++i];
                break;
            case "--input-size":
                if (int.TryParse(args[++i], out var inputSize))
                {
                    result.InputSize = inputSize;
                }

                break;
            case "--num-classes":
                if (int.TryParse(args[++i], out var numClasses))
                {
                    result.NumClasses = numClasses;
                }

                break;
            case "--epochs":
                if (int.TryParse(args[++i], out var epochs))
                {
                    result.Epochs = epochs;
                }

                break;
            case "--batch-size":
                if (int.TryParse(args[++i], out var batchSize))
                {
                    result.BatchSize = batchSize;
                }

                break;
            case "--learning-rate":
                if (double.TryParse(args[++i], NumberStyles.Float, CultureInfo.InvariantCulture, out var learningRate))
                {
                    result.LearningRate = learningRate;
                }

                break;
        }
    }

    if (string.IsNullOrWhiteSpace(result.Task))
    {
        throw new InvalidOperationException("--task detection|ocr is required.");
    }

    if (string.IsNullOrWhiteSpace(result.AnnotationPath))
    {
        throw new InvalidOperationException("--annotation <path> is required.");
    }

    if (!File.Exists(result.AnnotationPath))
    {
        throw new FileNotFoundException("Annotation file not found.", result.AnnotationPath);
    }

    if (string.Equals(result.Task, "detection", StringComparison.OrdinalIgnoreCase))
    {
        if (string.IsNullOrWhiteSpace(result.ImageRoot))
        {
            throw new InvalidOperationException("Detection requires --image-root <dir>.");
        }

        if (!Directory.Exists(result.ImageRoot))
        {
            throw new DirectoryNotFoundException($"Image root not found: {result.ImageRoot}");
        }
    }

    return result;
}

static TrainingConfig BuildTrainingConfigFromData(DataTrainingCliArgs args, RunMode runMode)
{
    var isOcr = string.Equals(args.Task, "ocr", StringComparison.OrdinalIgnoreCase);
    var dataset = new DatasetConfig();
    if (isOcr)
    {
        dataset.Format = string.IsNullOrWhiteSpace(args.DatasetFormat) ? "ocr-manifest-v1" : args.DatasetFormat;
        dataset.ManifestPath = args.AnnotationPath;
        dataset.RootPath = args.ImageRoot ?? string.Empty;
        dataset.AnnotationPath = args.AnnotationPath;
    }
    else
    {
        var rootPath = args.ImageRoot ?? string.Empty;
        dataset.Format = string.IsNullOrWhiteSpace(args.DatasetFormat) ? "coco" : args.DatasetFormat;
        dataset.RootPath = rootPath;
        dataset.AnnotationPath = ToRelativeIfUnderRoot(rootPath, args.AnnotationPath);
        dataset.TrainSplit = "train";
        dataset.ValSplit = "val";
        dataset.TestSplit = "test";
        dataset.SkipInvalidSamples = false;
    }

    var config = new TrainingConfig
    {
        ConfigVersion = "1.0",
        RunName = string.IsNullOrWhiteSpace(args.RunName) ? string.Empty : args.RunName,
        Mode = runMode,
        Task = isOcr ? TaskType.Ocr : TaskType.Detection,
        Backend = BackendType.TorchSharp,
        Device = args.Device ?? DeviceType.Auto,
        Dataset = dataset,
        Model = new ModelConfig
        {
            Name = isOcr ? "ocr-cli-data" : "det-cli-data",
            Architecture = string.IsNullOrWhiteSpace(args.Architecture) ? (isOcr ? "crnn-like" : "yolo-like") : args.Architecture
        },
        Optimization = new OptimizationConfig
        {
            Epochs = args.Epochs > 0 ? args.Epochs : (isOcr ? 2 : 10),
            BatchSize = args.BatchSize > 0 ? args.BatchSize : (isOcr ? 8 : 2),
            LearningRate = args.LearningRate > 0 ? args.LearningRate : 0.001,
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
            ExperimentGroup = "cli-data-training"
        },
        Logging = new LoggingConfig
        {
            Level = "Information",
            WriteJsonLogs = false
        }
    };

    if (!isOcr)
    {
        var numClasses = args.NumClasses > 0 ? args.NumClasses : 2;
        config.TaskOptions["numClasses"] = JsonSerializer.SerializeToElement(numClasses);
        if (args.InputSize > 0)
        {
            config.Model.Parameters["inputSize"] = JsonSerializer.SerializeToElement(args.InputSize);
        }
        else
        {
            config.Model.Parameters["inputSize"] = JsonSerializer.SerializeToElement(640);
        }
    }

    return config;
}

static async Task<string> WriteTempTrainingConfigAsync(TrainingConfig config)
{
    var tempDir = Path.Combine(Path.GetTempPath(), "TransformersMini", "temp-configs");
    Directory.CreateDirectory(tempDir);
    var tempPath = Path.Combine(tempDir, $"cli-train-{DateTime.Now:yyyyMMdd-HHmmss}.json");
    var options = new JsonSerializerOptions
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };
    options.Converters.Add(new JsonStringEnumConverter());
    await File.WriteAllTextAsync(tempPath, JsonSerializer.Serialize(config, options));
    return tempPath;
}

static string ToRelativeIfUnderRoot(string rootPath, string fullFilePath)
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

file sealed class DataTrainingCliArgs
{
    public string Task { get; set; } = string.Empty;
    public string AnnotationPath { get; set; } = string.Empty;
    public string? ImageRoot { get; set; }
    public string? DatasetFormat { get; set; }
    public DeviceType? Device { get; set; }
    public string? RunName { get; set; }
    public string? Architecture { get; set; }
    public int InputSize { get; set; } = 640;
    public int NumClasses { get; set; } = 2;
    public int Epochs { get; set; } = 0;
    public int BatchSize { get; set; } = 0;
    public double LearningRate { get; set; } = 0;
}
