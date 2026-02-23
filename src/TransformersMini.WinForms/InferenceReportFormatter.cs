using System.Text;
using System.Text.Json;
using TransformersMini.Contracts.Runtime;

namespace TransformersMini.WinForms;

internal static class InferenceReportFormatter
{
    public static async Task<string> BuildSummaryAsync(RunResult result, CancellationToken ct)
    {
        var sb = new StringBuilder();
        sb.AppendLine("推理完成！");
        sb.AppendLine($"RunId: {result.RunId}");
        sb.AppendLine($"Status: {result.Status}");
        sb.AppendLine($"Message: {result.Message}");
        sb.AppendLine($"RunDir: {result.RunDirectory}");

        var inferReportPath = Path.Combine(result.RunDirectory, "reports", "inference.json");
        if (File.Exists(inferReportPath))
        {
            sb.AppendLine();
            sb.AppendLine("--- 推理报告 ---");
            try
            {
                var reportJson = await File.ReadAllTextAsync(inferReportPath, ct);
                using var doc = JsonDocument.Parse(reportJson);
                foreach (var prop in doc.RootElement.EnumerateObject())
                {
                    sb.AppendLine($"  {prop.Name}: {prop.Value.GetRawText()}");
                }
            }
            catch
            {
                sb.AppendLine("（读取推理报告失败）");
            }
        }

        var samplesPath = Path.Combine(result.RunDirectory, "reports", "inference-samples.jsonl");
        if (File.Exists(samplesPath))
        {
            sb.AppendLine();
            sb.AppendLine("--- 样本明细（前5条） ---");
            try
            {
                var lines = await File.ReadAllLinesAsync(samplesPath, ct);
                foreach (var line in lines.Take(5))
                {
                    sb.AppendLine($"  {line}");
                }
            }
            catch
            {
                sb.AppendLine("（读取样本明细失败）");
            }
        }

        return sb.ToString();
    }
}
