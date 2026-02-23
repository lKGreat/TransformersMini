using System.Text.Json;
using System.Text.Json.Serialization;
using TransformersMini.Contracts.Abstractions;
using TransformersMini.Contracts.Runtime;

namespace TransformersMini.Application.Services;

public sealed class AnnotationService : IAnnotationService
{
    private static readonly HashSet<string> SupportedImageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".bmp", ".webp"
    };

    public Task<AnnotationSession> CreateSessionFromImageDirectoryAsync(string imageDirectory, IReadOnlyList<string> classNames, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var normalizedDirectory = Path.GetFullPath(imageDirectory);
        if (!Directory.Exists(normalizedDirectory))
        {
            throw new DirectoryNotFoundException($"图像目录不存在：{normalizedDirectory}");
        }

        var classes = NormalizeClassNames(classNames);
        var imagePaths = Directory.EnumerateFiles(normalizedDirectory, "*.*", SearchOption.TopDirectoryOnly)
            .Where(path => SupportedImageExtensions.Contains(Path.GetExtension(path)))
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var images = imagePaths.Select(CreateImageDocument).ToList();
        var session = new AnnotationSession
        {
            SessionName = $"annotation-{DateTime.Now:yyyyMMdd-HHmmss}",
            SourceDirectory = normalizedDirectory,
            ClassNames = classes,
            Images = images,
            Version = CreateNextVersion(1)
        };
        return Task.FromResult(session);
    }

    public async Task<AnnotationSession> LoadFromCocoAsync(string cocoFilePath, string imageBaseDirectory, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var normalizedFile = Path.GetFullPath(cocoFilePath);
        var normalizedBaseDirectory = Path.GetFullPath(imageBaseDirectory);
        if (!File.Exists(normalizedFile))
        {
            throw new FileNotFoundException("COCO 文件不存在。", normalizedFile);
        }

        if (!Directory.Exists(normalizedBaseDirectory))
        {
            throw new DirectoryNotFoundException($"图像基目录不存在：{normalizedBaseDirectory}");
        }

        await using var stream = File.OpenRead(normalizedFile);
        var coco = await JsonSerializer.DeserializeAsync<CocoFileDto>(stream, JsonOptions, ct) ?? new CocoFileDto();
        var classNames = coco.Categories
            .OrderBy(c => c.Id)
            .Select(c => c.Name ?? $"class_{c.Id}")
            .ToList();
        var classNameMap = coco.Categories.ToDictionary(x => x.Id, x => x.Name ?? $"class_{x.Id}");

        var annotationsByImage = coco.Annotations
            .GroupBy(x => x.ImageId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var images = new List<AnnotationImageDocument>(coco.Images.Count);
        foreach (var image in coco.Images.OrderBy(x => x.Id))
        {
            ct.ThrowIfCancellationRequested();
            var imagePath = Path.GetFullPath(Path.Combine(normalizedBaseDirectory, image.FileName ?? string.Empty));
            var boxes = new List<AnnotationBox>();
            if (annotationsByImage.TryGetValue(image.Id, out var imageAnnotations))
            {
                foreach (var ann in imageAnnotations)
                {
                    var bbox = ann.Bbox ?? [];
                    if (bbox.Count < 4)
                    {
                        continue;
                    }

                    classNameMap.TryGetValue(ann.CategoryId, out var className);
                    boxes.Add(new AnnotationBox
                    {
                        ClassId = ann.CategoryId,
                        ClassName = className ?? $"class_{ann.CategoryId}",
                        X = bbox[0],
                        Y = bbox[1],
                        Width = bbox[2],
                        Height = bbox[3],
                        Source = "coco"
                    });
                }
            }

            images.Add(new AnnotationImageDocument
            {
                ImageId = image.Id.ToString(),
                ImagePath = imagePath,
                Width = image.Width,
                Height = image.Height,
                Boxes = boxes
            });
        }

        return new AnnotationSession
        {
            SessionName = Path.GetFileNameWithoutExtension(normalizedFile),
            SourceDirectory = normalizedBaseDirectory,
            ClassNames = NormalizeClassNames(classNames),
            Images = images,
            Version = CreateNextVersion(1)
        };
    }

    public async Task<AnnotationSession> LoadFromYoloAsync(string imageDirectory, string labelsDirectory, string classesFilePath, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var normalizedImageDirectory = Path.GetFullPath(imageDirectory);
        var normalizedLabelsDirectory = Path.GetFullPath(labelsDirectory);
        var normalizedClassesFile = Path.GetFullPath(classesFilePath);
        if (!Directory.Exists(normalizedImageDirectory))
        {
            throw new DirectoryNotFoundException($"图像目录不存在：{normalizedImageDirectory}");
        }

        if (!Directory.Exists(normalizedLabelsDirectory))
        {
            throw new DirectoryNotFoundException($"标注目录不存在：{normalizedLabelsDirectory}");
        }

        if (!File.Exists(normalizedClassesFile))
        {
            throw new FileNotFoundException("classes 文件不存在。", normalizedClassesFile);
        }

        var classNames = (await File.ReadAllLinesAsync(normalizedClassesFile, ct))
            .Select(line => line.Trim())
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .ToList();
        classNames = NormalizeClassNames(classNames);

        var imagePaths = Directory.EnumerateFiles(normalizedImageDirectory, "*.*", SearchOption.TopDirectoryOnly)
            .Where(path => SupportedImageExtensions.Contains(Path.GetExtension(path)))
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var imageDocuments = new List<AnnotationImageDocument>(imagePaths.Count);
        foreach (var imagePath in imagePaths)
        {
            ct.ThrowIfCancellationRequested();
            var imageDoc = CreateImageDocument(imagePath);
            var labelPath = Path.Combine(normalizedLabelsDirectory, Path.GetFileNameWithoutExtension(imagePath) + ".txt");
            if (!File.Exists(labelPath))
            {
                imageDocuments.Add(imageDoc);
                continue;
            }

            var lines = await File.ReadAllLinesAsync(labelPath, ct);
            foreach (var line in lines)
            {
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                if (parts.Length < 5 || !int.TryParse(parts[0], out var classId))
                {
                    continue;
                }

                if (!TryParseFloat(parts[1], out var cx) ||
                    !TryParseFloat(parts[2], out var cy) ||
                    !TryParseFloat(parts[3], out var w) ||
                    !TryParseFloat(parts[4], out var h))
                {
                    continue;
                }

                var absW = w * imageDoc.Width;
                var absH = h * imageDoc.Height;
                var absX = (cx * imageDoc.Width) - absW / 2f;
                var absY = (cy * imageDoc.Height) - absH / 2f;
                imageDoc.Boxes.Add(new AnnotationBox
                {
                    ClassId = classId,
                    ClassName = classId >= 0 && classId < classNames.Count ? classNames[classId] : $"class_{classId}",
                    X = Math.Max(0, absX),
                    Y = Math.Max(0, absY),
                    Width = Math.Max(0, absW),
                    Height = Math.Max(0, absH),
                    Source = "yolo"
                });
            }

            imageDocuments.Add(imageDoc);
        }

        return new AnnotationSession
        {
            SessionName = $"yolo-{DateTime.Now:yyyyMMdd-HHmmss}",
            SourceDirectory = normalizedImageDirectory,
            ClassNames = classNames,
            Images = imageDocuments,
            Version = CreateNextVersion(1)
        };
    }

    public async Task SaveAsCocoAsync(AnnotationSession session, string outputFilePath, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var normalizedFile = Path.GetFullPath(outputFilePath);
        Directory.CreateDirectory(Path.GetDirectoryName(normalizedFile) ?? ".");

        var categories = session.ClassNames.Select((name, index) => new CocoCategoryDto
        {
            Id = index,
            Name = name
        }).ToList();

        var images = new List<CocoImageDto>(session.Images.Count);
        var annotations = new List<CocoAnnotationDto>();
        var annotationId = 1;

        for (var imageIndex = 0; imageIndex < session.Images.Count; imageIndex++)
        {
            ct.ThrowIfCancellationRequested();
            var image = session.Images[imageIndex];
            var cocoImageId = imageIndex + 1;
            images.Add(new CocoImageDto
            {
                Id = cocoImageId,
                FileName = Path.GetFileName(image.ImagePath),
                Width = image.Width,
                Height = image.Height
            });

            foreach (var box in image.Boxes)
            {
                annotations.Add(new CocoAnnotationDto
                {
                    Id = annotationId++,
                    ImageId = cocoImageId,
                    CategoryId = box.ClassId,
                    Bbox = [box.X, box.Y, box.Width, box.Height],
                    Area = box.Width * box.Height,
                    IsCrowd = 0
                });
            }
        }

        var coco = new CocoFileDto
        {
            Images = images,
            Annotations = annotations,
            Categories = categories
        };

        await using var stream = File.Create(normalizedFile);
        await JsonSerializer.SerializeAsync(stream, coco, JsonOptions, ct);
    }

    public async Task SaveAsYoloAsync(AnnotationSession session, string outputDirectory, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var normalizedOutputDirectory = Path.GetFullPath(outputDirectory);
        var labelsDirectory = Path.Combine(normalizedOutputDirectory, "labels");
        var imagesDirectory = Path.Combine(normalizedOutputDirectory, "images");
        Directory.CreateDirectory(labelsDirectory);
        Directory.CreateDirectory(imagesDirectory);

        var classesPath = Path.Combine(normalizedOutputDirectory, "classes.txt");
        await File.WriteAllLinesAsync(classesPath, session.ClassNames, ct);

        foreach (var image in session.Images)
        {
            ct.ThrowIfCancellationRequested();
            if (!File.Exists(image.ImagePath))
            {
                continue;
            }

            var destinationImagePath = Path.Combine(imagesDirectory, Path.GetFileName(image.ImagePath));
            File.Copy(image.ImagePath, destinationImagePath, true);

            var lines = new List<string>(image.Boxes.Count);
            foreach (var box in image.Boxes)
            {
                if (image.Width <= 0 || image.Height <= 0)
                {
                    continue;
                }

                var cx = (box.X + box.Width / 2f) / image.Width;
                var cy = (box.Y + box.Height / 2f) / image.Height;
                var w = box.Width / image.Width;
                var h = box.Height / image.Height;
                lines.Add($"{box.ClassId} {FormatFloat(cx)} {FormatFloat(cy)} {FormatFloat(w)} {FormatFloat(h)}");
            }

            var labelPath = Path.Combine(labelsDirectory, Path.GetFileNameWithoutExtension(image.ImagePath) + ".txt");
            await File.WriteAllLinesAsync(labelPath, lines, ct);
        }
    }

    public async Task<AnnotationSession> ImportDetectionPredictionsAsync(AnnotationSession session, string inferenceSamplesJsonlPath, float minScore, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var normalizedPath = Path.GetFullPath(inferenceSamplesJsonlPath);
        if (!File.Exists(normalizedPath))
        {
            throw new FileNotFoundException("推理样本明细文件不存在。", normalizedPath);
        }

        var nextSession = CloneSession(session);
        var imagesByName = nextSession.Images.ToDictionary(
            item => Path.GetFileName(item.ImagePath),
            item => item,
            StringComparer.OrdinalIgnoreCase);

        var lines = await File.ReadAllLinesAsync(normalizedPath, ct);
        foreach (var line in lines)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            using var doc = JsonDocument.Parse(line);
            var root = doc.RootElement;
            var imageFileName = TryGetString(root, "imagePath");
            if (string.IsNullOrWhiteSpace(imageFileName))
            {
                imageFileName = TryGetString(root, "sampleId");
            }

            imageFileName = Path.GetFileName(imageFileName ?? string.Empty);
            if (string.IsNullOrWhiteSpace(imageFileName) || !imagesByName.TryGetValue(imageFileName, out var imageDoc))
            {
                continue;
            }

            var detections = TryGetArray(root, "detections");
            if (detections is null)
            {
                continue;
            }

            foreach (var detection in detections.Value.EnumerateArray())
            {
                var score = TryGetFloat(detection, "score");
                if (score < minScore)
                {
                    continue;
                }

                var classId = (int)TryGetFloat(detection, "classId");
                var className = TryGetString(detection, "className");
                if (string.IsNullOrWhiteSpace(className))
                {
                    className = classId >= 0 && classId < nextSession.ClassNames.Count
                        ? nextSession.ClassNames[classId]
                        : $"class_{classId}";
                }

                imageDoc.Boxes.Add(new AnnotationBox
                {
                    ClassId = classId,
                    ClassName = className,
                    X = TryGetFloat(detection, "x"),
                    Y = TryGetFloat(detection, "y"),
                    Width = TryGetFloat(detection, "width"),
                    Height = TryGetFloat(detection, "height"),
                    Score = score,
                    Source = "prediction"
                });
            }
        }

        return new AnnotationSession
        {
            SessionName = nextSession.SessionName,
            SourceDirectory = nextSession.SourceDirectory,
            ClassNames = [.. nextSession.ClassNames],
            Images = [.. nextSession.Images],
            Version = CreateNextVersion(session.Version.Revision + 1)
        };
    }

    private static AnnotationImageDocument CreateImageDocument(string imagePath)
    {
        var (imageWidth, imageHeight) = TryReadImageSize(imagePath);
        return new AnnotationImageDocument
        {
            ImageId = Guid.NewGuid().ToString("N"),
            ImagePath = Path.GetFullPath(imagePath),
            Width = imageWidth,
            Height = imageHeight,
            Boxes = []
        };
    }

    private static List<string> NormalizeClassNames(IReadOnlyList<string> classNames)
    {
        var normalized = classNames.Select(name => name.Trim())
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (normalized.Count == 0)
        {
            normalized.Add("default");
        }

        return normalized;
    }

    private static AnnotationVersion CreateNextVersion(int revision)
    {
        return new AnnotationVersion
        {
            Revision = revision,
            UpdatedAt = DateTimeOffset.UtcNow,
            UpdatedBy = Environment.UserName
        };
    }

    private static AnnotationSession CloneSession(AnnotationSession session)
    {
        return new AnnotationSession
        {
            SessionName = session.SessionName,
            SourceDirectory = session.SourceDirectory,
            ClassNames = [.. session.ClassNames],
            Images =
            [
                .. session.Images.Select(image => new AnnotationImageDocument
                {
                    ImageId = image.ImageId,
                    ImagePath = image.ImagePath,
                    Width = image.Width,
                    Height = image.Height,
                    Boxes = [.. image.Boxes.Select(box => new AnnotationBox
                    {
                        BoxId = box.BoxId,
                        ClassId = box.ClassId,
                        ClassName = box.ClassName,
                        X = box.X,
                        Y = box.Y,
                        Width = box.Width,
                        Height = box.Height,
                        Score = box.Score,
                        Source = box.Source
                    })]
                })
            ],
            Version = session.Version
        };
    }

    private static bool TryParseFloat(string text, out float value)
    {
        return float.TryParse(text, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out value);
    }

    private static string FormatFloat(float value)
    {
        return value.ToString("0.######", System.Globalization.CultureInfo.InvariantCulture);
    }

    private static string? TryGetString(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out var value))
        {
            return null;
        }

        return value.ValueKind == JsonValueKind.String ? value.GetString() : value.GetRawText();
    }

    private static JsonElement? TryGetArray(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out var value) || value.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        return value;
    }

    private static float TryGetFloat(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out var value))
        {
            return 0f;
        }

        if (value.ValueKind == JsonValueKind.Number && value.TryGetSingle(out var number))
        {
            return number;
        }

        if (value.ValueKind == JsonValueKind.String && TryParseFloat(value.GetString() ?? string.Empty, out var parsed))
        {
            return parsed;
        }

        return 0f;
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private sealed class CocoFileDto
    {
        [JsonPropertyName("images")]
        public List<CocoImageDto> Images { get; init; } = [];

        [JsonPropertyName("annotations")]
        public List<CocoAnnotationDto> Annotations { get; init; } = [];

        [JsonPropertyName("categories")]
        public List<CocoCategoryDto> Categories { get; init; } = [];
    }

    private sealed class CocoImageDto
    {
        [JsonPropertyName("id")]
        public int Id { get; init; }

        [JsonPropertyName("file_name")]
        public string? FileName { get; init; }

        [JsonPropertyName("width")]
        public int Width { get; init; }

        [JsonPropertyName("height")]
        public int Height { get; init; }
    }

    private sealed class CocoAnnotationDto
    {
        [JsonPropertyName("id")]
        public int Id { get; init; }

        [JsonPropertyName("image_id")]
        public int ImageId { get; init; }

        [JsonPropertyName("category_id")]
        public int CategoryId { get; init; }

        [JsonPropertyName("bbox")]
        public List<float>? Bbox { get; init; }

        [JsonPropertyName("area")]
        public float Area { get; init; }

        [JsonPropertyName("iscrowd")]
        public int IsCrowd { get; init; }
    }

    private sealed class CocoCategoryDto
    {
        [JsonPropertyName("id")]
        public int Id { get; init; }

        [JsonPropertyName("name")]
        public string? Name { get; init; }
    }

    private static (int Width, int Height) TryReadImageSize(string imagePath)
    {
        try
        {
            using var stream = File.OpenRead(imagePath);
            using var reader = new BinaryReader(stream);

            var signature = reader.ReadBytes(8);
            if (signature.Length == 8 &&
                signature[0] == 137 &&
                signature[1] == 80 &&
                signature[2] == 78 &&
                signature[3] == 71)
            {
                stream.Seek(16, SeekOrigin.Begin);
                var widthBytes = reader.ReadBytes(4);
                var heightBytes = reader.ReadBytes(4);
                if (BitConverter.IsLittleEndian)
                {
                    Array.Reverse(widthBytes);
                    Array.Reverse(heightBytes);
                }

                return (BitConverter.ToInt32(widthBytes, 0), BitConverter.ToInt32(heightBytes, 0));
            }

            stream.Seek(0, SeekOrigin.Begin);
            if (reader.ReadByte() == 0x42 && reader.ReadByte() == 0x4D)
            {
                stream.Seek(18, SeekOrigin.Begin);
                return (Math.Abs(reader.ReadInt32()), Math.Abs(reader.ReadInt32()));
            }

            stream.Seek(0, SeekOrigin.Begin);
            if (reader.ReadByte() == 0xFF && reader.ReadByte() == 0xD8)
            {
                while (stream.Position < stream.Length)
                {
                    if (reader.ReadByte() != 0xFF)
                    {
                        continue;
                    }

                    var marker = reader.ReadByte();
                    while (marker == 0xFF)
                    {
                        marker = reader.ReadByte();
                    }

                    if (marker is 0xC0 or 0xC1 or 0xC2 or 0xC3 or 0xC5 or 0xC6 or 0xC7 or 0xC9 or 0xCA or 0xCB or 0xCD or 0xCE or 0xCF)
                    {
                        _ = ReadBigEndianUInt16(reader);
                        _ = reader.ReadByte();
                        var height = ReadBigEndianUInt16(reader);
                        var width = ReadBigEndianUInt16(reader);
                        return (width, height);
                    }

                    var segmentLength = ReadBigEndianUInt16(reader);
                    if (segmentLength < 2)
                    {
                        break;
                    }

                    stream.Seek(segmentLength - 2, SeekOrigin.Current);
                }
            }
        }
        catch
        {
            // 中文说明：读取图像尺寸失败时返回默认值，允许后续人工修正。
        }

        return (1, 1);
    }

    private static ushort ReadBigEndianUInt16(BinaryReader reader)
    {
        var high = reader.ReadByte();
        var low = reader.ReadByte();
        return (ushort)((high << 8) | low);
    }
}
