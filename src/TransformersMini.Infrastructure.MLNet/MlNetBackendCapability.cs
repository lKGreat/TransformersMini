using TransformersMini.Contracts.Abstractions;
using TransformersMini.SharedKernel.Core;

namespace TransformersMini.Infrastructure.MLNet;

public sealed class MlNetBackendCapability : IBackendCapability
{
    public BackendType BackendType => BackendType.MLNet;

    public bool Supports(TaskType task, RunMode mode) => false;

    public string[] GetLimitations(TaskType task) =>
        task switch
        {
            TaskType.Detection => ["ML.NET 检测训练后端尚未实现，请改用 TorchSharp 后端。"],
            TaskType.Ocr => ["ML.NET OCR 训练后端尚未实现，请改用 TorchSharp 后端。"],
            _ => ["ML.NET 后端当前仅保留扩展位，尚未实现训练能力。"]
        };
}
