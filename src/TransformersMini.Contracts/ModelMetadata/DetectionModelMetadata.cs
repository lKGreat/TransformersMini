namespace TransformersMini.Contracts.ModelMetadata;

/// <summary>
/// 检测模型 artifacts/model-metadata.json 顶层结构。
/// 训练完成后写入，供推理链路读取恢复模型参数。
/// </summary>
public sealed record DetectionModelMetadataDto(
    string Framework,
    string Task,
    string Device,
    string Model,
    int InputSize,
    DetectionPreprocessingDto Preprocessing,
    DetectionTargetEncodingDto TargetEncoding,
    DetectionLossWeightsDto LossWeights,
    string HeadType,
    string Status);

/// <summary>
/// 检测训练 reports/summary.json 顶层结构。
/// </summary>
public sealed record DetectionTrainSummaryDto(
    string Task,
    string Mode,
    string Backend,
    string Device,
    int SampleCount,
    int Epochs,
    int StepsPerEpoch,
    int InputSize,
    DetectionPreprocessingDto Preprocessing,
    DetectionTargetEncodingDto TargetEncoding,
    DetectionLossWeightsDto LossWeights,
    DetectionLossSummaryDto LossSummary,
    string HeadType,
    string Status);

/// <summary>
/// 检测验证/测试 reports/validate.json、reports/test.json 顶层结构。
/// </summary>
public sealed record DetectionEvalReportDto(
    string Task,
    string Mode,
    string Backend,
    string Device,
    int SampleCount,
    int InputSize,
    DetectionPreprocessingDto Preprocessing,
    DetectionTargetEncodingDto TargetEncoding,
    string MetricType,
    DetectionEvalMetricsDto Metrics,
    IReadOnlyList<DetectionEvalSampleDetailDto> SampleDetails,
    string Status);

/// <summary>
/// 检测预处理参数快照，用于模型元数据与推理配置对齐。
/// </summary>
public sealed record DetectionPreprocessingDto(
    int InputSize,
    float[] NormalizeMean,
    float[] NormalizeStd,
    string ResizeSampler,
    string TargetBoxStrategy);

/// <summary>
/// 检测目标编码参数快照，描述 topK 多框编码方式。
/// </summary>
public sealed record DetectionTargetEncodingDto(
    int TopK,
    int ValuesPerBox,
    int FlattenedSize);

/// <summary>
/// 检测 loss 权重配置快照。
/// </summary>
public sealed record DetectionLossWeightsDto(
    double Bbox,
    double Category,
    double Objectness);

/// <summary>
/// 检测训练 loss 汇总（全轮次均值）。
/// </summary>
public sealed record DetectionLossSummaryDto(
    double AverageTotalLoss,
    double AverageBboxLoss,
    double AverageCategoryLoss,
    double AverageObjectnessLoss,
    int TrainStepCount);

/// <summary>
/// 检测评估指标（近似 IoU/PR）。
/// </summary>
public sealed record DetectionEvalMetricsDto(
    double MeanIou,
    double PrecisionAtIou50,
    double RecallAtIou50,
    int TruePositive,
    int FalsePositive,
    int FalseNegative,
    float IouThreshold);

/// <summary>
/// 检测评估单样本明细，用于错误分析。
/// </summary>
public sealed record DetectionEvalSampleDetailDto(
    int SampleIndex,
    string SampleId,
    string SourcePath,
    int PredictedPositiveCount,
    int TargetPositiveCount,
    int TruePositive,
    int FalsePositive,
    int FalseNegative,
    double MeanMatchedIou);
