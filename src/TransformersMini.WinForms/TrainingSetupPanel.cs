using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using TransformersMini.Contracts.Abstractions;
using TransformersMini.Contracts.Configurations;
using TransformersMini.Contracts.Runtime;
using TransformersMini.SharedKernel.Core;

namespace TransformersMini.WinForms;

public sealed partial class TrainingSetupPanel : UserControl
{
    private readonly IRunControlService _runControl;
    private readonly IRunQueryRepository _runQueryRepository;
    private readonly ISystemProbe _systemProbe;
    private readonly System.Windows.Forms.Timer _timer = new() { Interval = 1500 };
    private bool _isRefreshing;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    static TrainingSetupPanel()
    {
        JsonOptions.Converters.Add(new JsonStringEnumConverter());
    }

    public TrainingSetupPanel(IRunControlService runControl, IRunQueryRepository runQueryRepository, ISystemProbe systemProbe)
    {
        _runControl = runControl;
        _runQueryRepository = runQueryRepository;
        _systemProbe = systemProbe;
        InitializeComponent();
        InitializeDefaults();

        _timer.Tick += async (_, _) => await RefreshRunsAsync();
        HandleCreated += async (_, _) =>
        {
            _timer.Start();
            RefreshRuntimeHint();
            await RefreshRunsAsync();
        };
    }

    private void InitializeDefaults()
    {
        cmbTask.Items.AddRange(["Detection", "Ocr"]);
        cmbTask.SelectedIndex = 0;
        cmbTask.SelectedIndexChanged += (_, _) => ResetDefaultsForTask();

        cmbDevice.Items.AddRange(["Auto", "Cpu", "Cuda"]);
        cmbDevice.SelectedIndex = 0;
        cmbDevice.SelectedIndexChanged += (_, _) => RefreshRuntimeHint();

        txtArchitecture.Text = "yolo-like";
        txtInputSize.Text = "640";
        txtNumClasses.Text = "2";
        txtEpochs.Text = "10";
        txtBatchSize.Text = "2";
        txtLearningRate.Text = "0.001";

        cmbDatasetFormat.Items.AddRange(["COCO", "YOLO", "OCR-MANIFEST"]);
        cmbDatasetFormat.SelectedItem = "COCO";
    }

    private void ResetDefaultsForTask()
    {
        var task = (cmbTask.SelectedItem?.ToString() ?? "Detection").ToLowerInvariant();
        if (task == "ocr")
        {
            txtArchitecture.Text = "crnn-like";
            txtInputSize.Text = "32";
            txtNumClasses.Text = "0";
            txtEpochs.Text = "2";
            txtBatchSize.Text = "8";
            txtLearningRate.Text = "0.001";
            cmbDatasetFormat.SelectedItem = "OCR-MANIFEST";
            return;
        }

        txtArchitecture.Text = "yolo-like";
        txtInputSize.Text = "640";
        txtNumClasses.Text = "2";
        txtEpochs.Text = "10";
        txtBatchSize.Text = "2";
        txtLearningRate.Text = "0.001";
        cmbDatasetFormat.SelectedItem = "COCO";
    }

    private void btnBrowseAnnotation_Click(object sender, EventArgs e)
    {
        using var dialog = new OpenFileDialog
        {
            Filter = "标注文件 (*.json;*.jsonl;*.txt)|*.json;*.jsonl;*.txt|所有文件 (*.*)|*.*",
            Title = "选择标注文件"
        };
        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        txtAnnotationPath.Text = dialog.FileName;
        TryAutoDetectFormat(dialog.FileName);
    }

