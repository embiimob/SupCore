using System.Windows.Forms;
using SupCore.Wallet;

namespace SupCore.Forms
{
    /// <summary>
    /// WalletWindow – per-coin wallet management UI.
    /// Provides: address generation, key import/export, balance retrieval, and transaction sending.
    /// </summary>
    public partial class WalletWindow : Form
    {
        private readonly WalletManager _walletManager;

        public WalletWindow(WalletManager walletManager)
        {
            _walletManager = walletManager;
            InitializeComponent();
            PopulateCoinTabs();
        }

        // ── Simple input dialog (replaces VB InputBox) ─────────────────────────────
        private static string? ShowInputDialog(string prompt, string title, string defaultValue = "")
        {
            using var dlg = new Form
            {
                Text = title,
                ClientSize = new Size(420, 110),
                FormBorderStyle = FormBorderStyle.FixedDialog,
                StartPosition = FormStartPosition.CenterParent,
                MaximizeBox = false,
                MinimizeBox = false
            };
            var lbl = new Label { Text = prompt, Location = new Point(10, 10), AutoSize = true };
            var txt = new TextBox { Text = defaultValue, Location = new Point(10, 32), Width = 395 };
            var btnOk = new Button
            {
                Text = "OK", DialogResult = DialogResult.OK,
                Location = new Point(250, 68), Width = 75
            };
            var btnCancel = new Button
            {
                Text = "Cancel", DialogResult = DialogResult.Cancel,
                Location = new Point(330, 68), Width = 75
            };
            dlg.Controls.AddRange(new Control[] { lbl, txt, btnOk, btnCancel });
            dlg.AcceptButton = btnOk;
            dlg.CancelButton = btnCancel;
            return dlg.ShowDialog() == DialogResult.OK ? txt.Text : null;
        }

        private void PopulateCoinTabs()
        {
            foreach (CoinType coin in Enum.GetValues<CoinType>())
            {
                var tab = BuildCoinTab(coin);
                tabControl.TabPages.Add(tab);
            }
        }

