using System.Text;
using System.Text.Json;
using TransformersMini.Contracts.Abstractions;
using TransformersMini.Contracts.Runtime;
using TransformersMini.SharedKernel.Core;

namespace TransformersMini.WinForms;

/// <summary>
/// 单图交互推理界面，支持检测（显示框和分数）与 OCR（显示识别文本）。
/// 用于调试、演示和错误分析。遵守三文件结构约定。
/// </summary>
public sealed partial class SingleImageInferenceForm : Form
{
    private readonly IInferenceOrchestrator _inferenceOrchestrator;
    private string? _selectedImagePath;
    private bool _isRunning;

    public SingleImageInferenceForm(IInferenceOrchestrator inferenceOrchestrator)
    {
        _inferenceOrchestrator = inferenceOrchestrator;
        InitializeComponent();
    }

    private void btnBrowseImage_Click(object sender, EventArgs e)
    {
        using var dialog = new OpenFileDialog
        {
            Filter = "图像文件 (*.png;*.jpg;*.jpeg;*.bmp)|*.png;*.jpg;*.jpeg;*.bmp|所有文件 (*.*)|*.*",
            Title = "选择推理图像"
        };
        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            _selectedImagePath = dialog.FileName;
            txtImagePath.Text = dialog.FileName;
            picPreview.ImageLocation = dialog.FileName;
        }
    }

    private void btnBrowseConfig_Click(object sender, EventArgs e)
    {
        using var dialog = new OpenFileDialog
        {
            Filter = "JSON 配置 (*.json)|*.json|所有文件 (*.*)|*.*",
            Title = "选择推理配置文件"
        };
        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            txtConfigPath.Text = dialog.FileName;
        }
    }

    private void btnBrowseModelDir_Click(object sender, EventArgs e)
    {
        using var dialog = new FolderBrowserDialog
        {
            Description = "选择训练产物目录",
            UseDescriptionForTitle = true
        };
        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            txtModelDir.Text = dialog.SelectedPath;
        }
    }

    private async void btnRunInfer_Click(object sender, EventArgs e)
    {
        if (_isRunning)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(txtConfigPath.Text))
        {
            MessageBox.Show(this, "请先选择配置文件。", "参数缺失", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        _isRunning = true;
        btnRunInfer.Enabled = false;
        txtResults.Clear();
        txtResults.AppendText("正在推理...\r\n");

        try
        {
            // 将单张图片推理复用批量推理流程（MaxSamples=1）
            var command = new RunInferenceCommand
            {
                ConfigPath = txtConfigPath.Text,
                ModelRunDirectory = txtModelDir.Text,
                RequestedRunName = "single-image-infer",
                ForcedDevice = DeviceType.Cpu,
                MaxSamples = 1
            };

            var result = await _inferenceOrchestrator.ExecuteAsync(command, CancellationToken.None);

            var sb = new StringBuilder();
            sb.AppendLine($"推理完成！RunId: {result.RunId}");
            sb.AppendLine($"Status: {result.Status}");
            sb.AppendLine();

            // 读取推理报告
            var inferReportPath = Path.Combine(result.RunDirectory, "reports", "inference.json");
            var samplesPath = Path.Combine(result.RunDirectory, "reports", "inference-samples.jsonl");

            if (File.Exists(inferReportPath))
            {
                sb.AppendLine("--- 推理汇总 ---");
                try
                {
                    var reportJson = await File.ReadAllTextAsync(inferReportPath);
                    using var doc = JsonDocument.Parse(reportJson);
                    var task = doc.RootElement.TryGetProperty("task", out var taskEl) ? taskEl.GetString() : "unknown";

                    if (string.Equals(task, "detection", StringComparison.OrdinalIgnoreCase))
                    {
                        AppendDetectionSummary(sb, doc.RootElement);
                    }
                    else if (string.Equals(task, "ocr", StringComparison.OrdinalIgnoreCase))
                    {
                        AppendOcrSummary(sb, doc.RootElement);
                    }
                    else
                    {
                        foreach (var prop in doc.RootElement.EnumerateObject())
                        {
                            sb.AppendLine($"  {prop.Name}: {prop.Value.GetRawText()}");
                        }
                    }
                }
                catch
                {
                    sb.AppendLine("（读取报告失败）");
                }
            }

            if (File.Exists(samplesPath))
            {
                sb.AppendLine();
                sb.AppendLine("--- 样本明细 ---");
                try
                {
                    var firstLine = (await File.ReadAllLinesAsync(samplesPath)).FirstOrDefault();
                    if (!string.IsNullOrWhiteSpace(firstLine))
                    {
                        using var sampleDoc = JsonDocument.Parse(firstLine);
                        foreach (var prop in sampleDoc.RootElement.EnumerateObject())
                        {
                            sb.AppendLine($"  {prop.Name}: {prop.Value.GetRawText()}");
                        }
                    }
                }
                catch
                {
                    sb.AppendLine("（读取样本明细失败）");
                }
            }

            txtResults.Text = sb.ToString();

            if (!string.IsNullOrWhiteSpace(result.RunDirectory))
            {
                lblRunDir.Text = $"RunDir: {result.RunDirectory}";
            }
        }
        catch (Exception ex)
        {
            txtResults.AppendText($"\r\n推理失败：{ex.Message}\r\n");
            MessageBox.Show(this, ex.Message, "推理失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            _isRunning = false;
            btnRunInfer.Enabled = true;
        }
    }

    private static void AppendDetectionSummary(StringBuilder sb, JsonElement root)
    {
        sb.AppendLine("任务类型：检测（Detection）");
        if (root.TryGetProperty("totalDetectedBoxes", out var boxes))
        {
            sb.AppendLine($"  检测到框数量: {boxes.GetRawText()}");
        }

        if (root.TryGetProperty("averageBoxesPerSample", out var avg))
        {
            sb.AppendLine($"  平均框数/样本: {avg.GetRawText()}");
        }

        if (root.TryGetProperty("inputSize", out var size))
        {
            sb.AppendLine($"  输入尺寸: {size.GetRawText()}");
        }
    }

    private static void AppendOcrSummary(StringBuilder sb, JsonElement root)
    {
        sb.AppendLine("任务类型：OCR");
        if (root.TryGetProperty("averageCer", out var cer))
        {
            sb.AppendLine($"  平均 CER: {cer.GetRawText()}");
        }

        if (root.TryGetProperty("exactMatchCount", out var exact))
        {
            sb.AppendLine($"  精确匹配数: {exact.GetRawText()}");
        }
    }

    private void btnClose_Click(object sender, EventArgs e) => Close();
}
