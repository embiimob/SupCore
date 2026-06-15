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

            // Auto-start testnet connection (fire-and-forget with swallowed exceptions since
            // the Connections panel will display the failure state when it opens).
            _ = Task.Run(async () =>
            {
                try { await BlockchainApiClient.GetSyncStatusAsync(CoinType.BitcoinTestnet); }
                catch { /* Status is shown in Connections panel; ignore startup probe errors. */ }
            });

            Application.Run(new SupMain(walletManager));
        }
    }
}
