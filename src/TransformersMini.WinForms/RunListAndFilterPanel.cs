using System.Text;
using System.Text.Json;
using TransformersMini.Contracts.Abstractions;
using TransformersMini.Contracts.Runtime;
using TransformersMini.SharedKernel.Core;

namespace TransformersMini.WinForms;

public sealed class RunListAndFilterPanel : UserControl
{
    private readonly IRunControlService _runControl;
    private readonly IRunQueryRepository _runQueryRepository;
    private readonly ISystemProbe _systemProbe;
    private readonly TextBox _configPathText = new() { Dock = DockStyle.Top, PlaceholderText = "Config path..." };
    private readonly ComboBox _modeCombo = new() { Dock = DockStyle.Top, DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly ComboBox _deviceCombo = new() { Dock = DockStyle.Top, DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly Label _runtimeHintLabel = new() { AutoSize = false, Height = 34, Width = 760 };
    private readonly Button _browseButton = new() { Text = "Browse Config" };
    private readonly Button _startButton = new() { Text = "Start" };
    private readonly Button _cancelButton = new() { Text = "Cancel Selected" };
    private readonly Button _refreshButton = new() { Text = "Refresh" };
    private readonly TextBox _tagFilterKeyText = new() { Width = 260, PlaceholderText = "Tag Key" };
    private readonly TextBox _tagFilterValueText = new() { Width = 170, PlaceholderText = "Tag Value" };
    private readonly Button _applyFilterButton = new() { Text = "Apply Filter" };
    private readonly Button _clearFilterButton = new() { Text = "Clear Filter" };
    private readonly DataGridView _runsGrid = new() { Dock = DockStyle.Fill, ReadOnly = true, AutoGenerateColumns = true };
    private readonly TextBox _detailsText = new() { Dock = DockStyle.Bottom, Multiline = true, Height = 260, ScrollBars = ScrollBars.Both };
    private readonly System.Windows.Forms.Timer _timer = new() { Interval = 1500 };
    private bool _isRefreshing;

    public RunListAndFilterPanel(IRunControlService runControl, IRunQueryRepository runQueryRepository, ISystemProbe systemProbe)
    {
        _runControl = runControl;
        _runQueryRepository = runQueryRepository;
        _systemProbe = systemProbe;
        Dock = DockStyle.Fill;

        _modeCombo.Items.AddRange(["Run", "Train", "Validate", "Test"]);
        _modeCombo.SelectedIndex = 0;
        _deviceCombo.Items.AddRange(["Auto", "Cpu", "Cuda"]);
        _deviceCombo.SelectedIndex = 0;

        var topPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 38,
            AutoSize = false,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false
        };
        topPanel.Controls.AddRange([_browseButton, _startButton, _cancelButton, _refreshButton, _tagFilterKeyText, _tagFilterValueText, _applyFilterButton, _clearFilterButton]);

        var configPanel = new Panel { Dock = DockStyle.Top, Height = 72 };
        _configPathText.Top = 6;
        _configPathText.Left = 6;
        _configPathText.Width = 1030;
        _modeCombo.Top = 38;
        _modeCombo.Left = 6;
        _modeCombo.Width = 180;
        _deviceCombo.Top = 38;
        _deviceCombo.Left = 200;
        _deviceCombo.Width = 180;
        _runtimeHintLabel.Top = 40;
        _runtimeHintLabel.Left = 410;
        _runtimeHintLabel.Width = 630;
        configPanel.Controls.AddRange([_configPathText, _modeCombo, _deviceCombo, _runtimeHintLabel]);

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
        HandleCreated += async (_, _) =>
        {
            _timer.Start();
            RefreshRuntimeHint();
            await RefreshRunsAsync();
        };
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _timer.Stop();
            _timer.Dispose();
        }

        base.Dispose(disposing);
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
            var tagKeyFilter = (_tagFilterKeyText.Text ?? string.Empty).Trim();
            var tagValueFilter = (_tagFilterValueText.Text ?? string.Empty).Trim();
            var filter = new RunQueryFilter
            {
                TagKey = string.IsNullOrWhiteSpace(tagKeyFilter) ? null : tagKeyFilter,
                TagValue = string.IsNullOrWhiteSpace(tagValueFilter) ? null : tagValueFilter,
                Limit = 200,
                OrderBy = "started_at desc"
            };

            try
            {
                var queryResult = await _runQueryRepository.QueryAsync(filter, CancellationToken.None);
                _runsGrid.DataSource = queryResult.Items.ToList();
            }
            catch
            {
                var runs = await _runControl.ListRunsAsync(CancellationToken.None);
                _runsGrid.DataSource = runs.ToList();
            }
        }
        finally
        {
            _isRefreshing = false;
        }
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

        _detailsText.Text = BuildRunDetailText(detail);
    }

    private static string BuildRunDetailText(RunDetailDto detail)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"RunId: {detail.RunId}");
        sb.AppendLine($"Status: {detail.Status}");
        sb.AppendLine($"Task/Backend/Mode: {detail.Task}/{detail.Backend}/{detail.Mode}");
        sb.AppendLine($"Device: {detail.Device}");
        sb.AppendLine($"Config: {detail.ConfigPath}");
        sb.AppendLine($"RunDir: {detail.RunDirectory}");
        sb.AppendLine($"Message: {detail.Message}");
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
        foreach (var artifact in detail.Artifacts.OrderByDescending(x => x.UpdatedAt).Take(40))
        {
            sb.AppendLine($"- [{artifact.Kind}] {artifact.Path} ({artifact.SizeBytes} bytes)");
        }

        AppendReportInsights(sb, detail.RunDirectory);
        return sb.ToString();
    }

    private static void AppendReportInsights(StringBuilder sb, string runDirectory)
    {
        var reportCandidates = new[]
        {
            Path.Combine(runDirectory, "reports", "summary.json"),
            Path.Combine(runDirectory, "reports", "validate.json"),
            Path.Combine(runDirectory, "reports", "test.json"),
            Path.Combine(runDirectory, "reports", "inference.json")
        };

        var reportPath = reportCandidates.FirstOrDefault(File.Exists);
        if (string.IsNullOrWhiteSpace(reportPath))
        {
            return;
        }

        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(reportPath));
            var root = doc.RootElement;
            sb.AppendLine();
            sb.AppendLine("Report Snapshot:");
            foreach (var property in root.EnumerateObject().Take(16))
            {
                sb.AppendLine($"- {property.Name}: {property.Value.GetRawText()}");
            }
        }
        catch
        {
            // 中文说明：报告解析失败不影响主流程。
        }
    }

    private void RefreshRuntimeHint()
    {
        var selectedDevice = _deviceCombo.SelectedItem?.ToString() ?? "Auto";
        var hasCuda = _systemProbe.IsCudaAvailable();
        var isCudaBuild = IsTorchSharpCudaBuild();
        if (selectedDevice == "Cuda" && !isCudaBuild)
        {
            _runtimeHintLabel.Text = "提示：当前程序为 CPU 构建模式，不能使用 CUDA 设备。";
            _runtimeHintLabel.ForeColor = Color.DarkRed;
            return;
        }

        if (selectedDevice == "Cuda" && !hasCuda)
        {
            _runtimeHintLabel.Text = "提示：当前机器未检测到可用 CUDA 设备。";
            _runtimeHintLabel.ForeColor = Color.DarkRed;
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
            warningText = "当前 WinForms 程序为 CPU 构建模式，但你选择了 CUDA 设备。";
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

    private static bool IsTorchSharpCudaBuild()
    {
#if TORCHSHARP_CUDA_BUILD
        return true;
#else
        return false;
#endif
    }
}
