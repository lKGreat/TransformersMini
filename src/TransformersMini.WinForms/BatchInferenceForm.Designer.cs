namespace TransformersMini.WinForms;

partial class BatchInferenceForm
{
    private System.ComponentModel.IContainer components = null;

    protected override void Dispose(bool disposing)
    {
        if (disposing && (components != null))
        {
            components.Dispose();
        }
        base.Dispose(disposing);
    }

    private void InitializeComponent()
    {
        this.lblConfig = new System.Windows.Forms.Label();
        this.txtConfigPath = new System.Windows.Forms.TextBox();
        this.btnBrowseConfig = new System.Windows.Forms.Button();
        this.lblModelDir = new System.Windows.Forms.Label();
        this.txtModelRunDir = new System.Windows.Forms.TextBox();
        this.btnBrowseModelDir = new System.Windows.Forms.Button();
        this.lblRunName = new System.Windows.Forms.Label();
        this.txtRunName = new System.Windows.Forms.TextBox();
        this.lblDevice = new System.Windows.Forms.Label();
        this.cmbDevice = new System.Windows.Forms.ComboBox();
        this.lblCudaStatus = new System.Windows.Forms.Label();
        this.lblMaxSamples = new System.Windows.Forms.Label();
        this.txtMaxSamples = new System.Windows.Forms.TextBox();
        this.btnStartInfer = new System.Windows.Forms.Button();
        this.btnClose = new System.Windows.Forms.Button();
        this.lblResults = new System.Windows.Forms.Label();
        this.txtResults = new System.Windows.Forms.TextBox();
        this.SuspendLayout();
        //
        // lblConfig
        //
        this.lblConfig.AutoSize = true;
        this.lblConfig.Location = new System.Drawing.Point(12, 16);
        this.lblConfig.Name = "lblConfig";
        this.lblConfig.Size = new System.Drawing.Size(74, 15);
        this.lblConfig.Text = "推理配置文件：";
        //
        // txtConfigPath
        //
        this.txtConfigPath.Location = new System.Drawing.Point(100, 13);
        this.txtConfigPath.Name = "txtConfigPath";
        this.txtConfigPath.Size = new System.Drawing.Size(480, 23);
        this.txtConfigPath.PlaceholderText = "选择 JSON 配置文件...";
        //
        // btnBrowseConfig
        //
        this.btnBrowseConfig.Location = new System.Drawing.Point(590, 12);
        this.btnBrowseConfig.Name = "btnBrowseConfig";
        this.btnBrowseConfig.Size = new System.Drawing.Size(75, 25);
        this.btnBrowseConfig.Text = "浏览...";
        this.btnBrowseConfig.Click += new System.EventHandler(this.btnBrowseConfig_Click);
        //
        // lblModelDir
        //
        this.lblModelDir.AutoSize = true;
        this.lblModelDir.Location = new System.Drawing.Point(12, 50);
        this.lblModelDir.Name = "lblModelDir";
        this.lblModelDir.Size = new System.Drawing.Size(80, 15);
        this.lblModelDir.Text = "训练产物目录：";
        //
        // txtModelRunDir
        //
        this.txtModelRunDir.Location = new System.Drawing.Point(100, 47);
        this.txtModelRunDir.Name = "txtModelRunDir";
        this.txtModelRunDir.Size = new System.Drawing.Size(480, 23);
        this.txtModelRunDir.PlaceholderText = "（可选）包含 artifacts/model-metadata.json 的 run 目录";
        //
        // btnBrowseModelDir
        //
        this.btnBrowseModelDir.Location = new System.Drawing.Point(590, 46);
        this.btnBrowseModelDir.Name = "btnBrowseModelDir";
        this.btnBrowseModelDir.Size = new System.Drawing.Size(75, 25);
        this.btnBrowseModelDir.Text = "浏览...";
        this.btnBrowseModelDir.Click += new System.EventHandler(this.btnBrowseModelDir_Click);
        //
        // lblRunName
        //
        this.lblRunName.AutoSize = true;
        this.lblRunName.Location = new System.Drawing.Point(12, 84);
        this.lblRunName.Name = "lblRunName";
        this.lblRunName.Text = "Run 名称：";
        //
        // txtRunName
        //
        this.txtRunName.Location = new System.Drawing.Point(100, 81);
        this.txtRunName.Name = "txtRunName";
        this.txtRunName.Size = new System.Drawing.Size(240, 23);
        this.txtRunName.PlaceholderText = "（可选）";
        //
        // lblDevice
        //
        this.lblDevice.AutoSize = true;
        this.lblDevice.Location = new System.Drawing.Point(12, 118);
        this.lblDevice.Name = "lblDevice";
        this.lblDevice.Text = "推理设备：";
        //
        // cmbDevice
        //
        this.cmbDevice.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
        this.cmbDevice.Location = new System.Drawing.Point(100, 115);
        this.cmbDevice.Name = "cmbDevice";
        this.cmbDevice.Size = new System.Drawing.Size(120, 23);
        //
        // lblCudaStatus
        //
        this.lblCudaStatus.AutoSize = true;
        this.lblCudaStatus.Location = new System.Drawing.Point(230, 118);
        this.lblCudaStatus.Name = "lblCudaStatus";
        this.lblCudaStatus.Text = "CUDA 状态";
        //
        // lblMaxSamples
        //
        this.lblMaxSamples.AutoSize = true;
        this.lblMaxSamples.Location = new System.Drawing.Point(12, 152);
        this.lblMaxSamples.Name = "lblMaxSamples";
        this.lblMaxSamples.Text = "最大样本数（0=全量）：";
        //
        // txtMaxSamples
        //
        this.txtMaxSamples.Location = new System.Drawing.Point(160, 149);
        this.txtMaxSamples.Name = "txtMaxSamples";
        this.txtMaxSamples.Size = new System.Drawing.Size(80, 23);
        this.txtMaxSamples.Text = "0";
        //
        // btnStartInfer
        //
        this.btnStartInfer.Font = new System.Drawing.Font("Segoe UI", 10f, System.Drawing.FontStyle.Bold);
        this.btnStartInfer.Location = new System.Drawing.Point(12, 186);
        this.btnStartInfer.Name = "btnStartInfer";
        this.btnStartInfer.Size = new System.Drawing.Size(130, 32);
        this.btnStartInfer.Text = "▶ 开始推理";
        this.btnStartInfer.Click += new System.EventHandler(this.btnStartInfer_Click);
        //
        // btnClose
        //
        this.btnClose.Location = new System.Drawing.Point(155, 186);
        this.btnClose.Name = "btnClose";
        this.btnClose.Size = new System.Drawing.Size(75, 32);
        this.btnClose.Text = "关闭";
        this.btnClose.Click += new System.EventHandler(this.btnClose_Click);
        //
        // lblResults
        //
        this.lblResults.AutoSize = true;
        this.lblResults.Location = new System.Drawing.Point(12, 232);
        this.lblResults.Name = "lblResults";
        this.lblResults.Text = "推理结果：";
        //
        // txtResults
        //
        this.txtResults.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
        this.txtResults.Location = new System.Drawing.Point(12, 252);
        this.txtResults.Multiline = true;
        this.txtResults.Name = "txtResults";
        this.txtResults.ReadOnly = true;
        this.txtResults.ScrollBars = System.Windows.Forms.ScrollBars.Both;
        this.txtResults.Size = new System.Drawing.Size(668, 240);
        //
        // BatchInferenceForm
        //
        this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
        this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
        this.ClientSize = new System.Drawing.Size(700, 510);
        this.Controls.Add(this.lblConfig);
        this.Controls.Add(this.txtConfigPath);
        this.Controls.Add(this.btnBrowseConfig);
        this.Controls.Add(this.lblModelDir);
        this.Controls.Add(this.txtModelRunDir);
        this.Controls.Add(this.btnBrowseModelDir);
        this.Controls.Add(this.lblRunName);
        this.Controls.Add(this.txtRunName);
        this.Controls.Add(this.lblDevice);
        this.Controls.Add(this.cmbDevice);
        this.Controls.Add(this.lblCudaStatus);
        this.Controls.Add(this.lblMaxSamples);
        this.Controls.Add(this.txtMaxSamples);
        this.Controls.Add(this.btnStartInfer);
        this.Controls.Add(this.btnClose);
        this.Controls.Add(this.lblResults);
        this.Controls.Add(this.txtResults);
        this.MinimumSize = new System.Drawing.Size(716, 549);
        this.Name = "BatchInferenceForm";
        this.Text = "批量推理";
        this.ResumeLayout(false);
        this.PerformLayout();
    }

    private System.Windows.Forms.Label lblConfig;
    private System.Windows.Forms.TextBox txtConfigPath;
    private System.Windows.Forms.Button btnBrowseConfig;
    private System.Windows.Forms.Label lblModelDir;
    private System.Windows.Forms.TextBox txtModelRunDir;
    private System.Windows.Forms.Button btnBrowseModelDir;
    private System.Windows.Forms.Label lblRunName;
    private System.Windows.Forms.TextBox txtRunName;
    private System.Windows.Forms.Label lblDevice;
    private System.Windows.Forms.ComboBox cmbDevice;
    private System.Windows.Forms.Label lblCudaStatus;
    private System.Windows.Forms.Label lblMaxSamples;
    private System.Windows.Forms.TextBox txtMaxSamples;
    private System.Windows.Forms.Button btnStartInfer;
    private System.Windows.Forms.Button btnClose;
    private System.Windows.Forms.Label lblResults;
    private System.Windows.Forms.TextBox txtResults;
}