        private TabPage BuildCoinTab(CoinType coin)
        {
            var page = new TabPage(coin.GetShortName()) { Tag = coin };

            // ── address list ──
            var lblAddresses = new Label
            {
                Text = "Addresses",
                Location = new Point(8, 8),
                AutoSize = true
            };

            var lstAddresses = new ListBox
            {
                Name = $"lst_{coin}",
                Location = new Point(8, 28),
                Size = new Size(560, 120),
                HorizontalScrollbar = true
            };
            RefreshAddressList(coin, lstAddresses);

            // ── balance label ──
            var lblBalance = new Label
            {
                Name = $"lblBal_{coin}",
                Text = "Balance: –",
                Location = new Point(8, 155),
                AutoSize = true
            };

            // ── buttons row 1: key management ──
            int btnY = 178;
            var btnNew = new Button { Text = "New Address", Location = new Point(8, btnY), Width = 120 };
            btnNew.Click += (_, _) => NewAddress(coin, lstAddresses);

            var btnImport = new Button { Text = "Import Key", Location = new Point(136, btnY), Width = 110 };
            btnImport.Click += (_, _) => ImportKey(coin, lstAddresses);

            var btnExport = new Button { Text = "Export Key", Location = new Point(254, btnY), Width = 110 };
            btnExport.Click += (_, _) => ExportKey(coin, lstAddresses);

            var btnCopy = new Button { Text = "Copy Address", Location = new Point(372, btnY), Width = 120 };
            btnCopy.Click += (_, _) => CopyAddress(lstAddresses);

            var btnRefreshBal = new Button { Text = "Refresh Balance", Location = new Point(500, btnY), Width = 130 };
            btnRefreshBal.Click += async (_, _) => await RefreshBalanceAsync(coin, lstAddresses, lblBalance);

            // ── send group ──
            int sendY = btnY + 40;
            var grpSend = new GroupBox { Text = "Send", Location = new Point(8, sendY), Size = new Size(640, 100) };

            var lblTo = new Label { Text = "To:", Location = new Point(8, 22), AutoSize = true };
            var txtTo = new TextBox { Name = $"txtTo_{coin}", Location = new Point(40, 19), Width = 380 };

            var lblAmt = new Label { Text = "Amount:", Location = new Point(8, 54), AutoSize = true };
            var txtAmount = new TextBox { Name = $"txtAmt_{coin}", Location = new Point(60, 50), Width = 120, Text = "0.001" };

            var lblFee = new Label { Text = "Fee (sat):", Location = new Point(192, 54), AutoSize = true };
            var txtFee = new TextBox { Name = $"txtFee_{coin}", Location = new Point(255, 50), Width = 80, Text = "1000" };

            var btnSend = new Button { Text = "Send", Location = new Point(345, 48), Width = 80 };
            btnSend.Click += async (_, _) =>
                await SendTransactionAsync(coin, lstAddresses, txtTo.Text, txtAmount.Text, txtFee.Text);

            grpSend.Controls.AddRange(new Control[] { lblTo, txtTo, lblAmt, txtAmount, lblFee, txtFee, btnSend });

            // ── wallet encryption ──
            int encY = sendY + 108;
            var grpEnc = new GroupBox { Text = "Wallet Encryption", Location = new Point(8, encY), Size = new Size(640, 62) };

            var lblPwd = new Label { Text = "Password:", Location = new Point(8, 24), AutoSize = true };
            var txtPwd = new TextBox
            {
                Location = new Point(72, 21), Width = 200, UseSystemPasswordChar = true
            };
            var btnEncrypt = new Button { Text = "Encrypt Wallet", Location = new Point(282, 19), Width = 120 };
            btnEncrypt.Click += (_, _) => EncryptWallet(txtPwd.Text);

            var btnUnlock = new Button { Text = "Unlock", Location = new Point(412, 19), Width = 80 };
            btnUnlock.Click += (_, _) => UnlockWallet(txtPwd.Text);

            var btnLock = new Button { Text = "Lock", Location = new Point(502, 19), Width = 60 };
            btnLock.Click += (_, _) => { _walletManager.Lock(); MessageBox.Show("Wallet locked."); };

            grpEnc.Controls.AddRange(new Control[] { lblPwd, txtPwd, btnEncrypt, btnUnlock, btnLock });

            page.Controls.AddRange(new Control[]
            {
                lblAddresses, lstAddresses, lblBalance,
                btnNew, btnImport, btnExport, btnCopy, btnRefreshBal,
                grpSend, grpEnc
            });

            return page;
        }

        // ── Address operations ─────────────────────────────────────────────────────

        private void RefreshAddressList(CoinType coin, ListBox lst)
        {
            lst.Items.Clear();
            foreach (var entry in _walletManager.GetEntries(coin))
            {
                string display = string.IsNullOrWhiteSpace(entry.Label)
                    ? entry.Address
                    : $"{entry.Address}  [{entry.Label}]";
                lst.Items.Add(display);
            }
        }

