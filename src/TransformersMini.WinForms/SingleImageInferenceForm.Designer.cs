namespace TransformersMini.WinForms;

partial class SingleImageInferenceForm
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
        this.lblImage = new System.Windows.Forms.Label();
        this.txtImagePath = new System.Windows.Forms.TextBox();
        this.btnBrowseImage = new System.Windows.Forms.Button();
        this.lblConfig = new System.Windows.Forms.Label();
        this.txtConfigPath = new System.Windows.Forms.TextBox();
        this.btnBrowseConfig = new System.Windows.Forms.Button();
        this.lblModelDir = new System.Windows.Forms.Label();
        this.txtModelDir = new System.Windows.Forms.TextBox();
        this.btnBrowseModelDir = new System.Windows.Forms.Button();
        this.picPreview = new System.Windows.Forms.PictureBox();
        this.btnRunInfer = new System.Windows.Forms.Button();
        this.btnClose = new System.Windows.Forms.Button();
        this.lblResultsHeader = new System.Windows.Forms.Label();
        this.txtResults = new System.Windows.Forms.TextBox();
        this.lblRunDir = new System.Windows.Forms.Label();
        ((System.ComponentModel.ISupportInitialize)(this.picPreview)).BeginInit();
        this.SuspendLayout();
        //
        // lblImage
        //
        this.lblImage.AutoSize = true;
        this.lblImage.Location = new System.Drawing.Point(12, 16);
        this.lblImage.Name = "lblImage";
        this.lblImage.Text = "图像路径：";
        //
        // txtImagePath
        //
        this.txtImagePath.Location = new System.Drawing.Point(90, 13);
        this.txtImagePath.Name = "txtImagePath";
        this.txtImagePath.ReadOnly = true;
        this.txtImagePath.Size = new System.Drawing.Size(380, 23);
        this.txtImagePath.PlaceholderText = "选择推理图像（可选）...";
        //
        // btnBrowseImage
        //
        this.btnBrowseImage.Location = new System.Drawing.Point(480, 12);
        this.btnBrowseImage.Name = "btnBrowseImage";
        this.btnBrowseImage.Size = new System.Drawing.Size(75, 25);
        this.btnBrowseImage.Text = "浏览...";
        this.btnBrowseImage.Click += new System.EventHandler(this.btnBrowseImage_Click);
        //
        // lblConfig
        //
        this.lblConfig.AutoSize = true;
        this.lblConfig.Location = new System.Drawing.Point(12, 50);
        this.lblConfig.Name = "lblConfig";
        this.lblConfig.Text = "配置文件：";
        //
        // txtConfigPath
        //
        this.txtConfigPath.Location = new System.Drawing.Point(90, 47);
        this.txtConfigPath.Name = "txtConfigPath";
        this.txtConfigPath.Size = new System.Drawing.Size(380, 23);
        this.txtConfigPath.PlaceholderText = "选择推理配置文件...";
        //
        // btnBrowseConfig
        //
        this.btnBrowseConfig.Location = new System.Drawing.Point(480, 46);
        this.btnBrowseConfig.Name = "btnBrowseConfig";
        this.btnBrowseConfig.Size = new System.Drawing.Size(75, 25);
        this.btnBrowseConfig.Text = "浏览...";
        this.btnBrowseConfig.Click += new System.EventHandler(this.btnBrowseConfig_Click);
        //
        // lblModelDir
        //
        this.lblModelDir.AutoSize = true;
        this.lblModelDir.Location = new System.Drawing.Point(12, 84);
        this.lblModelDir.Name = "lblModelDir";
        this.lblModelDir.Text = "训练产物目录：";
        //
        // txtModelDir
        //
        this.txtModelDir.Location = new System.Drawing.Point(100, 81);
        this.txtModelDir.Name = "txtModelDir";
        this.txtModelDir.Size = new System.Drawing.Size(370, 23);
        this.txtModelDir.PlaceholderText = "（可选）包含 artifacts/model-metadata.json 的目录";
        //
        // btnBrowseModelDir
        //
        this.btnBrowseModelDir.Location = new System.Drawing.Point(480, 80);
        this.btnBrowseModelDir.Name = "btnBrowseModelDir";
        this.btnBrowseModelDir.Size = new System.Drawing.Size(75, 25);
        this.btnBrowseModelDir.Text = "浏览...";
        this.btnBrowseModelDir.Click += new System.EventHandler(this.btnBrowseModelDir_Click);
        //
        // picPreview
        //
        this.picPreview.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
        this.picPreview.Location = new System.Drawing.Point(565, 12);
        this.picPreview.Name = "picPreview";
        this.picPreview.Size = new System.Drawing.Size(200, 200);
        this.picPreview.SizeMode = System.Windows.Forms.PictureBoxSizeMode.Zoom;
        //
        // btnRunInfer
        //
        this.btnRunInfer.Font = new System.Drawing.Font("Segoe UI", 10f, System.Drawing.FontStyle.Bold);
        this.btnRunInfer.Location = new System.Drawing.Point(12, 120);
        this.btnRunInfer.Name = "btnRunInfer";
        this.btnRunInfer.Size = new System.Drawing.Size(130, 32);
        this.btnRunInfer.Text = "▶ 单图推理";
        this.btnRunInfer.Click += new System.EventHandler(this.btnRunInfer_Click);
        //
        // btnClose
        //
        this.btnClose.Location = new System.Drawing.Point(155, 120);
        this.btnClose.Name = "btnClose";
        this.btnClose.Size = new System.Drawing.Size(75, 32);
        this.btnClose.Text = "关闭";
        this.btnClose.Click += new System.EventHandler(this.btnClose_Click);
        //
        // lblResultsHeader
        //
        this.lblResultsHeader.AutoSize = true;
        this.lblResultsHeader.Location = new System.Drawing.Point(12, 168);
        this.lblResultsHeader.Name = "lblResultsHeader";
        this.lblResultsHeader.Text = "推理结果（检测框/分数 或 OCR 文本）：";
        //
        // txtResults
        //
        this.txtResults.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
        this.txtResults.Location = new System.Drawing.Point(12, 188);
        this.txtResults.Multiline = true;
        this.txtResults.Name = "txtResults";
        this.txtResults.ReadOnly = true;
        this.txtResults.ScrollBars = System.Windows.Forms.ScrollBars.Both;
        this.txtResults.Size = new System.Drawing.Size(750, 260);
        //
        // lblRunDir
        //
        this.lblRunDir.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left;
        this.lblRunDir.AutoSize = true;
        this.lblRunDir.Location = new System.Drawing.Point(12, 460);
        this.lblRunDir.Name = "lblRunDir";
        this.lblRunDir.Text = string.Empty;
        //
        // SingleImageInferenceForm
        //
        this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
        this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
        this.ClientSize = new System.Drawing.Size(784, 481);
        this.Controls.Add(this.lblImage);
        this.Controls.Add(this.txtImagePath);
        this.Controls.Add(this.btnBrowseImage);
        this.Controls.Add(this.lblConfig);
        this.Controls.Add(this.txtConfigPath);
        this.Controls.Add(this.btnBrowseConfig);
        this.Controls.Add(this.lblModelDir);
        this.Controls.Add(this.txtModelDir);
        this.Controls.Add(this.btnBrowseModelDir);
        this.Controls.Add(this.picPreview);
        this.Controls.Add(this.btnRunInfer);
        this.Controls.Add(this.btnClose);
        this.Controls.Add(this.lblResultsHeader);
        this.Controls.Add(this.txtResults);
        this.Controls.Add(this.lblRunDir);
        this.MinimumSize = new System.Drawing.Size(800, 520);
        this.Name = "SingleImageInferenceForm";
        this.Text = "单图推理 — 检测/OCR";
        ((System.ComponentModel.ISupportInitialize)(this.picPreview)).EndInit();
        this.ResumeLayout(false);
        this.PerformLayout();
    }

    private System.Windows.Forms.Label lblImage;
    private System.Windows.Forms.TextBox txtImagePath;
    private System.Windows.Forms.Button btnBrowseImage;
    private System.Windows.Forms.Label lblConfig;
    private System.Windows.Forms.TextBox txtConfigPath;
    private System.Windows.Forms.Button btnBrowseConfig;
    private System.Windows.Forms.Label lblModelDir;
    private System.Windows.Forms.TextBox txtModelDir;
    private System.Windows.Forms.Button btnBrowseModelDir;
    private System.Windows.Forms.PictureBox picPreview;
    private System.Windows.Forms.Button btnRunInfer;
    private System.Windows.Forms.Button btnClose;
    private System.Windows.Forms.Label lblResultsHeader;
    private System.Windows.Forms.TextBox txtResults;
    private System.Windows.Forms.Label lblRunDir;
}
