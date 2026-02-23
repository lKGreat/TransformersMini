namespace TransformersMini.WinForms;

public interface IWorkspaceShellContext
{
    void ShowInfo(string message);
    void ShowWarning(string message);
    void ShowError(string message);
    void PickFile(string? initialPath, Action<string> onSelected);
    void PickFolder(string? initialPath, Action<string> onSelected);
}
