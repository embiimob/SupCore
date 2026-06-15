using System.Windows.Forms;
using SupCore.Wallet;
using SupCore.Forms;

namespace SupCore
{
    internal static class Program
    {
        /// <summary>
        /// Main entry point for SupCore – Internal Wallet Edition.
        ///
        /// On startup the application:
        ///   1. Initialises the internal <see cref="WalletManager"/>.
        ///   2. Auto-connects to Bitcoin Testnet (via the public Blockstream API).
        ///   3. Opens the main window.
        /// </summary>
        [STAThread]
        static void Main()
        {
            ApplicationConfiguration.Initialize();

            // Shared wallet manager – a single instance is passed to all forms.
            var walletManager = new WalletManager();

            // Auto-start testnet connection (fire-and-forget; status shown in Connections panel)
            _ = Task.Run(() => BlockchainApiClient.GetSyncStatusAsync(CoinType.BitcoinTestnet));

            Application.Run(new SupMain(walletManager));
        }
    }
}
