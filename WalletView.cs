using SUP.Wallet;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace SUP
{
    /// <summary>
    /// Full wallet management UI.
    /// Tabs: Sync Status | Addresses | Send | Receive | Security
    /// </summary>
    public partial class WalletView : Form
    {
        private System.Windows.Forms.Timer _refreshTimer;
        private CoinNetworkId _activeNetwork = CoinNetworkId.BitcoinTestnet;

        public WalletView()
        {
            InitializeComponent();
        }

        // ── Form load ──────────────────────────────────────────────────────

        private void WalletView_Load(object sender, EventArgs e)
        {
            cboNetwork.Items.Clear();
            foreach (var cfg in CoinNetworkConfig.All)
                cboNetwork.Items.Add(cfg.DisplayName);
            cboNetwork.SelectedIndex = 0;

            _refreshTimer = new System.Windows.Forms.Timer { Interval = 2000 };
            _refreshTimer.Tick += (s, e2) => RefreshStatus();
            _refreshTimer.Start();

            RefreshStatus();
        }

        private void cboNetwork_SelectedIndexChanged(object sender, EventArgs e)
        {
            _activeNetwork = CoinNetworkConfig.All[cboNetwork.SelectedIndex].Id;
            RefreshAddresses();
            RefreshReceive();
        }

        // ── Tab 1: Sync Status ─────────────────────────────────────────────

        private void RefreshStatus()
        {
            if (InvokeRequired) { Invoke(new Action(UpdateStatusTab)); return; }
            UpdateStatusTab();
        }

        private void UpdateStatusTab()
        {
            var sb = new System.Text.StringBuilder();
            foreach (var cfg in CoinNetworkConfig.All)
            {
                var wallet = NodeHostManager.GetWallet(cfg.Id);
                string status = wallet.IsOpen ? "Open" : "Stopped";
                int addrCount = wallet.IsOpen ? wallet.GetAddresses().Count : 0;
                sb.AppendLine($"{cfg.DisplayName,-22} {status,-22} {addrCount} addr");
            }
            txtSyncStatus.Text = sb.ToString();
        }

        // ── Tab 2: Addresses ───────────────────────────────────────────────

        private void RefreshAddresses()
        {
            lvwAddresses.Items.Clear();
            var wm = GetWallet();
            if (wm == null || !wm.IsOpen) { lblWalletStatus.Text = "Wallet locked"; return; }

            foreach (var entry in wm.GetAddresses())
            {
                var item = new ListViewItem(entry.Address);
                item.SubItems.Add(entry.Label ?? "");
                item.SubItems.Add(entry.IsHD
                    ? (entry.DerivationPath ?? "HD (no path)")
                    : "imported");
                lvwAddresses.Items.Add(item);
            }
            lblWalletStatus.Text = $"Wallet open – {wm.GetAddresses().Count} addresses";
        }

        private void btnNewAddress_Click(object sender, EventArgs e)
        {
            string label = ShowInputDialog("Enter optional label for new address:", "New Address", "");
            try
            {
                EnsureWalletOpen();
                var entry = GetWallet().NewAddress(label);
                RefreshAddresses();
                MessageBox.Show("Created: " + entry.Address, "New Address", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex) { MessageBox.Show(ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error); }
        }

        private void btnCopyAddress_Click(object sender, EventArgs e)
        {
            if (lvwAddresses.SelectedItems.Count == 0) return;
            Clipboard.SetText(lvwAddresses.SelectedItems[0].Text);
        }

        private void btnImportWIF_Click(object sender, EventArgs e)
        {
            string wif = ShowInputDialog("Enter WIF private key to import:", "Import WIF", "");
            if (string.IsNullOrWhiteSpace(wif)) return;
            try
            {
                EnsureWalletOpen();
                string label = ShowInputDialog("Label (optional):", "Import WIF", "");
                var entry = GetWallet().ImportWIF(wif, label);
                RefreshAddresses();
                MessageBox.Show("Imported: " + entry.Address, "Import WIF", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex) { MessageBox.Show(ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error); }
        }

        private void btnExportWIF_Click(object sender, EventArgs e)
        {
            if (lvwAddresses.SelectedItems.Count == 0) { MessageBox.Show("Select an address first."); return; }
            string addr = lvwAddresses.SelectedItems[0].Text;
            try
            {
                EnsureWalletOpen();
                string wif = GetWallet().ExportWIF(addr);
                var dlg = new Form { Text = "WIF Export", Width = 500, Height = 120, StartPosition = FormStartPosition.CenterParent };
                var tb  = new TextBox { Text = wif, ReadOnly = true, Dock = DockStyle.Fill, Font = new Font("Courier New", 9f) };
                dlg.Controls.Add(tb);
                dlg.ShowDialog(this);
            }
            catch (Exception ex) { MessageBox.Show(ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error); }
        }

        // ── Tab 3: Send ────────────────────────────────────────────────────

        private readonly List<KeyValuePair<string, decimal>> _sendOutputs =
            new List<KeyValuePair<string, decimal>>();

        private void btnAddOutput_Click(object sender, EventArgs e)
        {
            string addr = txtSendAddress.Text.Trim();
            if (!decimal.TryParse(txtSendAmount.Text.Trim(), out decimal amt) || amt <= 0)
            { MessageBox.Show("Enter a valid amount."); return; }
            if (string.IsNullOrWhiteSpace(addr))
            { MessageBox.Show("Enter a recipient address."); return; }

            _sendOutputs.Add(new KeyValuePair<string, decimal>(addr, amt));
            lvwSendOutputs.Items.Add(addr).SubItems.Add(amt.ToString("F8"));
            txtSendAddress.Clear();
            txtSendAmount.Clear();
        }

        private void btnRemoveOutput_Click(object sender, EventArgs e)
        {
            if (lvwSendOutputs.SelectedIndices.Count == 0) return;
            int idx = lvwSendOutputs.SelectedIndices[0];
            _sendOutputs.RemoveAt(idx);
            lvwSendOutputs.Items.RemoveAt(idx);
        }

        private async void btnSend_Click(object sender, EventArgs e)
        {
            if (_sendOutputs.Count == 0) { MessageBox.Show("Add at least one output."); return; }
            btnSend.Enabled = false;
            lblSendResult.Text = "Sending…";
            try
            {
                EnsureWalletOpen();
                // Build ordered dict preserving insertion order
                var dict = new Dictionary<string, decimal>();
                foreach (var kv in _sendOutputs) dict[kv.Key] = kv.Value;
                string txid = await Task.Run(() => GetWallet().SendMany(dict));
                lblSendResult.Text = "Sent! TxID: " + txid;
                _sendOutputs.Clear();
                lvwSendOutputs.Items.Clear();
            }
            catch (Exception ex)
            {
                lblSendResult.Text = "Error: " + ex.Message;
                MessageBox.Show(ex.Message, "Send Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally { btnSend.Enabled = true; }
        }

        // ── Tab 4: Receive ─────────────────────────────────────────────────

        private void RefreshReceive()
        {
            try
            {
                var wm = GetWallet();
                if (wm == null || !wm.IsOpen) { txtReceiveAddress.Text = ""; return; }
                var addrs = wm.GetAddresses();
                txtReceiveAddress.Text = addrs.Count > 0 ? addrs[0].Address : "(no addresses)";
            }
            catch { txtReceiveAddress.Text = ""; }
        }

        private void btnNewReceiveAddress_Click(object sender, EventArgs e)
        {
            try
            {
                EnsureWalletOpen();
                var entry = GetWallet().NewAddress("receive");
                txtReceiveAddress.Text = entry.Address;
                RefreshAddresses();
            }
            catch (Exception ex) { MessageBox.Show(ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error); }
        }

        private void btnCopyReceive_Click(object sender, EventArgs e)
        {
            if (!string.IsNullOrWhiteSpace(txtReceiveAddress.Text))
                Clipboard.SetText(txtReceiveAddress.Text);
        }

        // ── Tab 5: Security ────────────────────────────────────────────────

        private void btnOpenWallet_Click(object sender, EventArgs e)
        {
            string pass = AskPassword("Enter wallet password (leave blank for no encryption):");
            try
            {
                var wm = GetWallet();
                wm.Open(pass);
                lblSecurityStatus.Text = "Wallet open";
                RefreshAddresses();
                RefreshReceive();
            }
            catch (Exception ex)
            {
                lblSecurityStatus.Text = "Error: " + ex.Message;
                MessageBox.Show(ex.Message, "Open Wallet", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void btnEncryptWallet_Click(object sender, EventArgs e)
        {
            try
            {
                EnsureWalletOpen();
                string pass1 = AskPassword("Enter new password:");
                string pass2 = AskPassword("Confirm new password:");
                if (pass1 != pass2) { MessageBox.Show("Passwords do not match."); return; }
                GetWallet().EncryptWallet(pass1);
                lblSecurityStatus.Text = "Wallet encrypted";
                MessageBox.Show("Wallet encrypted successfully.", "Encrypt", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void btnLockWallet_Click(object sender, EventArgs e)
        {
            GetWallet()?.Lock();
            lblSecurityStatus.Text = "Wallet locked";
            lvwAddresses.Items.Clear();
        }

        private void btnUnlockWallet_Click(object sender, EventArgs e)
        {
            string pass = AskPassword("Enter wallet password:");
            try
            {
                GetWallet().Unlock(pass);
                lblSecurityStatus.Text = "Wallet unlocked";
                RefreshAddresses();
            }
            catch (Exception ex) { MessageBox.Show(ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error); }
        }

        // ── Helpers ────────────────────────────────────────────────────────

        private WalletManager GetWallet()
            => NodeHostManager.GetWallet(_activeNetwork);

        private void EnsureWalletOpen()
        {
            var wm = GetWallet();
            if (!wm.IsOpen)
            {
                string pass = AskPassword("Enter wallet password:");
                wm.Open(pass);
            }
        }

        private static string ShowInputDialog(string prompt, string title, string defaultValue)
        {
            var dlg = new Form { Text = title, Width = 460, Height = 130,
                StartPosition = FormStartPosition.CenterParent, FormBorderStyle = FormBorderStyle.FixedDialog,
                MaximizeBox = false, MinimizeBox = false };
            var lbl = new Label { Text = prompt, Location = new Point(10, 12), AutoSize = true };
            var tb  = new TextBox { Location = new Point(10, 32), Width = 420, Text = defaultValue };
            var btn = new Button { Text = "OK", DialogResult = DialogResult.OK, Location = new Point(10, 64), Width = 70 };
            dlg.Controls.AddRange(new System.Windows.Forms.Control[] { lbl, tb, btn });
            dlg.AcceptButton = btn;
            dlg.ShowDialog();
            return tb.Text;
        }

        private static string AskPassword(string prompt)
        {
            var dlg = new Form { Text = "Password", Width = 360, Height = 130,
                StartPosition = FormStartPosition.CenterParent, FormBorderStyle = FormBorderStyle.FixedDialog,
                MaximizeBox = false, MinimizeBox = false };
            var lbl  = new Label { Text = prompt, Location = new Point(10, 12), AutoSize = true };
            var tb   = new TextBox { Location = new Point(10, 32), Width = 320, UseSystemPasswordChar = true };
            var btn  = new Button { Text = "OK", DialogResult = DialogResult.OK, Location = new Point(10, 64), Width = 70 };
            dlg.Controls.AddRange(new System.Windows.Forms.Control[] { lbl, tb, btn });
            dlg.AcceptButton = btn;
            dlg.ShowDialog();
            return tb.Text;
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            _refreshTimer?.Stop();
            _refreshTimer?.Dispose();
            base.OnFormClosed(e);
        }
    }
}
