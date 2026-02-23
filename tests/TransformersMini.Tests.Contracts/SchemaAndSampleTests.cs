using System.Text.Json;
using Xunit;

namespace TransformersMini.Tests.Contracts;

public sealed class SchemaAndSampleTests
{
    [Theory]
    [InlineData("specs/schemas/training-config.schema.json")]
    [InlineData("specs/schemas/ocr-manifest.schema.json")]
    [InlineData("specs/schemas/run-metadata.schema.json")]
    [InlineData("configs/detection/sample.det.train.json")]
    [InlineData("configs/detection/sample.det.validate.json")]
    [InlineData("configs/detection/sample.det.test.json")]
    [InlineData("configs/ocr/sample.ocr.train.json")]
    [InlineData("configs/ocr/sample.ocr.validate.json")]
    [InlineData("configs/ocr/sample.ocr.test.json")]
    public void JsonFiles_AreParsable(string relativePath)
    {
        var repoRoot = FindRepoRoot();
        var fullPath = Path.Combine(repoRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
        var text = File.ReadAllText(fullPath);
        using var _ = JsonDocument.Parse(text);
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
            if (File.Exists(Path.Combine(candidate, "TransformersMini.sln")) || File.Exists(Path.Combine(candidate, "TransformersMini.slnx")))
            {
                return candidate;
            }
        }

        throw new DirectoryNotFoundException("Repository root not found.");
    }
}
