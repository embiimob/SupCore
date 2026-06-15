using System.Windows.Forms;
using SupCore.Wallet;
using SupCore.Forms;

namespace SupCore.Forms
{
    /// <summary>
    /// Main application window.
    /// Hosts the toolbar with "Connections" and "Wallet" buttons, and a placeholder
    /// area where sub-forms can be embedded or launched.
    /// </summary>
    public partial class SupMain : Form
    {
        private readonly WalletManager _walletManager;

        public SupMain(WalletManager walletManager)
        {
            _walletManager = walletManager;
            InitializeComponent();
        }

        private void btnConnections_Click(object sender, EventArgs e)
        {
            var conn = new Connections(_walletManager);
            conn.ShowDialog(this);
        }

        private void btnWallet_Click(object sender, EventArgs e)
        {
            var wallet = new WalletWindow(_walletManager);
            wallet.Show(this);
        }
    }
}
