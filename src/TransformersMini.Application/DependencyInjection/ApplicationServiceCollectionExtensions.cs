using Microsoft.Extensions.DependencyInjection;
using TransformersMini.Application.Services;
using TransformersMini.Contracts.Abstractions;

namespace TransformersMini.Application.DependencyInjection;

public static class ApplicationServiceCollectionExtensions
{
    public static IServiceCollection AddTransformersMiniApplication(this IServiceCollection services)
    {
        services.AddSingleton<ITrainingConfigLoader, JsonTrainingConfigLoader>();
        services.AddSingleton<ITrainingOrchestrator, TrainingOrchestrator>();
        services.AddSingleton<IInferenceOrchestrator, InferenceOrchestrator>();
        services.AddSingleton<IRunControlService, RunControlService>();
        return services;
    }
}
