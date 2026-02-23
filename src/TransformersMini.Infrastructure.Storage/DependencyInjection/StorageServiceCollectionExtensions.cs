using Microsoft.Extensions.DependencyInjection;
using TransformersMini.Contracts.Abstractions;
using TransformersMini.Infrastructure.Storage.Storage;

namespace TransformersMini.Infrastructure.Storage.DependencyInjection;

public static class StorageServiceCollectionExtensions
{
    public static IServiceCollection AddTransformersMiniStorage(this IServiceCollection services, string? runsRoot = null)
    {
        if (string.IsNullOrWhiteSpace(runsRoot))
        {
            var defaultRoot = Path.Combine(Environment.CurrentDirectory, "runs");
            var defaultDbPath = Path.Combine(defaultRoot, "runs.db");
            services.AddSingleton<IArtifactStore, FileArtifactStore>();
            services.AddSingleton<IRunRepository, SqliteRunRepository>();
            services.AddSingleton<IRunQueryRepository>(_ => new SqliteRunQueryRepository(defaultDbPath));
            return services;
        }

        var normalizedRoot = Path.GetFullPath(runsRoot);
        var dbPath = Path.Combine(normalizedRoot, "runs.db");
        services.AddSingleton<IArtifactStore>(_ => new FileArtifactStore(normalizedRoot));
        services.AddSingleton<IRunRepository>(_ => new SqliteRunRepository(dbPath));
        services.AddSingleton<IRunQueryRepository>(_ => new SqliteRunQueryRepository(dbPath));
        return services;
    }
}
