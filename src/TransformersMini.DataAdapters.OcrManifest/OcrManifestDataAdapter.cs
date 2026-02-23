using System.Text.Json;
using TransformersMini.Contracts.Abstractions;
using TransformersMini.Contracts.Configurations;
using TransformersMini.Contracts.Data;

namespace TransformersMini.DataAdapters.OcrManifest;

public sealed class OcrManifestDataAdapter : IDataAdapter
{
    public string DatasetFormat => "ocr-manifest-v1";

    public async Task<DataSplitBundle> LoadAsync(DatasetConfig config, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(config.ManifestPath))
        {
            throw new InvalidOperationException("Dataset.ManifestPath is required for OCR manifest.");
        }

        var manifestPath = Path.GetFullPath(config.ManifestPath);
        var baseDir = Path.GetDirectoryName(manifestPath) ?? Directory.GetCurrentDirectory();
        var train = new List<DataSample>();
        var val = new List<DataSample>();
        var test = new List<DataSample>();

        if (!File.Exists(manifestPath))
        {
            train.Add(new DataSample("ocr-placeholder-1", Path.Combine(baseDir, "placeholder.png"), "demo", "train"));
            return new DataSplitBundle { Train = train, Validation = val, Test = test };
        }

        foreach (var line in await File.ReadAllLinesAsync(manifestPath, ct))
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            using var doc = JsonDocument.Parse(line);
            var root = doc.RootElement;
            var id = root.TryGetProperty("id", out var idEl) ? idEl.GetString() ?? Guid.NewGuid().ToString("N") : Guid.NewGuid().ToString("N");
            var imagePath = root.GetProperty("imagePath").GetString() ?? string.Empty;
            var text = root.TryGetProperty("text", out var textEl) ? textEl.GetString() : null;
            var split = root.TryGetProperty("split", out var splitEl) ? splitEl.GetString() ?? "train" : "train";
            var resolvedPath = Path.IsPathRooted(imagePath) ? imagePath : Path.GetFullPath(Path.Combine(baseDir, imagePath));
            var sample = new DataSample(id, resolvedPath, text, split);

            switch (split.ToLowerInvariant())
            {
                case "val":
                case "validation":
                    val.Add(sample);
                    break;
                case "test":
                    test.Add(sample);
                    break;
                default:
                    train.Add(sample);
                    break;
            }
        }

        return new DataSplitBundle { Train = train, Validation = val, Test = test };
    }
}
