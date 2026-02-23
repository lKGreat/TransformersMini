using System.Text.Json;
using TransformersMini.Contracts.ModelMetadata;
using Xunit;

namespace TransformersMini.Tests.Contracts;

public sealed class SchemaAndSampleTests
{
    [Theory]
    [InlineData("specs/schemas/training-config.schema.json")]
    [InlineData("specs/schemas/ocr-manifest.schema.json")]
    [InlineData("specs/schemas/run-metadata.schema.json")]
    [InlineData("specs/schemas/detection-model-metadata.schema.json")]
    [InlineData("specs/schemas/ocr-model-metadata.schema.json")]
    [InlineData("configs/detection/sample.det.train.json")]
    [InlineData("configs/detection/sample.det.validate.json")]
    [InlineData("configs/detection/sample.det.test.json")]
    [InlineData("configs/detection/sample.det.model-metadata.json")]
    [InlineData("configs/ocr/sample.ocr.train.json")]
    [InlineData("configs/ocr/sample.ocr.validate.json")]
    [InlineData("configs/ocr/sample.ocr.test.json")]
    [InlineData("configs/ocr/sample.ocr.model-metadata.json")]
    public void JsonFiles_AreParsable(string relativePath)
    {
        var repoRoot = FindRepoRoot();
        var fullPath = Path.Combine(repoRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
        var text = File.ReadAllText(fullPath);
        using var _ = JsonDocument.Parse(text);
    }

    [Fact]
    public void DetectionModelMetadata_Sample_DeserializesToDto()
    {
        var repoRoot = FindRepoRoot();
        var fullPath = Path.Combine(repoRoot, "configs", "detection", "sample.det.model-metadata.json");
        var json = File.ReadAllText(fullPath);
        var dto = JsonSerializer.Deserialize<DetectionModelMetadataDto>(json,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        Assert.NotNull(dto);
        Assert.Equal("detection", dto!.Task);
        Assert.Equal("TorchSharp", dto.Framework);
        Assert.True(dto.InputSize > 0);
        Assert.NotNull(dto.Preprocessing);
        Assert.Equal(3, dto.Preprocessing.NormalizeMean.Length);
        Assert.Equal(3, dto.Preprocessing.NormalizeStd.Length);
        Assert.NotNull(dto.TargetEncoding);
        Assert.True(dto.TargetEncoding.TopK > 0);
        Assert.Equal(dto.TargetEncoding.TopK * dto.TargetEncoding.ValuesPerBox, dto.TargetEncoding.FlattenedSize);
        Assert.NotNull(dto.LossWeights);
    }

    [Fact]
    public void OcrModelMetadata_Sample_DeserializesToDto()
    {
        var repoRoot = FindRepoRoot();
        var fullPath = Path.Combine(repoRoot, "configs", "ocr", "sample.ocr.model-metadata.json");
        var json = File.ReadAllText(fullPath);
        var dto = JsonSerializer.Deserialize<OcrModelMetadataDto>(json,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        Assert.NotNull(dto);
        Assert.Equal("ocr", dto!.Task);
        Assert.Equal("TorchSharp", dto.Framework);
        Assert.NotNull(dto.Options);
        Assert.True(dto.Options.InputHeight > 0);
        Assert.True(dto.Options.InputWidth > 0);
        Assert.True(dto.Options.MaxTextLength > 0);
        Assert.True(dto.Options.CharsetSize > 0);
        Assert.NotEmpty(dto.Options.ResizeSampler);
    }

    [Fact]
    public void DetectionModelMetadata_RequiredFields_ArePresent_InSchema()
    {
        var repoRoot = FindRepoRoot();
        var schemaPath = Path.Combine(repoRoot, "specs", "schemas", "detection-model-metadata.schema.json");
        var schemaJson = File.ReadAllText(schemaPath);
        using var schemaDoc = JsonDocument.Parse(schemaJson);
        var required = schemaDoc.RootElement.GetProperty("required");
        var requiredFields = required.EnumerateArray().Select(x => x.GetString()).ToHashSet(StringComparer.OrdinalIgnoreCase);

        Assert.Contains("framework", requiredFields);
        Assert.Contains("task", requiredFields);
        Assert.Contains("preprocessing", requiredFields);
        Assert.Contains("targetEncoding", requiredFields);
        Assert.Contains("lossWeights", requiredFields);
    }

    [Fact]
    public void OcrModelMetadata_RequiredFields_ArePresent_InSchema()
    {
        var repoRoot = FindRepoRoot();
        var schemaPath = Path.Combine(repoRoot, "specs", "schemas", "ocr-model-metadata.schema.json");
        var schemaJson = File.ReadAllText(schemaPath);
        using var schemaDoc = JsonDocument.Parse(schemaJson);
        var required = schemaDoc.RootElement.GetProperty("required");
        var requiredFields = required.EnumerateArray().Select(x => x.GetString()).ToHashSet(StringComparer.OrdinalIgnoreCase);

        Assert.Contains("framework", requiredFields);
        Assert.Contains("task", requiredFields);
        Assert.Contains("options", requiredFields);
    }

    [Fact]
    public void DetectionModelMetadata_MissingRequiredField_IsDetectable()
    {
        // 验证：缺少 task 字段时反序列化后 Task 为 null
        const string invalidJson = """{"framework":"TorchSharp","device":"Cpu","model":"m","inputSize":640,"preprocessing":null,"targetEncoding":null,"lossWeights":null,"headType":"h","status":"s"}""";
        var dto = JsonSerializer.Deserialize<DetectionModelMetadataDto>(invalidJson,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        // task 字段缺失，record 构造需要所有参数，若反序列化为 null 则说明 schema 校验是必要的
        Assert.Null(dto!.Task);
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
