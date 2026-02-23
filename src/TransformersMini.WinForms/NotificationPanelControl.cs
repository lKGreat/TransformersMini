namespace TransformersMini.WinForms;

public sealed class NotificationPanelControl : UserControl
{
    private readonly Label _messageLabel = new()
    {
        Dock = DockStyle.Fill,
        AutoEllipsis = true,
        TextAlign = ContentAlignment.MiddleLeft
    };

    private readonly Button _clearButton = new()
    {
        Dock = DockStyle.Right,
        Width = 70,
        Text = "清空"
    };

    public NotificationPanelControl()
    {
        Height = 34;
        Dock = DockStyle.Top;
        BorderStyle = BorderStyle.FixedSingle;
        BackColor = Color.WhiteSmoke;
        _clearButton.Click += (_, _) => Clear();
        Controls.Add(_messageLabel);
        Controls.Add(_clearButton);
        ShowInfo("就绪。");
    }

    public void ShowInfo(string message)
    {
        BackColor = Color.WhiteSmoke;
        _messageLabel.ForeColor = Color.Black;
        _messageLabel.Text = $"[信息] {message}";
    }

    public void ShowWarning(string message)
    {
        BackColor = Color.FromArgb(255, 249, 196);
        _messageLabel.ForeColor = Color.DarkOrange;
        _messageLabel.Text = $"[警告] {message}";
    }

    public void ShowError(string message)
    {
        BackColor = Color.FromArgb(255, 235, 238);
        _messageLabel.ForeColor = Color.DarkRed;
        _messageLabel.Text = $"[错误] {message}";
    }

    public void Clear()
    {
        BackColor = Color.WhiteSmoke;
        _messageLabel.ForeColor = Color.Black;
        _messageLabel.Text = string.Empty;
    }
}
