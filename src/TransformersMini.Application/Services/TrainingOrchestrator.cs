using Microsoft.Extensions.Logging;
using TransformersMini.Contracts.Abstractions;
using TransformersMini.Contracts.Configurations;
using TransformersMini.Contracts.Runtime;
using TransformersMini.Domain.Capabilities;
using TransformersMini.SharedKernel.Core;

namespace TransformersMini.Application.Services;

public sealed class TrainingOrchestrator : ITrainingOrchestrator
{
    private readonly ITrainingConfigLoader _configLoader;
    private readonly IEnumerable<ITrainingTask> _tasks;
    private readonly IEnumerable<IDataAdapter> _dataAdapters;
    private readonly IEnumerable<IBackendCapability> _backendCapabilities;
    private readonly IRunRepository _runRepository;
    private readonly IArtifactStore _artifactStore;
    private readonly ISystemProbe _systemProbe;
    private readonly ILogger<TrainingOrchestrator> _logger;

    public TrainingOrchestrator(
        ITrainingConfigLoader configLoader,
        IEnumerable<ITrainingTask> tasks,
        IEnumerable<IDataAdapter> dataAdapters,
        IEnumerable<IBackendCapability> backendCapabilities,
        IRunRepository runRepository,
        IArtifactStore artifactStore,
        ISystemProbe systemProbe,
        ILogger<TrainingOrchestrator> logger)
    {
        _configLoader = configLoader;
        _tasks = tasks;
        _dataAdapters = dataAdapters;
        _backendCapabilities = backendCapabilities;
        _runRepository = runRepository;
        _artifactStore = artifactStore;
        _systemProbe = systemProbe;
        _logger = logger;
    }

    public async Task<RunResult> ExecuteAsync(RunTrainingCommand command, CancellationToken ct)
    {
        var (config, resolvedJson) = await _configLoader.LoadAsync(command, ct);
        ResolveDevice(config);

        var capabilityError = BackendCapabilityValidator.Validate(_backendCapabilities, config.Backend, config.Task, config.Mode);
        if (capabilityError is not null)
        {
            throw new InvalidOperationException(capabilityError);
        }

        var adapter = _dataAdapters.FirstOrDefault(x => x.DatasetFormat.Equals(config.Dataset.Format, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException($"No data adapter registered for dataset format '{config.Dataset.Format}'.");
        var task = _tasks.FirstOrDefault(x => x.TaskType == config.Task && x.Supports(config.Mode))
            ?? throw new InvalidOperationException($"No training task registered for {config.Task}/{config.Mode}.");

        var runId = string.IsNullOrWhiteSpace(command.RequestedRunId) ? BuildRunId(config) : command.RequestedRunId!;
        var metadata = new RunMetadata
        {
            RunId = runId,
            RunName = string.IsNullOrWhiteSpace(config.RunName) ? runId : config.RunName,
            ConfigPath = Path.GetFullPath(command.ConfigPath),
            Mode = config.Mode,
            Task = config.Task,
            Backend = config.Backend,
            Device = config.Device,
            StartedAt = DateTimeOffset.UtcNow,
            Status = RunStatus.Pending
        };

        metadata.RunDirectory = await _artifactStore.PrepareRunDirectoryAsync(metadata, ct);
        await _runRepository.CreateRunAsync(metadata, ct);
        await _artifactStore.WriteTextAsync(runId, "resolved-config.json", resolvedJson, ct);
        await _runRepository.AppendEventAsync(runId, new RunEvent("Information", "RunCreated", "Run metadata created.", DateTimeOffset.UtcNow), ct);

        if (command.DryRun)
        {
            await _artifactStore.WriteTextAsync(runId, "reports/summary.json", "{\"status\":\"dry-run\"}", ct);
            await _runRepository.UpdateStatusAsync(runId, RunStatus.Succeeded, "Dry run validation completed.", DateTimeOffset.UtcNow, ct);
            return new RunResult(runId, RunStatus.Succeeded, "Dry run validation completed.", metadata.RunDirectory);
        }

        var data = await adapter.LoadAsync(config.Dataset, ct);
        var context = new TrainingExecutionContext
        {
            RunId = runId,
            Config = config,
            Metadata = metadata,
            RunRepository = _runRepository,
            ArtifactStore = _artifactStore,
            Data = data,
            RunDirectory = metadata.RunDirectory,
            ResolvedConfigJson = resolvedJson,
            DryRun = false
        };

        await _runRepository.AppendEventAsync(
            runId,
            new RunEvent("Information", "RunStarting", $"Task={config.Task}, Mode={config.Mode}, Device={config.Device}, Dataset={config.Dataset.Format}", DateTimeOffset.UtcNow),
            ct);
        await _runRepository.UpdateStatusAsync(runId, RunStatus.Running, "Task started.", null, ct);

        try
        {
            var result = await task.ExecuteAsync(context, ct);
            await _runRepository.UpdateStatusAsync(runId, result.Status, result.Message, DateTimeOffset.UtcNow, ct);
            return result;
        }
        catch (OperationCanceledException)
        {
            await _runRepository.UpdateStatusAsync(runId, RunStatus.Canceled, "Canceled by user.", DateTimeOffset.UtcNow, CancellationToken.None);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Run {RunId} failed.", runId);
            var diagnosticMessage =
                $"Unhandled exception: {ex.GetType().Name} | Task={config.Task} | Mode={config.Mode} | Device={config.Device} | ConfigPath={command.ConfigPath}";
            await _runRepository.AppendEventAsync(
                runId,
                new RunEvent("Error", "UnhandledException", $"{diagnosticMessage} | Message={ex.Message}", DateTimeOffset.UtcNow),
                CancellationToken.None);
            await _runRepository.UpdateStatusAsync(runId, RunStatus.Failed, diagnosticMessage, DateTimeOffset.UtcNow, CancellationToken.None);
            throw;
        }
    }

    private void ResolveDevice(TrainingConfig config)
    {
        if (config.Device == DeviceType.Cuda && !_systemProbe.IsCudaAvailable())
        {
            throw new InvalidOperationException("CUDA requested but not available.");
        }

        if (config.Device == DeviceType.Auto)
        {
            config.Device = _systemProbe.IsCudaAvailable() ? DeviceType.Cuda : DeviceType.Cpu;
        }
    }

    private static string BuildRunId(TrainingConfig config)
    {
        var prefix = config.Task.ToString().ToLowerInvariant();
        var id = $"{DateTimeOffset.UtcNow:yyyyMMdd-HHmmss}-{prefix}-{Guid.NewGuid():N}";
        return id.Length <= 48 ? id : id[..48];
    }
}
