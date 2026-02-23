using TransformersMini.Contracts.Abstractions;
using TransformersMini.Contracts.Runtime;
using TransformersMini.SharedKernel.Core;

namespace TransformersMini.WinForms;

/// <summary>
/// 批量推理操作面板，供检测/OCR 批量推理使用。
/// 独立 Form，不依赖 MainForm，遵守三文件结构约定。
/// </summary>
public sealed partial class BatchInferenceForm : Form
{
    private readonly IInferenceOrchestrator _inferenceOrchestrator;
    private readonly ISystemProbe _systemProbe;
    private bool _isRunning;

    public BatchInferenceForm(IInferenceOrchestrator inferenceOrchestrator, ISystemProbe systemProbe)
    {
        _inferenceOrchestrator = inferenceOrchestrator;
        _systemProbe = systemProbe;
        InitializeComponent();
        InitializeDeviceCombo();
    }

    public void ApplyQuickPreset(string configPath, string modelRunDirectory, string? runName, int maxSamples = 20, bool autoStart = true)
    {
        txtConfigPath.Text = configPath;
        txtModelRunDir.Text = modelRunDirectory;
        txtRunName.Text = runName ?? string.Empty;
        txtMaxSamples.Text = maxSamples > 0 ? maxSamples.ToString() : "0";

        if (autoStart)
        {
            btnStartInfer_Click(this, EventArgs.Empty);
        }
    }

    private void InitializeDeviceCombo()
    {
        cmbDevice.Items.Clear();
        cmbDevice.Items.AddRange(["Auto", "Cpu", "Cuda"]);
        cmbDevice.SelectedIndex = 0;

        var hasCuda = _systemProbe.IsCudaAvailable();
        lblCudaStatus.Text = hasCuda ? "CUDA 可用" : "CUDA 不可用，使用 CPU";
        lblCudaStatus.ForeColor = hasCuda ? System.Drawing.Color.DarkGreen : System.Drawing.Color.DarkOrange;
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
            Description = "选择训练产物目录（包含 artifacts/model-metadata.json）",
            UseDescriptionForTitle = true
        };
        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            txtModelRunDir.Text = dialog.SelectedPath;
        }
    }

    private async void btnStartInfer_Click(object sender, EventArgs e)
    {
        if (_isRunning)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(txtConfigPath.Text))
        {
            MessageBox.Show(this, "请先选择推理配置文件。", "参数缺失", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        _isRunning = true;
        btnStartInfer.Enabled = false;
        txtResults.Clear();
        txtResults.AppendText("正在启动推理...\r\n");

        try
        {
            DeviceType? device = cmbDevice.SelectedItem?.ToString() switch
            {
                "Cpu" => DeviceType.Cpu,
                "Cuda" => DeviceType.Cuda,
                _ => null
            };

            var maxSamples = 0;
            if (!string.IsNullOrWhiteSpace(txtMaxSamples.Text) &&
                int.TryParse(txtMaxSamples.Text, out var ms) && ms > 0)
            {
                maxSamples = ms;
            }

            var command = new RunInferenceCommand
            {
                ConfigPath = txtConfigPath.Text,
                ModelRunDirectory = txtModelRunDir.Text,
                RequestedRunName = string.IsNullOrWhiteSpace(txtRunName.Text) ? null : txtRunName.Text,
                ForcedDevice = device,
                MaxSamples = maxSamples
            };

            var result = await _inferenceOrchestrator.ExecuteAsync(command, CancellationToken.None);

            txtResults.Text = await InferenceReportFormatter.BuildSummaryAsync(result, CancellationToken.None);
        }
        catch (Exception ex)
        {
            txtResults.AppendText($"\r\n推理失败：{ex.Message}\r\n");
            MessageBox.Show(this, ex.Message, "推理失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            _isRunning = false;
            btnStartInfer.Enabled = true;
        }
    }

    private void btnClose_Click(object sender, EventArgs e)
    {
        Close();
    }
}
