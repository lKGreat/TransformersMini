namespace TransformersMini.WinForms;

public enum PickerSelectionMode
{
    File,
    Folder
}

public sealed class InlineFilePickerControl : UserControl
{
    private readonly ComboBox _rootsCombo = new() { Dock = DockStyle.Top, DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly TextBox _filterText = new() { Dock = DockStyle.Top, PlaceholderText = "过滤..." };
    private readonly ListBox _directoryList = new() { Dock = DockStyle.Left, Width = 260 };
    private readonly ListBox _fileList = new() { Dock = DockStyle.Fill };
    private readonly Button _selectButton = new() { Dock = DockStyle.Bottom, Height = 32, Text = "选择" };
    private readonly Button _cancelButton = new() { Dock = DockStyle.Bottom, Height = 28, Text = "取消" };
    private string _currentDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
    private PickerSelectionMode _mode = PickerSelectionMode.File;

    public event Action<string>? SelectionConfirmed;
    public event Action? SelectionCancelled;

    public InlineFilePickerControl()
    {
        Dock = DockStyle.Right;
        Width = 460;
        BorderStyle = BorderStyle.FixedSingle;
        Visible = false;

        var center = new Panel { Dock = DockStyle.Fill };
        center.Controls.Add(_fileList);
        center.Controls.Add(_directoryList);

        Controls.Add(center);
        Controls.Add(_selectButton);
        Controls.Add(_cancelButton);
        Controls.Add(_filterText);
        Controls.Add(_rootsCombo);

        _rootsCombo.SelectedIndexChanged += (_, _) =>
        {
            var selected = _rootsCombo.SelectedItem?.ToString();
            if (!string.IsNullOrWhiteSpace(selected))
            {
                LoadDirectory(selected);
            }
        };
        _directoryList.DoubleClick += (_, _) =>
        {
            if (_directoryList.SelectedItem is null)
            {
                return;
            }

            var target = _directoryList.SelectedItem.ToString() ?? string.Empty;
            if (target == "..")
            {
                var parent = Directory.GetParent(_currentDirectory);
                if (parent is not null)
                {
                    LoadDirectory(parent.FullName);
                }

                return;
            }

            var next = Path.Combine(_currentDirectory, target);
            if (Directory.Exists(next))
            {
                LoadDirectory(next);
            }
        };
        _filterText.TextChanged += (_, _) => ApplyFilter();
        _selectButton.Click += (_, _) =>
        {
            var selected = ResolveSelectedPath();
            if (!string.IsNullOrWhiteSpace(selected))
            {
                SelectionConfirmed?.Invoke(selected);
            }
        };
        _cancelButton.Click += (_, _) => SelectionCancelled?.Invoke();

        InitializeRoots();
    }

    public void Open(PickerSelectionMode mode, string? initialPath = null)
    {
        _mode = mode;
        Visible = true;
        BringToFront();
        var target = string.IsNullOrWhiteSpace(initialPath) ? _currentDirectory : initialPath;
        if (File.Exists(target))
        {
            target = Path.GetDirectoryName(target) ?? _currentDirectory;
        }

        if (!Directory.Exists(target))
        {
            target = _currentDirectory;
        }

        LoadDirectory(target);
    }

    public void ClosePicker()
    {
        Visible = false;
    }

    private void InitializeRoots()
    {
        _rootsCombo.Items.Clear();
        foreach (var drive in DriveInfo.GetDrives().Where(d => d.IsReady))
        {
            _rootsCombo.Items.Add(drive.RootDirectory.FullName);
        }

        var desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        var documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        if (!string.IsNullOrWhiteSpace(desktop))
        {
            _rootsCombo.Items.Add(desktop);
        }

        if (!string.IsNullOrWhiteSpace(documents))
        {
            _rootsCombo.Items.Add(documents);
        }

        if (_rootsCombo.Items.Count > 0)
        {
            _rootsCombo.SelectedIndex = 0;
        }
    }

    private void LoadDirectory(string directory)
    {
        try
        {
            _currentDirectory = Path.GetFullPath(directory);
            _directoryList.Items.Clear();
            _fileList.Items.Clear();

            var parent = Directory.GetParent(_currentDirectory);
            if (parent is not null)
            {
                _directoryList.Items.Add("..");
            }

            foreach (var dir in Directory.EnumerateDirectories(_currentDirectory).OrderBy(x => x))
            {
                _directoryList.Items.Add(Path.GetFileName(dir));
            }

            foreach (var file in Directory.EnumerateFiles(_currentDirectory).OrderBy(x => x))
            {
                _fileList.Items.Add(Path.GetFileName(file));
            }

            ApplyFilter();
        }
        catch
        {
            _directoryList.Items.Clear();
            _fileList.Items.Clear();
        }
    }

    private void ApplyFilter()
    {
        var filter = (_filterText.Text ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(filter))
        {
            return;
        }

        for (var i = _directoryList.Items.Count - 1; i >= 0; i--)
        {
            var item = _directoryList.Items[i]?.ToString() ?? string.Empty;
            if (item == "..")
            {
                continue;
            }

            if (!item.Contains(filter, StringComparison.OrdinalIgnoreCase))
            {
                _directoryList.Items.RemoveAt(i);
            }
        }

        for (var i = _fileList.Items.Count - 1; i >= 0; i--)
        {
            var item = _fileList.Items[i]?.ToString() ?? string.Empty;
            if (!item.Contains(filter, StringComparison.OrdinalIgnoreCase))
            {
                _fileList.Items.RemoveAt(i);
            }
        }
    }

    private string? ResolveSelectedPath()
    {
        if (_mode == PickerSelectionMode.Folder)
        {
            if (_directoryList.SelectedItem is null)
            {
                return _currentDirectory;
            }

            var selected = _directoryList.SelectedItem.ToString() ?? string.Empty;
            if (selected == "..")
            {
                return Directory.GetParent(_currentDirectory)?.FullName ?? _currentDirectory;
            }

            var combined = Path.Combine(_currentDirectory, selected);
            return Directory.Exists(combined) ? combined : _currentDirectory;
        }

        if (_fileList.SelectedItem is null)
        {
            return null;
        }

        var file = _fileList.SelectedItem.ToString() ?? string.Empty;
        var path = Path.Combine(_currentDirectory, file);
        return File.Exists(path) ? path : null;
    }
}
