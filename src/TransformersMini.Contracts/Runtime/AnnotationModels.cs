namespace TransformersMini.Contracts.Runtime;

/// <summary>
/// 标注会话：UI 编辑时使用的内部统一模型。
/// </summary>
public sealed class AnnotationSession
{
    public string SessionName { get; init; } = "annotation-session";
    public string? SourceDirectory { get; init; }
    public List<string> ClassNames { get; init; } = [];
    public List<AnnotationImageDocument> Images { get; init; } = [];
    public AnnotationVersion Version { get; init; } = new();
}

public sealed class AnnotationVersion
{
    public int Revision { get; init; } = 1;
    public DateTimeOffset UpdatedAt { get; init; } = DateTimeOffset.UtcNow;
    public string UpdatedBy { get; init; } = Environment.UserName;
}

public sealed class AnnotationImageDocument
{
    public string ImageId { get; init; } = string.Empty;
    public string ImagePath { get; init; } = string.Empty;
    public int Width { get; init; }
    public int Height { get; init; }
    public List<AnnotationBox> Boxes { get; init; } = [];
}

public sealed class AnnotationBox
{
    public string BoxId { get; init; } = Guid.NewGuid().ToString("N");
    public int ClassId { get; set; }
    public string ClassName { get; set; } = string.Empty;
    public float X { get; set; }
    public float Y { get; set; }
    public float Width { get; set; }
    public float Height { get; set; }
    public float? Score { get; set; }
    public string Source { get; set; } = "manual";
}
