using TransformersMini.SharedKernel.Core;

namespace TransformersMini.Contracts.Runtime;

public sealed class DataTrainingBuildRequest
{
    public TaskType Task { get; init; } = TaskType.Detection;
    public RunMode Mode { get; init; } = RunMode.Train;
    public string AnnotationPath { get; init; } = string.Empty;
    public string? ImageRoot { get; init; }
    public string? DatasetFormat { get; init; }
    public DeviceType Device { get; init; } = DeviceType.Auto;
    public string? RunName { get; init; }
    public string? Architecture { get; init; }
    public int InputSize { get; init; } = 640;
    public int NumClasses { get; init; } = 2;
    public int Epochs { get; init; }
    public int BatchSize { get; init; }
    public double LearningRate { get; init; }
    public string ExperimentGroup { get; init; } = "user-training";
    public string ModelName { get; init; } = string.Empty;
}
