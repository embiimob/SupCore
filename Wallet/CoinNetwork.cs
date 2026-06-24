using System;

namespace SUP.Wallet
{
    /// <summary>
    /// Identifies each supported blockchain network.
    /// </summary>
    public enum CoinNetworkId
    {
        BitcoinTestnet,
        BitcoinMainnet,
        Litecoin,
        Dogecoin,
        Mazacoin
    }

    /// <summary>
    /// Per-coin configuration: daemon executable name, data directory, RPC port, NBitcoin Network, and address version byte.
    /// </summary>
    public class CoinNetworkConfig
    {
        public CoinNetworkId Id { get; }
        public string ShortName { get; }
        public string DisplayName { get; }
        /// <summary>Daemon executable name without path (e.g. bitcoind.exe).</summary>
        public string DaemonExeName { get; }
        /// <summary>Data directory folder name relative to the app base directory.</summary>
        public string DataDirectory { get; }
        /// <summary>Standard RPC port.</summary>
        public int RpcPort { get; }
        /// <summary>Optional second port used by some daemons for P2P; 0 if unused.</summary>
        public int P2PPort { get; }
        /// <summary>Extra command-line flags appended to daemon launch args.</summary>
        public string ExtraArgs { get; }
        /// <summary>NBitcoin network object.</summary>
        public NBitcoin.Network Network { get; }
        /// <summary>Address version byte (for legacy address generation on non-Bitcoin coins).</summary>
        public byte AddressVersionByte { get; }
        /// <summary>True if this is a testnet chain.</summary>
        public bool IsTestNet { get; }

        public CoinNetworkConfig(
            CoinNetworkId id,
            string shortName,
            string displayName,
            string daemonExeName,
            string dataDirectory,
            int rpcPort,
            int p2pPort,
            string extraArgs,
            NBitcoin.Network network,
            byte addressVersionByte,
            bool isTestNet)
        {
            Id = id;
            ShortName = shortName;
            DisplayName = displayName;
            DaemonExeName = daemonExeName;
            DataDirectory = dataDirectory;
            RpcPort = rpcPort;
            P2PPort = p2pPort;
            ExtraArgs = extraArgs;
            Network = network;
            AddressVersionByte = addressVersionByte;
            IsTestNet = isTestNet;
        }

        // ── Static instances ───────────────────────────────────────────────

        public static readonly CoinNetworkConfig BitcoinTestnet = new CoinNetworkConfig(
            id: CoinNetworkId.BitcoinTestnet,
            shortName: "BTCT",
            displayName: "Bitcoin Testnet",
            daemonExeName: "bitcoind.exe",
            dataDirectory: "bitcoin",
            rpcPort: 18332,
            p2pPort: 18333,
            extraArgs: "-testnet",
            network: NBitcoin.Network.TestNet,
            addressVersionByte: 111,
            isTestNet: true);

        public static readonly CoinNetworkConfig BitcoinMainnet = new CoinNetworkConfig(
            id: CoinNetworkId.BitcoinMainnet,
            shortName: "BTC",
            displayName: "Bitcoin Mainnet",
            daemonExeName: "bitcoind.exe",
            dataDirectory: "bitcoin",
            rpcPort: 8332,
            p2pPort: 8333,
            extraArgs: "",
            network: NBitcoin.Network.Main,
            addressVersionByte: 0,
            isTestNet: false);

        public static readonly CoinNetworkConfig Litecoin = new CoinNetworkConfig(
            id: CoinNetworkId.Litecoin,
            shortName: "LTC",
            displayName: "Litecoin",
            daemonExeName: "litecoind.exe",
            dataDirectory: "litecoin",
            rpcPort: 9332,
            p2pPort: 9333,
            extraArgs: "",
            network: NBitcoin.Network.Main,
            addressVersionByte: 48,
            isTestNet: false);

        public static readonly CoinNetworkConfig Dogecoin = new CoinNetworkConfig(
            id: CoinNetworkId.Dogecoin,
            shortName: "DOGE",
            displayName: "Dogecoin",
            daemonExeName: "dogecoind.exe",
            dataDirectory: "dogecoin",
            rpcPort: 22555,
            p2pPort: 22556,
            extraArgs: "",
            network: NBitcoin.Network.Main,
            addressVersionByte: 30,
            isTestNet: false);

        public static readonly CoinNetworkConfig Mazacoin = new CoinNetworkConfig(
            id: CoinNetworkId.Mazacoin,
            shortName: "MZC",
            displayName: "Mazacoin",
            daemonExeName: "mazad.exe",
            dataDirectory: "mazacoin",
            rpcPort: 12832,
            p2pPort: 12833,
            extraArgs: "",
            network: NBitcoin.Network.Main,
            addressVersionByte: 50,
            isTestNet: false);

        public static readonly CoinNetworkConfig[] All = new[]
        {
            BitcoinTestnet,
            BitcoinMainnet,
            Litecoin,
            Dogecoin,
            Mazacoin
        };

        public static CoinNetworkConfig ById(CoinNetworkId id)
        {
            foreach (var cfg in All)
                if (cfg.Id == id) return cfg;
            throw new ArgumentException("Unknown network: " + id);
        }

        /// <summary>Build the RPC URL for this coin.</summary>
        public string RpcUrl => $"http://127.0.0.1:{RpcPort}";
    }
}
