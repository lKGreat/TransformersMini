using TransformersMini.Contracts.Abstractions;

namespace TransformersMini.WinForms;

public sealed class InferencePanel : UserControl
{
    public InferencePanel(IInferenceOrchestrator inferenceOrchestrator, ISystemProbe systemProbe)
    {
        Dock = DockStyle.Fill;

        var title = new Label
        {
            Text = "推理工作台（检测 + OCR）",
            Font = new Font("Segoe UI", 12, FontStyle.Bold),
            AutoSize = true,
            Top = 16,
            Left = 16
        };

        var description = new Label
        {
            Text = "统一入口：批量推理用于回归和产物落盘，单图推理用于快速调试和演示。",
            AutoSize = true,
            Top = 52,
            Left = 16
        };

        var openBatchButton = new Button
        {
            Text = "打开批量推理",
            Width = 160,
            Height = 36,
            Top = 96,
            Left = 16
        };
        openBatchButton.Click += (_, _) =>
        {
            var form = new BatchInferenceForm(inferenceOrchestrator, systemProbe);
            form.Show(this);
        };

        var openSingleButton = new Button
        {
            Text = "打开单图推理",
            Width = 160,
            Height = 36,
            Top = 96,
            Left = 196
        };
        openSingleButton.Click += (_, _) =>
        {
            var form = new SingleImageInferenceForm(inferenceOrchestrator);
            form.Show(this);
        };

        Controls.Add(title);
        Controls.Add(description);
        Controls.Add(openBatchButton);
        Controls.Add(openSingleButton);
    }
}
