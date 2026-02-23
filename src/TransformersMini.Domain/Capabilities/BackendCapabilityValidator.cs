using TransformersMini.Contracts.Abstractions;
using TransformersMini.SharedKernel.Core;

namespace TransformersMini.Domain.Capabilities;

public static class BackendCapabilityValidator
{
    public static string? Validate(
        IEnumerable<IBackendCapability> capabilities,
        BackendType backend,
        TaskType task,
        RunMode mode)
    {
        var capability = capabilities.FirstOrDefault(x => x.BackendType == backend);
        if (capability is null)
        {
            return $"未注册后端能力提供器：{backend}。";
        }

        if (capability.Supports(task, mode))
        {
            return null;
        }

        var limitations = capability.GetLimitations(task);
        var detail = limitations.Length > 0
            ? $" 说明：{string.Join(" ", limitations)}"
            : string.Empty;
        var suggestion = backend != BackendType.TorchSharp
            ? " 建议：检测/OCR 训练优先使用 TorchSharp 后端。"
            : string.Empty;
        return $"不支持的任务/后端/模式组合：{task}/{backend}/{mode}。{detail}{suggestion}";
    }
}
