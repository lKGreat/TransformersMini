using System.Text;
using System.Text.Json;
using TransformersMini.Contracts.Abstractions;
using TransformersMini.Contracts.Runtime;
using TransformersMini.SharedKernel.Core;

namespace TransformersMini.WinForms;

public sealed class MainForm : Form
{
    private readonly IRunControlService _runControl;
    private readonly ISystemProbe _systemProbe;
    private readonly TextBox _configPathText = new() { Dock = DockStyle.Top, PlaceholderText = "Config path..." };
    private readonly ComboBox _modeCombo = new() { Dock = DockStyle.Top, DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly ComboBox _deviceCombo = new() { Dock = DockStyle.Top, DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly ComboBox _buildModeCombo = new() { Dock = DockStyle.Top, DropDownStyle = ComboBoxStyle.DropDownList, Enabled = false };
    private readonly Label _buildModeLabel = new() { AutoSize = true, Text = "TorchSharp 构建模式：" };
    private readonly Label _runtimeHintLabel = new() { AutoSize = false, Height = 34, Width = 760 };
    private readonly Button _browseButton = new() { Text = "Browse Config" };
    private readonly Button _startButton = new() { Text = "Start" };
    private readonly Button _cancelButton = new() { Text = "Cancel Selected" };
    private readonly Button _refreshButton = new() { Text = "Refresh" };
    private readonly TextBox _tagFilterKeyText = new() { Width = 260, PlaceholderText = "Tag Key (e.g. det.preprocess.target_box_strategy)" };
    private readonly TextBox _tagFilterValueText = new() { Width = 170, PlaceholderText = "Tag Value" };
    private readonly Button _applyFilterButton = new() { Text = "Apply Filter" };
    private readonly Button _clearFilterButton = new() { Text = "Clear Filter" };
    private readonly DataGridView _runsGrid = new() { Dock = DockStyle.Fill, ReadOnly = true, AutoGenerateColumns = true };
    private readonly TextBox _detailsText = new() { Dock = DockStyle.Bottom, Multiline = true, Height = 220, ScrollBars = ScrollBars.Both };
    private readonly System.Windows.Forms.Timer _timer = new() { Interval = 1500 };
    private bool _isRefreshing;

    public MainForm(IRunControlService runControl, ISystemProbe systemProbe)
    {
        _runControl = runControl;
        _systemProbe = systemProbe;
        Text = "TransformersMini Training Workbench";
        Width = 1200;
        Height = 820;

        _modeCombo.Items.AddRange(new object[] { "Run", "Train", "Validate", "Test" });
        _modeCombo.SelectedIndex = 0;
        _deviceCombo.Items.AddRange(new object[] { "Auto", "Cpu", "Cuda" });
        _deviceCombo.SelectedIndex = 0;
        _buildModeCombo.Items.AddRange(new object[] { GetTorchSharpBuildModeText() });
        _buildModeCombo.SelectedIndex = 0;

        var topPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 38,
            AutoSize = false,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false
        };
        topPanel.Controls.AddRange([_browseButton, _startButton, _cancelButton, _refreshButton, _tagFilterKeyText, _tagFilterValueText, _applyFilterButton, _clearFilterButton]);

        var configPanel = new Panel { Dock = DockStyle.Top, Height = 82 };
        _configPathText.Top = 6;
        _configPathText.Left = 6;
        _configPathText.Width = 1150;
        _modeCombo.Top = 42;
        _modeCombo.Left = 6;
        _modeCombo.Width = 180;
        _deviceCombo.Top = 42;
        _deviceCombo.Left = 200;
        _deviceCombo.Width = 180;
        _buildModeLabel.Top = 45;
        _buildModeLabel.Left = 400;
        _buildModeCombo.Top = 42;
        _buildModeCombo.Left = 520;
        _buildModeCombo.Width = 160;
        _runtimeHintLabel.Top = 42;
        _runtimeHintLabel.Left = 700;
        _runtimeHintLabel.Width = 450;
        configPanel.Controls.AddRange([_configPathText, _modeCombo, _deviceCombo, _buildModeLabel, _buildModeCombo, _runtimeHintLabel]);

        Controls.Add(_runsGrid);
        Controls.Add(_detailsText);
        Controls.Add(topPanel);
        Controls.Add(configPanel);

        _browseButton.Click += BrowseButton_Click;
        _startButton.Click += StartButton_Click;
        _cancelButton.Click += CancelButton_Click;
        _refreshButton.Click += async (_, _) => await RefreshRunsAsync();
        _applyFilterButton.Click += async (_, _) => await RefreshRunsAsync();
        _clearFilterButton.Click += async (_, _) =>
        {
            _tagFilterKeyText.Text = string.Empty;
            _tagFilterValueText.Text = string.Empty;
            await RefreshRunsAsync();
        };
        _runsGrid.SelectionChanged += async (_, _) => await LoadSelectedRunAsync();
        _timer.Tick += async (_, _) => await RefreshRunsAsync();
        _deviceCombo.SelectedIndexChanged += (_, _) => RefreshRuntimeHint();
        Load += async (_, _) =>
        {
            _timer.Start();
            RefreshRuntimeHint();
            await RefreshRunsAsync();
        };
    }

    private void BrowseButton_Click(object? sender, EventArgs e)
    {
        using var dialog = new OpenFileDialog
        {
            Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*"
        };
        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            _configPathText.Text = dialog.FileName;
        }
    }

