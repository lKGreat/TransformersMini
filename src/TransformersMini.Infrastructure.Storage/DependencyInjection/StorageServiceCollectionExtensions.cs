using Microsoft.Extensions.DependencyInjection;
using TransformersMini.Contracts.Abstractions;
using TransformersMini.Infrastructure.Storage.Storage;

namespace TransformersMini.Infrastructure.Storage.DependencyInjection;

public static class StorageServiceCollectionExtensions
{
    public static IServiceCollection AddTransformersMiniStorage(this IServiceCollection services)
    {
        services.AddSingleton<IArtifactStore, FileArtifactStore>();
        services.AddSingleton<IRunRepository, SqliteRunRepository>();
        return services;
    }
}
