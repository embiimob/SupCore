namespace SupCore.Forms
{
    partial class SupMain
    {
        private System.ComponentModel.IContainer components = null;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
                components.Dispose();
            base.Dispose(disposing);
        }

        private void InitializeComponent()
        {
            this.toolStrip = new System.Windows.Forms.ToolStrip();
            this.btnConnections = new System.Windows.Forms.ToolStripButton();
            this.btnWallet = new System.Windows.Forms.ToolStripButton();
            this.lblStatus = new System.Windows.Forms.Label();
            this.toolStrip.SuspendLayout();
            this.SuspendLayout();

            // ── toolStrip ──
            this.toolStrip.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
                this.btnConnections, this.btnWallet });
            this.toolStrip.Location = new System.Drawing.Point(0, 0);
            this.toolStrip.Name = "toolStrip";
            this.toolStrip.Size = new System.Drawing.Size(900, 25);
            this.toolStrip.TabIndex = 0;

            // ── btnConnections ──
            this.btnConnections.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.btnConnections.Name = "btnConnections";
            this.btnConnections.Text = "Connections";
            this.btnConnections.ToolTipText = "Manage blockchain connections";
            this.btnConnections.Click += new System.EventHandler(this.btnConnections_Click);

            // ── btnWallet ──
            this.btnWallet.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.btnWallet.Name = "btnWallet";
            this.btnWallet.Text = "Wallet";
            this.btnWallet.ToolTipText = "Open internal wallet";
            this.btnWallet.Click += new System.EventHandler(this.btnWallet_Click);

            // ── lblStatus ──
            this.lblStatus.AutoSize = true;
            this.lblStatus.Location = new System.Drawing.Point(12, 30);
            this.lblStatus.Name = "lblStatus";
            this.lblStatus.Text = "SupCore – Internal Wallet Edition";

            // ── SupMain ──
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(900, 600);
            this.Controls.Add(this.toolStrip);
            this.Controls.Add(this.lblStatus);
            this.Name = "SupMain";
            this.Text = "Sup!?  –  Internal Wallet Edition";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.toolStrip.ResumeLayout(false);
            this.toolStrip.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();
        }

        private System.Windows.Forms.ToolStrip toolStrip = null!;
        private System.Windows.Forms.ToolStripButton btnConnections = null!;
        private System.Windows.Forms.ToolStripButton btnWallet = null!;
        private System.Windows.Forms.Label lblStatus = null!;
    }
}
