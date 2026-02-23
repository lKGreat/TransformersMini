using System.Text.Json;
using System.Text.Json.Serialization;
using TransformersMini.Contracts.Abstractions;
using TransformersMini.Contracts.Configurations;
using TransformersMini.Contracts.Runtime;

namespace TransformersMini.Application.Services;

public sealed class JsonTrainingConfigLoader : ITrainingConfigLoader
{
    private readonly JsonSchemaStrictValidator _schemaValidator = new();
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    static JsonTrainingConfigLoader()
    {
        JsonOptions.Converters.Add(new JsonStringEnumConverter());
    }

    public async Task<(TrainingConfig Config, string ResolvedJson)> LoadAsync(RunTrainingCommand command, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(command.ConfigPath))
        {
            throw new InvalidOperationException("ConfigPath is required.");
        }

        if (!File.Exists(command.ConfigPath))
        {
            throw new FileNotFoundException("Training config not found.", command.ConfigPath);
        }

        var rawJson = await File.ReadAllTextAsync(command.ConfigPath, ct);
        var schemaPath = TryResolveTrainingConfigSchemaPath(command.ConfigPath);
        if (schemaPath is not null)
        {
            _schemaValidator.Validate(rawJson, schemaPath);
        }

        var config = JsonSerializer.Deserialize<TrainingConfig>(rawJson, JsonOptions)
            ?? throw new InvalidOperationException("Failed to deserialize training config.");

        if (command.ForcedMode.HasValue)
        {
            config.Mode = command.ForcedMode.Value;
        }

        if (command.ForcedDevice.HasValue)
        {
            config.Device = command.ForcedDevice.Value;
        }

        if (!string.IsNullOrWhiteSpace(command.RequestedRunName))
        {
            config.RunName = command.RequestedRunName;
        }

        ValidateConfig(config);
        return (config, JsonSerializer.Serialize(config, JsonOptions));
    }

    private static string? TryResolveTrainingConfigSchemaPath(string configPath)
    {
        var candidates = new[]
        {
            Path.Combine(Environment.CurrentDirectory, "specs", "schemas", "training-config.schema.json"),
            Path.Combine(AppContext.BaseDirectory, "specs", "schemas", "training-config.schema.json")
        };

        foreach (var candidate in candidates)
        {
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        var current = Path.GetDirectoryName(Path.GetFullPath(configPath)) ?? Environment.CurrentDirectory;
        for (var i = 0; i < 10; i++)
        {
            var candidate = Path.Combine(current, "specs", "schemas", "training-config.schema.json");
            if (File.Exists(candidate))
            {
                return candidate;
            }

            var parent = Directory.GetParent(current);
            if (parent is null)
            {
                break;
            }

            current = parent.FullName;
        }

        return null;
    }

    private static void ValidateConfig(TrainingConfig config)
    {
        if (string.IsNullOrWhiteSpace(config.ConfigVersion)) throw new InvalidOperationException("ConfigVersion is required.");
        if (config.Optimization.Epochs <= 0) throw new InvalidOperationException("Optimization.Epochs must be > 0.");
        if (config.Optimization.BatchSize <= 0) throw new InvalidOperationException("Optimization.BatchSize must be > 0.");
        if (config.Optimization.LearningRate <= 0) throw new InvalidOperationException("Optimization.LearningRate must be > 0.");
        if (string.IsNullOrWhiteSpace(config.Dataset.Format)) throw new InvalidOperationException("Dataset.Format is required.");
        if (string.IsNullOrWhiteSpace(config.Dataset.RootPath) && string.IsNullOrWhiteSpace(config.Dataset.ManifestPath))
        {
            throw new InvalidOperationException("Dataset.RootPath or Dataset.ManifestPath is required.");
        }
    }
}
