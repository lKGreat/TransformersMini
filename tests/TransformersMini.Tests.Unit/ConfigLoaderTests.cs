using System.Text.Json;
using TransformersMini.Application.Services;
using TransformersMini.Contracts.Runtime;
using TransformersMini.SharedKernel.Core;
using Xunit;

namespace TransformersMini.Tests.Unit;

public sealed class ConfigLoaderTests
{
    [Fact]
    public async Task LoadAsync_AppliesForcedModeAndDevice()
    {
        var repoRoot = FindRepoRoot();
        var tempDir = Path.Combine(repoRoot, "data", "samples", "unit-tests");
        Directory.CreateDirectory(tempDir);
        var tempFile = Path.Combine(tempDir, $"config-{Guid.NewGuid():N}.json");
        try
        {
            await File.WriteAllTextAsync(tempFile, JsonSerializer.Serialize(new
            {
                configVersion = "1.0",
                mode = "Train",
                task = "Detection",
                backend = "TorchSharp",
                device = "Auto",
                dataset = new { format = "coco", rootPath = "." },
                optimization = new { epochs = 1, batchSize = 1, learningRate = 0.001 }
            }));

            var loader = new JsonTrainingConfigLoader();
            var (config, _) = await loader.LoadAsync(new RunTrainingCommand
            {
                ConfigPath = tempFile,
                ForcedMode = RunMode.Test,
                ForcedDevice = DeviceType.Cpu,
                RequestedRunName = "unit-test"
            }, CancellationToken.None);

            Assert.Equal(RunMode.Test, config.Mode);
            Assert.Equal(DeviceType.Cpu, config.Device);
            Assert.Equal("unit-test", config.RunName);
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    private static string FindRepoRoot()
    {
        var current = AppContext.BaseDirectory;
        for (var i = 0; i < 10; i++)
        {
            var candidate = current;
            for (var j = 0; j < i; j++)
            {
                candidate = Path.GetFullPath(Path.Combine(candidate, ".."));
            }

            if (File.Exists(Path.Combine(candidate, "TransformersMini.sln")) || File.Exists(Path.Combine(candidate, "TransformersMini.slnx")))
            {
                return candidate;
            }
        }

        throw new DirectoryNotFoundException("未找到仓库根目录。");
    }
}
