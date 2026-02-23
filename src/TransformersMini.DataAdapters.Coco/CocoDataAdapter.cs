using System.Text.Json;
using TransformersMini.Contracts.Abstractions;
using TransformersMini.Contracts.Configurations;
using TransformersMini.Contracts.Data;

namespace TransformersMini.DataAdapters.Coco;

public sealed class CocoDataAdapter : IDataAdapter
{
    public string DatasetFormat => "coco";

    public async Task<DataSplitBundle> LoadAsync(DatasetConfig config, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        var samples = new List<DataSample>();
        var annotationPath = Path.IsPathRooted(config.AnnotationPath)
            ? config.AnnotationPath
            : Path.GetFullPath(Path.Combine(config.RootPath, config.AnnotationPath));

        if (File.Exists(annotationPath))
        {
            await using var stream = File.OpenRead(annotationPath);
            using var json = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
            var images = json.RootElement.TryGetProperty("images", out var imagesElement)
                ? imagesElement.EnumerateArray().Take(16).ToList()
                : [];

            foreach (var image in images)
            {
                var id = image.TryGetProperty("id", out var idEl) ? idEl.ToString() : Guid.NewGuid().ToString("N");
                var fileName = image.TryGetProperty("file_name", out var fileNameEl) ? fileNameEl.GetString() ?? string.Empty : string.Empty;
                samples.Add(new DataSample(id, Path.Combine(config.RootPath, fileName), null, config.TrainSplit));
            }
        }

        if (samples.Count == 0)
        {
            samples.Add(new DataSample("coco-placeholder-1", Path.Combine(config.RootPath, "placeholder.jpg"), null, config.TrainSplit));
        }

        return new DataSplitBundle
        {
            Train = samples,
            Validation = samples.Take(1).ToList(),
            Test = samples.Take(1).ToList()
        };
    }
}
