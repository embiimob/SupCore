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

            // Subscribe to status-change events for each node
            foreach (var cfg in CoinNetworkConfig.All)
            {
                var node = NodeHostManager.GetNode(cfg.Id);
                node.StatusChanged += (s2, e2) => UpdateRowFromNode(cfg.Id);
            }

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
            var row = _rows[id];

            if (node.IsRunning)
            {
                btn.Text = "Start";
                btn.BackColor = SystemColors.Control;
                btn.ForeColor = SystemColors.ControlText;
                Task.Run(() => node.Stop());
            }
            else
            {
                btn.Text = "Stop";
                btn.BackColor = Color.Blue;
                btn.ForeColor = Color.Yellow;
                node.Start(
                    reindex: row.ChkReindex.Checked,
                    rescan:  row.ChkRescan.Checked);
            }
        }

        private void UpdateRowFromNode(CoinNetworkId id)
        {
            if (!_rows.TryGetValue(id, out var row)) return;
            var node = NodeHostManager.GetNode(id);
            if (InvokeRequired)
            {
                Invoke(new Action(() => ApplyRowState(row, node)));
            }
            else
            {
                ApplyRowState(row, node);
            }
        }

        private static void ApplyRowState(CoinStatusRow row, NodeHost node)
        {
            if (node.IsRunning)
            {
                row.BtnToggle.Text      = "Stop";
                row.BtnToggle.BackColor = Color.Blue;
                row.BtnToggle.ForeColor = Color.Yellow;
            }
            else
            {
                row.BtnToggle.Text      = "Start";
                row.BtnToggle.BackColor = SystemColors.Control;
                row.BtnToggle.ForeColor = SystemColors.ControlText;
            }
            row.LblStatus.Text  = node.StatusText;
            int pct = (int)Math.Min(100, Math.Max(0, node.SyncPercent));
            row.Progress.Value  = pct;
            row.LblHeight.Text  = node.ChainHeaders > 0
                ? $"{node.SyncedBlocks}/{node.ChainHeaders}"
                : "";
        }

        private void RefreshAllStatus()
        {
            NodeHostManager.RefreshAll();
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
    }
}
