using System.Text.Json;
using TransformersMini.SharedKernel.Core;

namespace TransformersMini.Contracts.Configurations;

public sealed class TrainingConfig
{
    public string ConfigVersion { get; set; } = "1.0";
    public string RunName { get; set; } = string.Empty;
    public RunMode Mode { get; set; }
    public TaskType Task { get; set; }
    public BackendType Backend { get; set; }
    public DeviceType Device { get; set; } = DeviceType.Auto;
    public DatasetConfig Dataset { get; set; } = new();
    public ModelConfig Model { get; set; } = new();
    public OptimizationConfig Optimization { get; set; } = new();
    public RuntimeConfig Runtime { get; set; } = new();
    public OutputConfig Output { get; set; } = new();
    public LoggingConfig Logging { get; set; } = new();
    public Dictionary<string, JsonElement> TaskOptions { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

public sealed class DatasetConfig
{
    public string Format { get; set; } = string.Empty;
    public string RootPath { get; set; } = string.Empty;
    public string AnnotationPath { get; set; } = string.Empty;
    public string? ManifestPath { get; set; }
    public string TrainSplit { get; set; } = "train";
    public string ValSplit { get; set; } = "val";
    public string TestSplit { get; set; } = "test";
    public bool SkipInvalidSamples { get; set; }
}

public sealed class ModelConfig
{
    public string Name { get; set; } = "baseline";
    public string Architecture { get; set; } = string.Empty;
    public string? PretrainedPath { get; set; }
    public Dictionary<string, JsonElement> Parameters { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

public sealed class OptimizationConfig
{
    public int Epochs { get; set; } = 1;
    public int BatchSize { get; set; } = 1;
    public double LearningRate { get; set; } = 0.001;
    public int Seed { get; set; } = 42;
}

public sealed class RuntimeConfig
{
    public int MaxWorkers { get; set; } = 1;
    public bool Deterministic { get; set; } = true;
    public bool SaveCheckpoints { get; set; }
    public int CheckpointEveryEpochs { get; set; } = 1;
}

public sealed class OutputConfig
{
    public string BaseRunDirectory { get; set; } = "runs";
    public string? ExperimentGroup { get; set; }
}

public sealed class LoggingConfig
{
    public string Level { get; set; } = "Information";
    public bool WriteJsonLogs { get; set; }
}
