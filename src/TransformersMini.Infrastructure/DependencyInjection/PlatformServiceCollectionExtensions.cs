using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TransformersMini.Application.DependencyInjection;
using TransformersMini.Contracts.Abstractions;
using TransformersMini.DataAdapters.Coco;
using TransformersMini.DataAdapters.OcrManifest;
using TransformersMini.Infrastructure.MLNet;
using TransformersMini.Infrastructure.Runtime;
using TransformersMini.Infrastructure.Storage.DependencyInjection;
using TransformersMini.Infrastructure.TorchSharp;
using TransformersMini.Training.Tasks.Detection;
using TransformersMini.Training.Tasks.Ocr;

namespace TransformersMini.Infrastructure.DependencyInjection;

public static class PlatformServiceCollectionExtensions
{
    public static IServiceCollection AddTransformersMiniPlatform(this IServiceCollection services)
    {
        services.AddLogging(builder => builder.AddConsole());
        services.AddTransformersMiniApplication();
        services.AddTransformersMiniStorage();

        services.AddSingleton<ISystemProbe, SystemProbe>();

        services.AddSingleton<IBackendCapability, TorchSharpBackendCapability>();
        services.AddSingleton<IBackendCapability, MlNetBackendCapability>();

        services.AddSingleton<IDataAdapter, CocoDataAdapter>();
        services.AddSingleton<IDataAdapter, OcrManifestDataAdapter>();

        services.AddSingleton<ITrainingTask, DetectionTrainingTask>();
        services.AddSingleton<ITrainingTask, OcrTrainingTask>();

        return services;
    }
}
