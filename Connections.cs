using SUP.P2FK;
using SUP.Wallet;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace SUP
{
    public partial class Connections : Form
    {
        private System.Windows.Forms.Timer _statusTimer;

        // Per-coin UI rows indexed by CoinNetworkId
        private readonly Dictionary<CoinNetworkId, CoinStatusRow> _rows =
            new Dictionary<CoinNetworkId, CoinStatusRow>();
        private readonly Dictionary<CoinNetworkId, int> _rowTops =
            new Dictionary<CoinNetworkId, int>();
        private const int ChainColumnX = 6;
        private const int ChainColumnWidth = 60;
        private const int StatusColumnX = 70;
        private const int ProgressColumnX = 190;
        private const int HeightColumnX = 338;
        private const int ReindexColumnX = 540;
        private const int RescanColumnX = 590;

        private struct CoinStatusRow
        {
            public Button BtnToggle;
            public ProgressBar Progress;
            public Label LblStatus;
            public Label LblHeight;
            public CheckBox ChkReindex;
            public CheckBox ChkRescan;
        }

        public Connections()
        {
            InitializeComponent();
            grpWalletNodes.Resize += (s, e) => LayoutWalletNodeColumns();
            SetupTooltips();
            SetupStatusTimer();
        }

        // ── Constructor helpers ────────────────────────────────────────────

        private void SetupTooltips()
        {
            var tt = new ToolTip();
            tt.SetToolTip(btnIPFS,   "Launches the IPFS daemon. Displays active if currently running.");
            tt.SetToolTip(chkLiveFeedPinning, "Check to pin all images and videos displayed in your live feed.");
            tt.SetToolTip(chkUseIpfsApiHelpers,
                "When checked, IPFS attachments are first fetched from public gateways " +
                "(ipfs.io then p2fk.io) before falling back to the local Kubo node.");
            tt.SetToolTip(btnAddIPFS,   "Adds all files in the SUP IPFS cache to the IPFS network.");
            tt.SetToolTip(btnPinIPFS,   "Pins all files in the SUP IPFS cache to your local IPFS store.");
            tt.SetToolTip(btnUnpinIPFS, "Removes the pin from all files in the SUP IPFS cache.");
            tt.SetToolTip(btnClearIPFSisLoadingCache,
                "Clears blocks preventing SUP from launching a new IPFS download process.");
            tt.SetToolTip(btnPurgeIPFS,  "Deletes all files in the SUP IPFS cache.");
            tt.SetToolTip(btnPurge,      "Removes all cached files and config in the SUP root folder.");
            tt.SetToolTip(btnPurgeBlock, "Removes all blocked transaction IDs and addresses.");
            tt.SetToolTip(btnPurgeMute,  "Removes all address-based muting.");
            tt.SetToolTip(chkEnableRpc,
                "Starts an internal JSON-RPC server on port 8334.\n" +
                "Supports searchrawtransactions, sendmany (no shuffle), dumpprivkey, etc.\n" +
                "Use this to connect external CLI tools to the internal wallet.");
        }

        private void SetupStatusTimer()
        {
            _statusTimer = new System.Windows.Forms.Timer { Interval = 2000 };
            _statusTimer.Tick += (s, e) => RefreshAllStatus();
            _statusTimer.Start();
        }

        // ── Form load ──────────────────────────────────────────────────────

        private void Connections_Load(object sender, EventArgs e)
        {
            // Restore settings flags
            chkLiveFeedPinning.Checked      = File.Exists("IPFS_PINNING_ENABLED");
            chkUseIpfsApiHelpers.Checked    = File.Exists("IPFS_API_HELPERS_ENABLED");
            chkWalkieTalkie.Checked         = File.Exists("WALKIE_TALKIE_ENABLED");
            chkFilterLivePostings.Checked   = File.Exists("LIVE_FILTER_ENABLED");
            chkEnableRpc.Checked            = File.Exists("INTERNAL_RPC_ENABLED");

            // Wire up per-coin row references from the Designer-created controls
            _rows[CoinNetworkId.BitcoinTestnet] = new CoinStatusRow
            {
                BtnToggle  = btnBTCT,  Progress = prgBTCT, LblStatus = lblStatusBTCT,
                LblHeight  = lblHeightBTCT, ChkReindex = chkReindexBTCT, ChkRescan = chkRescanBTCT
            };
            _rows[CoinNetworkId.BitcoinMainnet] = new CoinStatusRow
            {
                BtnToggle  = btnBTC,   Progress = prgBTC,  LblStatus = lblStatusBTC,
                LblHeight  = lblHeightBTC,  ChkReindex = chkReindexBTC,  ChkRescan = chkRescanBTC
            };
            _rows[CoinNetworkId.Litecoin] = new CoinStatusRow
            {
                BtnToggle  = btnLTC,   Progress = prgLTC,  LblStatus = lblStatusLTC,
                LblHeight  = lblHeightLTC,  ChkReindex = chkReindexLTC,  ChkRescan = chkRescanLTC
            };
            _rows[CoinNetworkId.Dogecoin] = new CoinStatusRow
            {
                BtnToggle  = btnDOG,   Progress = prgDOG,  LblStatus = lblStatusDOG,
                LblHeight  = lblHeightDOG,  ChkReindex = chkReindexDOG,  ChkRescan = chkRescanDOG
            };
            _rows[CoinNetworkId.Mazacoin] = new CoinStatusRow
            {
                BtnToggle  = btnMZC,   Progress = prgMZC,  LblStatus = lblStatusMZC,
                LblHeight  = lblHeightMZC,  ChkReindex = chkReindexMZC,  ChkRescan = chkRescanMZC
            };

            LayoutWalletNodeColumns();

            // Check IPFS daemon status
            Task.Run(CheckIpfsStatus);

            // Initial status refresh
            RefreshAllStatus();
        }

        // ── Node toggle buttons ────────────────────────────────────────────

        private void BtnToggle_Click(object sender, EventArgs e)
        {
            var btn = (Button)sender;
            var id = (CoinNetworkId)btn.Tag;
            var node = NodeHostManager.GetNode(id);
            var wallet = NodeHostManager.GetWallet(id);
            var row = _rows[id];

            if (node.IsRunning)
            {
                try { node.Stop(); }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message, "Node Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                wallet.Lock();
            }
            else
            {
                if (!OpenWalletWithPrompt(wallet))
                {
                    UpdateRowStatus(id);
                    return;
                }

                try
                {
                    node.Start(row.ChkReindex.Checked, row.ChkRescan.Checked);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message, "Node Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            UpdateRowStatus(id);
        }

        private static bool OpenWalletWithPrompt(WalletManager wallet)
        {
            // Try with no password first (new wallets and unencrypted wallets)
            try
            {
                wallet.Open("");
                return true;
            }
            catch (InvalidOperationException ex) when (ex.Message.StartsWith("Wrong password"))
            {
                // Wallet file exists and is encrypted — fall through to password prompt
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Wallet Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }

            string pass = PromptWalletPassword("Enter wallet password:");
            try
            {
                wallet.Open(pass);
                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Wallet Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }
        }

        private void UpdateRowStatus(CoinNetworkId id)
        {
            if (!_rows.TryGetValue(id, out var row)) return;
            var wallet = NodeHostManager.GetWallet(id);
            var node = NodeHostManager.GetNode(id);
            if (InvokeRequired)
            {
                Invoke(new Action(() => ApplyCoinRowState(id, row, wallet, node)));
            }
            else
            {
                ApplyCoinRowState(id, row, wallet, node);
            }
        }

        private void ApplyCoinRowState(CoinNetworkId id, CoinStatusRow row, WalletManager wallet, NodeHost node)
        {
            if (node.IsRunning)
            {
                row.BtnToggle.Text      = "Stop";
                row.BtnToggle.BackColor = Color.Blue;
                row.BtnToggle.ForeColor = Color.Yellow;
                row.LblStatus.Text = node.StatusText;
                int progress = (int)Math.Round(node.SyncPercent);
                if (progress < 0) progress = 0;
                if (progress > 100) progress = 100;
                row.Progress.Value = progress;
                row.LblHeight.Text = BuildHeightSummary(id, wallet, node);
            }
            else
            {
                row.BtnToggle.Text      = "Start";
                row.BtnToggle.BackColor = SystemColors.Control;
                row.BtnToggle.ForeColor = SystemColors.ControlText;
                row.LblStatus.Text      = string.IsNullOrWhiteSpace(node.StatusText) || node.StatusText == "Stopped"
                    ? (wallet.IsOpen ? "Wallet Open" : "Stopped")
                    : node.StatusText;
                row.Progress.Value      = 0;
                row.LblHeight.Text      = wallet.IsOpen
                    ? wallet.GetAddresses().Count + " addr"
                    : "";
            }
        }

        private void RefreshAllStatus()
        {
            NodeHostManager.RefreshAll();
            foreach (var cfg in CoinNetworkConfig.All)
                UpdateRowStatus(cfg.Id);
        }

        private static string PromptWalletPassword(string prompt)
        {
            var dlg = new Form
            {
                Text = "Wallet Password", Width = 360, Height = 130,
                StartPosition = FormStartPosition.CenterParent,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                MaximizeBox = false, MinimizeBox = false
            };
            var lbl = new Label { Text = prompt, Location = new System.Drawing.Point(10, 12), AutoSize = true };
            var tb  = new TextBox { Location = new System.Drawing.Point(10, 32), Width = 320, UseSystemPasswordChar = true };
            var btn = new Button { Text = "OK", DialogResult = DialogResult.OK, Location = new System.Drawing.Point(10, 64), Width = 70 };
            dlg.Controls.AddRange(new System.Windows.Forms.Control[] { lbl, tb, btn });
            dlg.AcceptButton = btn;
            dlg.ShowDialog();
            return tb.Text;
        }

        // ── RPC server checkbox ────────────────────────────────────────────

        private void chkEnableRpc_CheckedChanged(object sender, EventArgs e)
        {
            if (chkEnableRpc.Checked)
            {
                File.WriteAllText("INTERNAL_RPC_ENABLED", "");
                NodeHostManager.StartRpcServer();
            }
            else
            {
                try { File.Delete("INTERNAL_RPC_ENABLED"); } catch { }
                NodeHostManager.StopRpcServer();
            }
        }

        // ── Wallet View button ─────────────────────────────────────────────

        private void btnOpenWallet_Click(object sender, EventArgs e)
        {
            new WalletView().Show();
        }

        // ── IPFS ───────────────────────────────────────────────────────────

        private void CheckIpfsStatus()
        {
            try
            {
                var proc = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = @"ipfs\ipfs.exe",
                        Arguments = "swarm peers",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        CreateNoWindow = true
                    }
                };
                proc.Start();
                string output = proc.StandardOutput.ReadToEnd();
                if (output.Length > 0)
                    Invoke((MethodInvoker)(() =>
                    {
                        btnIPFS.Text      = "IPFS daemon active";
                        btnIPFS.ForeColor = Color.Yellow;
                        btnIPFS.BackColor = Color.Blue;
                        btnPinIPFS.Enabled   = true;
                        btnUnpinIPFS.Enabled = true;
                        btnAddIPFS.Enabled   = true;
                    }));
                else
                    Invoke((MethodInvoker)(() =>
                    {
                        btnIPFS.Text      = "enable IPFS daemon";
                        btnIPFS.ForeColor = Color.Black;
                        btnIPFS.BackColor = Color.White;
                    }));
            }
            catch { }
        }

        private void btnIPFS_Click(object sender, EventArgs e)
        {
            if (btnIPFS.Text == "IPFS daemon active")
            {
                btnIPFS.Text      = "enable IPFS daemon";
                btnIPFS.BackColor = Color.White;
                btnIPFS.ForeColor = Color.Black;

                var proc = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = @"ipfs\ipfs.exe", Arguments = "shutdown",
                        UseShellExecute = false, CreateNoWindow = true
                    }
                };
                proc.Start();
                btnPinIPFS.Enabled   = false;
                btnUnpinIPFS.Enabled = false;
            }
            else
            {
                btnIPFS.Text      = "IPFS daemon active";
                btnIPFS.ForeColor = Color.Yellow;
                btnIPFS.BackColor = Color.Blue;

                string ipfsDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                Environment.SetEnvironmentVariable("IPFS_PATH", ipfsDir + @"\ipfs");

                var init = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = @"ipfs\ipfs.exe", Arguments = "init",
                        UseShellExecute = false, CreateNoWindow = true
                    }
                };
                init.Start();
                init.WaitForExit();

                var daemon = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = @"ipfs\ipfs.exe",
                        Arguments = $"daemon --repo-dir {ipfsDir + @"\ipfs"}",
                        UseShellExecute = false, CreateNoWindow = true
                    }
                };
                daemon.Start();

                btnPinIPFS.Enabled   = true;
                btnUnpinIPFS.Enabled = true;
                btnAddIPFS.Enabled   = true;
            }
        }

        private async void btnIPFSPin_Click(object sender, EventArgs e)
        {
            try
            {
                Invoke((MethodInvoker)(() => {
                    btnPinIPFS.Text = "pinning"; btnPinIPFS.ForeColor = Color.Yellow; btnPinIPFS.BackColor = Color.Blue;
                }));
                foreach (string sub in Directory.GetDirectories("ipfs"))
                {
                    string hash = Path.GetFileName(sub);
                    using (var p = new Process())
                    {
                        p.StartInfo.FileName = @"ipfs\ipfs.exe"; p.StartInfo.Arguments = "pin add " + hash;
                        p.StartInfo.UseShellExecute = false; p.Start();
                        await Task.Run(() => p.WaitForExit(10000));
                    }
                }
            }
            finally
            {
                try { Invoke((MethodInvoker)(() => { btnPinIPFS.Text = "pin cache"; btnPinIPFS.ForeColor = Color.Black; btnPinIPFS.BackColor = Color.White; })); } catch { }
            }
        }

        private async void btnIPFSAdd_Click(object sender, EventArgs e)
        {
            try
            {
                Invoke((MethodInvoker)(() => { btnAddIPFS.Text = "adding"; btnAddIPFS.ForeColor = Color.Yellow; btnAddIPFS.BackColor = Color.Blue; }));
                foreach (string sub in Directory.GetDirectories("ipfs"))
                    foreach (string file in Directory.GetFiles(sub, "*", SearchOption.TopDirectoryOnly))
                        if (!file.EndsWith("-thumbnail.jpg", StringComparison.OrdinalIgnoreCase))
                        {
                            using (var p = new Process())
                            {
                                p.StartInfo.FileName = @"ipfs\ipfs.exe"; p.StartInfo.Arguments = $"add \"{file}\"";
                                p.StartInfo.UseShellExecute = false; p.Start();
                                await Task.Run(() => p.WaitForExit(600000));
                            }
                        }
            }
            finally
            {
                try { Invoke((MethodInvoker)(() => { btnAddIPFS.Text = "add cache"; btnAddIPFS.ForeColor = Color.Black; btnAddIPFS.BackColor = Color.White; })); } catch { }
            }
        }

        private async void btnUnpinIPFS_Click(object sender, EventArgs e)
        {
            try
            {
                Invoke((MethodInvoker)(() => { btnUnpinIPFS.Text = "unpinning"; btnUnpinIPFS.ForeColor = Color.Yellow; btnUnpinIPFS.BackColor = Color.Blue; }));
                foreach (string sub in Directory.GetDirectories("ipfs"))
                {
                    using (var p = new Process())
                    {
                        p.StartInfo.FileName = @"ipfs\ipfs.exe"; p.StartInfo.Arguments = "pin rm " + Path.GetFileName(sub);
                        p.StartInfo.UseShellExecute = false; p.Start();
                        await Task.Run(() => p.WaitForExit(5000));
                    }
                }
            }
            finally
            {
                try { Invoke((MethodInvoker)(() => { btnUnpinIPFS.Text = "unpin cache"; btnUnpinIPFS.ForeColor = Color.Black; btnUnpinIPFS.BackColor = Color.White; })); } catch { }
            }
        }

        private void btnPurgeIPFS_Click(object sender, EventArgs e)
        {
            try
            {
                foreach (string sub in Directory.GetDirectories("ipfs"))
                    Directory.Delete(sub, true);
                foreach (string file in Directory.GetFiles("ipfs"))
                    if (!file.Contains("ipfs.exe")) File.Delete(file);
            }
            catch { Directory.CreateDirectory("ipfs"); }
        }

        private void btnPurge_Click(object sender, EventArgs e)
        {
            try { Directory.Delete("root", true); } catch { }
            try { Directory.CreateDirectory(@"root\sig"); } catch { }
        }

        private void btnPurgeIPFSBuilding_Click(object sender, EventArgs e)
        {
            foreach (string sub in Directory.GetDirectories("ipfs"))
                if (sub.EndsWith("-build") || Directory.GetFiles(sub).Length == 0)
                    try { Directory.Delete(sub, true); } catch { }
        }

        private void btnPurgeBlock_Click(object sender, EventArgs e)
        {
            try
            {
                foreach (string sub in Directory.GetDirectories("root"))
                    try
                    {
                        foreach (string file in Directory.GetFiles(sub))
                            if (Path.GetFileName(file).Equals("BLOCK", StringComparison.OrdinalIgnoreCase))
                            {
                                string[] parts = file.Split('\\');
                                var roots = P2FK.Root.GetRootsByAddress(parts[parts.Length - 2],
                                    "good-user", "better-password", @"http://127.0.0.1:18332");
                                foreach (var r in roots)
                                {
                                    try { Directory.Delete(@"root\" + r.TransactionId, true); } catch { }
                                    foreach (string k in r.Keyword.Keys)
                                        try { Directory.Delete(@"root\" + k, true); } catch { }
                                }
                                try { Directory.Delete(sub, true); } catch { }
                            }
                    }
                    catch { }
            }
            catch { }
        }

        private void brnPurgeMute_Click(object sender, EventArgs e)
        {
            try
            {
                foreach (string sub in Directory.GetDirectories("root"))
                    foreach (string file in Directory.GetFiles(sub))
                        if (Path.GetFileName(file).Equals("MUTE", StringComparison.OrdinalIgnoreCase))
                            File.Delete(file);
            }
            catch { }
        }

        private void chkFilterLivePostings_CheckedChanged(object sender, EventArgs e)
        {
            if (chkFilterLivePostings.Checked) File.WriteAllText("LIVE_FILTER_ENABLED", "");
            else try { File.Delete("LIVE_FILTER_ENABLED"); } catch { }
        }

        private void chkWalkieTalkie_CheckedChanged(object sender, EventArgs e)
        {
            if (chkWalkieTalkie.Checked) File.WriteAllText("WALKIE_TALKIE_ENABLED", "");
            else try { File.Delete("WALKIE_TALKIE_ENABLED"); } catch { }
        }

        private void chkLiveFeedPinning_CheckedChanged(object sender, EventArgs e)
        {
            if (chkLiveFeedPinning.Checked) File.WriteAllText("IPFS_PINNING_ENABLED", "");
            else try { File.Delete("IPFS_PINNING_ENABLED"); } catch { }
        }

        private void chkUseIpfsApiHelpers_CheckedChanged(object sender, EventArgs e)
        {
            if (chkUseIpfsApiHelpers.Checked) File.WriteAllText("IPFS_API_HELPERS_ENABLED", "");
            else try { File.Delete("IPFS_API_HELPERS_ENABLED"); } catch { }
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            _statusTimer?.Stop();
            _statusTimer?.Dispose();
            base.OnFormClosed(e);
        }

        // ── Per-coin control lookup helpers (kept here, not in Designer) ──

        private void AddCoinRow(CoinNetworkId id, string chainLabel, int y)
        {
            var lblChain  = GetChainLabel(id);
            var btnToggle = GetToggleButton(id);
            var prg       = GetProgressBar(id);
            var lblStatus = GetStatusLabel(id);
            var lblHeight = GetHeightLabel(id);
            var chkRe     = GetReindexChk(id);
            var chkRs     = GetRescanChk(id);
            _rowTops[id]  = y;

            lblChain.Text     = chainLabel;
            lblChain.AutoSize = true;
            lblChain.Location = new System.Drawing.Point(ChainColumnX, y + 4);

            lblStatus.AutoSize = false;
            lblStatus.Location = new System.Drawing.Point(StatusColumnX, y + 4);
            lblStatus.Size     = new System.Drawing.Size(110, 13);
            lblStatus.Text     = "Stopped";

            prg.Location = new System.Drawing.Point(ProgressColumnX, y);
            prg.Size     = new System.Drawing.Size(140, 18);
            prg.Minimum  = 0;
            prg.Maximum  = 100;
            prg.Value    = 0;

            lblHeight.AutoSize = false;
            lblHeight.Location = new System.Drawing.Point(HeightColumnX, y + 4);
            lblHeight.Size     = new System.Drawing.Size(200, 13);
            lblHeight.Text     = "";

            chkRe.Text     = "";
            chkRe.AutoSize = false;
            chkRe.Size     = new System.Drawing.Size(15, 15);
            chkRe.Location = new System.Drawing.Point(ReindexColumnX + 5, y + 2);

            chkRs.Text     = "";
            chkRs.AutoSize = false;
            chkRs.Size     = new System.Drawing.Size(15, 15);
            chkRs.Location = new System.Drawing.Point(RescanColumnX + 6, y + 2);

            btnToggle.Text     = "Start";
            btnToggle.Tag      = id;
            btnToggle.Location = new System.Drawing.Point(640, y - 1);
            btnToggle.Size     = new System.Drawing.Size(72, 23);
            btnToggle.Click   += new System.EventHandler(this.BtnToggle_Click);
            btnToggle.BringToFront();

            foreach (System.Windows.Forms.Control c in new System.Windows.Forms.Control[] {
                lblChain, lblStatus, prg, lblHeight, chkRe, chkRs, btnToggle })
                this.grpWalletNodes.Controls.Add(c);
        }

        private static void AddLabel(System.Windows.Forms.GroupBox parent, System.Windows.Forms.Label lbl,
            string text, int x, int y, int w, int h)
        {
            lbl.Text     = text;
            lbl.AutoSize = false;
            lbl.Font     = new System.Drawing.Font("Microsoft Sans Serif", 8.25f, System.Drawing.FontStyle.Bold);
            lbl.Location = new System.Drawing.Point(x, y);
            lbl.Size     = new System.Drawing.Size(w, h);
            parent.Controls.Add(lbl);
        }

        private void LayoutWalletNodeColumns()
        {
            if (grpWalletNodes == null || grpWalletNodes.IsDisposed)
                return;

            int buttonWidth = 72;
            int buttonHeight = 23;
            int buttonRightMargin = 12;
            int buttonX = Math.Max(0, grpWalletNodes.ClientSize.Width - buttonRightMargin - buttonWidth);
            int rescanColumnX = buttonX - 56;
            int reindexColumnX = rescanColumnX - 56;
            int heightColumnX = HeightColumnX;
            int heightColumnWidth = Math.Max(120, reindexColumnX - heightColumnX - 10);
            int progressColumnX = ProgressColumnX;
            int progressColumnWidth = Math.Max(90, heightColumnX - progressColumnX - 8);
            int statusColumnX = StatusColumnX;
            int statusColumnWidth = Math.Max(80, progressColumnX - statusColumnX - 10);

            lblColChain.Location = new Point(ChainColumnX, 20);
            lblColChain.Size = new Size(ChainColumnWidth, 13);

            lblColStatus.Location = new Point(statusColumnX, 20);
            lblColStatus.Size = new Size(statusColumnWidth, 13);

            lblColProgress.Location = new Point(progressColumnX, 20);
            lblColProgress.Size = new Size(progressColumnWidth, 13);

            lblColHeight.Location = new Point(heightColumnX, 20);
            lblColHeight.Size = new Size(heightColumnWidth, 13);

            lblColReindex.Location = new Point(reindexColumnX, 20);
            lblColReindex.Size = new Size(50, 13);

            lblColRescan.Location = new Point(rescanColumnX, 20);
            lblColRescan.Size = new Size(50, 13);

            foreach (var pair in _rowTops)
            {
                if (!_rows.TryGetValue(pair.Key, out var row))
                    continue;

                int y = pair.Value;

                row.LblStatus.Location = new Point(statusColumnX, y + 4);
                row.LblStatus.Size = new Size(statusColumnWidth, 13);

                row.Progress.Location = new Point(progressColumnX, y);
                row.Progress.Size = new Size(progressColumnWidth, 18);

                row.LblHeight.Location = new Point(heightColumnX, y + 4);
                row.LblHeight.Size = new Size(heightColumnWidth, 13);

                row.ChkReindex.Location = new Point(reindexColumnX + 18, y + 2);
                row.ChkRescan.Location = new Point(rescanColumnX + 16, y + 2);

                row.BtnToggle.Location = new Point(buttonX, y - 1);
                row.BtnToggle.Size = new Size(buttonWidth, buttonHeight);
                row.BtnToggle.BringToFront();
            }
        }

        private static string BuildHeightSummary(CoinNetworkId id, WalletManager wallet, NodeHost node)
        {
            int blocks = Math.Max(0, node.SyncedBlocks);
            int headers = Math.Max(blocks, node.ChainHeaders);
            int blocksBehind = Math.Max(0, headers - blocks);
            string summary = string.Format("{0:N0}/{1:N0} blk", blocks, headers);

            if (blocksBehind > 0)
            {
                double yearsBehind = EstimateYearsBehind(id, blocksBehind);
                summary += string.Format(" • {0:F1}y behind", yearsBehind);
            }

            if (node.ChainTxCount > 0)
                summary += string.Format(" • {0:N0} tx", node.ChainTxCount);

            if (wallet.IsOpen)
                summary += string.Format(" • {0} addr", wallet.GetAddresses().Count);

            return summary;
        }

        private static double EstimateYearsBehind(CoinNetworkId id, int blocksBehind)
        {
            double minutesPerBlock = 10.0;
            switch (id)
            {
                case CoinNetworkId.Litecoin:
                case CoinNetworkId.Mazacoin:
                    minutesPerBlock = 2.5;
                    break;
                case CoinNetworkId.Dogecoin:
                    minutesPerBlock = 1.0;
                    break;
            }

            return (blocksBehind * minutesPerBlock) / (60.0 * 24.0 * 365.25);
        }

        private System.Windows.Forms.Label GetChainLabel(CoinNetworkId id)
        {
            switch (id)
            {
                case CoinNetworkId.BitcoinTestnet: return this.lblChainBTCT;
                case CoinNetworkId.BitcoinMainnet: return this.lblChainBTC;
                case CoinNetworkId.Litecoin:       return this.lblChainLTC;
                case CoinNetworkId.Dogecoin:       return this.lblChainDOG;
                default:                           return this.lblChainMZC;
            }
        }

        private System.Windows.Forms.Button GetToggleButton(CoinNetworkId id)
        {
            switch (id)
            {
                case CoinNetworkId.BitcoinTestnet: return this.btnBTCT;
                case CoinNetworkId.BitcoinMainnet: return this.btnBTC;
                case CoinNetworkId.Litecoin:       return this.btnLTC;
                case CoinNetworkId.Dogecoin:       return this.btnDOG;
                default:                           return this.btnMZC;
            }
        }

        private System.Windows.Forms.ProgressBar GetProgressBar(CoinNetworkId id)
        {
            switch (id)
            {
                case CoinNetworkId.BitcoinTestnet: return this.prgBTCT;
                case CoinNetworkId.BitcoinMainnet: return this.prgBTC;
                case CoinNetworkId.Litecoin:       return this.prgLTC;
                case CoinNetworkId.Dogecoin:       return this.prgDOG;
                default:                           return this.prgMZC;
            }
        }

        private System.Windows.Forms.Label GetStatusLabel(CoinNetworkId id)
        {
            switch (id)
            {
                case CoinNetworkId.BitcoinTestnet: return this.lblStatusBTCT;
                case CoinNetworkId.BitcoinMainnet: return this.lblStatusBTC;
                case CoinNetworkId.Litecoin:       return this.lblStatusLTC;
                case CoinNetworkId.Dogecoin:       return this.lblStatusDOG;
                default:                           return this.lblStatusMZC;
            }
        }

        private System.Windows.Forms.Label GetHeightLabel(CoinNetworkId id)
        {
            switch (id)
            {
                case CoinNetworkId.BitcoinTestnet: return this.lblHeightBTCT;
                case CoinNetworkId.BitcoinMainnet: return this.lblHeightBTC;
                case CoinNetworkId.Litecoin:       return this.lblHeightLTC;
                case CoinNetworkId.Dogecoin:       return this.lblHeightDOG;
                default:                           return this.lblHeightMZC;
            }
        }

        private System.Windows.Forms.CheckBox GetReindexChk(CoinNetworkId id)
        {
            switch (id)
            {
                case CoinNetworkId.BitcoinTestnet: return this.chkReindexBTCT;
                case CoinNetworkId.BitcoinMainnet: return this.chkReindexBTC;
                case CoinNetworkId.Litecoin:       return this.chkReindexLTC;
                case CoinNetworkId.Dogecoin:       return this.chkReindexDOG;
                default:                           return this.chkReindexMZC;
            }
        }

        private System.Windows.Forms.CheckBox GetRescanChk(CoinNetworkId id)
        {
            switch (id)
            {
                case CoinNetworkId.BitcoinTestnet: return this.chkRescanBTCT;
                case CoinNetworkId.BitcoinMainnet: return this.chkRescanBTC;
                case CoinNetworkId.Litecoin:       return this.chkRescanLTC;
                case CoinNetworkId.Dogecoin:       return this.chkRescanDOG;
                default:                           return this.chkRescanMZC;
            }
        }
    }
}
