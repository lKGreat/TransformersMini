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

var (command, runCommand) = ParseArgs(args);
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

var orchestrator = provider.GetRequiredService<ITrainingOrchestrator>();
var result = await orchestrator.ExecuteAsync(runCommand, CancellationToken.None);
Console.WriteLine($"RunId: {result.RunId}");
Console.WriteLine($"Status: {result.Status}");
Console.WriteLine($"Message: {result.Message}");
Console.WriteLine($"RunDir: {result.RunDirectory}");

static (string Command, RunTrainingCommand RunCommand) ParseArgs(string[] args)
{
    var command = args[0].ToLowerInvariant();
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

    return (command, new RunTrainingCommand
    {
        ConfigPath = configPath,
        DryRun = dryRun,
        RequestedRunName = runName,
        ForcedDevice = device
    });
}

static void PrintHelp()
{
    Console.WriteLine("TransformersMini CLI");
    Console.WriteLine("Commands:");
    Console.WriteLine("  run --config <path> [--dry-run] [--device cpu|cuda|auto] [--run-name <name>]");
    Console.WriteLine("  train --config <path> [--dry-run]");
    Console.WriteLine("  validate --config <path> [--dry-run]");
    Console.WriteLine("  test --config <path> [--dry-run]");
}
