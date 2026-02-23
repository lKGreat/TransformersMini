using TransformersMini.Contracts.Abstractions;
using TransformersMini.Contracts.Runtime;

namespace TransformersMini.WinForms;

public sealed class InferencePanel : UserControl
{
    public InferencePanel(IInferenceOrchestrator inferenceOrchestrator, ISystemProbe systemProbe, IRunQueryRepository runQueryRepository)
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

        var quickInferLatestButton = new Button
        {
            Text = "一键推理最近训练产物",
            Width = 200,
            Height = 36,
            Top = 96,
            Left = 376
        };
        quickInferLatestButton.Click += async (_, _) =>
        {
            quickInferLatestButton.Enabled = false;
            try
            {
                var query = await runQueryRepository.QueryAsync(new RunQueryFilter
                {
                    Mode = "Train",
                    Status = "Succeeded",
                    Limit = 1,
                    OrderBy = "started_at desc"
                }, CancellationToken.None);

                var latest = query.Items.FirstOrDefault();
                if (latest is null)
                {
                    MessageBox.Show(this, "未找到成功训练的历史运行，无法一键推理。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                var resolvedConfigPath = Path.Combine(latest.RunDirectory, "resolved-config.json");
                var configPath = File.Exists(resolvedConfigPath) ? resolvedConfigPath : latest.ConfigPath;
                if (string.IsNullOrWhiteSpace(configPath) || !File.Exists(configPath))
                {
                    MessageBox.Show(this, $"未找到可用配置文件。\r\n尝试路径：{resolvedConfigPath}", "一键推理失败", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                var form = new BatchInferenceForm(inferenceOrchestrator, systemProbe);
                form.Show(this);
                form.ApplyQuickPreset(
                    configPath,
                    latest.RunDirectory,
                    $"{latest.RunName}-quick-infer",
                    maxSamples: 20,
                    autoStart: true);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"一键推理失败：{ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                quickInferLatestButton.Enabled = true;
            }
        };

        Controls.Add(title);
        Controls.Add(description);
        Controls.Add(openBatchButton);
        Controls.Add(openSingleButton);
        Controls.Add(quickInferLatestButton);
    }
}
