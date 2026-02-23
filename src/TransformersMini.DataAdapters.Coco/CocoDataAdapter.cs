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
            var annotationsByImageId = BuildAnnotationMap(json.RootElement);
            var images = json.RootElement.TryGetProperty("images", out var imagesElement)
                ? imagesElement.EnumerateArray().Take(16).ToList()
                : [];

            foreach (var image in images)
            {
                var id = image.TryGetProperty("id", out var idEl) ? idEl.ToString() : Guid.NewGuid().ToString("N");
                var fileName = image.TryGetProperty("file_name", out var fileNameEl) ? fileNameEl.GetString() ?? string.Empty : string.Empty;
                var width = image.TryGetProperty("width", out var widthEl) && widthEl.TryGetDouble(out var widthValue) ? widthValue : 1d;
                var height = image.TryGetProperty("height", out var heightEl) && heightEl.TryGetDouble(out var heightValue) ? heightValue : 1d;
                annotationsByImageId.TryGetValue(id, out var imageAnnotations);
                imageAnnotations ??= [];

                var metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["coco.image_id"] = id,
                    ["coco.image_width"] = width.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    ["coco.image_height"] = height.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    ["coco.annotation_count"] = imageAnnotations.Count.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    ["coco.annotations_json"] = JsonSerializer.Serialize(imageAnnotations)
                };
                samples.Add(new DataSample(id, Path.Combine(config.RootPath, fileName), null, config.TrainSplit, metadata));
            }
        }

        if (samples.Count == 0)
        {
            samples.Add(new DataSample(
                "coco-placeholder-1",
                Path.Combine(config.RootPath, "placeholder.jpg"),
                null,
                config.TrainSplit,
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["coco.image_id"] = "coco-placeholder-1",
                    ["coco.image_width"] = "640",
                    ["coco.image_height"] = "640",
                    ["coco.annotation_count"] = "1",
                    ["coco.annotations_json"] = "[{\"categoryId\":0,\"bbox\":[160,160,320,320]}]"
                }));
        }

        return new DataSplitBundle
        {
            Train = samples,
            Validation = samples.Take(1).ToList(),
            Test = samples.Take(1).ToList()
        };
    }

    private static Dictionary<string, List<CocoAnnotationLite>> BuildAnnotationMap(JsonElement root)
    {
        var map = new Dictionary<string, List<CocoAnnotationLite>>(StringComparer.OrdinalIgnoreCase);
        if (!root.TryGetProperty("annotations", out var annotationsElement) || annotationsElement.ValueKind != JsonValueKind.Array)
        {
            return map;
        }

        foreach (var ann in annotationsElement.EnumerateArray())
        {
            var imageId = ann.TryGetProperty("image_id", out var imageIdEl) ? imageIdEl.ToString() : null;
            if (string.IsNullOrWhiteSpace(imageId))
            {
                continue;
            }

            if (!ann.TryGetProperty("bbox", out var bboxEl) || bboxEl.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            var bbox = bboxEl.EnumerateArray().Select(x => x.GetDouble()).ToArray();
            if (bbox.Length < 4)
            {
                continue;
            }

            var categoryId = ann.TryGetProperty("category_id", out var categoryEl) && categoryEl.TryGetInt32(out var categoryValue)
                ? categoryValue
                : 0;

            if (!map.TryGetValue(imageId, out var list))
            {
                list = [];
                map[imageId] = list;
            }

            list.Add(new CocoAnnotationLite(categoryId, [bbox[0], bbox[1], bbox[2], bbox[3]]));
        }

        return map;
    }

    private sealed record CocoAnnotationLite(int CategoryId, double[] Bbox);
}
