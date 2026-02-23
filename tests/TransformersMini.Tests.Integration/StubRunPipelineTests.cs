using Microsoft.Data.Sqlite;
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

    [Fact]
    public async Task DryRun_WritesRunMetadataIntoSqlite()
    {
        var repoRoot = FindRepoRoot();
        var configPath = Path.Combine(repoRoot, "configs", "detection", "sample.det.train.json");

        var services = new ServiceCollection();
        services.AddTransformersMiniPlatform();
        using var provider = services.BuildServiceProvider();

        var runId = $"it-{Guid.NewGuid():N}"[..18];
        var orchestrator = provider.GetRequiredService<ITrainingOrchestrator>();
        var runRepository = provider.GetRequiredService<IRunRepository>();
        var result = await orchestrator.ExecuteAsync(new RunTrainingCommand
        {
            ConfigPath = configPath,
            DryRun = true,
            RequestedRunId = runId
        }, CancellationToken.None);

        var detail = await runRepository.GetAsync(result.RunId, CancellationToken.None);
        Assert.NotNull(detail);

        var monthDir = Directory.GetParent(result.RunDirectory)!.FullName;
        var yearDir = Directory.GetParent(monthDir)!.FullName;
        var runRoot = Directory.GetParent(yearDir)!.FullName;
        var dbPath = Path.Combine(runRoot, "runs.db");
        Assert.True(File.Exists(dbPath));

        await using var conn = new SqliteConnection($"Data Source={dbPath}");
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT COUNT(1) FROM runs WHERE run_id = $run_id;";
        cmd.Parameters.AddWithValue("$run_id", result.RunId);
        var count = Convert.ToInt32(await cmd.ExecuteScalarAsync());
        Assert.True(count >= 1);
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

            if (File.Exists(Path.Combine(candidate, "TransformersMini.slnx")))
            {
                return candidate;
            }
        }

        throw new DirectoryNotFoundException("Repository root not found.");
    }
}
