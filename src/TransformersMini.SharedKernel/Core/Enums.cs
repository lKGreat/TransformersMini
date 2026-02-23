namespace TransformersMini.SharedKernel.Core;

public enum RunMode
{
    Train,
    Validate,
    Test
}

public enum TaskType
{
    Detection,
    Ocr
}

public enum BackendType
{
    TorchSharp,
    MLNet
}

public enum DeviceType
{
    Auto,
    Cpu,
    Cuda
}

public enum RunStatus
{
    Pending,
    Running,
    Succeeded,
    Failed,
    Canceled
}
