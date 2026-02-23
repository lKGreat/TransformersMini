using TransformersMini.Contracts.Abstractions;

namespace TransformersMini.WinForms;

public sealed class AnnotationWorkspaceHostControl : UserControl
{
    public AnnotationWorkspaceHostControl(IAnnotationService annotationService, IWorkspaceShellContext shell)
    {
        Dock = DockStyle.Fill;

        var hint = new Label
        {
            Dock = DockStyle.Top,
            Height = 34,
            TextAlign = ContentAlignment.MiddleLeft,
            Text = "标注工作区已内嵌到主界面。建议先在“训练/推理”页完成数据与产物准备后再标注。"
        };

        var host = new Panel { Dock = DockStyle.Fill };
        var form = new AnnotationWorkspaceForm(annotationService)
        {
            TopLevel = false,
            FormBorderStyle = FormBorderStyle.None,
            Dock = DockStyle.Fill
        };
        host.Controls.Add(form);
        form.Show();

        Controls.Add(host);
        Controls.Add(hint);
        shell.ShowInfo("标注工作区已加载。");
    }
}
