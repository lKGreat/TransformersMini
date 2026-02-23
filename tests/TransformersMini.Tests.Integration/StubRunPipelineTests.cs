using Microsoft.Extensions.DependencyInjection;
using TransformersMini.Contracts.Abstractions;
using TransformersMini.Contracts.Runtime;
using TransformersMini.Infrastructure.DependencyInjection;
using Xunit;

namespace TransformersMini.Tests.Integration;

public sealed class StubRunPipelineTests
{
    [Fact]
    public async Task DryRun_GeneratesRunDirectoryAndSummary()
    {
        var repoRoot = FindRepoRoot();
        var configPath = Path.Combine(repoRoot, "configs", "detection", "sample.det.train.json");

        var services = new ServiceCollection();
        services.AddTransformersMiniPlatform();
        using var provider = services.BuildServiceProvider();

        var orchestrator = provider.GetRequiredService<ITrainingOrchestrator>();
        var result = await orchestrator.ExecuteAsync(new RunTrainingCommand
        {
            ConfigPath = configPath,
            DryRun = true,
            RequestedRunId = $"it-{Guid.NewGuid():N}"[..18]
        }, CancellationToken.None);

        Assert.Equal(TransformersMini.SharedKernel.Core.RunStatus.Succeeded, result.Status);
        Assert.True(Directory.Exists(result.RunDirectory));
        Assert.True(File.Exists(Path.Combine(result.RunDirectory, "resolved-config.json")));
        Assert.True(File.Exists(Path.Combine(result.RunDirectory, "reports", "summary.json")));
    }

    private static string FindRepoRoot()
    {
        var current = AppContext.BaseDirectory;
        for (var i = 0; i < 8; i++)
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

        throw new DirectoryNotFoundException("Repository root not found.");
    }
}
