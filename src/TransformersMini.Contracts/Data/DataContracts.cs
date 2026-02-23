namespace TransformersMini.Contracts.Data;

public sealed record DataSample(
    string Id,
    string SourcePath,
    string? Label,
    string Split,
    IReadOnlyDictionary<string, string>? Metadata = null);

public sealed class DataSplitBundle
{
    public IReadOnlyList<DataSample> Train { get; init; } = Array.Empty<DataSample>();
    public IReadOnlyList<DataSample> Validation { get; init; } = Array.Empty<DataSample>();
    public IReadOnlyList<DataSample> Test { get; init; } = Array.Empty<DataSample>();
}
