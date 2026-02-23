using TransformersMini.Contracts.Abstractions;

namespace TransformersMini.WinForms;

public sealed class MainForm : Form
{
    public MainForm(
        IRunControlService runControl,
        ISystemProbe systemProbe,
        IInferenceOrchestrator inferenceOrchestrator,
        IRunQueryRepository runQueryRepository,
        IAnnotationService annotationService)
    {
        Text = "TransformersMini Workbench";
        Width = 1200;
        Height = 820;
        MinimumSize = new Size(1200, 780);

        var tabs = new TabControl { Dock = DockStyle.Fill };
        var runTab = new TabPage("训练");
        var inferenceTab = new TabPage("推理");
        var annotationTab = new TabPage("标注");

        var runPanel = new TrainingSetupPanel(runControl, runQueryRepository, systemProbe);
        runTab.Controls.Add(runPanel);

        var inferencePanel = new InferencePanel(inferenceOrchestrator, systemProbe);
        inferenceTab.Controls.Add(inferencePanel);

        var annotationIntroPanel = new Panel { Dock = DockStyle.Fill };
        var annotationTitle = new Label
        {
            Text = "增强标注工作台",
            Font = new Font("Segoe UI", 13, FontStyle.Bold),
            AutoSize = true,
            Top = 26,
            Left = 24
        };
        var annotationDesc = new Label
        {
            Text = "支持框选/移动/撤销重做、导入推理结果、COCO/YOLO 双向导入导出。",
            AutoSize = true,
            Top = 62,
            Left = 24
        };
        var openAnnotationButton = new Button
        {
            Text = "打开标注工作台",
            Width = 180,
            Height = 38,
            Top = 102,
            Left = 24
        };
        openAnnotationButton.Click += (_, _) =>
        {
            var form = new AnnotationWorkspaceForm(annotationService);
            form.Show(this);
        };
        annotationIntroPanel.Controls.Add(annotationTitle);
        annotationIntroPanel.Controls.Add(annotationDesc);
        annotationIntroPanel.Controls.Add(openAnnotationButton);
        annotationTab.Controls.Add(annotationIntroPanel);

        tabs.TabPages.Add(runTab);
        tabs.TabPages.Add(inferenceTab);
        tabs.TabPages.Add(annotationTab);
        Controls.Add(tabs);
    }
}
