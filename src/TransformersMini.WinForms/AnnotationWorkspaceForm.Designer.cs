namespace TransformersMini.WinForms;

partial class AnnotationWorkspaceForm
{
    private System.ComponentModel.IContainer components = null;

    protected override void Dispose(bool disposing)
    {
        if (disposing && (components != null))
        {
            this.picCanvas.Image?.Dispose();
            components.Dispose();
        }

        base.Dispose(disposing);
    }

    private void InitializeComponent()
    {
        this.txtImageDirectory = new System.Windows.Forms.TextBox();
        this.btnBrowseImageDirectory = new System.Windows.Forms.Button();
        this.txtClasses = new System.Windows.Forms.TextBox();
        this.btnApplyClasses = new System.Windows.Forms.Button();
        this.lstImages = new System.Windows.Forms.ListBox();
        this.picCanvas = new System.Windows.Forms.PictureBox();
        this.lstBoxes = new System.Windows.Forms.ListBox();
        this.cmbCurrentClass = new System.Windows.Forms.ComboBox();
        this.btnDeleteBox = new System.Windows.Forms.Button();
        this.btnPrevImage = new System.Windows.Forms.Button();
        this.btnNextImage = new System.Windows.Forms.Button();
        this.btnUndo = new System.Windows.Forms.Button();
        this.btnRedo = new System.Windows.Forms.Button();
        this.btnCopyPreviousBoxes = new System.Windows.Forms.Button();
        this.btnApplyClassToAll = new System.Windows.Forms.Button();
        this.btnImportPredictions = new System.Windows.Forms.Button();
        this.btnLoadCoco = new System.Windows.Forms.Button();
        this.btnLoadYolo = new System.Windows.Forms.Button();
        this.btnSaveCoco = new System.Windows.Forms.Button();
        this.btnSaveYolo = new System.Windows.Forms.Button();
        this.lblStatus = new System.Windows.Forms.Label();
        ((System.ComponentModel.ISupportInitialize)(this.picCanvas)).BeginInit();
        this.SuspendLayout();
        //
        // txtImageDirectory
        //
        this.txtImageDirectory.Location = new System.Drawing.Point(12, 12);
        this.txtImageDirectory.Name = "txtImageDirectory";
        this.txtImageDirectory.PlaceholderText = "图像目录...";
        this.txtImageDirectory.Size = new System.Drawing.Size(420, 23);
        //
        // btnBrowseImageDirectory
        //
        this.btnBrowseImageDirectory.Location = new System.Drawing.Point(438, 11);
        this.btnBrowseImageDirectory.Name = "btnBrowseImageDirectory";
        this.btnBrowseImageDirectory.Size = new System.Drawing.Size(88, 25);
        this.btnBrowseImageDirectory.Text = "加载图像目录";
        this.btnBrowseImageDirectory.Click += new System.EventHandler(this.btnBrowseImageDirectory_Click);
        //
        // txtClasses
        //
        this.txtClasses.Location = new System.Drawing.Point(542, 12);
        this.txtClasses.Name = "txtClasses";
        this.txtClasses.PlaceholderText = "类别（逗号分隔），如 person,car";
        this.txtClasses.Size = new System.Drawing.Size(320, 23);
        //
        // btnApplyClasses
        //
        this.btnApplyClasses.Location = new System.Drawing.Point(868, 11);
        this.btnApplyClasses.Name = "btnApplyClasses";
        this.btnApplyClasses.Size = new System.Drawing.Size(84, 25);
        this.btnApplyClasses.Text = "更新类别";
        this.btnApplyClasses.Click += new System.EventHandler(this.btnApplyClasses_Click);
        //
        // lstImages
        //
        this.lstImages.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left;
        this.lstImages.FormattingEnabled = true;
        this.lstImages.ItemHeight = 15;
        this.lstImages.Location = new System.Drawing.Point(12, 48);
        this.lstImages.Name = "lstImages";
        this.lstImages.Size = new System.Drawing.Size(240, 574);
        this.lstImages.SelectedIndexChanged += new System.EventHandler(this.lstImages_SelectedIndexChanged);
        //
        // picCanvas
        //
        this.picCanvas.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
        this.picCanvas.BackColor = System.Drawing.Color.Black;
        this.picCanvas.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
        this.picCanvas.Location = new System.Drawing.Point(258, 84);
        this.picCanvas.Name = "picCanvas";
        this.picCanvas.Size = new System.Drawing.Size(694, 538);
        this.picCanvas.SizeMode = System.Windows.Forms.PictureBoxSizeMode.Zoom;
        this.picCanvas.Paint += new System.Windows.Forms.PaintEventHandler(this.picCanvas_Paint);
        this.picCanvas.MouseDown += new System.Windows.Forms.MouseEventHandler(this.picCanvas_MouseDown);
        this.picCanvas.MouseMove += new System.Windows.Forms.MouseEventHandler(this.picCanvas_MouseMove);
        this.picCanvas.MouseUp += new System.Windows.Forms.MouseEventHandler(this.picCanvas_MouseUp);
        //
        // lstBoxes
        //
        this.lstBoxes.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right;
        this.lstBoxes.FormattingEnabled = true;
        this.lstBoxes.ItemHeight = 15;
        this.lstBoxes.Location = new System.Drawing.Point(958, 84);
        this.lstBoxes.Name = "lstBoxes";
        this.lstBoxes.Size = new System.Drawing.Size(314, 514);
        this.lstBoxes.SelectedIndexChanged += new System.EventHandler(this.lstBoxes_SelectedIndexChanged);
        //
        // cmbCurrentClass
        //
        this.cmbCurrentClass.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
        this.cmbCurrentClass.FormattingEnabled = true;
        this.cmbCurrentClass.Location = new System.Drawing.Point(258, 48);
        this.cmbCurrentClass.Name = "cmbCurrentClass";
        this.cmbCurrentClass.Size = new System.Drawing.Size(160, 23);
        //
        // btnDeleteBox
        //
        this.btnDeleteBox.Location = new System.Drawing.Point(424, 47);
        this.btnDeleteBox.Name = "btnDeleteBox";
        this.btnDeleteBox.Size = new System.Drawing.Size(74, 25);
        this.btnDeleteBox.Text = "删除框";
        this.btnDeleteBox.Click += new System.EventHandler(this.btnDeleteBox_Click);
        //
        // btnPrevImage
        //
        this.btnPrevImage.Location = new System.Drawing.Point(504, 47);
        this.btnPrevImage.Name = "btnPrevImage";
        this.btnPrevImage.Size = new System.Drawing.Size(74, 25);
        this.btnPrevImage.Text = "上一张";
        this.btnPrevImage.Click += new System.EventHandler(this.btnPrevImage_Click);
        //
        // btnNextImage
        //
        this.btnNextImage.Location = new System.Drawing.Point(584, 47);
        this.btnNextImage.Name = "btnNextImage";
        this.btnNextImage.Size = new System.Drawing.Size(74, 25);
        this.btnNextImage.Text = "下一张";
        this.btnNextImage.Click += new System.EventHandler(this.btnNextImage_Click);
        //
        // btnUndo
        //
        this.btnUndo.Location = new System.Drawing.Point(664, 47);
        this.btnUndo.Name = "btnUndo";
        this.btnUndo.Size = new System.Drawing.Size(60, 25);
        this.btnUndo.Text = "撤销";
        this.btnUndo.Click += new System.EventHandler(this.btnUndo_Click);
        //
        // btnRedo
        //
        this.btnRedo.Location = new System.Drawing.Point(730, 47);
        this.btnRedo.Name = "btnRedo";
        this.btnRedo.Size = new System.Drawing.Size(60, 25);
        this.btnRedo.Text = "重做";
        this.btnRedo.Click += new System.EventHandler(this.btnRedo_Click);
        //
        // btnCopyPreviousBoxes
        //
        this.btnCopyPreviousBoxes.Location = new System.Drawing.Point(796, 47);
        this.btnCopyPreviousBoxes.Name = "btnCopyPreviousBoxes";
        this.btnCopyPreviousBoxes.Size = new System.Drawing.Size(72, 25);
        this.btnCopyPreviousBoxes.Text = "复制上张";
        this.btnCopyPreviousBoxes.Click += new System.EventHandler(this.btnCopyPreviousBoxes_Click);
        //
        // btnApplyClassToAll
        //
        this.btnApplyClassToAll.Location = new System.Drawing.Point(874, 47);
        this.btnApplyClassToAll.Name = "btnApplyClassToAll";
        this.btnApplyClassToAll.Size = new System.Drawing.Size(78, 25);
        this.btnApplyClassToAll.Text = "整图改类";
        this.btnApplyClassToAll.Click += new System.EventHandler(this.btnApplyClassToAll_Click);
        //
        // btnImportPredictions
        //
        this.btnImportPredictions.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
        this.btnImportPredictions.Location = new System.Drawing.Point(958, 48);
        this.btnImportPredictions.Name = "btnImportPredictions";
        this.btnImportPredictions.Size = new System.Drawing.Size(99, 25);
        this.btnImportPredictions.Text = "导入推理结果";
        this.btnImportPredictions.Click += new System.EventHandler(this.btnImportPredictions_Click);
        //
        // btnLoadCoco
        //
        this.btnLoadCoco.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
        this.btnLoadCoco.Location = new System.Drawing.Point(1063, 48);
        this.btnLoadCoco.Name = "btnLoadCoco";
        this.btnLoadCoco.Size = new System.Drawing.Size(67, 25);
        this.btnLoadCoco.Text = "导入COCO";
        this.btnLoadCoco.Click += new System.EventHandler(this.btnLoadCoco_Click);
        //
        // btnLoadYolo
        //
        this.btnLoadYolo.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
        this.btnLoadYolo.Location = new System.Drawing.Point(1136, 48);
        this.btnLoadYolo.Name = "btnLoadYolo";
        this.btnLoadYolo.Size = new System.Drawing.Size(67, 25);
        this.btnLoadYolo.Text = "导入YOLO";
        this.btnLoadYolo.Click += new System.EventHandler(this.btnLoadYolo_Click);
        //
        // btnSaveCoco
        //
        this.btnSaveCoco.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
        this.btnSaveCoco.Location = new System.Drawing.Point(1209, 48);
        this.btnSaveCoco.Name = "btnSaveCoco";
        this.btnSaveCoco.Size = new System.Drawing.Size(63, 25);
        this.btnSaveCoco.Text = "导出COCO";
        this.btnSaveCoco.Click += new System.EventHandler(this.btnSaveCoco_Click);
        //
        // btnSaveYolo
        //
        this.btnSaveYolo.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right;
        this.btnSaveYolo.Location = new System.Drawing.Point(1189, 604);
        this.btnSaveYolo.Name = "btnSaveYolo";
        this.btnSaveYolo.Size = new System.Drawing.Size(83, 25);
        this.btnSaveYolo.Text = "导出YOLO";
        this.btnSaveYolo.Click += new System.EventHandler(this.btnSaveYolo_Click);
        //
        // lblStatus
        //
        this.lblStatus.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
        this.lblStatus.AutoSize = true;
        this.lblStatus.Location = new System.Drawing.Point(12, 609);
        this.lblStatus.Name = "lblStatus";
        this.lblStatus.Size = new System.Drawing.Size(55, 15);
        this.lblStatus.Text = "就绪";
        //
        // AnnotationWorkspaceForm
        //
        this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
        this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
        this.ClientSize = new System.Drawing.Size(1284, 641);
        this.Controls.Add(this.txtImageDirectory);
        this.Controls.Add(this.btnBrowseImageDirectory);
        this.Controls.Add(this.txtClasses);
        this.Controls.Add(this.btnApplyClasses);
        this.Controls.Add(this.lstImages);
        this.Controls.Add(this.cmbCurrentClass);
        this.Controls.Add(this.btnDeleteBox);
        this.Controls.Add(this.btnPrevImage);
        this.Controls.Add(this.btnNextImage);
        this.Controls.Add(this.btnUndo);
        this.Controls.Add(this.btnRedo);
        this.Controls.Add(this.btnCopyPreviousBoxes);
        this.Controls.Add(this.btnApplyClassToAll);
        this.Controls.Add(this.btnImportPredictions);
        this.Controls.Add(this.btnLoadCoco);
        this.Controls.Add(this.btnLoadYolo);
        this.Controls.Add(this.btnSaveCoco);
        this.Controls.Add(this.picCanvas);
        this.Controls.Add(this.lstBoxes);
        this.Controls.Add(this.btnSaveYolo);
        this.Controls.Add(this.lblStatus);
        this.MinimumSize = new System.Drawing.Size(1300, 680);
        this.Name = "AnnotationWorkspaceForm";
        this.Text = "标注工作台（检测）";
        ((System.ComponentModel.ISupportInitialize)(this.picCanvas)).EndInit();
        this.ResumeLayout(false);
        this.PerformLayout();
    }

    private System.Windows.Forms.TextBox txtImageDirectory;
    private System.Windows.Forms.Button btnBrowseImageDirectory;
    private System.Windows.Forms.TextBox txtClasses;
    private System.Windows.Forms.Button btnApplyClasses;
    private System.Windows.Forms.ListBox lstImages;
    private System.Windows.Forms.PictureBox picCanvas;
    private System.Windows.Forms.ListBox lstBoxes;
    private System.Windows.Forms.ComboBox cmbCurrentClass;
    private System.Windows.Forms.Button btnDeleteBox;
    private System.Windows.Forms.Button btnPrevImage;
    private System.Windows.Forms.Button btnNextImage;
    private System.Windows.Forms.Button btnUndo;
    private System.Windows.Forms.Button btnRedo;
    private System.Windows.Forms.Button btnCopyPreviousBoxes;
    private System.Windows.Forms.Button btnApplyClassToAll;
    private System.Windows.Forms.Button btnImportPredictions;
    private System.Windows.Forms.Button btnLoadCoco;
    private System.Windows.Forms.Button btnLoadYolo;
    private System.Windows.Forms.Button btnSaveCoco;
    private System.Windows.Forms.Button btnSaveYolo;
    private System.Windows.Forms.Label lblStatus;
}
