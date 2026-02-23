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
            return $"Backend capability provider not registered for {backend}.";
        }

        return capability.Supports(task, mode)
            ? null
            : $"Unsupported task/backend/mode combination: {task}/{backend}/{mode}.";
    }
}
