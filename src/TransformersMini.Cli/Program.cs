using Microsoft.Extensions.DependencyInjection;
using TransformersMini.Contracts.Abstractions;
using TransformersMini.Contracts.Runtime;
using TransformersMini.Infrastructure.DependencyInjection;
using TransformersMini.SharedKernel.Core;

var services = new ServiceCollection();
services.AddTransformersMiniPlatform();
using var provider = services.BuildServiceProvider();

if (args.Length == 0 || args[0] is "--help" or "-h")
{
    PrintHelp();
    return;
}

var command = args[0].ToLowerInvariant();

// infer 命令单独处理
if (command == "infer")
{
    var inferCommand = ParseInferArgs(args);
    var sysProbe = provider.GetRequiredService<ISystemProbe>();
    ValidateBuildModeAndDeviceForCli(inferCommand.ForcedDevice, sysProbe);
    var inferOrchestrator = provider.GetRequiredService<IInferenceOrchestrator>();
    var inferResult = await inferOrchestrator.ExecuteAsync(inferCommand, CancellationToken.None);
    Console.WriteLine($"RunId: {inferResult.RunId}");
    Console.WriteLine($"Status: {inferResult.Status}");
    Console.WriteLine($"Message: {inferResult.Message}");
    Console.WriteLine($"RunDir: {inferResult.RunDirectory}");
    return;
}

var (_, runCommand) = ParseArgs(args);
runCommand = new RunTrainingCommand
{
    ConfigPath = runCommand.ConfigPath,
    OverrideJson = runCommand.OverrideJson,
    DryRun = runCommand.DryRun,
    RequestedRunId = runCommand.RequestedRunId,
    RequestedRunName = runCommand.RequestedRunName,
    ForcedDevice = runCommand.ForcedDevice,
    ForcedMode = command switch
    {
        "train" => RunMode.Train,
        "validate" => RunMode.Validate,
        "test" => RunMode.Test,
        _ => runCommand.ForcedMode
    }
};

var systemProbe = provider.GetRequiredService<ISystemProbe>();
ValidateBuildModeAndDeviceForCli(runCommand.ForcedDevice, systemProbe);

var orchestrator = provider.GetRequiredService<ITrainingOrchestrator>();
var result = await orchestrator.ExecuteAsync(runCommand, CancellationToken.None);
Console.WriteLine($"RunId: {result.RunId}");
Console.WriteLine($"Status: {result.Status}");
Console.WriteLine($"Message: {result.Message}");
Console.WriteLine($"RunDir: {result.RunDirectory}");

static (string Command, RunTrainingCommand RunCommand) ParseArgs(string[] args)
{
    var cmd = args[0].ToLowerInvariant();
    var configPath = string.Empty;
    var dryRun = false;
    string? runName = null;
    DeviceType? device = null;

    for (var i = 1; i < args.Length; i++)
    {
        switch (args[i])
        {
            case "--config":
                configPath = args[++i];
                break;
            case "--dry-run":
                dryRun = true;
                break;
            case "--run-name":
                runName = args[++i];
                break;
            case "--device":
                device = args[++i].ToLowerInvariant() switch
                {
                    "cpu" => DeviceType.Cpu,
                    "cuda" => DeviceType.Cuda,
                    _ => DeviceType.Auto
                };
                break;
        }
    }

    if (string.IsNullOrWhiteSpace(configPath))
    {
        throw new InvalidOperationException("--config <path> is required.");
    }

    return (cmd, new RunTrainingCommand
    {
        ConfigPath = configPath,
        DryRun = dryRun,
        RequestedRunName = runName,
        ForcedDevice = device
    });
}

static RunInferenceCommand ParseInferArgs(string[] args)
{
    var configPath = string.Empty;
    var modelRunDirectory = string.Empty;
    string? runName = null;
    DeviceType? device = null;
    var maxSamples = 0;

    for (var i = 1; i < args.Length; i++)
    {
        switch (args[i])
        {
            case "--config":
                configPath = args[++i];
                break;
            case "--model-run-dir":
                modelRunDirectory = args[++i];
                break;
            case "--run-name":
                runName = args[++i];
                break;
            case "--device":
                device = args[++i].ToLowerInvariant() switch
                {
                    "cpu" => DeviceType.Cpu,
                    "cuda" => DeviceType.Cuda,
                    _ => DeviceType.Auto
                };
                break;
            case "--max-samples":
                if (int.TryParse(args[++i], out var ms))
                {
                    maxSamples = ms;
                }

                break;
        }
    }

    if (string.IsNullOrWhiteSpace(configPath))
    {
        throw new InvalidOperationException("infer --config <path> is required.");
    }

    return new RunInferenceCommand
    {
        ConfigPath = configPath,
        ModelRunDirectory = modelRunDirectory,
        RequestedRunName = runName,
        ForcedDevice = device,
        MaxSamples = maxSamples
    };
}

static void PrintHelp()
{
    Console.WriteLine("TransformersMini CLI");
    Console.WriteLine("Commands:");
    Console.WriteLine("  run --config <path> [--dry-run] [--device cpu|cuda|auto] [--run-name <name>]");
    Console.WriteLine("  train --config <path> [--dry-run]");
    Console.WriteLine("  validate --config <path> [--dry-run]");
    Console.WriteLine("  test --config <path> [--dry-run]");
    Console.WriteLine("  infer --config <path> [--model-run-dir <dir>] [--device cpu|cuda|auto] [--max-samples <n>] [--run-name <name>]");
}

static void ValidateBuildModeAndDeviceForCli(DeviceType? forcedDevice, ISystemProbe systemProbe)
{
    var requestedDevice = forcedDevice ?? DeviceType.Auto;

    if (requestedDevice == DeviceType.Cuda && !IsTorchSharpCudaBuild())
    {
        throw new InvalidOperationException(
            "当前 CLI 为 CPU 构建模式，但指定了 --device cuda。\n" +
            "请先执行：dotnet build .\\TransformersMini.slnx -c Release -p:UseTorchSharpCuda=true\n" +
            "然后再执行：dotnet run --project .\\src\\TransformersMini.Cli\\TransformersMini.Cli.csproj -c Release -p:UseTorchSharpCuda=true -- train --config <配置文件> --device cuda");
    }

    if (requestedDevice == DeviceType.Auto && !IsTorchSharpCudaBuild())
    {
        Console.WriteLine("提示：当前 CLI 为 CPU 构建模式，--device auto 将按 CPU 运行。");
    }

    if (requestedDevice == DeviceType.Cuda && !systemProbe.IsCudaAvailable())
    {
        throw new InvalidOperationException("当前机器未检测到可用 CUDA GPU/驱动，无法按 --device cuda 运行。");
    }
}

static bool IsTorchSharpCudaBuild()
{
#if TORCHSHARP_CUDA_BUILD
    return true;
#else
    return false;
#endif
}
