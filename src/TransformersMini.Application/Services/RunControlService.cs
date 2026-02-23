using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using TransformersMini.Contracts.Abstractions;
using TransformersMini.Contracts.Runtime;

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

    public Task<string> StartAsync(RunTrainingCommand command, CancellationToken ct)
    {
        var runId = command.RequestedRunId ?? $"job-{Guid.NewGuid():N}"[..16];
        var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct);

        if (!_running.TryAdd(runId, linkedCts))
        {
            throw new InvalidOperationException($"Run id already active: {runId}");
        }

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
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Run {RunId} failed.", runId);
            }
            finally
            {
                if (_running.TryRemove(runId, out var cts))
                {
                    cts.Dispose();
                }
            }
        }, CancellationToken.None);

        return Task.FromResult(runId);
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
