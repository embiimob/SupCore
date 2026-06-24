namespace SUP
{
    partial class WalletView
    {
        private System.ComponentModel.IContainer components = null;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null)) components.Dispose();
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        private void InitializeComponent()
        {
            this.cboNetwork = new System.Windows.Forms.ComboBox();
            this.lblNetwork = new System.Windows.Forms.Label();
            this.tabControl = new System.Windows.Forms.TabControl();
            // Tab pages
            this.tabSync       = new System.Windows.Forms.TabPage();
            this.tabAddresses  = new System.Windows.Forms.TabPage();
            this.tabSend       = new System.Windows.Forms.TabPage();
            this.tabReceive    = new System.Windows.Forms.TabPage();
            this.tabSecurity   = new System.Windows.Forms.TabPage();
            // Tab 1: Sync
            this.txtSyncStatus = new System.Windows.Forms.TextBox();
            // Tab 2: Addresses
            this.lvwAddresses  = new System.Windows.Forms.ListView();
            this.colAddress    = new System.Windows.Forms.ColumnHeader();
            this.colLabel      = new System.Windows.Forms.ColumnHeader();
            this.colPath       = new System.Windows.Forms.ColumnHeader();
            this.btnNewAddress = new System.Windows.Forms.Button();
            this.btnCopyAddress= new System.Windows.Forms.Button();
            this.btnImportWIF  = new System.Windows.Forms.Button();
            this.btnExportWIF  = new System.Windows.Forms.Button();
            this.lblWalletStatus = new System.Windows.Forms.Label();
            // Tab 3: Send
            this.lblSendAddress  = new System.Windows.Forms.Label();
            this.txtSendAddress  = new System.Windows.Forms.TextBox();
            this.lblSendAmount   = new System.Windows.Forms.Label();
            this.txtSendAmount   = new System.Windows.Forms.TextBox();
            this.btnAddOutput    = new System.Windows.Forms.Button();
            this.btnRemoveOutput = new System.Windows.Forms.Button();
            this.lvwSendOutputs  = new System.Windows.Forms.ListView();
            this.colSendAddr     = new System.Windows.Forms.ColumnHeader();
            this.colSendAmt      = new System.Windows.Forms.ColumnHeader();
            this.btnSend         = new System.Windows.Forms.Button();
            this.lblSendResult   = new System.Windows.Forms.Label();
            this.lblSendNote     = new System.Windows.Forms.Label();
            // Tab 4: Receive
            this.lblReceive         = new System.Windows.Forms.Label();
            this.txtReceiveAddress  = new System.Windows.Forms.TextBox();
            this.btnNewReceiveAddress = new System.Windows.Forms.Button();
            this.btnCopyReceive     = new System.Windows.Forms.Button();
            // Tab 5: Security
            this.btnOpenWallet   = new System.Windows.Forms.Button();
            this.btnEncryptWallet= new System.Windows.Forms.Button();
            this.btnLockWallet   = new System.Windows.Forms.Button();
            this.btnUnlockWallet = new System.Windows.Forms.Button();
            this.lblSecurityStatus = new System.Windows.Forms.Label();
            this.lblSecurityNote   = new System.Windows.Forms.Label();

            this.tabControl.SuspendLayout();
            this.SuspendLayout();

            // ── Network selector ─────────────────────────────────────────
            this.lblNetwork.Text     = "Network:";
            this.lblNetwork.Location = new System.Drawing.Point(8, 10);
            this.lblNetwork.AutoSize = true;

            this.cboNetwork.Location = new System.Drawing.Point(65, 6);
            this.cboNetwork.Size     = new System.Drawing.Size(200, 21);
            this.cboNetwork.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cboNetwork.SelectedIndexChanged += new System.EventHandler(this.cboNetwork_SelectedIndexChanged);

            // ── TabControl ──────────────────────────────────────────────
            this.tabControl.Location   = new System.Drawing.Point(8, 34);
            this.tabControl.Size       = new System.Drawing.Size(756, 490);
            this.tabControl.TabIndex   = 0;
            this.tabControl.Controls.Add(this.tabSync);
            this.tabControl.Controls.Add(this.tabAddresses);
            this.tabControl.Controls.Add(this.tabSend);
            this.tabControl.Controls.Add(this.tabReceive);
            this.tabControl.Controls.Add(this.tabSecurity);

            // ── Tab 1: Sync Status ───────────────────────────────────────
            this.tabSync.Text     = "Sync Status";
            this.tabSync.TabIndex = 0;

            this.txtSyncStatus.Multiline  = true;
            this.txtSyncStatus.ReadOnly   = true;
            this.txtSyncStatus.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
            this.txtSyncStatus.Font       = new System.Drawing.Font("Courier New", 9f);
            this.txtSyncStatus.Dock       = System.Windows.Forms.DockStyle.Fill;
            this.tabSync.Controls.Add(this.txtSyncStatus);

            // ── Tab 2: Addresses ─────────────────────────────────────────
            this.tabAddresses.Text     = "Addresses";
            this.tabAddresses.TabIndex = 1;

            this.colAddress.Text  = "Address";
            this.colAddress.Width = 320;
            this.colLabel.Text    = "Label";
            this.colLabel.Width   = 120;
            this.colPath.Text     = "Derivation";
            this.colPath.Width    = 160;

            this.lvwAddresses.Columns.AddRange(new System.Windows.Forms.ColumnHeader[] {
                this.colAddress, this.colLabel, this.colPath });
            this.lvwAddresses.FullRowSelect = true;
            this.lvwAddresses.View          = System.Windows.Forms.View.Details;
            this.lvwAddresses.Location      = new System.Drawing.Point(4, 4);
            this.lvwAddresses.Size          = new System.Drawing.Size(728, 380);

            this.btnNewAddress.Text     = "New Address";
            this.btnNewAddress.Location = new System.Drawing.Point(4, 392);
            this.btnNewAddress.Size     = new System.Drawing.Size(95, 23);
            this.btnNewAddress.Click   += new System.EventHandler(this.btnNewAddress_Click);

            this.btnCopyAddress.Text     = "Copy";
            this.btnCopyAddress.Location = new System.Drawing.Point(107, 392);
            this.btnCopyAddress.Size     = new System.Drawing.Size(60, 23);
            this.btnCopyAddress.Click   += new System.EventHandler(this.btnCopyAddress_Click);

            this.btnImportWIF.Text     = "Import WIF";
            this.btnImportWIF.Location = new System.Drawing.Point(175, 392);
            this.btnImportWIF.Size     = new System.Drawing.Size(85, 23);
            this.btnImportWIF.Click   += new System.EventHandler(this.btnImportWIF_Click);

            this.btnExportWIF.Text     = "Export WIF";
            this.btnExportWIF.Location = new System.Drawing.Point(268, 392);
            this.btnExportWIF.Size     = new System.Drawing.Size(85, 23);
            this.btnExportWIF.Click   += new System.EventHandler(this.btnExportWIF_Click);

            this.lblWalletStatus.AutoSize = true;
            this.lblWalletStatus.Location = new System.Drawing.Point(4, 424);
            this.lblWalletStatus.Text     = "";

            this.tabAddresses.Controls.AddRange(new System.Windows.Forms.Control[] {
                this.lvwAddresses, this.btnNewAddress, this.btnCopyAddress,
                this.btnImportWIF, this.btnExportWIF, this.lblWalletStatus });

            // ── Tab 3: Send ──────────────────────────────────────────────
            this.tabSend.Text     = "Send";
            this.tabSend.TabIndex = 2;

            this.lblSendNote.Text     = "Outputs are broadcast in the order listed — no shuffling (required by Sup!?).";
            this.lblSendNote.AutoSize = true;
            this.lblSendNote.Font     = new System.Drawing.Font("Microsoft Sans Serif", 7.5f, System.Drawing.FontStyle.Italic);
            this.lblSendNote.Location = new System.Drawing.Point(4, 4);

            this.lblSendAddress.Text     = "Address:";
            this.lblSendAddress.AutoSize = true;
            this.lblSendAddress.Location = new System.Drawing.Point(4, 24);

            this.txtSendAddress.Location = new System.Drawing.Point(60, 20);
            this.txtSendAddress.Size     = new System.Drawing.Size(380, 20);

            this.lblSendAmount.Text     = "Amount:";
            this.lblSendAmount.AutoSize = true;
            this.lblSendAmount.Location = new System.Drawing.Point(450, 24);

            this.txtSendAmount.Location = new System.Drawing.Point(505, 20);
            this.txtSendAmount.Size     = new System.Drawing.Size(100, 20);

            this.btnAddOutput.Text     = "Add ↓";
            this.btnAddOutput.Location = new System.Drawing.Point(614, 18);
            this.btnAddOutput.Size     = new System.Drawing.Size(55, 23);
            this.btnAddOutput.Click   += new System.EventHandler(this.btnAddOutput_Click);

            this.colSendAddr.Text  = "Address";
            this.colSendAddr.Width = 380;
            this.colSendAmt.Text   = "Amount";
            this.colSendAmt.Width  = 120;

            this.lvwSendOutputs.Columns.AddRange(new System.Windows.Forms.ColumnHeader[] {
                this.colSendAddr, this.colSendAmt });
            this.lvwSendOutputs.FullRowSelect = true;
            this.lvwSendOutputs.View          = System.Windows.Forms.View.Details;
            this.lvwSendOutputs.Location      = new System.Drawing.Point(4, 50);
            this.lvwSendOutputs.Size          = new System.Drawing.Size(724, 300);

            this.btnRemoveOutput.Text     = "Remove Selected";
            this.btnRemoveOutput.Location = new System.Drawing.Point(4, 358);
            this.btnRemoveOutput.Size     = new System.Drawing.Size(110, 23);
            this.btnRemoveOutput.Click   += new System.EventHandler(this.btnRemoveOutput_Click);

            this.btnSend.Text     = "Send";
            this.btnSend.Location = new System.Drawing.Point(122, 358);
            this.btnSend.Size     = new System.Drawing.Size(70, 23);
            this.btnSend.Click   += new System.EventHandler(this.btnSend_Click);

            this.lblSendResult.AutoSize = true;
            this.lblSendResult.Location = new System.Drawing.Point(4, 392);
            this.lblSendResult.Text     = "";
            this.lblSendResult.Font     = new System.Drawing.Font("Courier New", 8f);

            this.tabSend.Controls.AddRange(new System.Windows.Forms.Control[] {
                this.lblSendNote, this.lblSendAddress, this.txtSendAddress,
                this.lblSendAmount, this.txtSendAmount, this.btnAddOutput,
                this.lvwSendOutputs, this.btnRemoveOutput, this.btnSend, this.lblSendResult });

            // ── Tab 4: Receive ───────────────────────────────────────────
            this.tabReceive.Text     = "Receive";
            this.tabReceive.TabIndex = 3;

            this.lblReceive.Text     = "Receiving Address:";
            this.lblReceive.AutoSize = true;
            this.lblReceive.Location = new System.Drawing.Point(8, 16);

            this.txtReceiveAddress.ReadOnly = true;
            this.txtReceiveAddress.Font     = new System.Drawing.Font("Courier New", 10f);
            this.txtReceiveAddress.Location = new System.Drawing.Point(8, 36);
            this.txtReceiveAddress.Size     = new System.Drawing.Size(460, 22);

            this.btnNewReceiveAddress.Text     = "New Address";
            this.btnNewReceiveAddress.Location = new System.Drawing.Point(8, 68);
            this.btnNewReceiveAddress.Size     = new System.Drawing.Size(95, 23);
            this.btnNewReceiveAddress.Click   += new System.EventHandler(this.btnNewReceiveAddress_Click);

            this.btnCopyReceive.Text     = "Copy";
            this.btnCopyReceive.Location = new System.Drawing.Point(111, 68);
            this.btnCopyReceive.Size     = new System.Drawing.Size(60, 23);
            this.btnCopyReceive.Click   += new System.EventHandler(this.btnCopyReceive_Click);

            this.tabReceive.Controls.AddRange(new System.Windows.Forms.Control[] {
                this.lblReceive, this.txtReceiveAddress, this.btnNewReceiveAddress, this.btnCopyReceive });

            // ── Tab 5: Security ──────────────────────────────────────────
            this.tabSecurity.Text     = "Security";
            this.tabSecurity.TabIndex = 4;

            this.lblSecurityNote.Text =
                "Wallet keys are stored in the wallet/ directory, AES-256 encrypted.\r\n" +
                "Open/unlock the wallet to access private keys and sign transactions.\r\n" +
                "Encrypting with a password protects keys at rest.";
            this.lblSecurityNote.Location = new System.Drawing.Point(8, 8);
            this.lblSecurityNote.Size     = new System.Drawing.Size(700, 52);

            this.btnOpenWallet.Text     = "Open / Create Wallet";
            this.btnOpenWallet.Location = new System.Drawing.Point(8, 72);
            this.btnOpenWallet.Size     = new System.Drawing.Size(140, 23);
            this.btnOpenWallet.Click   += new System.EventHandler(this.btnOpenWallet_Click);

            this.btnUnlockWallet.Text     = "Unlock Wallet";
            this.btnUnlockWallet.Location = new System.Drawing.Point(156, 72);
            this.btnUnlockWallet.Size     = new System.Drawing.Size(100, 23);
            this.btnUnlockWallet.Click   += new System.EventHandler(this.btnUnlockWallet_Click);

            this.btnLockWallet.Text     = "Lock Wallet";
            this.btnLockWallet.Location = new System.Drawing.Point(264, 72);
            this.btnLockWallet.Size     = new System.Drawing.Size(90, 23);
            this.btnLockWallet.Click   += new System.EventHandler(this.btnLockWallet_Click);

            this.btnEncryptWallet.Text     = "Change Password";
            this.btnEncryptWallet.Location = new System.Drawing.Point(362, 72);
            this.btnEncryptWallet.Size     = new System.Drawing.Size(120, 23);
            this.btnEncryptWallet.Click   += new System.EventHandler(this.btnEncryptWallet_Click);

            this.lblSecurityStatus.AutoSize = true;
            this.lblSecurityStatus.Location = new System.Drawing.Point(8, 108);
            this.lblSecurityStatus.Text     = "Wallet not open";

            this.tabSecurity.Controls.AddRange(new System.Windows.Forms.Control[] {
                this.lblSecurityNote, this.btnOpenWallet, this.btnUnlockWallet,
                this.btnLockWallet, this.btnEncryptWallet, this.lblSecurityStatus });

            // ── Form ─────────────────────────────────────────────────────
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode       = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize          = new System.Drawing.Size(772, 535);
            this.Controls.Add(this.lblNetwork);
            this.Controls.Add(this.cboNetwork);
            this.Controls.Add(this.tabControl);
            this.MinimumSize         = new System.Drawing.Size(788, 574);
            this.Name                = "WalletView";
            this.StartPosition       = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text                = "Wallet";
            this.Load               += new System.EventHandler(this.WalletView_Load);

            this.tabControl.ResumeLayout(false);
            this.ResumeLayout(false);
            this.PerformLayout();
        }

        #endregion

        private System.Windows.Forms.ComboBox cboNetwork;
        private System.Windows.Forms.Label lblNetwork;
        private System.Windows.Forms.TabControl tabControl;
        private System.Windows.Forms.TabPage tabSync;
        private System.Windows.Forms.TabPage tabAddresses;
        private System.Windows.Forms.TabPage tabSend;
        private System.Windows.Forms.TabPage tabReceive;
        private System.Windows.Forms.TabPage tabSecurity;
        // Tab 1
        private System.Windows.Forms.TextBox txtSyncStatus;
        // Tab 2
        private System.Windows.Forms.ListView lvwAddresses;
        private System.Windows.Forms.ColumnHeader colAddress;
        private System.Windows.Forms.ColumnHeader colLabel;
        private System.Windows.Forms.ColumnHeader colPath;
        private System.Windows.Forms.Button btnNewAddress;
        private System.Windows.Forms.Button btnCopyAddress;
        private System.Windows.Forms.Button btnImportWIF;
        private System.Windows.Forms.Button btnExportWIF;
        private System.Windows.Forms.Label lblWalletStatus;
        // Tab 3
        private System.Windows.Forms.Label lblSendAddress;
        private System.Windows.Forms.TextBox txtSendAddress;
        private System.Windows.Forms.Label lblSendAmount;
        private System.Windows.Forms.TextBox txtSendAmount;
        private System.Windows.Forms.Button btnAddOutput;
        private System.Windows.Forms.Button btnRemoveOutput;
        private System.Windows.Forms.ListView lvwSendOutputs;
        private System.Windows.Forms.ColumnHeader colSendAddr;
        private System.Windows.Forms.ColumnHeader colSendAmt;
        private System.Windows.Forms.Button btnSend;
        private System.Windows.Forms.Label lblSendResult;
        private System.Windows.Forms.Label lblSendNote;
        // Tab 4
        private System.Windows.Forms.Label lblReceive;
        private System.Windows.Forms.TextBox txtReceiveAddress;
        private System.Windows.Forms.Button btnNewReceiveAddress;
        private System.Windows.Forms.Button btnCopyReceive;
        // Tab 5
        private System.Windows.Forms.Button btnOpenWallet;
        private System.Windows.Forms.Button btnEncryptWallet;
        private System.Windows.Forms.Button btnLockWallet;
        private System.Windows.Forms.Button btnUnlockWallet;
        private System.Windows.Forms.Label lblSecurityStatus;
        private System.Windows.Forms.Label lblSecurityNote;
    }
}
