namespace TransformersMini.Contracts.ModelMetadata;

/// <summary>
/// OCR 模型 artifacts/model-metadata.json 顶层结构。
/// 训练完成后写入，供推理链路读取恢复模型参数。
/// </summary>
public sealed record OcrModelMetadataDto(
    string Framework,
    string Task,
    string Device,
    string Model,
    OcrTensorOptionsDto Options,
    string Status);

/// <summary>
/// OCR 训练 reports/summary.json 顶层结构。
/// </summary>
public sealed record OcrTrainSummaryDto(
    string Task,
    string Mode,
    string Backend,
    string Device,
    int SampleCount,
    int Epochs,
    int StepsPerEpoch,
    OcrTensorOptionsDto Options,
    OcrEvalMetricsDto Metrics,
    string Status);

/// <summary>
/// OCR 验证/测试 reports/validate.json、reports/test.json 顶层结构。
/// </summary>
public sealed record OcrEvalReportDto(
    string Task,
    string Mode,
    string Backend,
    string Device,
    int SampleCount,
    OcrTensorOptionsDto Options,
    string MetricType,
    OcrEvalMetricsDto Metrics,
    string Status);

/// <summary>
/// OCR 模型张量参数快照，包含 OCR 特有字段：字符集、输入尺寸、最大长度。
/// </summary>
public sealed record OcrTensorOptionsDto(
    int InputHeight,
    int InputWidth,
    int MaxTextLength,
    int CharsetSize,
    string ResizeSampler);

/// <summary>
/// OCR 评估指标（CER / WER / 精确匹配率）。
/// </summary>
public sealed record OcrEvalMetricsDto(
    double Cer,
    double Wer,
    int ExactMatchCount,
    int SampleCount,
    int MaxTextLength);
