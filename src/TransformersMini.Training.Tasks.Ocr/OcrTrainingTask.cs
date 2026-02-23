using System.Text.Json;
using TransformersMini.Contracts.Abstractions;
using TransformersMini.Contracts.Runtime;
using TransformersMini.SharedKernel.Core;

namespace TransformersMini.Training.Tasks.Ocr;

public sealed class OcrTrainingTask : ITrainingTask
{
    public TaskType TaskType => TaskType.Ocr;

    public bool Supports(RunMode mode) => true;

    public async Task<RunResult> ExecuteAsync(TrainingExecutionContext context, CancellationToken ct)
    {
        await context.RunRepository.AppendEventAsync(
            context.RunId,
            new RunEvent("Warning", "OcrStub", "OCR task is stubbed in the foundation iteration.", DateTimeOffset.UtcNow),
            ct);

        await context.RunRepository.AppendMetricAsync(
            context.RunId,
            new MetricPoint("cer", 1, 0.25, DateTimeOffset.UtcNow),
            ct);

        var report = JsonSerializer.Serialize(new
        {
            task = "ocr",
            mode = context.Config.Mode.ToString(),
            backend = context.Config.Backend.ToString(),
            device = context.Config.Device.ToString(),
            status = "stub-complete"
        });
        var reportName = context.Config.Mode == RunMode.Train ? "reports/summary.json" : $"reports/{context.Config.Mode.ToString().ToLowerInvariant()}.json";
        await context.ArtifactStore.WriteTextAsync(context.RunId, reportName, report, ct);

        return new RunResult(context.RunId, RunStatus.Succeeded, "OCR stub pipeline completed.", context.RunDirectory);
    }
}
