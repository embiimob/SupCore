using System.Windows.Forms;
using SupCore.Wallet;

namespace SupCore.Forms
{
    /// <summary>
    /// Connections panel – replaces the old bitcoin-qt / dogecoin-qt / litecoin-qt launch buttons
    /// with an internal C# wallet status display.
    ///
    /// Coins are connected on-demand by clicking their respective buttons.
    ///
    /// The panel polls every 30 seconds and prefers local node RPC sync data. If no
    /// local daemon is reachable it falls back to API reachability mode.
    /// </summary>
    public partial class Connections : Form
    {
        private readonly WalletManager _walletManager;
        private System.Windows.Forms.Timer _refreshTimer = null!;

        // Status labels per coin – populated by InitializeComponent / BuildCoinRow
        private readonly Dictionary<CoinType, Label> _statusLabels = new();
        private readonly Dictionary<CoinType, Button> _startButtons = new();

        // Tracks which coins are currently online so the timer only polls connected ones.
        private readonly HashSet<CoinType> _onlineCoins = new();

        public Connections(WalletManager walletManager)
        {
            _walletManager = walletManager;
            InitializeComponent();
            BuildCoinRows();
            SetupRefreshTimer();
        }

        // ── UI construction ────────────────────────────────────────────────────────

        private void BuildCoinRows()
        {
            int y = 16;
            foreach (CoinType coin in Enum.GetValues<CoinType>())
            {
                BuildCoinRow(coin, y);
                y += 52;
            }
        }

        private void BuildCoinRow(CoinType coin, int y)
        {
            // Coin label
            var lblName = new Label
            {
                Text = coin.GetDisplayName(),
                Location = new Point(12, y + 4),
                Width = 180,
                Font = new System.Drawing.Font("Segoe UI", 9f, System.Drawing.FontStyle.Bold)
            };

            // Status label – shows online/offline and sync info
            var lblStatus = new Label
            {
                Name = $"lblStatus_{coin}",
                Text = "Not connected",
                Location = new Point(200, y + 4),
                Width = 360,
                ForeColor = System.Drawing.Color.Gray
            };
            _statusLabels[coin] = lblStatus;

                // Start / Refresh button
            bool isAutoStarted = coin == CoinType.BitcoinTestnet;
            var btnStart = new Button
            {
                Name = $"btn_{coin}",
                    Text = isAutoStarted ? "Connect" : "Connect",
                Location = new Point(570, y),
                Width = 100,
                Height = 26,
                BackColor = isAutoStarted
                    ? System.Drawing.Color.LightBlue
                    : System.Drawing.Color.WhiteSmoke
            };
            btnStart.Click += async (_, _) => await ConnectCoinAsync(coin);
            _startButtons[coin] = btnStart;

            pnlCoins.Controls.AddRange(new Control[] { lblName, lblStatus, btnStart });
        }

        private void SetupRefreshTimer()
        {
            _refreshTimer = new System.Windows.Forms.Timer { Interval = 30_000 };
            // Only refresh coins that are already online to avoid hammering APIs for offline coins.
            _refreshTimer.Tick += async (_, _) =>
            {
                foreach (var coin in _onlineCoins.ToList())
                    await RefreshStatusAsync(coin);
            };
            _refreshTimer.Start();
        }

        // ── Event: form loaded ─────────────────────────────────────────────────────
        private async void Connections_Load(object sender, EventArgs e)
        {
            // Check which coins are already reachable
            foreach (var coin in Enum.GetValues<CoinType>())
                _ = RefreshStatusAsync(coin); // fire-and-forget; update UI when done
        }

        // ── API connectivity ───────────────────────────────────────────────────────

        private async Task ConnectCoinAsync(CoinType coin)
        {
            if (_startButtons.TryGetValue(coin, out var btn))
            {
                btn.Text = "Connecting…";
                btn.BackColor = System.Drawing.Color.LightBlue;
            }

            _ = BlockchainApiClient.TryStartDaemon(coin);

            for (int i = 0; i < 3; i++)
            {
                var status = await RefreshStatusAsync(coin);
                if (status.IsOnline && status.Source == SyncSource.LocalNode) break;
                await Task.Delay(1200);
            }
        }