    private async void StartButton_Click(object? sender, EventArgs e)
    {
        try
        {
            if (!ValidateBuildModeAndDeviceSelection(out var warningText))
            {
                MessageBox.Show(this, warningText, "运行配置提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var mode = _modeCombo.SelectedItem?.ToString() ?? "Run";
            var runId = await _runControl.StartAsync(new RunTrainingCommand
            {
                ConfigPath = _configPathText.Text,
                ForcedMode = mode switch
                {
                    "Train" => RunMode.Train,
                    "Validate" => RunMode.Validate,
                    "Test" => RunMode.Test,
                    _ => null
                },
                ForcedDevice = _deviceCombo.SelectedItem?.ToString() switch
                {
                    "Cpu" => DeviceType.Cpu,
                    "Cuda" => DeviceType.Cuda,
                    _ => DeviceType.Auto
                }
            }, CancellationToken.None);

            _detailsText.Text = $"Started run: {runId}";
            await RefreshRunsAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Start failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private async void CancelButton_Click(object? sender, EventArgs e)
    {
        if (_runsGrid.CurrentRow?.DataBoundItem is not RunSummaryDto run)
        {
            return;
        }

        await _runControl.CancelAsync(run.RunId, CancellationToken.None);
        await RefreshRunsAsync();
    }

    private async Task RefreshRunsAsync()
    {
        if (_isRefreshing)
        {
            return;
        }

        _isRefreshing = true;
        try
        {
            var runs = await _runControl.ListRunsAsync(CancellationToken.None);
            var filteredRuns = await ApplyTagFiltersAsync(runs);
            _runsGrid.DataSource = filteredRuns.ToList();
        }
        finally
        {
            _isRefreshing = false;
        }
    }

    private async Task<IReadOnlyList<RunSummaryDto>> ApplyTagFiltersAsync(IReadOnlyList<RunSummaryDto> runs)
    {
        var tagKeyFilter = (_tagFilterKeyText.Text ?? string.Empty).Trim();
        var tagValueFilter = (_tagFilterValueText.Text ?? string.Empty).Trim();

        if (string.IsNullOrWhiteSpace(tagKeyFilter) && string.IsNullOrWhiteSpace(tagValueFilter))
        {
            return runs;
        }

        var result = new List<RunSummaryDto>(runs.Count);
        foreach (var run in runs)
        {
            var detail = await _runControl.GetRunAsync(run.RunId, CancellationToken.None);
            if (detail is null)
            {
                continue;
            }

            var matched = detail.Tags.Any(tag =>
                (string.IsNullOrWhiteSpace(tagKeyFilter) || tag.Key.Contains(tagKeyFilter, StringComparison.OrdinalIgnoreCase)) &&
                (string.IsNullOrWhiteSpace(tagValueFilter) || tag.Value.Contains(tagValueFilter, StringComparison.OrdinalIgnoreCase)));

            if (matched)
            {
                result.Add(run);
            }
        }

        return result;
    }

    private async Task LoadSelectedRunAsync()
    {
        if (_runsGrid.CurrentRow?.DataBoundItem is not RunSummaryDto run)
        {
            return;
        }

        var detail = await _runControl.GetRunAsync(run.RunId, CancellationToken.None);
        if (detail is null)
        {
            return;
        }

        var sb = new StringBuilder();
        sb.AppendLine($"RunId: {detail.RunId}");
        sb.AppendLine($"Status: {detail.Status}");
        sb.AppendLine($"Task/Backend/Mode: {detail.Task}/{detail.Backend}/{detail.Mode}");
        sb.AppendLine($"Device: {detail.Device}");
        sb.AppendLine($"Config: {detail.ConfigPath}");
        sb.AppendLine($"RunDir: {detail.RunDirectory}");
        sb.AppendLine($"Message: {detail.Message}");
        sb.AppendLine();
        sb.AppendLine("Metrics:");
        foreach (var metric in detail.Metrics.TakeLast(20))
        {
            sb.AppendLine($"- {metric.Name} step={metric.Step} value={metric.Value}");
        }

        sb.AppendLine();
        sb.AppendLine("Latest Metrics:");
        foreach (var metric in detail.LatestMetrics.OrderBy(x => x.Name))
        {
            sb.AppendLine($"- {metric.Name} step={metric.Step} value={metric.Value}");
        }

        sb.AppendLine();
        sb.AppendLine("Tags:");
        foreach (var tag in detail.Tags.OrderBy(x => x.Key))
        {
            sb.AppendLine($"- {tag.Key} = {tag.Value}");
        }

        sb.AppendLine();
        sb.AppendLine("Artifacts:");
        foreach (var artifact in detail.Artifacts.OrderByDescending(x => x.UpdatedAt).Take(50))
        {
            sb.AppendLine($"- [{artifact.Kind}] {artifact.Path} ({artifact.SizeBytes} bytes)");
        }

        AppendPreprocessingSection(sb, detail);

        sb.AppendLine();
        sb.AppendLine("Events:");
        foreach (var evt in detail.Events.TakeLast(20))
        {
            sb.AppendLine($"- [{evt.Level}] {evt.EventType}: {evt.Message}");
        }

        _detailsText.Text = sb.ToString();
    }

    private static void AppendPreprocessingSection(StringBuilder sb, RunDetailDto detail)
    {
        var reportCandidates = new[]
        {
            Path.Combine(detail.RunDirectory, "reports", "summary.json"),
            Path.Combine(detail.RunDirectory, "reports", "validate.json"),
            Path.Combine(detail.RunDirectory, "reports", "test.json")
        };

        var reportPath = reportCandidates.FirstOrDefault(File.Exists);
        if (string.IsNullOrWhiteSpace(reportPath))
        {
            return;
        }

        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(reportPath));
            if (!doc.RootElement.TryGetProperty("preprocessing", out var preprocessing) || preprocessing.ValueKind != JsonValueKind.Object)
            {
                return;
            }

            sb.AppendLine();
            sb.AppendLine("Preprocessing:");

            if (preprocessing.TryGetProperty("InputSize", out var inputSize))
            {
                sb.AppendLine($"- InputSize = {inputSize.GetRawText()}");
            }

            if (preprocessing.TryGetProperty("resizeSampler", out var resizeSampler))
            {
                sb.AppendLine($"- resizeSampler = {resizeSampler.GetString()}");
            }

            if (preprocessing.TryGetProperty("targetBoxStrategy", out var targetBoxStrategy))
            {
                sb.AppendLine($"- targetBoxStrategy = {targetBoxStrategy.GetString()}");
            }

            if (preprocessing.TryGetProperty("normalizeMean", out var normalizeMean))
            {
                sb.AppendLine($"- normalizeMean = {normalizeMean.GetRawText()}");
            }

            if (preprocessing.TryGetProperty("normalizeStd", out var normalizeStd))
            {
                sb.AppendLine($"- normalizeStd = {normalizeStd.GetRawText()}");
            }
        }
        catch
        {
            // 中文说明：详情面板读取报告失败时忽略，不影响主流程。
        }
    }
    private void RefreshRuntimeHint()
    {
        var selectedDevice = _deviceCombo.SelectedItem?.ToString() ?? "Auto";
        var hasCuda = _systemProbe.IsCudaAvailable();
        var isCudaBuild = IsTorchSharpCudaBuild();

        if (selectedDevice == "Cuda" && !isCudaBuild)
        {
            _runtimeHintLabel.Text = "提示：当前程序为 CPU 构建模式，不能使用 CUDA 设备。请使用 CUDA 构建重新生成。";
            _runtimeHintLabel.ForeColor = Color.DarkRed;
            return;
        }

        if (selectedDevice == "Cuda" && !hasCuda)
        {
            _runtimeHintLabel.Text = "提示：当前机器未检测到可用 CUDA 设备或驱动不可用。";
            _runtimeHintLabel.ForeColor = Color.DarkRed;
            return;
        }

        if (selectedDevice == "Auto" && !isCudaBuild && hasCuda)
        {
            _runtimeHintLabel.Text = "提示：检测到 CUDA GPU，但当前是 CPU 构建模式，Auto 将退回 CPU。";
            _runtimeHintLabel.ForeColor = Color.DarkOrange;
            return;
        }

        _runtimeHintLabel.Text = hasCuda
            ? "运行时状态：已检测到 CUDA GPU。"
            : "运行时状态：未检测到 CUDA GPU，将使用 CPU。";
        _runtimeHintLabel.ForeColor = Color.DarkGreen;
    }

    private bool ValidateBuildModeAndDeviceSelection(out string warningText)
    {
        var selectedDevice = _deviceCombo.SelectedItem?.ToString() ?? "Auto";
        var isCudaBuild = IsTorchSharpCudaBuild();
        var hasCuda = _systemProbe.IsCudaAvailable();

        if (selectedDevice == "Cuda" && !isCudaBuild)
        {
            warningText = "当前 WinForms 程序为 CPU 构建模式，但你选择了 CUDA 设备。请使用 CUDA 构建（UseTorchSharpCuda=true）后再运行。";
            return false;
        }

        if (selectedDevice == "Cuda" && !hasCuda)
        {
            warningText = "当前机器未检测到可用 CUDA GPU/驱动，无法按 CUDA 设备运行。";
            return false;
        }

        warningText = string.Empty;
        return true;
    }

    private static string GetTorchSharpBuildModeText() => IsTorchSharpCudaBuild() ? "CUDA（编译时）" : "CPU（编译时）";

    private static bool IsTorchSharpCudaBuild()
    {
#if TORCHSHARP_CUDA_BUILD
        return true;
#else
        return false;
#endif
    }
}


