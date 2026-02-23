using TransformersMini.Contracts.Abstractions;
using TransformersMini.SharedKernel.Core;

namespace TransformersMini.Infrastructure.MLNet;

public sealed class MlNetBackendCapability : IBackendCapability
{
    public BackendType BackendType => BackendType.MLNet;

    public bool Supports(TaskType task, RunMode mode) => task is TaskType.Detection or TaskType.Ocr;

    public string[] GetLimitations(TaskType task) =>
        ["ML.NET backend is a placeholder in M0/M1. Real training implementation is planned in later iterations."];
}
