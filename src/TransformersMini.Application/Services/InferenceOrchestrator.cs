using Microsoft.Extensions.Logging;
using TransformersMini.Contracts.Abstractions;
using TransformersMini.Contracts.Configurations;
using TransformersMini.Contracts.Runtime;
using TransformersMini.SharedKernel.Core;

namespace TransformersMini.Application.Services;

/// <summary>
/// 统一推理编排入口，供 CLI infer 命令和 WinForms 推理面板调用。
/// 负责：加载配置、创建 run 记录、分发到具体任务推理实现、落盘报告。
/// </summary>
public sealed class InferenceOrchestrator : IInferenceOrchestrator
{
    private readonly ITrainingConfigLoader _configLoader;
    private readonly IEnumerable<IInferenceTask> _inferenceTasks;
    private readonly IEnumerable<IDataAdapter> _dataAdapters;
    private readonly IRunRepository _runRepository;
    private readonly IArtifactStore _artifactStore;
    private readonly ILogger<InferenceOrchestrator> _logger;

    public InferenceOrchestrator(
        ITrainingConfigLoader configLoader,
        IEnumerable<IInferenceTask> inferenceTasks,
        IEnumerable<IDataAdapter> dataAdapters,
        IRunRepository runRepository,
        IArtifactStore artifactStore,
        ILogger<InferenceOrchestrator> logger)
    {
        _configLoader = configLoader;
        _inferenceTasks = inferenceTasks;
        _dataAdapters = dataAdapters;
        _runRepository = runRepository;
        _artifactStore = artifactStore;
        _logger = logger;
    }

    public async Task<RunResult> ExecuteAsync(RunInferenceCommand command, CancellationToken ct)
    {
        // 使用与训练相同的配置加载器解析配置
        var trainCommand = new RunTrainingCommand
        {
            ConfigPath = command.ConfigPath,
            ForcedDevice = command.ForcedDevice,
            RequestedRunId = command.RequestedRunId,
            RequestedRunName = command.RequestedRunName
        };
        TrainingConfig config;
        string resolvedJson;
        try
        {
            (config, resolvedJson) = await _configLoader.LoadAsync(trainCommand, ct);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("JSON Schema 校验失败", StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "推理配置文件格式不正确。请使用训练 run 目录下的 resolved-config.json（或原始训练配置），不要使用 artifacts/model-metadata.json 或 reports/*.json。",
                ex);
        }

        // 设备解析
        if (command.ForcedDevice.HasValue)
        {
            config.Device = command.ForcedDevice.Value;
        }

        string? singleImagePath = null;
        if (!string.IsNullOrWhiteSpace(command.SingleImagePath))
        {
            singleImagePath = Path.GetFullPath(command.SingleImagePath);
            if (!File.Exists(singleImagePath))
            {
                throw new FileNotFoundException("单图推理图片不存在。", singleImagePath);
            }
        }

        var runId = command.RequestedRunId ?? $"infer-{Guid.NewGuid():N}"[..22];
        var runName = command.RequestedRunName ?? $"infer-{config.Task}-{DateTime.UtcNow:yyyyMMdd-HHmmss}";
        var startedAt = DateTimeOffset.UtcNow;

        var metadata = new RunMetadata
        {
            RunId = runId,
            RunName = runName,
            ConfigPath = Path.GetFullPath(command.ConfigPath),
            Mode = RunMode.Infer,
            Task = config.Task,
            Backend = config.Backend,
            Device = config.Device,
            Status = RunStatus.Pending,
            StartedAt = startedAt
        };

        var runDirectory = await _artifactStore.PrepareRunDirectoryAsync(metadata, ct);
        metadata.RunDirectory = runDirectory;
        await _runRepository.CreateRunAsync(metadata, ct);
        await _artifactStore.WriteTextAsync(runId, "resolved-config.json", resolvedJson, ct);
        await _runRepository.AppendEventAsync(
            runId,
            new RunEvent("Information", "InferRunCreated", $"Task={config.Task}, Device={config.Device}, ModelRunDirectory={command.ModelRunDirectory}", DateTimeOffset.UtcNow),
            ct);

        try
        {
            await _runRepository.UpdateStatusAsync(runId, RunStatus.Running, "Inference task started.", null, ct);
            // 加载数据（推理时主要使用 test split，fallback 到 val）
            var adapter = _dataAdapters.FirstOrDefault(a => string.Equals(a.DatasetFormat, config.Dataset.Format, StringComparison.OrdinalIgnoreCase))
                ?? throw new InvalidOperationException($"未找到数据适配器：{config.Dataset.Format}");
            var data = await adapter.LoadAsync(config.Dataset, ct);

            var inferTask = _inferenceTasks.FirstOrDefault(t => t.TaskType == config.Task)
                ?? throw new InvalidOperationException($"未找到推理任务实现：{config.Task}");

            var context = new InferenceExecutionContext
            {
                RunId = runId,
                Config = config,
                Metadata = metadata,
                RunRepository = _runRepository,
                ArtifactStore = _artifactStore,
                Data = data,
                RunDirectory = runDirectory,
                ModelRunDirectory = string.IsNullOrWhiteSpace(command.ModelRunDirectory)
                    ? runDirectory
                    : Path.GetFullPath(command.ModelRunDirectory),
                MaxSamples = command.MaxSamples,
                SingleImagePath = singleImagePath
            };

            var result = await inferTask.ExecuteAsync(context, ct);

            await _runRepository.UpdateStatusAsync(runId, result.Status, result.Message, DateTimeOffset.UtcNow, ct);
            _logger.LogInformation("推理完成 RunId={RunId} Status={Status}", runId, result.Status);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "推理异常 RunId={RunId}", runId);
            var diagnosticMessage =
                $"Inference failed: {ex.GetType().Name} | Task={config.Task} | Device={config.Device} | ConfigPath={command.ConfigPath} | ModelRunDirectory={command.ModelRunDirectory}";
            await _runRepository.AppendEventAsync(
                runId,
                new RunEvent("Error", "InferenceUnhandledException", $"{diagnosticMessage} | Message={ex.Message}", DateTimeOffset.UtcNow),
                CancellationToken.None);
            await _runRepository.UpdateStatusAsync(runId, RunStatus.Failed, diagnosticMessage, DateTimeOffset.UtcNow, CancellationToken.None);
            throw;
        }
    }
}
