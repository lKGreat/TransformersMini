using TransformersMini.Contracts.Abstractions;
using TransformersMini.Contracts.Runtime;
using TransformersMini.SharedKernel.Core;

namespace TransformersMini.WinForms;

public sealed class InferenceWorkspaceControl : UserControl
{
    private readonly IInferenceOrchestrator _inferenceOrchestrator;
    private readonly ISystemProbe _systemProbe;
    private readonly IRunQueryRepository _runQueryRepository;
    private readonly IWorkspaceShellContext _shell;

    private readonly TextBox _configPath = new() { Width = 520, PlaceholderText = "推理配置文件（resolved-config.json 或 train config）" };
    private readonly TextBox _modelRunDir = new() { Width = 520, PlaceholderText = "训练产物目录（run目录）" };
    private readonly TextBox _runName = new() { Width = 180, PlaceholderText = "可选推理run名" };
    private readonly TextBox _maxSamples = new() { Width = 80, Text = "20" };
    private readonly ComboBox _device = new() { Width = 120, DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly TextBox _singleImagePath = new() { Width = 520, PlaceholderText = "单图路径（可选，当前后端按maxSamples=1执行）" };
    private readonly TextBox _resultText = new() { Dock = DockStyle.Fill, Multiline = true, ScrollBars = ScrollBars.Both, ReadOnly = true };
    private readonly Label _runtimeHint = new() { AutoSize = true };
    private bool _isRunning;

    public InferenceWorkspaceControl(
        IInferenceOrchestrator inferenceOrchestrator,
        ISystemProbe systemProbe,
        IRunQueryRepository runQueryRepository,
        IWorkspaceShellContext shell)
    {
        _inferenceOrchestrator = inferenceOrchestrator;
        _systemProbe = systemProbe;
        _runQueryRepository = runQueryRepository;
        _shell = shell;
        Dock = DockStyle.Fill;

        _device.Items.AddRange(["Auto", "Cpu", "Cuda"]);
        _device.SelectedIndex = 0;
        _device.SelectedIndexChanged += (_, _) => RefreshRuntimeHint();

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 210,
            ColumnCount = 1,
            RowCount = 7
        };
        layout.RowStyles.Clear();
        for (var i = 0; i < 7; i++)
        {
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
        }

        layout.Controls.Add(BuildLine("配置文件", _configPath, "选择文件", () =>
        {
            _shell.PickFile(_configPath.Text, path => _configPath.Text = path);
        }), 0, 0);
        layout.Controls.Add(BuildLine("模型目录", _modelRunDir, "选择目录", () =>
        {
            _shell.PickFolder(_modelRunDir.Text, path => _modelRunDir.Text = path);
        }), 0, 1);
        layout.Controls.Add(BuildLine("单图路径", _singleImagePath, "选择文件", () =>
        {
            _shell.PickFile(_singleImagePath.Text, path => _singleImagePath.Text = path);
        }), 0, 2);

        var runLine = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false };
        runLine.Controls.Add(new Label { Text = "Run名", Width = 50, TextAlign = ContentAlignment.MiddleLeft });
        runLine.Controls.Add(_runName);
        runLine.Controls.Add(new Label { Text = "设备", Width = 40, TextAlign = ContentAlignment.MiddleLeft });
        runLine.Controls.Add(_device);
        runLine.Controls.Add(new Label { Text = "MaxSamples", Width = 85, TextAlign = ContentAlignment.MiddleLeft });
        runLine.Controls.Add(_maxSamples);
        layout.Controls.Add(runLine, 0, 3);