        private async Task<SyncStatus> RefreshStatusAsync(CoinType coin)
        {
            var status = await BlockchainApiClient.GetSyncStatusAsync(coin).ConfigureAwait(false);

            if (InvokeRequired)
                Invoke(() => UpdateCoinUI(coin, status));
            else
                UpdateCoinUI(coin, status);
            return status;
        }

        private async Task RefreshAllStatusAsync()
        {
            foreach (CoinType coin in Enum.GetValues<CoinType>())
                await RefreshStatusAsync(coin);
        }

        private void UpdateCoinUI(CoinType coin, SyncStatus status)
        {
            if (status.IsOnline)
                _onlineCoins.Add(coin);
            else
                _onlineCoins.Remove(coin);

            if (_statusLabels.TryGetValue(coin, out var lbl))
            {
                lbl.Text = status.StatusText;
                lbl.ForeColor = status.IsOnline
                    ? System.Drawing.Color.DarkGreen
                    : System.Drawing.Color.DarkRed;
            }

            if (_startButtons.TryGetValue(coin, out var btn))
            {
                if (status.IsOnline)
                {
                    if (status.Source == SyncSource.LocalNode)
                    {
                        btn.Text = status.IsFullySynced ? "Synced ✓" : "Syncing…";
                        btn.BackColor = status.IsFullySynced
                            ? System.Drawing.Color.LightGreen
                            : System.Drawing.Color.Khaki;
                    }
                    else
                    {
                        btn.Text = "API ✓";
                        btn.BackColor = System.Drawing.Color.LightYellow;
                    }
                }
                else
                {
                    btn.Text = "Retry";
                    btn.BackColor = System.Drawing.Color.MistyRose;
                }
            }
        }

        // ── IPFS panel (preserved from original) ──────────────────────────────────

        private void btnIPFS_Click(object sender, EventArgs e)
        {
            var process = new System.Diagnostics.Process
            {
                StartInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = @"ipfs\ipfs.exe",
                    Arguments = "swarm peers",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true
                }
            };
            try { process.Start(); }
            catch { MessageBox.Show("IPFS daemon not found. Place ipfs.exe in the ipfs\\ folder.", "IPFS"); return; }

            string output = process.StandardOutput.ReadToEnd();
            btnIPFS.Text = output.Length > 0 ? "IPFS daemon active" : "Start IPFS daemon";
        }

        private void chkLiveFeedPinning_CheckedChanged(object sender, EventArgs e)
        {
            try
            {
                if (chkLiveFeedPinning.Checked)
                    File.WriteAllText("IPFS_PINNING_ENABLED", "1");
                else if (File.Exists("IPFS_PINNING_ENABLED"))
                    File.Delete("IPFS_PINNING_ENABLED");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Could not update IPFS pinning flag: {ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void btnPurge_Click(object sender, EventArgs e)
        {
            var r = MessageBox.Show("Remove all cached files and configuration data?",
                "Purge Cache", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
            if (r != DialogResult.Yes) return;

            string[] filesToDelete = { "IPFS_PINNING_ENABLED", "IPFS_API_HELPERS_ENABLED",
                                        "WALKIE_TALKIE_ENABLED", "LIVE_FILTER_ENABLED" };
            var errors = new List<string>();
            foreach (var f in filesToDelete)
            {
                try { if (File.Exists(f)) File.Delete(f); }
                catch (Exception ex) { errors.Add($"{f}: {ex.Message}"); }
            }

            if (errors.Count > 0)
                MessageBox.Show("Some files could not be deleted:\n" + string.Join("\n", errors),
                    "Partial Purge", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            else
                MessageBox.Show("Cache purged.", "Done");
        }
    }
}
