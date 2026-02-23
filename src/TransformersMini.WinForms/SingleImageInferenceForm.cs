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
                MaxSamples = 1,
                SingleImagePath = txtImagePath.Text
            };

            var result = await _inferenceOrchestrator.ExecuteAsync(command, CancellationToken.None);

            txtResults.Text = await InferenceReportFormatter.BuildSummaryAsync(result, CancellationToken.None);

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

    private void btnClose_Click(object sender, EventArgs e) => Close();
}