    private void btnBrowseImageRoot_Click(object sender, EventArgs e)
    {
        using var dialog = new FolderBrowserDialog
        {
            Description = "选择图像根目录",
            UseDescriptionForTitle = true
        };
        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            txtImageRoot.Text = dialog.SelectedPath;
        }
    }

    private void TryAutoDetectFormat(string annotationPath)
    {
        try
        {
            var extension = Path.GetExtension(annotationPath).ToLowerInvariant();
            if (extension == ".json")
            {
                using var doc = JsonDocument.Parse(File.ReadAllText(annotationPath));
                var root = doc.RootElement;
                if (root.ValueKind == JsonValueKind.Object &&
                    root.TryGetProperty("images", out _) &&
                    root.TryGetProperty("annotations", out _) &&
                    root.TryGetProperty("categories", out _))
                {
                    cmbDatasetFormat.SelectedItem = "COCO";
                }
            }
            else if (extension == ".jsonl")
            {
                cmbDatasetFormat.SelectedItem = "OCR-MANIFEST";
            }
            else if (extension == ".txt")
            {
                cmbDatasetFormat.SelectedItem = "YOLO";
            }
        }
        catch
        {
            // 中文说明：自动识别失败不影响手工选择格式。
        }
    }

    private async void btnStartTrain_Click(object sender, EventArgs e) => await StartRunAsync(RunMode.Train);

    private async void btnStartValidate_Click(object sender, EventArgs e) => await StartRunAsync(RunMode.Validate);

    private async void btnStartTest_Click(object sender, EventArgs e) => await StartRunAsync(RunMode.Test);

    private async Task StartRunAsync(RunMode runMode)
    {
        try
        {
            if (!ValidateBuildModeAndDeviceSelection(out var warningText))
            {
                MessageBox.Show(this, warningText, "运行配置提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var config = BuildTrainingConfig(runMode);
            var configPath = await WriteTempConfigAsync(config);
            lblTempConfig.Text = $"临时配置路径：{configPath}";

            var runId = await _runControl.StartAsync(new RunTrainingCommand
            {
                ConfigPath = configPath,
                ForcedMode = runMode,
                ForcedDevice = config.Device
            }, CancellationToken.None);

            txtDetails.Text = $"Started run: {runId}\r\nTempConfig: {configPath}";
            await RefreshRunsAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "启动失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private TrainingConfig BuildTrainingConfig(RunMode mode)
    {
        var annotationPath = (txtAnnotationPath.Text ?? string.Empty).Trim();
        var imageRoot = (txtImageRoot.Text ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(annotationPath) || !File.Exists(annotationPath))
        {
            throw new InvalidOperationException("请先选择有效的标注文件。");
        }

        var selectedTask = (cmbTask.SelectedItem?.ToString() ?? "Detection").ToLowerInvariant();
        var selectedFormat = (cmbDatasetFormat.SelectedItem?.ToString() ?? "COCO").ToUpperInvariant();
        if (selectedFormat == "YOLO")
        {
            throw new InvalidOperationException("当前训练数据适配器未注册 YOLO，请先导出为 COCO 后训练。");
        }

        if (selectedTask == "detection" && string.IsNullOrWhiteSpace(imageRoot))
        {
            throw new InvalidOperationException("检测任务需要选择图像根目录。");
        }

        if (!int.TryParse(txtEpochs.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var epochs) || epochs <= 0)
        {
            throw new InvalidOperationException("Epochs 必须是大于 0 的整数。");
        }

        if (!int.TryParse(txtBatchSize.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var batchSize) || batchSize <= 0)
        {
            throw new InvalidOperationException("BatchSize 必须是大于 0 的整数。");
        }

        if (!double.TryParse(txtLearningRate.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var learningRate) || learningRate <= 0)
        {
            throw new InvalidOperationException("LearningRate 必须是大于 0 的数值。");
        }

        var dataset = new DatasetConfig();
        if (selectedTask == "ocr")
        {
            dataset.Format = "ocr-manifest-v1";
            dataset.ManifestPath = annotationPath;
            dataset.RootPath = imageRoot;
            dataset.AnnotationPath = annotationPath;
        }
        else
        {
            dataset.Format = "coco";
            dataset.RootPath = imageRoot;
            dataset.AnnotationPath = MakeRelativeIfUnderRoot(imageRoot, annotationPath);
            dataset.TrainSplit = "train";
            dataset.ValSplit = "val";
            dataset.TestSplit = "test";
            dataset.SkipInvalidSamples = false;
        }

        var inputSize = 0;
        _ = int.TryParse(txtInputSize.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out inputSize);
        var numClasses = 0;
        _ = int.TryParse(txtNumClasses.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out numClasses);

        var config = new TrainingConfig
        {
            ConfigVersion = "1.0",
            RunName = string.IsNullOrWhiteSpace(txtRunName.Text) ? string.Empty : txtRunName.Text.Trim(),
            Mode = mode,
            Task = selectedTask == "ocr" ? TaskType.Ocr : TaskType.Detection,
            Backend = BackendType.TorchSharp,
            Device = ToDeviceType(cmbDevice.SelectedItem?.ToString()),
            Dataset = dataset,
            Model = new ModelConfig
            {
                Name = selectedTask == "ocr" ? "ocr-custom" : "det-custom",
                Architecture = string.IsNullOrWhiteSpace(txtArchitecture.Text) ? "custom" : txtArchitecture.Text.Trim(),
                Parameters = inputSize > 0 ? new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase)
                {
                    ["inputSize"] = JsonSerializer.SerializeToElement(inputSize)
                } : new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase)
            },
            Optimization = new OptimizationConfig
            {
                Epochs = epochs,
                BatchSize = batchSize,
                LearningRate = learningRate,
                Seed = 42
            },
            Runtime = new RuntimeConfig
            {
                MaxWorkers = 1,
                Deterministic = true,
                SaveCheckpoints = false,
                CheckpointEveryEpochs = 1
            },
            Output = new OutputConfig
            {
                BaseRunDirectory = "runs",
                ExperimentGroup = "user-training"
            },
            Logging = new LoggingConfig
            {
                Level = "Information",
                WriteJsonLogs = false
            }
        };

        if (config.Task == TaskType.Detection && numClasses > 0)
        {
            config.TaskOptions["numClasses"] = JsonSerializer.SerializeToElement(numClasses);
        }

        return config;
    }

    private static string MakeRelativeIfUnderRoot(string rootPath, string fullFilePath)
    {
        if (string.IsNullOrWhiteSpace(rootPath))
        {
            return fullFilePath;
        }

        var normalizedRoot = Path.GetFullPath(rootPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var normalizedFile = Path.GetFullPath(fullFilePath);
        if (!normalizedFile.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase))
        {
            return normalizedFile;
        }

        return Path.GetRelativePath(normalizedRoot, normalizedFile);
    }

    private static DeviceType ToDeviceType(string? deviceText)
    {
        return deviceText switch
        {
            "Cpu" => DeviceType.Cpu,
            "Cuda" => DeviceType.Cuda,
            _ => DeviceType.Auto
        };
    }

    private static async Task<string> WriteTempConfigAsync(TrainingConfig config)
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "TransformersMini", "temp-configs");
        Directory.CreateDirectory(tempDir);
        var tempPath = Path.Combine(tempDir, $"train-{DateTime.Now:yyyyMMdd-HHmmss}.json");
        var json = JsonSerializer.Serialize(config, JsonOptions);
        await File.WriteAllTextAsync(tempPath, json);
        return tempPath;
    }

    private async void btnCancelRun_Click(object sender, EventArgs e)
    {
        if (runsGrid.CurrentRow?.DataBoundItem is not RunSummaryDto run)
        {
            return;
        }

        await _runControl.CancelAsync(run.RunId, CancellationToken.None);
        await RefreshRunsAsync();
    }

    private async void btnRefresh_Click(object sender, EventArgs e) => await RefreshRunsAsync();

    private async void btnApplyFilter_Click(object sender, EventArgs e) => await RefreshRunsAsync();

    private async void btnClearFilter_Click(object sender, EventArgs e)
    {
        txtTagKey.Text = string.Empty;
        txtTagValue.Text = string.Empty;
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
            var filter = new RunQueryFilter
            {
                TagKey = string.IsNullOrWhiteSpace(txtTagKey.Text) ? null : txtTagKey.Text.Trim(),
                TagValue = string.IsNullOrWhiteSpace(txtTagValue.Text) ? null : txtTagValue.Text.Trim(),
                Limit = 100,
                OrderBy = "started_at desc"
            };

            try
            {
                var queryResult = await _runQueryRepository.QueryAsync(filter, CancellationToken.None);
                runsGrid.DataSource = queryResult.Items.ToList();
            }
            catch
            {
                var runs = await _runControl.ListRunsAsync(CancellationToken.None);
                runsGrid.DataSource = runs.Take(100).ToList();
            }
        }
        finally
        {
            _isRefreshing = false;
        }
    }

    private async void runsGrid_SelectionChanged(object sender, EventArgs e)
    {
        if (runsGrid.CurrentRow?.DataBoundItem is not RunSummaryDto run)
        {
            return;
        }

        var detail = await _runControl.GetRunAsync(run.RunId, CancellationToken.None);
        if (detail is null)
        {
            return;
        }

        txtDetails.Text = BuildRunDetailText(detail);
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

        return sb.ToString();
    }

    private void RefreshRuntimeHint()
    {
        var selectedDevice = cmbDevice.SelectedItem?.ToString() ?? "Auto";
        var hasCuda = _systemProbe.IsCudaAvailable();
        var isCudaBuild = IsTorchSharpCudaBuild();
        if (selectedDevice == "Cuda" && !isCudaBuild)
        {
            lblRuntimeHint.Text = "提示：当前程序为 CPU 构建模式，不能使用 CUDA 设备。";
            lblRuntimeHint.ForeColor = Color.DarkRed;
            return;
        }

        if (selectedDevice == "Cuda" && !hasCuda)
        {
            lblRuntimeHint.Text = "提示：当前机器未检测到可用 CUDA 设备。";
            lblRuntimeHint.ForeColor = Color.DarkRed;
            return;
        }

        lblRuntimeHint.Text = hasCuda
            ? "运行时状态：已检测到 CUDA GPU。"
            : "运行时状态：未检测到 CUDA GPU，将使用 CPU。";
        lblRuntimeHint.ForeColor = Color.DarkGreen;
    }

    private bool ValidateBuildModeAndDeviceSelection(out string warningText)
    {
        var selectedDevice = cmbDevice.SelectedItem?.ToString() ?? "Auto";
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
