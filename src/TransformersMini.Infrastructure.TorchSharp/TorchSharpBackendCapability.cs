using TransformersMini.Contracts.Abstractions;
using TransformersMini.SharedKernel.Core;

namespace TransformersMini.Infrastructure.TorchSharp;

public sealed class TorchSharpBackendCapability : IBackendCapability
{
    public BackendType BackendType => BackendType.TorchSharp;

    public bool Supports(TaskType task, RunMode mode) => task is TaskType.Detection or TaskType.Ocr;

    public string[] GetLimitations(TaskType task) =>
        task == TaskType.Ocr
            ? ["OCR training is stubbed in the foundation iteration."]
            : Array.Empty<string>();
}
