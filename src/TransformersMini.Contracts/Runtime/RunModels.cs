using TransformersMini.Contracts.Configurations;
using TransformersMini.Contracts.Data;
using TransformersMini.Contracts.Abstractions;
using TransformersMini.SharedKernel.Core;

namespace TransformersMini.Contracts.Runtime;

public sealed class RunTrainingCommand
{
    public string ConfigPath { get; init; } = string.Empty;
    public string? OverrideJson { get; init; }
    public bool DryRun { get; init; }
    public string? RequestedRunId { get; init; }
    public string? RequestedRunName { get; init; }
    public RunMode? ForcedMode { get; init; }
    public DeviceType? ForcedDevice { get; init; }
}

public sealed record RunResult(string RunId, RunStatus Status, string Message, string RunDirectory);

public sealed class RunMetadata
{
    public string RunId { get; set; } = string.Empty;
    public string RunName { get; set; } = string.Empty;
    public string ConfigPath { get; set; } = string.Empty;
    public string RunDirectory { get; set; } = string.Empty;
    public RunMode Mode { get; set; }
    public TaskType Task { get; set; }
    public BackendType Backend { get; set; }
    public DeviceType Device { get; set; }
    public RunStatus Status { get; set; } = RunStatus.Pending;
    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset? EndedAt { get; set; }
    public string? Message { get; set; }
}

public sealed record MetricPoint(string Name, long Step, double Value, DateTimeOffset Timestamp);
public sealed record RunEvent(string Level, string EventType, string Message, DateTimeOffset Timestamp);

public class RunSummaryDto
{
    public string RunId { get; set; } = string.Empty;
    public string RunName { get; set; } = string.Empty;
    public string Task { get; set; } = string.Empty;
    public string Backend { get; set; } = string.Empty;
    public string Mode { get; set; } = string.Empty;
    public string Device { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset? EndedAt { get; set; }
    public string RunDirectory { get; set; } = string.Empty;
    public string ConfigPath { get; set; } = string.Empty;
}

public sealed class RunDetailDto : RunSummaryDto
{
    public List<MetricPoint> Metrics { get; set; } = new();
    public List<RunEvent> Events { get; set; } = new();
    public string? Message { get; set; }
}

public sealed class TrainingExecutionContext
{
    public required string RunId { get; init; }
    public required TrainingConfig Config { get; init; }
    public required RunMetadata Metadata { get; init; }
    public required IRunRepository RunRepository { get; init; }
    public required IArtifactStore ArtifactStore { get; init; }
    public required DataSplitBundle Data { get; init; }
    public required string RunDirectory { get; init; }
    public required string ResolvedConfigJson { get; init; }
    public bool DryRun { get; init; }
}