        var actionLine = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false };
        var batchBtn = new Button { Width = 120, Height = 26, Text = "批量推理" };
        var singleBtn = new Button { Width = 120, Height = 26, Text = "单图推理" };
        var quickBtn = new Button { Width = 180, Height = 26, Text = "一键推理最近训练产物" };
        batchBtn.Click += async (_, _) => await RunInferenceAsync(singleMode: false);
        singleBtn.Click += async (_, _) => await RunInferenceAsync(singleMode: true);
        quickBtn.Click += async (_, _) => await RunQuickLatestAsync();
        actionLine.Controls.Add(batchBtn);
        actionLine.Controls.Add(singleBtn);
        actionLine.Controls.Add(quickBtn);
        layout.Controls.Add(actionLine, 0, 4);
        layout.Controls.Add(_runtimeHint, 0, 5);

        var desc = new Label
        {
            Dock = DockStyle.Fill,
            Text = "说明：无弹框模式下所有路径通过内嵌文件选择器设置；结果在下方面板显示。",
            AutoSize = true
        };
        layout.Controls.Add(desc, 0, 6);

        Controls.Add(_resultText);
        Controls.Add(layout);
        RefreshRuntimeHint();
    }

    private static Control BuildLine(string title, Control editor, string buttonText, Action clickAction)
    {
        var line = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false };
        line.Controls.Add(new Label { Text = title, Width = 64, TextAlign = ContentAlignment.MiddleLeft });
        line.Controls.Add(editor);
        var btn = new Button { Width = 90, Height = 24, Text = buttonText };
        btn.Click += (_, _) => clickAction();
        line.Controls.Add(btn);
        return line;
    }

    private async Task RunQuickLatestAsync()
    {
        try
        {
            var query = await _runQueryRepository.QueryAsync(new RunQueryFilter
            {
                Mode = "Train",
                Status = "Succeeded",
                Limit = 1,
                OrderBy = "started_at desc"
            }, CancellationToken.None);
            var latest = query.Items.FirstOrDefault();
            if (latest is null)
            {
                _shell.ShowWarning("未找到可用的成功训练运行。");
                return;
            }

            var resolvedConfigPath = Path.Combine(latest.RunDirectory, "resolved-config.json");
            _configPath.Text = File.Exists(resolvedConfigPath) ? resolvedConfigPath : latest.ConfigPath;
            _modelRunDir.Text = latest.RunDirectory;
            _runName.Text = $"{latest.RunName}-quick-infer";
            _maxSamples.Text = "20";
            await RunInferenceAsync(singleMode: false);
        }
        catch (Exception ex)
        {
            _shell.ShowError($"一键推理失败：{ex.Message}");
        }
    }

    private async Task RunInferenceAsync(bool singleMode)
    {
        if (_isRunning)
        {
            return;
        }

        try
        {
            if (string.IsNullOrWhiteSpace(_configPath.Text) || !File.Exists(_configPath.Text))
            {
                _shell.ShowWarning("请先选择有效配置文件。");
                return;
            }

            _isRunning = true;
            _resultText.Text = "正在执行推理...\r\n";

            var forcedDevice = _device.SelectedItem?.ToString() switch
            {
                "Cpu" => DeviceType.Cpu,
                "Cuda" => DeviceType.Cuda,
                _ => DeviceType.Auto
            };
            if (!IsTorchSharpCudaBuild() && forcedDevice == DeviceType.Auto)
            {
                forcedDevice = DeviceType.Cpu;
            }

            var maxSamples = 0;
            if (!int.TryParse(_maxSamples.Text, out maxSamples) || maxSamples < 0)
            {
                maxSamples = 0;
            }

            if (singleMode)
            {
                maxSamples = 1;
            }

            var result = await _inferenceOrchestrator.ExecuteAsync(new RunInferenceCommand
            {
                ConfigPath = _configPath.Text,
                ModelRunDirectory = _modelRunDir.Text,
                RequestedRunName = string.IsNullOrWhiteSpace(_runName.Text) ? null : _runName.Text,
                ForcedDevice = forcedDevice,
                MaxSamples = maxSamples
            }, CancellationToken.None);

            _resultText.Text = await InferenceReportFormatter.BuildSummaryAsync(result, CancellationToken.None);
            _shell.ShowInfo($"推理完成：{result.RunId}");
        }
        catch (Exception ex)
        {
            _resultText.AppendText($"\r\n推理失败：{ex.Message}\r\n");
            _shell.ShowError($"推理失败：{ex.Message}");
        }
        finally
        {
            _isRunning = false;
        }
    }

    private void RefreshRuntimeHint()
    {
        var selectedDevice = _device.SelectedItem?.ToString() ?? "Auto";
        var hasCuda = _systemProbe.IsCudaAvailable();
        var isCudaBuild = IsTorchSharpCudaBuild();

        if (selectedDevice == "Cuda" && !isCudaBuild)
        {
            _runtimeHint.Text = "提示：当前程序为 CPU 构建模式，不能使用 CUDA 设备。";
            _runtimeHint.ForeColor = Color.DarkRed;
            return;
        }

        if (selectedDevice == "Cuda" && !hasCuda)
        {
            _runtimeHint.Text = "提示：当前机器未检测到可用 CUDA 设备。";
            _runtimeHint.ForeColor = Color.DarkRed;
            return;
        }

        _runtimeHint.Text = hasCuda
            ? "运行时状态：已检测到 CUDA GPU。"
            : "运行时状态：未检测到 CUDA GPU，将使用 CPU。";
        _runtimeHint.ForeColor = Color.DarkGreen;
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
