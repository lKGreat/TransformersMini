namespace TransformersMini.WinForms;

partial class TrainingSetupPanel
{
    private System.ComponentModel.IContainer components = null;

    protected override void Dispose(bool disposing)
    {
        if (disposing && (components != null))
        {
            _timer.Stop();
            _timer.Dispose();
            components.Dispose();
        }

        base.Dispose(disposing);
    }

    private void InitializeComponent()
    {
        this.cmbTask = new System.Windows.Forms.ComboBox();
        this.cmbDevice = new System.Windows.Forms.ComboBox();
        this.txtRunName = new System.Windows.Forms.TextBox();
        this.cmbDatasetFormat = new System.Windows.Forms.ComboBox();
        this.txtAnnotationPath = new System.Windows.Forms.TextBox();
        this.btnBrowseAnnotation = new System.Windows.Forms.Button();
        this.txtImageRoot = new System.Windows.Forms.TextBox();
        this.btnBrowseImageRoot = new System.Windows.Forms.Button();
        this.txtArchitecture = new System.Windows.Forms.TextBox();
        this.txtInputSize = new System.Windows.Forms.TextBox();
        this.txtNumClasses = new System.Windows.Forms.TextBox();
        this.txtEpochs = new System.Windows.Forms.TextBox();
        this.txtBatchSize = new System.Windows.Forms.TextBox();
        this.txtLearningRate = new System.Windows.Forms.TextBox();
        this.btnStartTrain = new System.Windows.Forms.Button();
        this.btnStartValidate = new System.Windows.Forms.Button();
        this.btnStartTest = new System.Windows.Forms.Button();
        this.btnCancelRun = new System.Windows.Forms.Button();
        this.chkDryRun = new System.Windows.Forms.CheckBox();
        this.lblRuntimeHint = new System.Windows.Forms.Label();
        this.txtTagKey = new System.Windows.Forms.TextBox();
        this.txtTagValue = new System.Windows.Forms.TextBox();
        this.btnApplyFilter = new System.Windows.Forms.Button();
        this.btnClearFilter = new System.Windows.Forms.Button();
        this.btnRefresh = new System.Windows.Forms.Button();
        this.runsGrid = new System.Windows.Forms.DataGridView();
        this.txtDetails = new System.Windows.Forms.TextBox();
        this.lblTempConfig = new System.Windows.Forms.Label();
        this.topPanel = new System.Windows.Forms.Panel();
        this.lblTask = new System.Windows.Forms.Label();
        this.lblDevice = new System.Windows.Forms.Label();
        this.lblRunName = new System.Windows.Forms.Label();
        this.lblFormat = new System.Windows.Forms.Label();
        this.lblAnnotation = new System.Windows.Forms.Label();
        this.lblImageRoot = new System.Windows.Forms.Label();
        this.lblArch = new System.Windows.Forms.Label();
        this.lblInput = new System.Windows.Forms.Label();
        this.lblClasses = new System.Windows.Forms.Label();
        this.lblEpochs = new System.Windows.Forms.Label();
        this.lblBatch = new System.Windows.Forms.Label();
        this.lblLr = new System.Windows.Forms.Label();
        this.filterPanel = new System.Windows.Forms.Panel();
        this.lblFilter = new System.Windows.Forms.Label();
        ((System.ComponentModel.ISupportInitialize)(this.runsGrid)).BeginInit();
        this.topPanel.SuspendLayout();
        this.filterPanel.SuspendLayout();
        this.SuspendLayout();
        //
        // cmbTask
        //
        this.cmbTask.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
        this.cmbTask.FormattingEnabled = true;
        this.cmbTask.Location = new System.Drawing.Point(72, 10);
        this.cmbTask.Name = "cmbTask";
        this.cmbTask.Size = new System.Drawing.Size(120, 23);
        //
        // cmbDevice
        //
        this.cmbDevice.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
        this.cmbDevice.FormattingEnabled = true;
        this.cmbDevice.Location = new System.Drawing.Point(248, 10);
        this.cmbDevice.Name = "cmbDevice";
        this.cmbDevice.Size = new System.Drawing.Size(120, 23);
        //
        // txtRunName
        //
        this.txtRunName.Location = new System.Drawing.Point(454, 10);
        this.txtRunName.Name = "txtRunName";
        this.txtRunName.PlaceholderText = "可选，留空自动生成";
        this.txtRunName.Size = new System.Drawing.Size(220, 23);
        //
        // cmbDatasetFormat
        //
        this.cmbDatasetFormat.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
        this.cmbDatasetFormat.FormattingEnabled = true;
        this.cmbDatasetFormat.Location = new System.Drawing.Point(72, 42);
        this.cmbDatasetFormat.Name = "cmbDatasetFormat";
        this.cmbDatasetFormat.Size = new System.Drawing.Size(120, 23);
        //
        // txtAnnotationPath
        //
        this.txtAnnotationPath.Location = new System.Drawing.Point(72, 74);
        this.txtAnnotationPath.Name = "txtAnnotationPath";
        this.txtAnnotationPath.PlaceholderText = "选择标注文件（COCO JSON / YOLO 入口文件）";
        this.txtAnnotationPath.Size = new System.Drawing.Size(520, 23);
        //
        // btnBrowseAnnotation
        //
        this.btnBrowseAnnotation.Location = new System.Drawing.Point(598, 73);
        this.btnBrowseAnnotation.Name = "btnBrowseAnnotation";
        this.btnBrowseAnnotation.Size = new System.Drawing.Size(76, 25);
        this.btnBrowseAnnotation.Text = "浏览...";
        this.btnBrowseAnnotation.Click += new System.EventHandler(this.btnBrowseAnnotation_Click);
        //
        // txtImageRoot
        //
        this.txtImageRoot.Location = new System.Drawing.Point(72, 106);
        this.txtImageRoot.Name = "txtImageRoot";
        this.txtImageRoot.PlaceholderText = "选择图像根目录";
        this.txtImageRoot.Size = new System.Drawing.Size(520, 23);
        //
        // btnBrowseImageRoot
        //
        this.btnBrowseImageRoot.Location = new System.Drawing.Point(598, 105);
        this.btnBrowseImageRoot.Name = "btnBrowseImageRoot";
        this.btnBrowseImageRoot.Size = new System.Drawing.Size(76, 25);
        this.btnBrowseImageRoot.Text = "浏览...";
        this.btnBrowseImageRoot.Click += new System.EventHandler(this.btnBrowseImageRoot_Click);
        //
        // txtArchitecture
        //
        this.txtArchitecture.Location = new System.Drawing.Point(72, 138);
        this.txtArchitecture.Name = "txtArchitecture";
        this.txtArchitecture.Size = new System.Drawing.Size(120, 23);
        //
        // txtInputSize
        //
        this.txtInputSize.Location = new System.Drawing.Point(248, 138);
        this.txtInputSize.Name = "txtInputSize";
        this.txtInputSize.Size = new System.Drawing.Size(90, 23);
        //
        // txtNumClasses
        //
        this.txtNumClasses.Location = new System.Drawing.Point(434, 138);
        this.txtNumClasses.Name = "txtNumClasses";
        this.txtNumClasses.Size = new System.Drawing.Size(90, 23);
        //
        // txtEpochs
        //
        this.txtEpochs.Location = new System.Drawing.Point(72, 170);
        this.txtEpochs.Name = "txtEpochs";
        this.txtEpochs.Size = new System.Drawing.Size(90, 23);
        //
        // txtBatchSize
        //
        this.txtBatchSize.Location = new System.Drawing.Point(248, 170);
        this.txtBatchSize.Name = "txtBatchSize";
        this.txtBatchSize.Size = new System.Drawing.Size(90, 23);
        //
        // txtLearningRate
        //
        this.txtLearningRate.Location = new System.Drawing.Point(434, 170);
        this.txtLearningRate.Name = "txtLearningRate";
        this.txtLearningRate.Size = new System.Drawing.Size(90, 23);
        //
        // btnStartTrain
        //
        this.btnStartTrain.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Bold);
        this.btnStartTrain.Location = new System.Drawing.Point(72, 204);
        this.btnStartTrain.Name = "btnStartTrain";
        this.btnStartTrain.Size = new System.Drawing.Size(90, 30);
        this.btnStartTrain.Text = "开始训练";
        this.btnStartTrain.Click += new System.EventHandler(this.btnStartTrain_Click);
        //
        // btnStartValidate
        //
        this.btnStartValidate.Location = new System.Drawing.Point(170, 204);
        this.btnStartValidate.Name = "btnStartValidate";
        this.btnStartValidate.Size = new System.Drawing.Size(76, 30);
        this.btnStartValidate.Text = "验证";
        this.btnStartValidate.Click += new System.EventHandler(this.btnStartValidate_Click);
        //
        // btnStartTest
        //
        this.btnStartTest.Location = new System.Drawing.Point(252, 204);
        this.btnStartTest.Name = "btnStartTest";
        this.btnStartTest.Size = new System.Drawing.Size(76, 30);
        this.btnStartTest.Text = "测试";
        this.btnStartTest.Click += new System.EventHandler(this.btnStartTest_Click);
        //
        // btnCancelRun
        //
        this.btnCancelRun.Location = new System.Drawing.Point(334, 204);
        this.btnCancelRun.Name = "btnCancelRun";
        this.btnCancelRun.Size = new System.Drawing.Size(104, 30);
        this.btnCancelRun.Text = "取消选中运行";
        this.btnCancelRun.Click += new System.EventHandler(this.btnCancelRun_Click);
        //
        // chkDryRun
        //
        this.chkDryRun.AutoSize = true;
        this.chkDryRun.Location = new System.Drawing.Point(444, 211);
        this.chkDryRun.Name = "chkDryRun";
        this.chkDryRun.Size = new System.Drawing.Size(67, 19);
        this.chkDryRun.Text = "DryRun";
        //
        // lblRuntimeHint
        //
        this.lblRuntimeHint.AutoSize = false;
        this.lblRuntimeHint.Location = new System.Drawing.Point(520, 206);
        this.lblRuntimeHint.Name = "lblRuntimeHint";
        this.lblRuntimeHint.Size = new System.Drawing.Size(554, 28);
        this.lblRuntimeHint.Text = "运行时状态";
        //
        // txtTagKey
        //
        this.txtTagKey.Location = new System.Drawing.Point(64, 8);
        this.txtTagKey.Name = "txtTagKey";
        this.txtTagKey.PlaceholderText = "Tag Key";
        this.txtTagKey.Size = new System.Drawing.Size(200, 23);
        //
        // txtTagValue
        //
        this.txtTagValue.Location = new System.Drawing.Point(270, 8);
        this.txtTagValue.Name = "txtTagValue";
        this.txtTagValue.PlaceholderText = "Tag Value";
        this.txtTagValue.Size = new System.Drawing.Size(160, 23);
        //
        // btnApplyFilter
        //
        this.btnApplyFilter.Location = new System.Drawing.Point(436, 7);
        this.btnApplyFilter.Name = "btnApplyFilter";
        this.btnApplyFilter.Size = new System.Drawing.Size(64, 25);
        this.btnApplyFilter.Text = "Apply";
        this.btnApplyFilter.Click += new System.EventHandler(this.btnApplyFilter_Click);
        //
        // btnClearFilter
        //
        this.btnClearFilter.Location = new System.Drawing.Point(506, 7);
        this.btnClearFilter.Name = "btnClearFilter";
        this.btnClearFilter.Size = new System.Drawing.Size(64, 25);
        this.btnClearFilter.Text = "Clear";
        this.btnClearFilter.Click += new System.EventHandler(this.btnClearFilter_Click);
        //
        // btnRefresh
        //
        this.btnRefresh.Location = new System.Drawing.Point(576, 7);
        this.btnRefresh.Name = "btnRefresh";
        this.btnRefresh.Size = new System.Drawing.Size(74, 25);
        this.btnRefresh.Text = "Refresh";
        this.btnRefresh.Click += new System.EventHandler(this.btnRefresh_Click);
        //
        // runsGrid
        //
        this.runsGrid.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
        this.runsGrid.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
        this.runsGrid.Location = new System.Drawing.Point(8, 292);
        this.runsGrid.Name = "runsGrid";
        this.runsGrid.ReadOnly = true;
        this.runsGrid.RowTemplate.Height = 25;
        this.runsGrid.Size = new System.Drawing.Size(1144, 290);
        this.runsGrid.SelectionChanged += new System.EventHandler(this.runsGrid_SelectionChanged);
        //
        // txtDetails
        //
        this.txtDetails.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
        this.txtDetails.Location = new System.Drawing.Point(8, 588);
        this.txtDetails.Multiline = true;
        this.txtDetails.Name = "txtDetails";
        this.txtDetails.ReadOnly = true;
        this.txtDetails.ScrollBars = System.Windows.Forms.ScrollBars.Both;
        this.txtDetails.Size = new System.Drawing.Size(1144, 154);
        //
        // lblTempConfig
        //
        this.lblTempConfig.AutoSize = true;
        this.lblTempConfig.Location = new System.Drawing.Point(72, 241);
        this.lblTempConfig.Name = "lblTempConfig";
        this.lblTempConfig.Size = new System.Drawing.Size(91, 15);
        this.lblTempConfig.Text = "临时配置路径：-";
        //
        // topPanel
        //
        this.topPanel.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
        this.topPanel.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
        this.topPanel.Controls.Add(this.lblTask);
        this.topPanel.Controls.Add(this.lblDevice);
        this.topPanel.Controls.Add(this.lblRunName);
        this.topPanel.Controls.Add(this.lblFormat);
        this.topPanel.Controls.Add(this.lblAnnotation);
        this.topPanel.Controls.Add(this.lblImageRoot);
        this.topPanel.Controls.Add(this.lblArch);
        this.topPanel.Controls.Add(this.lblInput);
        this.topPanel.Controls.Add(this.lblClasses);
        this.topPanel.Controls.Add(this.lblEpochs);
        this.topPanel.Controls.Add(this.lblBatch);
        this.topPanel.Controls.Add(this.lblLr);
        this.topPanel.Controls.Add(this.cmbTask);
        this.topPanel.Controls.Add(this.cmbDevice);
        this.topPanel.Controls.Add(this.txtRunName);
        this.topPanel.Controls.Add(this.cmbDatasetFormat);
        this.topPanel.Controls.Add(this.txtAnnotationPath);
        this.topPanel.Controls.Add(this.btnBrowseAnnotation);
        this.topPanel.Controls.Add(this.txtImageRoot);
        this.topPanel.Controls.Add(this.btnBrowseImageRoot);
        this.topPanel.Controls.Add(this.txtArchitecture);
        this.topPanel.Controls.Add(this.txtInputSize);
        this.topPanel.Controls.Add(this.txtNumClasses);
        this.topPanel.Controls.Add(this.txtEpochs);
        this.topPanel.Controls.Add(this.txtBatchSize);
        this.topPanel.Controls.Add(this.txtLearningRate);
        this.topPanel.Controls.Add(this.btnStartTrain);
        this.topPanel.Controls.Add(this.btnStartValidate);
        this.topPanel.Controls.Add(this.btnStartTest);
        this.topPanel.Controls.Add(this.btnCancelRun);
        this.topPanel.Controls.Add(this.chkDryRun);
        this.topPanel.Controls.Add(this.lblRuntimeHint);
        this.topPanel.Controls.Add(this.lblTempConfig);
        this.topPanel.Location = new System.Drawing.Point(8, 8);
        this.topPanel.Name = "topPanel";
        this.topPanel.Size = new System.Drawing.Size(1144, 268);
        //
        // labels
        //
        this.lblTask.AutoSize = true;
        this.lblTask.Location = new System.Drawing.Point(16, 13);
        this.lblTask.Text = "任务";
        this.lblDevice.AutoSize = true;
        this.lblDevice.Location = new System.Drawing.Point(206, 13);
        this.lblDevice.Text = "设备";
        this.lblRunName.AutoSize = true;
        this.lblRunName.Location = new System.Drawing.Point(390, 13);
        this.lblRunName.Text = "Run名称";
        this.lblFormat.AutoSize = true;
        this.lblFormat.Location = new System.Drawing.Point(16, 45);
        this.lblFormat.Text = "数据格式";
        this.lblAnnotation.AutoSize = true;
        this.lblAnnotation.Location = new System.Drawing.Point(16, 77);
        this.lblAnnotation.Text = "标注文件";
        this.lblImageRoot.AutoSize = true;
        this.lblImageRoot.Location = new System.Drawing.Point(16, 109);
        this.lblImageRoot.Text = "图像目录";
        this.lblArch.AutoSize = true;
        this.lblArch.Location = new System.Drawing.Point(16, 141);
        this.lblArch.Text = "架构";
        this.lblInput.AutoSize = true;
        this.lblInput.Location = new System.Drawing.Point(206, 141);
        this.lblInput.Text = "输入尺寸";
        this.lblClasses.AutoSize = true;
        this.lblClasses.Location = new System.Drawing.Point(360, 141);
        this.lblClasses.Text = "类别数";
        this.lblEpochs.AutoSize = true;
        this.lblEpochs.Location = new System.Drawing.Point(16, 173);
        this.lblEpochs.Text = "Epochs";
        this.lblBatch.AutoSize = true;
        this.lblBatch.Location = new System.Drawing.Point(206, 173);
        this.lblBatch.Text = "Batch";
        this.lblLr.AutoSize = true;
        this.lblLr.Location = new System.Drawing.Point(360, 173);
        this.lblLr.Text = "LearningRate";
        //
        // filterPanel
        //
        this.filterPanel.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
        this.filterPanel.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
        this.filterPanel.Controls.Add(this.lblFilter);
        this.filterPanel.Controls.Add(this.txtTagKey);
        this.filterPanel.Controls.Add(this.txtTagValue);
        this.filterPanel.Controls.Add(this.btnApplyFilter);
        this.filterPanel.Controls.Add(this.btnClearFilter);
        this.filterPanel.Controls.Add(this.btnRefresh);
        this.filterPanel.Location = new System.Drawing.Point(8, 282);
        this.filterPanel.Name = "filterPanel";
        this.filterPanel.Size = new System.Drawing.Size(1144, 38);
        //
        // lblFilter
        //
        this.lblFilter.AutoSize = true;
        this.lblFilter.Location = new System.Drawing.Point(10, 11);
        this.lblFilter.Text = "过滤";
        //
        // TrainingSetupPanel
        //
        this.Controls.Add(this.topPanel);
        this.Controls.Add(this.filterPanel);
        this.Controls.Add(this.runsGrid);
        this.Controls.Add(this.txtDetails);
        this.Name = "TrainingSetupPanel";
        this.Size = new System.Drawing.Size(1160, 750);
        ((System.ComponentModel.ISupportInitialize)(this.runsGrid)).EndInit();
        this.topPanel.ResumeLayout(false);
        this.topPanel.PerformLayout();
        this.filterPanel.ResumeLayout(false);
        this.filterPanel.PerformLayout();
        this.ResumeLayout(false);
        this.PerformLayout();
    }

    private System.Windows.Forms.ComboBox cmbTask;
    private System.Windows.Forms.ComboBox cmbDevice;
    private System.Windows.Forms.TextBox txtRunName;
    private System.Windows.Forms.ComboBox cmbDatasetFormat;
    private System.Windows.Forms.TextBox txtAnnotationPath;
    private System.Windows.Forms.Button btnBrowseAnnotation;
    private System.Windows.Forms.TextBox txtImageRoot;
    private System.Windows.Forms.Button btnBrowseImageRoot;
    private System.Windows.Forms.TextBox txtArchitecture;
    private System.Windows.Forms.TextBox txtInputSize;
    private System.Windows.Forms.TextBox txtNumClasses;
    private System.Windows.Forms.TextBox txtEpochs;
    private System.Windows.Forms.TextBox txtBatchSize;
    private System.Windows.Forms.TextBox txtLearningRate;
    private System.Windows.Forms.Button btnStartTrain;
    private System.Windows.Forms.Button btnStartValidate;
    private System.Windows.Forms.Button btnStartTest;
    private System.Windows.Forms.Button btnCancelRun;
    private System.Windows.Forms.CheckBox chkDryRun;
    private System.Windows.Forms.Label lblRuntimeHint;
    private System.Windows.Forms.TextBox txtTagKey;
    private System.Windows.Forms.TextBox txtTagValue;
    private System.Windows.Forms.Button btnApplyFilter;
    private System.Windows.Forms.Button btnClearFilter;
    private System.Windows.Forms.Button btnRefresh;
    private System.Windows.Forms.DataGridView runsGrid;
    private System.Windows.Forms.TextBox txtDetails;
    private System.Windows.Forms.Label lblTempConfig;
    private System.Windows.Forms.Panel topPanel;
    private System.Windows.Forms.Label lblTask;
    private System.Windows.Forms.Label lblDevice;
    private System.Windows.Forms.Label lblRunName;
    private System.Windows.Forms.Label lblFormat;
    private System.Windows.Forms.Label lblAnnotation;
    private System.Windows.Forms.Label lblImageRoot;
    private System.Windows.Forms.Label lblArch;
    private System.Windows.Forms.Label lblInput;
    private System.Windows.Forms.Label lblClasses;
    private System.Windows.Forms.Label lblEpochs;
    private System.Windows.Forms.Label lblBatch;
    private System.Windows.Forms.Label lblLr;
    private System.Windows.Forms.Panel filterPanel;
    private System.Windows.Forms.Label lblFilter;
}
