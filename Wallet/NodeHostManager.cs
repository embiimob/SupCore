using System;
using System.Collections.Generic;
using System.Linq;

namespace SUP.Wallet
{
    /// <summary>
    /// Static singleton that owns one <see cref="NodeHost"/> per network.
    /// Forms obtain node references and wallet managers from here.
    /// </summary>
    public static class NodeHostManager
    {
        private static readonly Dictionary<CoinNetworkId, NodeHost> _hosts =
            new Dictionary<CoinNetworkId, NodeHost>();

        private static readonly Dictionary<CoinNetworkId, WalletManager> _wallets =
            new Dictionary<CoinNetworkId, WalletManager>();

        static NodeHostManager()
        {
            foreach (var cfg in CoinNetworkConfig.All)
            {
                _hosts[cfg.Id] = new NodeHost(cfg);
                _wallets[cfg.Id] = new WalletManager(cfg);
            }
        }

        // ── Node access ────────────────────────────────────────────────────

        public static NodeHost GetNode(CoinNetworkId id) => _hosts[id];

        public static IEnumerable<NodeHost> AllNodes => _hosts.Values;

        /// <summary>Refresh sync status on all running nodes.</summary>
        public static void RefreshAll()
        {
            foreach (var h in _hosts.Values)
                h.RefreshStatus();
        }

        /// <summary>Stop all running nodes gracefully.</summary>
        public static void StopAll()
        {
            foreach (var h in _hosts.Values)
                try { h.Stop(); } catch { }
        }

        // ── Wallet access ──────────────────────────────────────────────────

        public static WalletManager GetWallet(CoinNetworkId id) => _wallets[id];

        // ── Internal RPC server ────────────────────────────────────────────

        private static InternalRpcServer _rpcServer;

        /// <summary>Start the optional internal JSON-RPC server for all networks.</summary>
        public static void StartRpcServer(string rpcUser = "good-user",
            string rpcPassword = "better-password")
        {
            StopRpcServer();
            _rpcServer = new InternalRpcServer(rpcUser, rpcPassword);
            _rpcServer.Start();
        }

        public static void StopRpcServer()
        {
            try { _rpcServer?.Stop(); } catch { }
            _rpcServer = null;
        }

        public static bool IsRpcServerRunning => _rpcServer != null && _rpcServer.IsRunning;
    }
}
