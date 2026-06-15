using SUP.Wallet;
namespace SUP
{
    partial class Connections
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(Connections));
            this.pnlMain = new System.Windows.Forms.Panel();
            // ── Wallet Nodes GroupBox ──
            this.grpWalletNodes = new System.Windows.Forms.GroupBox();
            this.lblColChain    = new System.Windows.Forms.Label();
            this.lblColStatus   = new System.Windows.Forms.Label();
            this.lblColProgress = new System.Windows.Forms.Label();
            this.lblColHeight   = new System.Windows.Forms.Label();
            this.lblColReindex  = new System.Windows.Forms.Label();
            this.lblColRescan   = new System.Windows.Forms.Label();
            // BTC Testnet row
            this.lblChainBTCT    = new System.Windows.Forms.Label();
            this.btnBTCT         = new System.Windows.Forms.Button();
            this.prgBTCT         = new System.Windows.Forms.ProgressBar();
            this.lblStatusBTCT   = new System.Windows.Forms.Label();
            this.lblHeightBTCT   = new System.Windows.Forms.Label();
            this.chkReindexBTCT  = new System.Windows.Forms.CheckBox();
            this.chkRescanBTCT   = new System.Windows.Forms.CheckBox();
            // BTC Mainnet row
            this.lblChainBTC     = new System.Windows.Forms.Label();
            this.btnBTC          = new System.Windows.Forms.Button();
            this.prgBTC          = new System.Windows.Forms.ProgressBar();
            this.lblStatusBTC    = new System.Windows.Forms.Label();
            this.lblHeightBTC    = new System.Windows.Forms.Label();
            this.chkReindexBTC   = new System.Windows.Forms.CheckBox();
            this.chkRescanBTC    = new System.Windows.Forms.CheckBox();
            // LTC row
            this.lblChainLTC     = new System.Windows.Forms.Label();
            this.btnLTC          = new System.Windows.Forms.Button();
            this.prgLTC          = new System.Windows.Forms.ProgressBar();
            this.lblStatusLTC    = new System.Windows.Forms.Label();
            this.lblHeightLTC    = new System.Windows.Forms.Label();
            this.chkReindexLTC   = new System.Windows.Forms.CheckBox();
            this.chkRescanLTC    = new System.Windows.Forms.CheckBox();
            // DOGE row
            this.lblChainDOG     = new System.Windows.Forms.Label();
            this.btnDOG          = new System.Windows.Forms.Button();
            this.prgDOG          = new System.Windows.Forms.ProgressBar();
            this.lblStatusDOG    = new System.Windows.Forms.Label();
            this.lblHeightDOG    = new System.Windows.Forms.Label();
            this.chkReindexDOG   = new System.Windows.Forms.CheckBox();
            this.chkRescanDOG    = new System.Windows.Forms.CheckBox();
            // MZC row
            this.lblChainMZC     = new System.Windows.Forms.Label();
            this.btnMZC          = new System.Windows.Forms.Button();
            this.prgMZC          = new System.Windows.Forms.ProgressBar();
            this.lblStatusMZC    = new System.Windows.Forms.Label();
            this.lblHeightMZC    = new System.Windows.Forms.Label();
            this.chkReindexMZC   = new System.Windows.Forms.CheckBox();
            this.chkRescanMZC    = new System.Windows.Forms.CheckBox();
            // RPC server + wallet view
            this.chkEnableRpc    = new System.Windows.Forms.CheckBox();
            this.btnOpenWallet   = new System.Windows.Forms.Button();
            // ── Settings GroupBox ──
            this.grpSettings       = new System.Windows.Forms.GroupBox();
            this.chkFilterLivePostings = new System.Windows.Forms.CheckBox();
            this.chkWalkieTalkie       = new System.Windows.Forms.CheckBox();
            // ── IPFS GroupBox ──
            this.grpIPFS           = new System.Windows.Forms.GroupBox();
            this.btnIPFS           = new System.Windows.Forms.Button();
            this.btnPinIPFS        = new System.Windows.Forms.Button();
            this.btnUnpinIPFS      = new System.Windows.Forms.Button();
            this.btnAddIPFS        = new System.Windows.Forms.Button();
            this.btnPurgeIPFS      = new System.Windows.Forms.Button();
            this.btnClearIPFSisLoadingCache = new System.Windows.Forms.Button();
            this.chkLiveFeedPinning    = new System.Windows.Forms.CheckBox();
            this.chkUseIpfsApiHelpers  = new System.Windows.Forms.CheckBox();
            // ── Maintenance ──
            this.grpMaintenance    = new System.Windows.Forms.GroupBox();
            this.btnPurge          = new System.Windows.Forms.Button();
            this.btnPurgeBlock     = new System.Windows.Forms.Button();
            this.btnPurgeMute      = new System.Windows.Forms.Button();
            this.lblVersion        = new System.Windows.Forms.Label();
            this.pictureBox1       = new System.Windows.Forms.PictureBox();

            this.pnlMain.SuspendLayout();
            this.grpWalletNodes.SuspendLayout();
            this.grpSettings.SuspendLayout();
            this.grpIPFS.SuspendLayout();
            this.grpMaintenance.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.pictureBox1)).BeginInit();
            this.SuspendLayout();

            // ── pnlMain ──────────────────────────────────────────────────
            this.pnlMain.AutoScroll = true;
            this.pnlMain.Dock = System.Windows.Forms.DockStyle.Fill;
            this.pnlMain.Controls.Add(this.grpWalletNodes);
            this.pnlMain.Controls.Add(this.grpSettings);
            this.pnlMain.Controls.Add(this.grpIPFS);
            this.pnlMain.Controls.Add(this.grpMaintenance);

            // ── grpWalletNodes ────────────────────────────────────────────
            this.grpWalletNodes.Text = "Wallet Nodes  (daemon replaces -qt)";
            this.grpWalletNodes.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25f, System.Drawing.FontStyle.Bold);
            this.grpWalletNodes.Location = new System.Drawing.Point(8, 8);
            this.grpWalletNodes.Size = new System.Drawing.Size(746, 240);
            this.grpWalletNodes.TabIndex = 0;

            // Column headers
            AddLabel(this.grpWalletNodes, this.lblColChain,   "Chain",    6,  20, 60,  13);
            AddLabel(this.grpWalletNodes, this.lblColStatus,  "Status",  70,  20, 110, 13);
            AddLabel(this.grpWalletNodes, this.lblColProgress,"Sync",   190,  20, 140, 13);
            AddLabel(this.grpWalletNodes, this.lblColHeight,  "Height", 338,  20, 100, 13);
            AddLabel(this.grpWalletNodes, this.lblColReindex, "Reindex",448,  20, 55,  13);
            AddLabel(this.grpWalletNodes, this.lblColRescan,  "Rescan", 510,  20, 55,  13);

            // Rows: y positions
            int[] rowY = { 40, 70, 100, 130, 160 };
            AddCoinRow(CoinNetworkId.BitcoinTestnet, "BTCT", rowY[0]);
            AddCoinRow(CoinNetworkId.BitcoinMainnet, "BTC",  rowY[1]);
            AddCoinRow(CoinNetworkId.Litecoin,       "LTC",  rowY[2]);
            AddCoinRow(CoinNetworkId.Dogecoin,       "DOGE", rowY[3]);
            AddCoinRow(CoinNetworkId.Mazacoin,       "MZC",  rowY[4]);

            // RPC server checkbox
            this.chkEnableRpc.Text     = "Enable internal RPC server (port 8334 – CLI compatible)";
            this.chkEnableRpc.AutoSize = true;
            this.chkEnableRpc.Location = new System.Drawing.Point(10, 196);
            this.chkEnableRpc.TabIndex = 50;
            this.chkEnableRpc.CheckedChanged += new System.EventHandler(this.chkEnableRpc_CheckedChanged);
            this.grpWalletNodes.Controls.Add(this.chkEnableRpc);

            // Open wallet button
            this.btnOpenWallet.Text      = "Open Wallet View";
            this.btnOpenWallet.Location  = new System.Drawing.Point(10, 214);
            this.btnOpenWallet.Size      = new System.Drawing.Size(130, 23);
            this.btnOpenWallet.TabIndex  = 51;
            this.btnOpenWallet.Click    += new System.EventHandler(this.btnOpenWallet_Click);
            this.grpWalletNodes.Controls.Add(this.btnOpenWallet);

            // ── grpSettings ───────────────────────────────────────────────
            this.grpSettings.Text     = "User Preferences";
            this.grpSettings.Location = new System.Drawing.Point(8, 254);
            this.grpSettings.Size     = new System.Drawing.Size(746, 66);
            this.grpSettings.TabIndex = 1;

            this.chkFilterLivePostings.Text     = "Filter live postings (only show live posts from who you follow)";
            this.chkFilterLivePostings.AutoSize = true;
            this.chkFilterLivePostings.Location = new System.Drawing.Point(10, 20);
            this.chkFilterLivePostings.TabIndex = 0;
            this.chkFilterLivePostings.CheckedChanged += new System.EventHandler(this.chkFilterLivePostings_CheckedChanged);
            this.grpSettings.Controls.Add(this.chkFilterLivePostings);

            this.chkWalkieTalkie.Text     = "Immediately deliver audio attachments (walkie-talkie mode)";
            this.chkWalkieTalkie.AutoSize = true;
            this.chkWalkieTalkie.Location = new System.Drawing.Point(10, 40);
            this.chkWalkieTalkie.TabIndex = 1;
            this.chkWalkieTalkie.CheckedChanged += new System.EventHandler(this.chkWalkieTalkie_CheckedChanged);
            this.grpSettings.Controls.Add(this.chkWalkieTalkie);

            // ── grpIPFS ───────────────────────────────────────────────────
            this.grpIPFS.Text     = "IPFS";
            this.grpIPFS.Location = new System.Drawing.Point(8, 326);
            this.grpIPFS.Size     = new System.Drawing.Size(746, 90);
            this.grpIPFS.TabIndex = 2;

            this.btnIPFS.Text     = "enable IPFS daemon";
            this.btnIPFS.Location = new System.Drawing.Point(8, 20);
            this.btnIPFS.Size     = new System.Drawing.Size(152, 23);
            this.btnIPFS.TabIndex = 0;
            this.btnIPFS.Click   += new System.EventHandler(this.btnIPFS_Click);
            this.grpIPFS.Controls.Add(this.btnIPFS);

            this.btnPinIPFS.Text     = "pin cache";
            this.btnPinIPFS.Enabled  = false;
            this.btnPinIPFS.Location = new System.Drawing.Point(168, 20);
            this.btnPinIPFS.Size     = new System.Drawing.Size(70, 23);
            this.btnPinIPFS.TabIndex = 1;
            this.btnPinIPFS.Click   += new System.EventHandler(this.btnIPFSPin_Click);
            this.grpIPFS.Controls.Add(this.btnPinIPFS);

            this.btnUnpinIPFS.Text     = "unpin cache";
            this.btnUnpinIPFS.Enabled  = false;
            this.btnUnpinIPFS.Location = new System.Drawing.Point(246, 20);
            this.btnUnpinIPFS.Size     = new System.Drawing.Size(75, 23);
            this.btnUnpinIPFS.TabIndex = 2;
            this.btnUnpinIPFS.Click   += new System.EventHandler(this.btnUnpinIPFS_Click);
            this.grpIPFS.Controls.Add(this.btnUnpinIPFS);

            this.btnAddIPFS.Text     = "add cache";
            this.btnAddIPFS.Enabled  = false;
            this.btnAddIPFS.Location = new System.Drawing.Point(329, 20);
            this.btnAddIPFS.Size     = new System.Drawing.Size(70, 23);
            this.btnAddIPFS.TabIndex = 3;
            this.btnAddIPFS.Click   += new System.EventHandler(this.btnIPFSAdd_Click);
            this.grpIPFS.Controls.Add(this.btnAddIPFS);

            this.btnClearIPFSisLoadingCache.Text     = "purge isloading";
            this.btnClearIPFSisLoadingCache.Location = new System.Drawing.Point(407, 20);
            this.btnClearIPFSisLoadingCache.Size     = new System.Drawing.Size(96, 23);
            this.btnClearIPFSisLoadingCache.TabIndex = 4;
            this.btnClearIPFSisLoadingCache.Click   += new System.EventHandler(this.btnPurgeIPFSBuilding_Click);
            this.grpIPFS.Controls.Add(this.btnClearIPFSisLoadingCache);

            this.btnPurgeIPFS.Text     = "purge cache";
            this.btnPurgeIPFS.Location = new System.Drawing.Point(511, 20);
            this.btnPurgeIPFS.Size     = new System.Drawing.Size(78, 23);
            this.btnPurgeIPFS.TabIndex = 5;
            this.btnPurgeIPFS.Click   += new System.EventHandler(this.btnPurgeIPFS_Click);
            this.grpIPFS.Controls.Add(this.btnPurgeIPFS);

            this.chkLiveFeedPinning.Text     = "IPFS pinning";
            this.chkLiveFeedPinning.AutoSize = true;
            this.chkLiveFeedPinning.Location = new System.Drawing.Point(8, 52);
            this.chkLiveFeedPinning.TabIndex = 6;
            this.chkLiveFeedPinning.CheckedChanged += new System.EventHandler(this.chkLiveFeedPinning_CheckedChanged);
            this.grpIPFS.Controls.Add(this.chkLiveFeedPinning);

            this.chkUseIpfsApiHelpers.Text     = "use IPFS API helpers";
            this.chkUseIpfsApiHelpers.AutoSize = true;
            this.chkUseIpfsApiHelpers.Location = new System.Drawing.Point(120, 52);
            this.chkUseIpfsApiHelpers.TabIndex = 7;
            this.chkUseIpfsApiHelpers.CheckedChanged += new System.EventHandler(this.chkUseIpfsApiHelpers_CheckedChanged);
            this.grpIPFS.Controls.Add(this.chkUseIpfsApiHelpers);

            // ── grpMaintenance ────────────────────────────────────────────
            this.grpMaintenance.Text     = "Maintenance";
            this.grpMaintenance.Location = new System.Drawing.Point(8, 422);
            this.grpMaintenance.Size     = new System.Drawing.Size(746, 60);
            this.grpMaintenance.TabIndex = 3;

            this.btnPurge.Text     = "purge root";
            this.btnPurge.Location = new System.Drawing.Point(8, 22);
            this.btnPurge.Size     = new System.Drawing.Size(93, 23);
            this.btnPurge.TabIndex = 0;
            this.btnPurge.Click   += new System.EventHandler(this.btnPurge_Click);
            this.grpMaintenance.Controls.Add(this.btnPurge);

            this.btnPurgeBlock.Text     = "purge block";
            this.btnPurgeBlock.Location = new System.Drawing.Point(109, 22);
            this.btnPurgeBlock.Size     = new System.Drawing.Size(89, 23);
            this.btnPurgeBlock.TabIndex = 1;
            this.btnPurgeBlock.Click   += new System.EventHandler(this.btnPurgeBlock_Click);
            this.grpMaintenance.Controls.Add(this.btnPurgeBlock);

            this.btnPurgeMute.Text     = "purge mute";
            this.btnPurgeMute.Location = new System.Drawing.Point(206, 22);
            this.btnPurgeMute.Size     = new System.Drawing.Size(93, 23);
            this.btnPurgeMute.TabIndex = 2;
            this.btnPurgeMute.Click   += new System.EventHandler(this.brnPurgeMute_Click);
            this.grpMaintenance.Controls.Add(this.btnPurgeMute);

            this.pictureBox1.Image       = ((System.Drawing.Image)(resources.GetObject("pictureBox1.Image")));
            this.pictureBox1.InitialImage = ((System.Drawing.Image)(resources.GetObject("pictureBox1.InitialImage")));
            this.pictureBox1.Location    = new System.Drawing.Point(630, 22);
            this.pictureBox1.Size        = new System.Drawing.Size(40, 40);
            this.pictureBox1.SizeMode    = System.Windows.Forms.PictureBoxSizeMode.Zoom;
            this.pictureBox1.TabStop     = false;
            this.grpMaintenance.Controls.Add(this.pictureBox1);

            this.lblVersion.Text      = "Sup!? v0.9.0-beta";
            this.lblVersion.Font      = new System.Drawing.Font("Microsoft Sans Serif", 8.25f, System.Drawing.FontStyle.Bold);
            this.lblVersion.AutoSize  = true;
            this.lblVersion.Location  = new System.Drawing.Point(450, 28);
            this.grpMaintenance.Controls.Add(this.lblVersion);

            // ── Form ──────────────────────────────────────────────────────
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode       = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize          = new System.Drawing.Size(762, 498);
            this.Controls.Add(this.pnlMain);
            this.MinimumSize         = new System.Drawing.Size(778, 537);
            this.Name                = "Connections";
            this.StartPosition       = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text                = "Connections";
            this.Load               += new System.EventHandler(this.Connections_Load);

            this.pnlMain.ResumeLayout(false);
            this.grpWalletNodes.ResumeLayout(false);
            this.grpSettings.ResumeLayout(false);
            this.grpIPFS.ResumeLayout(false);
            this.grpMaintenance.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.pictureBox1)).EndInit();
            this.ResumeLayout(false);
        }

        #endregion

        // ── Field declarations ─────────────────────────────────────────────
        private System.Windows.Forms.Panel pnlMain;
        private System.Windows.Forms.GroupBox grpWalletNodes;
        private System.Windows.Forms.Label lblColChain, lblColStatus, lblColProgress, lblColHeight, lblColReindex, lblColRescan;
        // BTCT row
        private System.Windows.Forms.Label lblChainBTCT;
        private System.Windows.Forms.Button btnBTCT;
        private System.Windows.Forms.ProgressBar prgBTCT;
        private System.Windows.Forms.Label lblStatusBTCT;
        private System.Windows.Forms.Label lblHeightBTCT;
        private System.Windows.Forms.CheckBox chkReindexBTCT;
        private System.Windows.Forms.CheckBox chkRescanBTCT;
        // BTC row
        private System.Windows.Forms.Label lblChainBTC;
        private System.Windows.Forms.Button btnBTC;
        private System.Windows.Forms.ProgressBar prgBTC;
        private System.Windows.Forms.Label lblStatusBTC;
        private System.Windows.Forms.Label lblHeightBTC;
        private System.Windows.Forms.CheckBox chkReindexBTC;
        private System.Windows.Forms.CheckBox chkRescanBTC;
        // LTC row
        private System.Windows.Forms.Label lblChainLTC;
        private System.Windows.Forms.Button btnLTC;
        private System.Windows.Forms.ProgressBar prgLTC;
        private System.Windows.Forms.Label lblStatusLTC;
        private System.Windows.Forms.Label lblHeightLTC;
        private System.Windows.Forms.CheckBox chkReindexLTC;
        private System.Windows.Forms.CheckBox chkRescanLTC;
        // DOGE row
        private System.Windows.Forms.Label lblChainDOG;
        private System.Windows.Forms.Button btnDOG;
        private System.Windows.Forms.ProgressBar prgDOG;
        private System.Windows.Forms.Label lblStatusDOG;
        private System.Windows.Forms.Label lblHeightDOG;
        private System.Windows.Forms.CheckBox chkReindexDOG;
        private System.Windows.Forms.CheckBox chkRescanDOG;
        // MZC row
        private System.Windows.Forms.Label lblChainMZC;
        private System.Windows.Forms.Button btnMZC;
        private System.Windows.Forms.ProgressBar prgMZC;
        private System.Windows.Forms.Label lblStatusMZC;
        private System.Windows.Forms.Label lblHeightMZC;
        private System.Windows.Forms.CheckBox chkReindexMZC;
        private System.Windows.Forms.CheckBox chkRescanMZC;
        // RPC + wallet
        private System.Windows.Forms.CheckBox chkEnableRpc;
        private System.Windows.Forms.Button btnOpenWallet;
        // Settings
        private System.Windows.Forms.GroupBox grpSettings;
        private System.Windows.Forms.CheckBox chkFilterLivePostings;
        private System.Windows.Forms.CheckBox chkWalkieTalkie;
        // IPFS
        private System.Windows.Forms.GroupBox grpIPFS;
        private System.Windows.Forms.Button btnIPFS;
        private System.Windows.Forms.Button btnPinIPFS;
        private System.Windows.Forms.Button btnUnpinIPFS;
        private System.Windows.Forms.Button btnAddIPFS;
        private System.Windows.Forms.Button btnPurgeIPFS;
        private System.Windows.Forms.Button btnClearIPFSisLoadingCache;
        private System.Windows.Forms.CheckBox chkLiveFeedPinning;
        private System.Windows.Forms.CheckBox chkUseIpfsApiHelpers;
        // Maintenance
        private System.Windows.Forms.GroupBox grpMaintenance;
        private System.Windows.Forms.Button btnPurge;
        private System.Windows.Forms.Button btnPurgeBlock;
        private System.Windows.Forms.Button btnPurgeMute;
        private System.Windows.Forms.Label lblVersion;
        private System.Windows.Forms.PictureBox pictureBox1;
    }
}
