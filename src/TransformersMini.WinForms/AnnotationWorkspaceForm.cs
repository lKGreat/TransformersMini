using System.Text.Json;
using TransformersMini.Contracts.Abstractions;
using TransformersMini.Contracts.Runtime;

namespace TransformersMini.WinForms;

public sealed partial class AnnotationWorkspaceForm : Form
{
    private readonly IAnnotationService _annotationService;
    private AnnotationSession? _session;
    private int _currentImageIndex = -1;
    private string? _selectedBoxId;
    private readonly Stack<string> _undoStack = new();
    private readonly Stack<string> _redoStack = new();
    private bool _isDrawing;
    private bool _isMoving;
    private PointF _drawStartImagePoint;
    private PointF _drawCurrentImagePoint;
    private PointF _moveStartImagePoint;
    private RectangleF _movingOriginalRect;
    private static readonly JsonSerializerOptions SnapshotJsonOptions = new() { WriteIndented = false };

    public AnnotationWorkspaceForm(IAnnotationService annotationService)
    {
        _annotationService = annotationService;
        InitializeComponent();
        txtClasses.Text = "person,car,bicycle";
    }

    private async void btnBrowseImageDirectory_Click(object sender, EventArgs e)
    {
        using var dialog = new FolderBrowserDialog
        {
            Description = "选择图片目录",
            UseDescriptionForTitle = true
        };
        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        txtImageDirectory.Text = dialog.SelectedPath;
        var classes = ParseClassesFromText();
        try
        {
            _session = await _annotationService.CreateSessionFromImageDirectoryAsync(dialog.SelectedPath, classes, CancellationToken.None);
            _currentImageIndex = _session.Images.Count > 0 ? 0 : -1;
            _undoStack.Clear();
            _redoStack.Clear();
            UpdateClassCombo();
            RefreshImageList();
            RefreshCurrentImage();
            lblStatus.Text = $"已加载 {_session.Images.Count} 张图片。";
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "加载失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void btnApplyClasses_Click(object sender, EventArgs e)
    {
        if (_session is null)
        {
            return;
        }

        var classes = ParseClassesFromText();
        if (classes.Count == 0)
        {
            MessageBox.Show(this, "至少需要一个类别。", "类别校验", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        CaptureSnapshot();
        _session.ClassNames.Clear();
        _session.ClassNames.AddRange(classes);
        foreach (var image in _session.Images)
        {
            foreach (var box in image.Boxes)
            {
                if (box.ClassId >= 0 && box.ClassId < _session.ClassNames.Count)
                {
                    box.ClassName = _session.ClassNames[box.ClassId];
                }
            }
        }

        UpdateClassCombo();
        RefreshBoxesList();
        picCanvas.Invalidate();
    }

    private void lstImages_SelectedIndexChanged(object sender, EventArgs e)
    {
        if (_session is null || lstImages.SelectedIndex < 0)
        {
            return;
        }

        _currentImageIndex = lstImages.SelectedIndex;
        _selectedBoxId = null;
        RefreshCurrentImage();
    }

    private void lstBoxes_SelectedIndexChanged(object sender, EventArgs e)
    {
        if (_session is null || _currentImageIndex < 0 || lstBoxes.SelectedIndex < 0)
        {
            return;
        }

        var image = _session.Images[_currentImageIndex];
        if (lstBoxes.SelectedIndex >= image.Boxes.Count)
        {
            return;
        }

        _selectedBoxId = image.Boxes[lstBoxes.SelectedIndex].BoxId;
        picCanvas.Invalidate();
    }

    private void btnDeleteBox_Click(object sender, EventArgs e)
    {
        var box = GetSelectedBox();
        if (box is null)
        {
            return;
        }

        CaptureSnapshot();
        var image = _session!.Images[_currentImageIndex];
        image.Boxes.RemoveAll(item => item.BoxId == box.BoxId);
        _selectedBoxId = null;
        RefreshBoxesList();
        picCanvas.Invalidate();
    }

    private void btnPrevImage_Click(object sender, EventArgs e)
    {
        if (_session is null || _session.Images.Count == 0 || _currentImageIndex <= 0)
        {
            return;
        }

        _currentImageIndex--;
        RefreshCurrentImage();
    }

    private void btnNextImage_Click(object sender, EventArgs e)
    {
        if (_session is null || _session.Images.Count == 0 || _currentImageIndex >= _session.Images.Count - 1)
        {
            return;
        }

        _currentImageIndex++;
        RefreshCurrentImage();
    }

    private void btnUndo_Click(object sender, EventArgs e)
    {
        if (_undoStack.Count == 0 || _session is null)
        {
            return;
        }

        _redoStack.Push(SerializeSession(_session));
        _session = DeserializeSession(_undoStack.Pop());
        EnsureCurrentIndexValid();
        UpdateClassCombo();
        RefreshImageList();
        RefreshCurrentImage();
    }

    private void btnRedo_Click(object sender, EventArgs e)
    {
        if (_redoStack.Count == 0 || _session is null)
        {
            return;
        }

        _undoStack.Push(SerializeSession(_session));
        _session = DeserializeSession(_redoStack.Pop());
        EnsureCurrentIndexValid();
        UpdateClassCombo();
        RefreshImageList();
        RefreshCurrentImage();
    }

    private void btnCopyPreviousBoxes_Click(object sender, EventArgs e)
    {
        if (_session is null || _currentImageIndex <= 0)
        {
            return;
        }

        var previous = _session.Images[_currentImageIndex - 1];
        var current = _session.Images[_currentImageIndex];
        CaptureSnapshot();
        current.Boxes.Clear();
        foreach (var box in previous.Boxes)
        {
            current.Boxes.Add(new AnnotationBox
            {
                ClassId = box.ClassId,
                ClassName = box.ClassName,
                X = box.X,
                Y = box.Y,
                Width = box.Width,
                Height = box.Height,
                Score = box.Score,
                Source = "copied-prev"
            });
        }

        RefreshBoxesList();
        picCanvas.Invalidate();
    }

    private void btnApplyClassToAll_Click(object sender, EventArgs e)
    {
        if (_session is null || _currentImageIndex < 0 || cmbCurrentClass.SelectedIndex < 0)
        {
            return;
        }

        var image = _session.Images[_currentImageIndex];
        if (image.Boxes.Count == 0)
        {
            return;
        }

        CaptureSnapshot();
        var classId = cmbCurrentClass.SelectedIndex;
        var className = _session.ClassNames[classId];
        foreach (var box in image.Boxes)
        {
            box.ClassId = classId;
            box.ClassName = className;
        }

        RefreshBoxesList();
        picCanvas.Invalidate();
    }

    private async void btnImportPredictions_Click(object sender, EventArgs e)
    {
        if (_session is null)
        {
            return;
        }

        using var dialog = new OpenFileDialog
        {
            Filter = "JSONL 文件 (*.jsonl)|*.jsonl|所有文件 (*.*)|*.*",
            Title = "选择 inference-samples.jsonl"
        };
        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        try
        {
            CaptureSnapshot();
            _session = await _annotationService.ImportDetectionPredictionsAsync(_session, dialog.FileName, 0.15f, CancellationToken.None);
            RefreshCurrentImage();
            lblStatus.Text = "已导入推理结果。";
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "导入推理失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private async void btnLoadCoco_Click(object sender, EventArgs e)
    {
        using var fileDialog = new OpenFileDialog
        {
            Filter = "COCO 文件 (*.json)|*.json|所有文件 (*.*)|*.*",
            Title = "选择 COCO 标注文件"
        };
        if (fileDialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        using var dirDialog = new FolderBrowserDialog
        {
            Description = "选择 COCO 对应的图像目录",
            UseDescriptionForTitle = true
        };
        if (dirDialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        try
        {
            _session = await _annotationService.LoadFromCocoAsync(fileDialog.FileName, dirDialog.SelectedPath, CancellationToken.None);
            _currentImageIndex = _session.Images.Count > 0 ? 0 : -1;
            _undoStack.Clear();
            _redoStack.Clear();
            txtImageDirectory.Text = dirDialog.SelectedPath;
            txtClasses.Text = string.Join(",", _session.ClassNames);
            UpdateClassCombo();
            RefreshImageList();
            RefreshCurrentImage();
            lblStatus.Text = "COCO 导入完成。";
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "导入 COCO 失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private async void btnLoadYolo_Click(object sender, EventArgs e)
    {
        using var imageDirDialog = new FolderBrowserDialog
        {
            Description = "选择图像目录",
            UseDescriptionForTitle = true
        };
        if (imageDirDialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        using var labelsDirDialog = new FolderBrowserDialog
        {
            Description = "选择 labels 目录",
            UseDescriptionForTitle = true
        };
        if (labelsDirDialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        using var classesDialog = new OpenFileDialog
        {
            Filter = "classes 文件 (*.txt)|*.txt|所有文件 (*.*)|*.*",
            Title = "选择 classes.txt"
        };
        if (classesDialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        try
        {
            _session = await _annotationService.LoadFromYoloAsync(imageDirDialog.SelectedPath, labelsDirDialog.SelectedPath, classesDialog.FileName, CancellationToken.None);
            _currentImageIndex = _session.Images.Count > 0 ? 0 : -1;
            _undoStack.Clear();
            _redoStack.Clear();
            txtImageDirectory.Text = imageDirDialog.SelectedPath;
            txtClasses.Text = string.Join(",", _session.ClassNames);
            UpdateClassCombo();
            RefreshImageList();
            RefreshCurrentImage();
            lblStatus.Text = "YOLO 导入完成。";
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "导入 YOLO 失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private async void btnSaveCoco_Click(object sender, EventArgs e)
    {
        if (_session is null)
        {
            return;
        }

        using var dialog = new SaveFileDialog
        {
            Filter = "COCO 文件 (*.json)|*.json",
            FileName = "annotations.coco.json"
        };
        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        try
        {
            await _annotationService.SaveAsCocoAsync(_session, dialog.FileName, CancellationToken.None);
            lblStatus.Text = $"COCO 导出成功：{dialog.FileName}";
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "导出 COCO 失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private async void btnSaveYolo_Click(object sender, EventArgs e)
    {
        if (_session is null)
        {
            return;
        }

        using var dialog = new FolderBrowserDialog
        {
            Description = "选择 YOLO 导出目录",
            UseDescriptionForTitle = true
        };
        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        try
        {
            await _annotationService.SaveAsYoloAsync(_session, dialog.SelectedPath, CancellationToken.None);
            lblStatus.Text = $"YOLO 导出成功：{dialog.SelectedPath}";
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "导出 YOLO 失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void picCanvas_Paint(object sender, PaintEventArgs e)
    {
        if (_session is null || _currentImageIndex < 0 || _currentImageIndex >= _session.Images.Count)
        {
            return;
        }

        var image = _session.Images[_currentImageIndex];
        var imageRect = GetImageDisplayRect();
        if (imageRect.Width <= 1 || imageRect.Height <= 1 || image.Width <= 0 || image.Height <= 0)
        {
            return;
        }

        using var normalPen = new Pen(Color.Lime, 2f);
        using var selectedPen = new Pen(Color.Red, 2f);
        using var predictionPen = new Pen(Color.DeepSkyBlue, 2f);
        using var textBrush = new SolidBrush(Color.Yellow);
        using var drawingPen = new Pen(Color.Gold, 2f) { DashStyle = System.Drawing.Drawing2D.DashStyle.Dash };

        foreach (var box in image.Boxes)
        {
            var rect = ImageToCanvas(box.X, box.Y, box.Width, box.Height, image, imageRect);
            var pen = box.Source == "prediction" ? predictionPen : normalPen;
            if (box.BoxId == _selectedBoxId)
            {
                pen = selectedPen;
            }

            e.Graphics.DrawRectangle(pen, rect.X, rect.Y, rect.Width, rect.Height);
            e.Graphics.DrawString($"{box.ClassName}", Font, textBrush, rect.X + 2, rect.Y + 2);
        }

        if (_isDrawing)
        {
            var preview = NormalizeRect(_drawStartImagePoint, _drawCurrentImagePoint);
            var previewCanvas = ImageToCanvas(preview.X, preview.Y, preview.Width, preview.Height, image, imageRect);
            e.Graphics.DrawRectangle(drawingPen, previewCanvas.X, previewCanvas.Y, previewCanvas.Width, previewCanvas.Height);
        }
    }

    private void picCanvas_MouseDown(object sender, MouseEventArgs e)
    {
        if (_session is null || _currentImageIndex < 0)
        {
            return;
        }

        if (!TryCanvasToImagePoint(e.Location, out var imagePoint))
        {
            return;
        }

        var image = _session.Images[_currentImageIndex];
        var selected = HitTest(image, imagePoint);
        if (selected is not null)
        {
            _selectedBoxId = selected.BoxId;
            _isMoving = true;
            _moveStartImagePoint = imagePoint;
            _movingOriginalRect = new RectangleF(selected.X, selected.Y, selected.Width, selected.Height);
            CaptureSnapshot();
            RefreshBoxesList();
            picCanvas.Invalidate();
            return;
        }

        _selectedBoxId = null;
        _isDrawing = true;
        _drawStartImagePoint = imagePoint;
        _drawCurrentImagePoint = imagePoint;
        CaptureSnapshot();
    }

    private void picCanvas_MouseMove(object sender, MouseEventArgs e)
    {
        if (_session is null || _currentImageIndex < 0)
        {
            return;
        }

        if (!TryCanvasToImagePoint(e.Location, out var imagePoint))
        {
            return;
        }

        if (_isDrawing)
        {
            _drawCurrentImagePoint = imagePoint;
            picCanvas.Invalidate();
            return;
        }

        if (!_isMoving || string.IsNullOrWhiteSpace(_selectedBoxId))
        {
            return;
        }

        var image = _session.Images[_currentImageIndex];
        var box = image.Boxes.FirstOrDefault(item => item.BoxId == _selectedBoxId);
        if (box is null)
        {
            return;
        }

        var dx = imagePoint.X - _moveStartImagePoint.X;
        var dy = imagePoint.Y - _moveStartImagePoint.Y;
        box.X = Clamp(_movingOriginalRect.X + dx, 0, Math.Max(0, image.Width - box.Width));
        box.Y = Clamp(_movingOriginalRect.Y + dy, 0, Math.Max(0, image.Height - box.Height));
        picCanvas.Invalidate();
    }

    private void picCanvas_MouseUp(object sender, MouseEventArgs e)
    {
        if (_session is null || _currentImageIndex < 0)
        {
            return;
        }

        var image = _session.Images[_currentImageIndex];
        if (_isDrawing)
        {
            _isDrawing = false;
            if (!TryCanvasToImagePoint(e.Location, out var endImagePoint))
            {
                return;
            }

            var rect = NormalizeRect(_drawStartImagePoint, endImagePoint);
            if (rect.Width >= 4 && rect.Height >= 4 && cmbCurrentClass.SelectedIndex >= 0 && cmbCurrentClass.SelectedIndex < _session.ClassNames.Count)
            {
                var classId = cmbCurrentClass.SelectedIndex;
                image.Boxes.Add(new AnnotationBox
                {
                    ClassId = classId,
                    ClassName = _session.ClassNames[classId],
                    X = Clamp(rect.X, 0, image.Width - 1),
                    Y = Clamp(rect.Y, 0, image.Height - 1),
                    Width = Clamp(rect.Width, 1, image.Width),
                    Height = Clamp(rect.Height, 1, image.Height),
                    Source = "manual"
                });
                _selectedBoxId = image.Boxes.Last().BoxId;
            }

            RefreshBoxesList();
            picCanvas.Invalidate();
            return;
        }

        if (_isMoving)
        {
            _isMoving = false;
            RefreshBoxesList();
            picCanvas.Invalidate();
        }
    }

    private void RefreshImageList()
    {
        lstImages.Items.Clear();
        if (_session is null)
        {
            return;
        }

        foreach (var image in _session.Images)
        {
            lstImages.Items.Add(Path.GetFileName(image.ImagePath));
        }
    }

    private void RefreshCurrentImage()
    {
        if (_session is null || _currentImageIndex < 0 || _currentImageIndex >= _session.Images.Count)
        {
            picCanvas.Image = null;
            lstBoxes.Items.Clear();
            lblStatus.Text = "无可显示图片。";
            return;
        }

        var image = _session.Images[_currentImageIndex];
        if (File.Exists(image.ImagePath))
        {
            picCanvas.Image?.Dispose();
            picCanvas.Image = Image.FromFile(image.ImagePath);
        }
        else
        {
            picCanvas.Image?.Dispose();
            picCanvas.Image = null;
        }

        if (lstImages.SelectedIndex != _currentImageIndex)
        {
            lstImages.SelectedIndex = _currentImageIndex;
        }

        RefreshBoxesList();
        lblStatus.Text = $"当前：{Path.GetFileName(image.ImagePath)} | 标注框：{image.Boxes.Count}";
        picCanvas.Invalidate();
    }

    private void RefreshBoxesList()
    {
        lstBoxes.Items.Clear();
        if (_session is null || _currentImageIndex < 0 || _currentImageIndex >= _session.Images.Count)
        {
            return;
        }

        var image = _session.Images[_currentImageIndex];
        foreach (var box in image.Boxes)
        {
            lstBoxes.Items.Add($"{box.ClassName} ({box.X:0},{box.Y:0},{box.Width:0},{box.Height:0}) [{box.Source}]");
        }

        if (!string.IsNullOrWhiteSpace(_selectedBoxId))
        {
            var selectedIndex = image.Boxes.FindIndex(item => item.BoxId == _selectedBoxId);
            if (selectedIndex >= 0 && selectedIndex < lstBoxes.Items.Count)
            {
                lstBoxes.SelectedIndex = selectedIndex;
            }
        }
    }

    private void UpdateClassCombo()
    {
        cmbCurrentClass.Items.Clear();
        if (_session is null)
        {
            return;
        }

        foreach (var className in _session.ClassNames)
        {
            cmbCurrentClass.Items.Add(className);
        }

        if (cmbCurrentClass.Items.Count > 0)
        {
            cmbCurrentClass.SelectedIndex = 0;
        }
    }

    private List<string> ParseClassesFromText()
    {
        return txtClasses.Text
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private AnnotationBox? GetSelectedBox()
    {
        if (_session is null || _currentImageIndex < 0 || string.IsNullOrWhiteSpace(_selectedBoxId))
        {
            return null;
        }

        return _session.Images[_currentImageIndex].Boxes.FirstOrDefault(item => item.BoxId == _selectedBoxId);
    }

    private AnnotationBox? HitTest(AnnotationImageDocument image, PointF imagePoint)
    {
        for (var i = image.Boxes.Count - 1; i >= 0; i--)
        {
            var box = image.Boxes[i];
            var rect = new RectangleF(box.X, box.Y, box.Width, box.Height);
            if (rect.Contains(imagePoint))
            {
                return box;
            }
        }

        return null;
    }

    private RectangleF GetImageDisplayRect()
    {
        if (picCanvas.Image is null)
        {
            return RectangleF.Empty;
        }

        var imageWidth = picCanvas.Image.Width;
        var imageHeight = picCanvas.Image.Height;
        if (imageWidth <= 0 || imageHeight <= 0 || picCanvas.Width <= 0 || picCanvas.Height <= 0)
        {
            return RectangleF.Empty;
        }

        var ratio = Math.Min((float)picCanvas.ClientSize.Width / imageWidth, (float)picCanvas.ClientSize.Height / imageHeight);
        var drawWidth = imageWidth * ratio;
        var drawHeight = imageHeight * ratio;
        var offsetX = (picCanvas.ClientSize.Width - drawWidth) / 2f;
        var offsetY = (picCanvas.ClientSize.Height - drawHeight) / 2f;
        return new RectangleF(offsetX, offsetY, drawWidth, drawHeight);
    }

    private bool TryCanvasToImagePoint(Point canvasPoint, out PointF imagePoint)
    {
        imagePoint = PointF.Empty;
        if (_session is null || _currentImageIndex < 0 || _currentImageIndex >= _session.Images.Count)
        {
            return false;
        }

        var image = _session.Images[_currentImageIndex];
        var imageRect = GetImageDisplayRect();
        if (imageRect.IsEmpty || !imageRect.Contains(canvasPoint))
        {
            return false;
        }

        var x = (canvasPoint.X - imageRect.X) / imageRect.Width * image.Width;
        var y = (canvasPoint.Y - imageRect.Y) / imageRect.Height * image.Height;
        imagePoint = new PointF(x, y);
        return true;
    }

    private static RectangleF ImageToCanvas(float x, float y, float width, float height, AnnotationImageDocument image, RectangleF imageRect)
    {
        var canvasX = imageRect.X + (x / image.Width) * imageRect.Width;
        var canvasY = imageRect.Y + (y / image.Height) * imageRect.Height;
        var canvasW = (width / image.Width) * imageRect.Width;
        var canvasH = (height / image.Height) * imageRect.Height;
        return new RectangleF(canvasX, canvasY, canvasW, canvasH);
    }

    private static RectangleF NormalizeRect(PointF a, PointF b)
    {
        var left = Math.Min(a.X, b.X);
        var top = Math.Min(a.Y, b.Y);
        var right = Math.Max(a.X, b.X);
        var bottom = Math.Max(a.Y, b.Y);
        return new RectangleF(left, top, right - left, bottom - top);
    }

    private static float Clamp(float value, float min, float max)
    {
        if (value < min)
        {
            return min;
        }

        if (value > max)
        {
            return max;
        }

        return value;
    }

    private void CaptureSnapshot()
    {
        if (_session is null)
        {
            return;
        }

        _undoStack.Push(SerializeSession(_session));
        _redoStack.Clear();
    }

    private static string SerializeSession(AnnotationSession session)
    {
        return JsonSerializer.Serialize(session, SnapshotJsonOptions);
    }

    private static AnnotationSession DeserializeSession(string snapshot)
    {
        return JsonSerializer.Deserialize<AnnotationSession>(snapshot, SnapshotJsonOptions) ?? new AnnotationSession();
    }

    private void EnsureCurrentIndexValid()
    {
        if (_session is null || _session.Images.Count == 0)
        {
            _currentImageIndex = -1;
            return;
        }

        if (_currentImageIndex < 0)
        {
            _currentImageIndex = 0;
            return;
        }

        if (_currentImageIndex >= _session.Images.Count)
        {
            _currentImageIndex = _session.Images.Count - 1;
        }
    }
}
