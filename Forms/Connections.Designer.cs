namespace SupCore.Forms
{
    partial class Connections
    {
        private System.ComponentModel.IContainer components = null;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
                components.Dispose();
            _refreshTimer?.Stop();
            base.Dispose(disposing);
        }

        private void InitializeComponent()
        {
            this.pnlCoins = new System.Windows.Forms.Panel();
            this.btnIPFS = new System.Windows.Forms.Button();
            this.chkLiveFeedPinning = new System.Windows.Forms.CheckBox();
            this.btnPurge = new System.Windows.Forms.Button();
            this.lblTitle = new System.Windows.Forms.Label();
            this.SuspendLayout();

            // ── pnlCoins: scrollable coin status area ──
            this.pnlCoins.AutoScroll = true;
            this.pnlCoins.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.pnlCoins.Location = new System.Drawing.Point(8, 36);
            this.pnlCoins.Name = "pnlCoins";
            this.pnlCoins.Size = new System.Drawing.Size(700, 290);
            this.pnlCoins.TabIndex = 0;

            // ── lblTitle ──
            this.lblTitle.AutoSize = false;
            this.lblTitle.Font = new System.Drawing.Font("Segoe UI", 12F, System.Drawing.FontStyle.Bold);
            this.lblTitle.Location = new System.Drawing.Point(8, 8);
            this.lblTitle.Name = "lblTitle";
            this.lblTitle.Size = new System.Drawing.Size(700, 24);
            this.lblTitle.Text = "SupCore Internal Wallet – Connections";
            this.lblTitle.TabIndex = 10;

            // ── IPFS button ──
            this.btnIPFS.Location = new System.Drawing.Point(8, 340);
            this.btnIPFS.Name = "btnIPFS";
            this.btnIPFS.Size = new System.Drawing.Size(160, 28);
            this.btnIPFS.TabIndex = 1;
            this.btnIPFS.Text = "Start IPFS daemon";
            this.btnIPFS.Click += new System.EventHandler(this.btnIPFS_Click);

            // ── live-feed pinning ──
            this.chkLiveFeedPinning.AutoSize = true;
            this.chkLiveFeedPinning.Location = new System.Drawing.Point(8, 378);
            this.chkLiveFeedPinning.Name = "chkLiveFeedPinning";
            this.chkLiveFeedPinning.TabIndex = 2;
            this.chkLiveFeedPinning.Text = "Pin live-feed media to IPFS";
            this.chkLiveFeedPinning.CheckedChanged += new System.EventHandler(this.chkLiveFeedPinning_CheckedChanged);

            // ── purge button ──
            this.btnPurge.Location = new System.Drawing.Point(560, 340);
            this.btnPurge.Name = "btnPurge";
            this.btnPurge.Size = new System.Drawing.Size(148, 28);
            this.btnPurge.TabIndex = 3;
            this.btnPurge.Text = "Purge Cache";
            this.btnPurge.Click += new System.EventHandler(this.btnPurge_Click);

            // ── Form ──
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(720, 420);
            this.Controls.Add(this.lblTitle);
            this.Controls.Add(this.pnlCoins);
            this.Controls.Add(this.btnIPFS);
            this.Controls.Add(this.chkLiveFeedPinning);
            this.Controls.Add(this.btnPurge);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.Name = "Connections";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "SupCore – Connections";
            this.Load += new System.EventHandler(this.Connections_Load);
            this.ResumeLayout(false);
            this.PerformLayout();
        }

        private System.Windows.Forms.Panel pnlCoins = null!;
        private System.Windows.Forms.Button btnIPFS = null!;
        private System.Windows.Forms.CheckBox chkLiveFeedPinning = null!;
        private System.Windows.Forms.Button btnPurge = null!;
        private System.Windows.Forms.Label lblTitle = null!;
    }
}
