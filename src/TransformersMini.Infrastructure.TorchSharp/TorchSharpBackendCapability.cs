using TransformersMini.Contracts.Abstractions;
using TransformersMini.SharedKernel.Core;

namespace TransformersMini.Infrastructure.TorchSharp;

public sealed class TorchSharpBackendCapability : IBackendCapability
{
    public BackendType BackendType => BackendType.TorchSharp;

    public bool Supports(TaskType task, RunMode mode) => task is TaskType.Detection or TaskType.Ocr;

    public string[] GetLimitations(TaskType task) =>
        task == TaskType.Ocr
            ? ["OCR 已实现 TorchSharp MVP（轻量 CNN + 近似字符序列评估），暂非最终识别模型。"]
            : Array.Empty<string>();
}
