namespace TransformersMini.SharedKernel.Core;

public enum RunMode
{
    Train,
    Validate,
    Test,
    /// <summary>推理模式，区别于训练/验证/测试。</summary>
    Infer
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