        private void NewAddress(CoinType coin, ListBox lst)
        {
            if (_walletManager.IsLocked && _walletManager.HasPassword)
            {
                MessageBox.Show("Please unlock the wallet before generating new keys.", "Wallet Locked",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            string label = ShowInputDialog("Enter an optional label for the new address:", "New Address") ?? "";
            string address = _walletManager.GenerateNewKey(coin, label);
            RefreshAddressList(coin, lst);
            MessageBox.Show($"New address created:\n{address}", "Address Generated",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void ImportKey(CoinType coin, ListBox lst)
        {
            string? wif = ShowInputDialog(
                $"Enter the WIF private key to import ({coin.GetDisplayName()}):", "Import Private Key");
            if (string.IsNullOrWhiteSpace(wif)) return;

            if (_walletManager.IsLocked && _walletManager.HasPassword)
            {
                MessageBox.Show("Please unlock the wallet first.", "Wallet Locked",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            string label = ShowInputDialog("Optional label:", "Import Key", "imported") ?? "imported";

            try
            {
                string address = _walletManager.ImportPrivateKey(coin, wif.Trim(), label);
                RefreshAddressList(coin, lst);
                MessageBox.Show($"Key imported.\nAddress: {address}", "Import Successful",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Import failed: {ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void ExportKey(CoinType coin, ListBox lst)
        {
            if (lst.SelectedItem is not string selectedText) { MessageBox.Show("Select an address first."); return; }
            string address = selectedText.Split(' ')[0];

            if (_walletManager.IsLocked && _walletManager.HasPassword)
            {
                MessageBox.Show("Please unlock the wallet first.", "Wallet Locked",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            try
            {
                string wif = _walletManager.ExportPrivateKey(coin, address);
                var form = new Form
                {
                    Text = "Exported Private Key",
                    Size = new Size(500, 120),
                    FormBorderStyle = FormBorderStyle.FixedDialog,
                    StartPosition = FormStartPosition.CenterParent,
                    MaximizeBox = false
                };
                var txt = new TextBox
                {
                    Text = wif, ReadOnly = true, Location = new Point(8, 8),
                    Width = 465, BackColor = System.Drawing.Color.LightYellow
                };
                var btnCopyKey = new Button
                {
                    Text = "Copy", Location = new Point(380, 40), Width = 90
                };
                btnCopyKey.Click += (_, _) => { Clipboard.SetText(wif); MessageBox.Show("Copied to clipboard."); };
                form.Controls.AddRange(new Control[] { txt, btnCopyKey });
                form.ShowDialog(this);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Export failed: {ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private static void CopyAddress(ListBox lst)
        {
            if (lst.SelectedItem is not string selectedText) { MessageBox.Show("Select an address first."); return; }
            string address = selectedText.Split(' ')[0];
            Clipboard.SetText(address);
            MessageBox.Show($"Copied: {address}", "Copied", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private async Task RefreshBalanceAsync(CoinType coin, ListBox lst, Label lblBalance)
        {
            if (lst.SelectedItem is string selectedText)
            {
                string address = selectedText.Split(' ')[0];
                lblBalance.Text = "Balance: loading…";
                decimal balance = await _walletManager.GetBalanceAsync(coin, address);
                lblBalance.Text = $"Balance ({address[..8]}…): {balance} {coin.GetShortName()}";
            }
            else
            {
                lblBalance.Text = "Total balance: loading…";
                decimal total = await _walletManager.GetTotalBalanceAsync(coin);
                lblBalance.Text = $"Total balance: {total} {coin.GetShortName()}";
            }
        }

        private async Task SendTransactionAsync(
            CoinType coin, ListBox lst, string toAddress, string amountText, string feeText)
        {
            if (lst.SelectedItem is not string selectedText)
            {
                MessageBox.Show("Select a from-address in the list first.", "No Address Selected",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            string fromAddress = selectedText.Split(' ')[0];

            if (!decimal.TryParse(amountText, System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out decimal amount) || amount <= 0)
            {
                MessageBox.Show("Invalid amount.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            if (!long.TryParse(feeText, out long fee) || fee < 0)
            {
                MessageBox.Show("Invalid fee.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            var confirm = MessageBox.Show(
                $"Send {amount} {coin.GetShortName()} to\n{toAddress}\n\nFee: {fee} satoshis\nFrom: {fromAddress}",
                "Confirm Transaction", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (confirm != DialogResult.Yes) return;

            if (_walletManager.IsLocked && _walletManager.HasPassword)
            {
                MessageBox.Show("Please unlock the wallet first.", "Wallet Locked",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            try
            {
                string txid = await _walletManager.SendAsync(coin, fromAddress, toAddress, amount, fee);
                MessageBox.Show($"Transaction broadcast!\nTxID: {txid}", "Success",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Transaction failed: {ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // ── Encryption helpers ─────────────────────────────────────────────────────

        private void EncryptWallet(string password)
        {
            if (string.IsNullOrWhiteSpace(password))
            {
                MessageBox.Show("Enter a password first.", "Password Required",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            try
            {
                _walletManager.EncryptWallet(password);
                MessageBox.Show("Wallet encrypted and locked. Keep your password safe!",
                    "Encrypted", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Encryption failed: {ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void UnlockWallet(string password)
        {
            if (!_walletManager.HasPassword)
            {
                MessageBox.Show("Wallet is not encrypted.", "Not Encrypted",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            bool ok = _walletManager.Unlock(password);
            MessageBox.Show(ok ? "Wallet unlocked." : "Incorrect password.",
                ok ? "Unlocked" : "Error",
                MessageBoxButtons.OK,
                ok ? MessageBoxIcon.Information : MessageBoxIcon.Error);
        }
    }
}
