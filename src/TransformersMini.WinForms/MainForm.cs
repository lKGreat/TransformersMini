using System.Text;
using TransformersMini.Contracts.Abstractions;
using TransformersMini.Contracts.Runtime;
using TransformersMini.SharedKernel.Core;

namespace TransformersMini.WinForms;

public sealed class MainForm : Form
{
    private readonly IRunControlService _runControl;
    private readonly TextBox _configPathText = new() { Dock = DockStyle.Top, PlaceholderText = "Config path..." };
    private readonly ComboBox _modeCombo = new() { Dock = DockStyle.Top, DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly ComboBox _deviceCombo = new() { Dock = DockStyle.Top, DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly Button _browseButton = new() { Text = "Browse Config" };
    private readonly Button _startButton = new() { Text = "Start" };
    private readonly Button _cancelButton = new() { Text = "Cancel Selected" };
    private readonly Button _refreshButton = new() { Text = "Refresh" };
    private readonly DataGridView _runsGrid = new() { Dock = DockStyle.Fill, ReadOnly = true, AutoGenerateColumns = true };
    private readonly TextBox _detailsText = new() { Dock = DockStyle.Bottom, Multiline = true, Height = 180, ScrollBars = ScrollBars.Both };
    private readonly System.Windows.Forms.Timer _timer = new() { Interval = 1500 };

    public MainForm(IRunControlService runControl)
    {
        _runControl = runControl;
        Text = "TransformersMini Training Workbench";
        Width = 1200;
        Height = 800;

        _modeCombo.Items.AddRange(new object[] { "Run", "Train", "Validate", "Test" });
        _modeCombo.SelectedIndex = 0;
        _deviceCombo.Items.AddRange(new object[] { "Auto", "Cpu", "Cuda" });
        _deviceCombo.SelectedIndex = 0;

        var topPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 38,
            AutoSize = false,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false
        };
        topPanel.Controls.AddRange([_browseButton, _startButton, _cancelButton, _refreshButton]);

        var configPanel = new Panel { Dock = DockStyle.Top, Height = 80 };
        _configPathText.Top = 6;
        _configPathText.Left = 6;
        _configPathText.Width = 1150;
        _modeCombo.Top = 42;
        _modeCombo.Left = 6;
        _modeCombo.Width = 180;
        _deviceCombo.Top = 42;
        _deviceCombo.Left = 200;
        _deviceCombo.Width = 180;
        configPanel.Controls.AddRange([_configPathText, _modeCombo, _deviceCombo]);

        Controls.Add(_runsGrid);
        Controls.Add(_detailsText);
        Controls.Add(topPanel);
        Controls.Add(configPanel);

        _browseButton.Click += BrowseButton_Click;
        _startButton.Click += StartButton_Click;
        _cancelButton.Click += CancelButton_Click;
        _refreshButton.Click += async (_, _) => await RefreshRunsAsync();
        _runsGrid.SelectionChanged += async (_, _) => await LoadSelectedRunAsync();
        _timer.Tick += async (_, _) => await RefreshRunsAsync();
        Load += async (_, _) =>
        {
            _timer.Start();
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
        var runs = await _runControl.ListRunsAsync(CancellationToken.None);
        _runsGrid.DataSource = runs.ToList();
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
        sb.AppendLine("Events:");
        foreach (var evt in detail.Events.TakeLast(20))
        {
            sb.AppendLine($"- [{evt.Level}] {evt.EventType}: {evt.Message}");
        }

        _detailsText.Text = sb.ToString();
    }
}
