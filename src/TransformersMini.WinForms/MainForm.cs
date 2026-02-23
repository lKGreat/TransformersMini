using TransformersMini.Contracts.Abstractions;

namespace TransformersMini.WinForms;

public sealed class MainForm : Form, IWorkspaceShellContext
{
    private readonly NotificationPanelControl _notificationPanel = new() { Dock = DockStyle.Top };
    private readonly InlineFilePickerControl _picker = new() { Dock = DockStyle.Right };
    private Action<string>? _pickerCallback;
    private readonly Panel _workspaceHost = new() { Dock = DockStyle.Fill };

    public MainForm(
        IRunControlService runControl,
        ISystemProbe systemProbe,
        IInferenceOrchestrator inferenceOrchestrator,
        IRunQueryRepository runQueryRepository,
        IDataTrainingConfigBuilder dataTrainingConfigBuilder,
        IAnnotationService annotationService)
    {
        Text = "TransformersMini Workbench";
        Width = 1400;
        Height = 920;
        MinimumSize = new Size(1320, 820);

        var leftNav = new ListBox
        {
            Dock = DockStyle.Fill,
            Font = new Font("Segoe UI", 10, FontStyle.Regular),
            IntegralHeight = false
        };
        leftNav.Items.AddRange(["训练", "推理", "标注", "运行查询"]);
        leftNav.SelectedIndex = 0;

        var navContainer = new Panel { Dock = DockStyle.Left, Width = 180 };
        var navTitle = new Label
        {
            Dock = DockStyle.Top,
            Height = 36,
            Font = new Font("Segoe UI", 10, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleCenter,
            Text = "工作区导航"
        };
        navContainer.Controls.Add(leftNav);
        navContainer.Controls.Add(navTitle);

        var trainPage = new TrainingSetupPanel(runControl, runQueryRepository, dataTrainingConfigBuilder, systemProbe, this) { Dock = DockStyle.Fill };
        var inferPage = new InferenceWorkspaceControl(inferenceOrchestrator, systemProbe, runQueryRepository, this) { Dock = DockStyle.Fill };
        var annotationPage = new AnnotationWorkspaceHostControl(annotationService, this) { Dock = DockStyle.Fill };
        var queryPage = new RunListAndFilterPanel(runControl, runQueryRepository, systemProbe) { Dock = DockStyle.Fill };

        leftNav.SelectedIndexChanged += (_, _) =>
        {
            _workspaceHost.Controls.Clear();
            _picker.ClosePicker();
            switch (leftNav.SelectedIndex)
            {
                case 0:
                    _workspaceHost.Controls.Add(trainPage);
                    ShowInfo("已切换到训练工作区。");
                    break;
                case 1:
                    _workspaceHost.Controls.Add(inferPage);
                    ShowInfo("已切换到推理工作区。");
                    break;
                case 2:
                    _workspaceHost.Controls.Add(annotationPage);
                    ShowInfo("已切换到标注工作区。");
                    break;
                default:
                    _workspaceHost.Controls.Add(queryPage);
                    ShowInfo("已切换到运行查询工作区。");
                    break;
            }
        };

        _picker.SelectionConfirmed += path =>
        {
            _picker.ClosePicker();
            var callback = _pickerCallback;
            _pickerCallback = null;
            callback?.Invoke(path);
        };
        _picker.SelectionCancelled += () =>
        {
            _pickerCallback = null;
            _picker.ClosePicker();
            ShowInfo("已取消选择。");
        };

        Controls.Add(_workspaceHost);
        Controls.Add(_picker);
        Controls.Add(navContainer);
        Controls.Add(_notificationPanel);
        _workspaceHost.Controls.Add(trainPage);
    }

    public void ShowInfo(string message) => _notificationPanel.ShowInfo(message);
    public void ShowWarning(string message) => _notificationPanel.ShowWarning(message);
    public void ShowError(string message) => _notificationPanel.ShowError(message);

    public void PickFile(string? initialPath, Action<string> onSelected)
    {
        _pickerCallback = onSelected;
        _picker.Open(PickerSelectionMode.File, initialPath);
        ShowInfo("请选择文件。");
    }

    public void PickFolder(string? initialPath, Action<string> onSelected)
    {
        _pickerCallback = onSelected;
        _picker.Open(PickerSelectionMode.Folder, initialPath);
        ShowInfo("请选择目录。");
    }
}
