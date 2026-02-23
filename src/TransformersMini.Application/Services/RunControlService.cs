using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using TransformersMini.Contracts.Abstractions;
using TransformersMini.Contracts.Runtime;
using TransformersMini.SharedKernel.Core;

namespace TransformersMini.Application.Services;

public sealed class RunControlService : IRunControlService
{
    private readonly ITrainingOrchestrator _orchestrator;
    private readonly IRunRepository _runRepository;
    private readonly ILogger<RunControlService> _logger;
    private readonly ConcurrentDictionary<string, CancellationTokenSource> _running = new();

    public RunControlService(ITrainingOrchestrator orchestrator, IRunRepository runRepository, ILogger<RunControlService> logger)
    {
        _orchestrator = orchestrator;
        _runRepository = runRepository;
        _logger = logger;
    }

    public async Task<string> StartAsync(RunTrainingCommand command, CancellationToken ct)
    {
        var runId = command.RequestedRunId ?? $"job-{Guid.NewGuid():N}"[..16];
        var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct);

        if (!_running.TryAdd(runId, linkedCts))
        {
            throw new InvalidOperationException($"Run id already active: {runId}");
        }

        // 预注册 Pending 状态的 run，确保监控轮询一定能查到记录。
        // 编排器随后会 upsert 完善所有字段。
        var seedMetadata = new RunMetadata
        {
            RunId = runId,
            RunName = command.RequestedRunName ?? runId,
            ConfigPath = command.ConfigPath,
            Status = RunStatus.Pending,
            StartedAt = DateTimeOffset.UtcNow,
            Message = "Queued – waiting for orchestrator."
        };
        await _runRepository.CreateRunAsync(seedMetadata, ct);

        var actualCommand = new RunTrainingCommand
        {
            ConfigPath = command.ConfigPath,
            OverrideJson = command.OverrideJson,
            DryRun = command.DryRun,
            RequestedRunId = runId,
            RequestedRunName = command.RequestedRunName,
            ForcedMode = command.ForcedMode,
            ForcedDevice = command.ForcedDevice
        };

        _ = Task.Run(async () =>
        {
            try
            {
                await _orchestrator.ExecuteAsync(actualCommand, linkedCts.Token);
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Run {RunId} canceled.", runId);
                await TrySetTerminalAsync(runId, RunStatus.Canceled, "Canceled by user.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Run {RunId} failed.", runId);
                await TrySetTerminalAsync(runId, RunStatus.Failed, $"{ex.GetType().Name}: {ex.Message}");
            }
            finally
            {
                if (_running.TryRemove(runId, out var cts))
                {
                    cts.Dispose();
                }
            }
        }, CancellationToken.None);

        return runId;
    }

    private async Task TrySetTerminalAsync(string runId, RunStatus status, string message)
    {
        try
        {
            var level = status == RunStatus.Failed ? "Error" : "Warning";
            await _runRepository.AppendEventAsync(
                runId,
                new RunEvent(level, "RunTerminated", message, DateTimeOffset.UtcNow),
                CancellationToken.None);
            await _runRepository.UpdateStatusAsync(
                runId, status, message, DateTimeOffset.UtcNow, CancellationToken.None);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to mark run {RunId} as {Status} in DB.", runId, status);
        }
    }

    public Task CancelAsync(string runId, CancellationToken ct)
    {
        if (_running.TryGetValue(runId, out var cts))
        {
            cts.Cancel();
        }

        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<RunSummaryDto>> ListRunsAsync(CancellationToken ct) => _runRepository.ListAsync(ct);

    public Task<RunDetailDto?> GetRunAsync(string runId, CancellationToken ct) => _runRepository.GetAsync(runId, ct);
}
