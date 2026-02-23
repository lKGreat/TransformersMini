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
public sealed record RunTagDto(string Key, string Value, DateTimeOffset UpdatedAt);
public sealed record RunArtifactDto(string Path, string Kind, long SizeBytes, DateTimeOffset UpdatedAt);
public sealed record LatestMetricDto(string Name, long Step, double Value, DateTimeOffset Timestamp);

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
    public List<RunTagDto> Tags { get; set; } = new();
    public List<RunArtifactDto> Artifacts { get; set; } = new();
    public List<LatestMetricDto> LatestMetrics { get; set; } = new();
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

/// <summary>
/// 运行查询过滤条件，首期支持任务/模式/后端/设备/状态/标签/时间范围/最新指标过滤。
/// </summary>
public sealed class RunQueryFilter
{
    /// <summary>任务类型过滤，null 表示不过滤。</summary>
    public string? Task { get; init; }
    /// <summary>运行模式过滤，null 表示不过滤。</summary>
    public string? Mode { get; init; }
    /// <summary>后端类型过滤，null 表示不过滤。</summary>
    public string? Backend { get; init; }
    /// <summary>设备类型过滤，null 表示不过滤。</summary>
    public string? Device { get; init; }
    /// <summary>运行状态过滤，null 表示不过滤。</summary>
    public string? Status { get; init; }
    /// <summary>标签键（模糊匹配），null 表示不过滤。</summary>
    public string? TagKey { get; init; }
    /// <summary>标签值（模糊匹配），null 表示不过滤。</summary>
    public string? TagValue { get; init; }
    /// <summary>开始时间下限，null 表示不过滤。</summary>
    public DateTimeOffset? StartedAfter { get; init; }
    /// <summary>开始时间上限，null 表示不过滤。</summary>
    public DateTimeOffset? StartedBefore { get; init; }
    /// <summary>分页：从第几条开始（0-based），默认 0。</summary>
    public int Offset { get; init; }
    /// <summary>分页：每页条数，0 表示不限制。</summary>
    public int Limit { get; init; }
    /// <summary>排序字段，默认 started_at DESC。</summary>
    public string? OrderBy { get; init; }
}

/// <summary>
/// 运行查询结果，包含分页结果与总数。
/// </summary>
public sealed class RunQueryResult
{
    /// <summary>当前页数据。</summary>
    public IReadOnlyList<RunSummaryDto> Items { get; init; } = [];
    /// <summary>满足过滤条件的总记录数（不含分页限制）。</summary>
    public int TotalCount { get; init; }
    /// <summary>当前偏移量。</summary>
    public int Offset { get; init; }
    /// <summary>每页限制。</summary>
    public int Limit { get; init; }
}

/// <summary>
/// CLI/WinForms 发起推理时构造的命令对象。
/// </summary>
public sealed class RunInferenceCommand
{
    /// <summary>训练配置路径，用于定位数据集与任务参数。</summary>
    public string ConfigPath { get; init; } = string.Empty;
    /// <summary>训练产物目录（artifacts/model-metadata.json 所在的 run 目录）。</summary>
    public string ModelRunDirectory { get; init; } = string.Empty;
    /// <summary>指定推理 run 目录，留空则自动生成。</summary>
    public string? RequestedRunId { get; init; }
    /// <summary>推理 run 名称。</summary>
    public string? RequestedRunName { get; init; }
    /// <summary>强制设备（覆盖配置）。</summary>
    public DeviceType? ForcedDevice { get; init; }
    /// <summary>推理样本数量上限，0 表示不限制。</summary>
    public int MaxSamples { get; init; }
}

/// <summary>
/// 推理任务执行上下文，传入 IInferenceTask.ExecuteAsync。
/// </summary>
public sealed class InferenceExecutionContext
{
    public required string RunId { get; init; }
    public required TrainingConfig Config { get; init; }
    public required RunMetadata Metadata { get; init; }
    public required IRunRepository RunRepository { get; init; }
    public required IArtifactStore ArtifactStore { get; init; }
    public required DataSplitBundle Data { get; init; }
    public required string RunDirectory { get; init; }
    /// <summary>训练产物目录，推理任务从此加载 model-metadata.json。</summary>
    public required string ModelRunDirectory { get; init; }
    /// <summary>推理样本数量上限，0 表示全量。</summary>
    public int MaxSamples { get; init; }
}
