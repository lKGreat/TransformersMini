using TransformersMini.Contracts.Configurations;
using TransformersMini.Contracts.Data;
using TransformersMini.Contracts.Runtime;
using TransformersMini.SharedKernel.Core;

namespace TransformersMini.Contracts.Abstractions;

public interface ITrainingOrchestrator
{
    Task<RunResult> ExecuteAsync(RunTrainingCommand command, CancellationToken ct);
}

public interface IRunControlService
{
    Task<string> StartAsync(RunTrainingCommand command, CancellationToken ct);
    Task CancelAsync(string runId, CancellationToken ct);
    Task<IReadOnlyList<RunSummaryDto>> ListRunsAsync(CancellationToken ct);
    Task<RunDetailDto?> GetRunAsync(string runId, CancellationToken ct);
}

public interface ITrainingTask
{
    TaskType TaskType { get; }
    bool Supports(RunMode mode);
    Task<RunResult> ExecuteAsync(TrainingExecutionContext context, CancellationToken ct);
}

public interface IDataAdapter
{
    string DatasetFormat { get; }
    Task<DataSplitBundle> LoadAsync(DatasetConfig config, CancellationToken ct);
}

public interface IBackendCapability
{
    BackendType BackendType { get; }
    bool Supports(TaskType task, RunMode mode);
    string[] GetLimitations(TaskType task);
}

public interface IComputeBackendSession : IAsyncDisposable
{
    BackendType BackendType { get; }
    DeviceType Device { get; }
}

public interface IComputeBackendSessionFactory
{
    BackendType BackendType { get; }
    Task<IComputeBackendSession> CreateAsync(TrainingExecutionContext context, CancellationToken ct);
}

public interface IRunRepository
{
    Task<string> CreateRunAsync(RunMetadata metadata, CancellationToken ct);
    Task UpdateStatusAsync(string runId, RunStatus status, string? message, DateTimeOffset? endedAt, CancellationToken ct);
    Task UpsertTagAsync(string runId, string key, string value, CancellationToken ct);
    Task AppendMetricAsync(string runId, MetricPoint metric, CancellationToken ct);
    Task AppendEventAsync(string runId, RunEvent evt, CancellationToken ct);
    Task<IReadOnlyList<RunSummaryDto>> ListAsync(CancellationToken ct);
    Task<RunDetailDto?> GetAsync(string runId, CancellationToken ct);
}

public interface IArtifactStore
{
    Task<string> PrepareRunDirectoryAsync(RunMetadata metadata, CancellationToken ct);
    Task WriteTextAsync(string runId, string relativePath, string content, CancellationToken ct);
    Task AppendLineAsync(string runId, string relativePath, string line, CancellationToken ct);
}

public interface ITrainingConfigLoader
{
    Task<(TrainingConfig Config, string ResolvedJson)> LoadAsync(RunTrainingCommand command, CancellationToken ct);
}

public interface ISystemProbe
{
    bool IsCudaAvailable();
}

/// <summary>
/// 运行查询仓库接口，支持多维度过滤，替代逐条 GetAsync 扫描。
/// </summary>
public interface IRunQueryRepository
{
    /// <summary>按过滤条件分页查询运行列表。</summary>
    Task<RunQueryResult> QueryAsync(RunQueryFilter filter, CancellationToken ct);
}

/// <summary>
/// 推理编排器入口：接受推理命令并执行完整推理流程。
/// </summary>
public interface IInferenceOrchestrator
{
    Task<RunResult> ExecuteAsync(RunInferenceCommand command, CancellationToken ct);
}

/// <summary>
/// 具体任务推理实现（检测/OCR 各自实现此接口）。
/// </summary>
public interface IInferenceTask
{
    TaskType TaskType { get; }
    Task<RunResult> ExecuteAsync(InferenceExecutionContext context, CancellationToken ct);
}
