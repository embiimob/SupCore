using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using SUP.RPCClient;

namespace SUP.Wallet
{
    /// <summary>
    /// Manages the lifecycle of a single blockchain daemon process
    /// (bitcoind / litecoind / dogecoind / mazad — no Qt required).
    ///
    /// The daemon is launched with -txindex=1 -addrindex=1 -server so that
    /// searchrawtransactions is available to the rest of the application.
    /// </summary>
    public class NodeHost : IDisposable
    {
        private const string RpcUser = "good-user";
        private const string RpcPassword = "better-password";

        private Process _process;
        private readonly object _lock = new object();
        private bool _disposed;

        public CoinNetworkConfig Config { get; }

        // ── Status state ───────────────────────────────────────────────────
        public int SyncedBlocks { get; private set; }
        public int ChainHeaders { get; private set; }
        public long ChainTxCount { get; private set; }
        public double SyncPercent { get; private set; }
        public string StatusText { get; private set; } = "Stopped";
        public bool IsRunning => _process != null && !_process.HasExited;

        // Fired whenever status properties change
        public event EventHandler StatusChanged;

        public NodeHost(CoinNetworkConfig config)
        {
            Config = config;
        }

        // ── Launch / Stop ──────────────────────────────────────────────────

        /// <summary>Start the daemon.  Has no effect if already running.</summary>
        public void Start(bool reindex = false, bool rescan = false)
        {
            lock (_lock)
            {
                if (IsRunning) return;

                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                string dataDir = Path.Combine(baseDir, Config.DataDirectory);
                Directory.CreateDirectory(dataDir);
                string daemonPath = ResolveExecutablePath(baseDir);

                if (string.IsNullOrEmpty(daemonPath))
                {
                    StatusText = BuildExecutableNotFoundMessage(baseDir);
                    StatusChanged?.Invoke(this, EventArgs.Empty);
                    return;
                }

                string args = BuildArgs(dataDir, reindex, rescan);

                var psi = new ProcessStartInfo
                {
                    FileName = daemonPath,
                    Arguments = args,
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    WindowStyle = ProcessWindowStyle.Hidden,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };

                _process = new Process { StartInfo = psi, EnableRaisingEvents = true };
                _process.Exited += (s, e) =>
                {
                    StatusText = "Stopped";
                    SyncedBlocks = 0;
                    ChainHeaders = 0;
                    ChainTxCount = 0;
                    SyncPercent = 0;
                    StatusChanged?.Invoke(this, EventArgs.Empty);
                };
                _process.Start();
                _process.BeginOutputReadLine();
                _process.BeginErrorReadLine();

                StatusText = "Starting…";
                StatusChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        /// <summary>Send RPC stop command; if that fails, kill the process.</summary>
        public void Stop()
        {
            lock (_lock)
            {
                if (!IsRunning) return;
                try
                {
                    var rpc = new CoinRPC(new Uri(Config.RpcUrl),
                        new NetworkCredential(RpcUser, RpcPassword));
                    rpc.HttpCall(Newtonsoft.Json.JsonConvert.SerializeObject(
                        new { method = "stop", @params = new object[0], id = 1 }));
                }
                catch { }

                try { _process?.WaitForExit(5000); } catch { }
                try { if (_process != null && !_process.HasExited) _process.Kill(); } catch { }
                _process = null;
                SyncedBlocks = 0;
                ChainHeaders = 0;
                ChainTxCount = 0;
                SyncPercent = 0;
                StatusText = "Stopped";
                StatusChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        /// <summary>
        /// Poll the daemon via RPC and refresh status properties.
        /// Called periodically from the UI timer.
        /// </summary>
        public void RefreshStatus()
        {
            if (!IsRunning)
            {
                if (StatusText != "Stopped") { StatusText = "Stopped"; StatusChanged?.Invoke(this, EventArgs.Empty); }
                return;
            }

            Task.Run(() =>
            {
                try
                {
                    var rpc = new CoinRPC(new Uri(Config.RpcUrl),
                        new NetworkCredential(RpcUser, RpcPassword));
                    var info = rpc.GetBlockchainInfo();
                    SyncedBlocks = info.blocks;
                    ChainHeaders = info.headers;
                    try
                    {
                        ChainTxCount = Math.Max(0L, rpc.GetChainTxStats().txcount);
                    }
                    catch
                    {
                        ChainTxCount = 0;
                    }
                    SyncPercent = ChainHeaders > 0
                        ? Math.Min(100.0, info.verificationprogress * 100.0)
                        : 0;
                    StatusText = SyncPercent >= 99.99
                        ? "Ready"
                        : info.initialblockdownload
                            ? $"Syncing… {SyncPercent:F1}%"
                            : $"Syncing {SyncedBlocks}/{ChainHeaders}";
                    StatusChanged?.Invoke(this, EventArgs.Empty);
                }
                catch
                {
                    if (IsRunning && StatusText != "Waiting…")
                    {
                        StatusText = "Waiting…";
                        StatusChanged?.Invoke(this, EventArgs.Empty);
                    }
                }
            });
        }

        // ── Helpers ────────────────────────────────────────────────────────

        private string BuildArgs(string dataDir, bool reindex, bool rescan)
        {
            var sb = new System.Text.StringBuilder();
            sb.Append($"-txindex=1 -addrindex=1 -datadir=\"{dataDir}\"");
            sb.Append($" -server -rpcuser={RpcUser} -rpcpassword={RpcPassword}");
            sb.Append($" -rpcport={Config.RpcPort}");
            if (!string.IsNullOrEmpty(Config.ExtraArgs))
                sb.Append(" " + Config.ExtraArgs);
            if (reindex) sb.Append(" -reindex");
            if (rescan) sb.Append(" -rescan");
            return sb.ToString();
        }

        private string ResolveExecutablePath(string baseDir)
        {
            foreach (string fileName in GetExecutableCandidates())
            {
                string candidatePath = Path.Combine(baseDir, fileName);
                if (File.Exists(candidatePath))
                    return candidatePath;
            }

            return null;
        }

        private string BuildExecutableNotFoundMessage(string baseDir)
        {
            return "Node executable not found: "
                + string.Join(", ", GetExecutableCandidates())
                + " in "
                + baseDir;
        }

        private string[] GetExecutableCandidates()
        {
            switch (Config.Id)
            {
                case CoinNetworkId.BitcoinTestnet:
                case CoinNetworkId.BitcoinMainnet:
                    return new[] { "bitcoind.exe", "bitcoin-qt.exe" };
                case CoinNetworkId.Litecoin:
                    return new[] { "litecoind.exe", "litecoin-qt.exe" };
                case CoinNetworkId.Dogecoin:
                    return new[] { "dogecoind.exe", "dogecoin-qt.exe" };
                case CoinNetworkId.Mazacoin:
                    return new[] { "mazad.exe", "maza-qt.exe" };
                default:
                    return new[] { Config.DaemonExeName };
            }
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;
                try { Stop(); } catch { }
                _process?.Dispose();
            }
        }
    }
}
