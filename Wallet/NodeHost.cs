using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NBitcoin;
using NBitcoin.Protocol;
using NBitcoin.Protocol.Behaviors;

namespace SUP.Wallet
{
    /// <summary>
    /// Fully embedded C# Bitcoin P2P node — no external daemon process required.
    /// Connects directly to the Bitcoin P2P network using NBitcoin.Protocol,
    /// downloads block headers and full blocks, and maintains an address-indexed
    /// transaction store on disk under the configured data directory.
    ///
    /// Files created in &lt;dataDirectory&gt;/:
    ///   node.info       — startup information (visible immediately on Start)
    ///   peers.dat       — serialised AddressManager (known peers)
    ///   headers.dat     — serialised SlimChain (block header hashes + heights)
    ///   sync.log        — human-readable sync progress log
    ///   index/          — per-address transaction index (.json per address)
    ///
    /// Bitcoin mainnet and testnet are supported.  Litecoin, Dogecoin and Mazacoin
    /// require their altcoin network definitions to be registered with NBitcoin
    /// before P2P sync can be activated; wallet key operations still work for them.
    /// </summary>
    public class NodeHost : IDisposable
    {
        private const int MaxPeerConnections = 8;

        private readonly object _lock = new object();
        private bool _disposed;

        // ── Status (same public interface as the previous process-launcher) ──
        public CoinNetworkConfig Config { get; }
        public int SyncedBlocks { get; private set; }
        public int ChainHeaders { get; private set; }
        public long ChainTxCount { get; private set; }
        public double SyncPercent { get; private set; }
        public string StatusText { get; private set; } = "Stopped";
        public bool IsRunning { get; private set; }

        /// <summary>Fired whenever any status property changes.</summary>
        public event EventHandler StatusChanged;

        // ── P2P components ──────────────────────────────────────────────────
        private NodesGroup _group;
        private SlimChain _slimChain;
        private AddressManager _addressManager;
        private BroadcastHub _broadcastHub;
        private CancellationTokenSource _cts;
        private Task _syncTask;

        // ── Address index ───────────────────────────────────────────────────
        // Always present (even when node is stopped) so wallets can read
        // previously indexed transactions without restarting the sync.
        private readonly AddressIndex _addressIndex;

        /// <summary>Returns the address-indexed transaction store for this network.</summary>
        public AddressIndex GetAddressIndex() => _addressIndex;

        // ── Data directory ──────────────────────────────────────────────────
        private readonly string _dataDir;
        private string PeersFile   => Path.Combine(_dataDir, "peers.dat");
        private string HeadersFile => Path.Combine(_dataDir, "headers.dat");

        public NodeHost(CoinNetworkConfig config)
        {
            Config = config;
            _dataDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, config.DataDirectory);
            string indexDir = Path.Combine(_dataDir, "index");
            _addressIndex = new AddressIndex(indexDir, config);
        }

        // ── Start / Stop ────────────────────────────────────────────────────

        /// <summary>Start the embedded P2P node.  Has no effect if already running.</summary>
        public void Start(bool reindex = false, bool rescan = false)
        {
            lock (_lock)
            {
                if (IsRunning) return;

                // Bitcoin mainnet and testnet are natively supported by NBitcoin.Protocol.
                // Altcoins need their network registered first; until then we show a clear
                // status message but do not prevent wallet key operations.
                if (Config.Id != CoinNetworkId.BitcoinMainnet &&
                    Config.Id != CoinNetworkId.BitcoinTestnet)
                {
                    StatusText = $"Embedded P2P sync not yet supported for {Config.DisplayName}. " +
                                  "Wallet key operations are available.";
                    StatusChanged?.Invoke(this, EventArgs.Empty);
                    return;
                }

                try
                {
                    // ── Prepare directories ─────────────────────────────────
                    Directory.CreateDirectory(_dataDir);
                    string indexDir = Path.Combine(_dataDir, "index");
                    Directory.CreateDirectory(indexDir);

                    // Write a visible marker immediately so the user can see the node started.
                    File.WriteAllText(
                        Path.Combine(_dataDir, "node.info"),
                        $"SUP EmbeddedNode\r\nNetwork : {Config.DisplayName}\r\nStarted : {DateTime.UtcNow:u}\r\n");

                    if (reindex)
                    {
                        if (File.Exists(HeadersFile)) File.Delete(HeadersFile);
                        foreach (string f in Directory.GetFiles(indexDir))
                            try { File.Delete(f); } catch { }
                    }

                    _cts = new CancellationTokenSource();

                    // ── SlimChain (header tracking) ─────────────────────────
                    _slimChain = new SlimChain(Config.Network.GenesisHash);
                    if (!reindex && File.Exists(HeadersFile))
                    {
                        try
                        {
                            using (var fs = File.OpenRead(HeadersFile))
                                _slimChain.Load(fs);
                            WriteLog($"Resumed from block header {_slimChain.Height}");
                        }
                        catch (Exception ex)
                        {
                            WriteLog("Could not load headers.dat (" + ex.Message + ") — starting from genesis");
                        }
                    }

                    // ── AddressManager (peer discovery) ─────────────────────
                    _addressManager = File.Exists(PeersFile)
                        ? AddressManager.LoadPeerFile(PeersFile, Config.Network)
                        : new AddressManager();

                    // ── Address index ───────────────────────────────────────
                    if (!reindex && !rescan)
                        _addressIndex.LoadFromDisk();
                    else if (rescan)
                        _addressIndex.Reset();

                    // ── NodesGroup + behaviors ──────────────────────────────
                    var parameters = new NodeConnectionParameters();
                    parameters.UserAgent = "/SUP:1.0/";

                    parameters.TemplateBehaviors.Add(
                        new AddressManagerBehavior(_addressManager)
                        {
                            Mode = AddressManagerBehaviorMode.AdvertizeDiscover
                        });
                    parameters.TemplateBehaviors.Add(new SlimChainBehavior(_slimChain));

                    _broadcastHub = new BroadcastHub();
                    parameters.TemplateBehaviors.Add(_broadcastHub.CreateBehavior());

                    _group = new NodesGroup(
                        Config.Network,
                        parameters,
                        new NodeRequirement { RequiredServices = NodeServices.Network })
                    {
                        MaximumNodeConnection = MaxPeerConnections
                    };
                    _group.Connect();

                    IsRunning = true;
                    StatusText = "Connecting…";
                    StatusChanged?.Invoke(this, EventArgs.Empty);

                    // ── Background sync task ────────────────────────────────
                    var token = _cts.Token;
                    _syncTask = Task.Run(() => SyncLoop(token), token);
                }
                catch (Exception ex)
                {
                    StatusText = "Start failed: " + ex.Message;
                    IsRunning = false;
                    try { _cts?.Cancel(); } catch { }
                    StatusChanged?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        /// <summary>Stop the embedded node and persist state to disk.</summary>
        public void Stop()
        {
            lock (_lock)
            {
                if (!IsRunning) return;
                IsRunning = false;

                try { _cts?.Cancel(); } catch { }
                try { _syncTask?.Wait(8000); } catch { }
                try { _group?.Disconnect(); } catch { }

                PersistState();

                _group        = null;
                _slimChain    = null;
                _addressManager = null;
                _broadcastHub = null;
                _cts          = null;

                SyncedBlocks = 0;
                ChainHeaders = 0;
                ChainTxCount = 0;
                SyncPercent  = 0;
                StatusText   = "Stopped";
                StatusChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        /// <summary>
        /// Fire the StatusChanged event so the UI re-reads the current status properties.
        /// The background sync loop updates the properties continuously; this method
        /// just triggers the notification.
        /// </summary>
        public void RefreshStatus()
        {
            if (IsRunning)
                StatusChanged?.Invoke(this, EventArgs.Empty);
            else if (StatusText != "Stopped")
            {
                StatusText = "Stopped";
                StatusChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        // ── Broadcast ────────────────────────────────────────────────────────

        /// <summary>Broadcast a signed transaction to the connected peers.</summary>
        public async Task<bool> BroadcastAsync(Transaction tx)
        {
            if (_broadcastHub == null || !IsRunning) return false;
            try
            {
                await _broadcastHub.BroadcastTransactionAsync(tx).ConfigureAwait(false);
                return true;
            }
            catch { return false; }
        }

        // ── Sync loop ─────────────────────────────────────────────────────────

        private void SyncLoop(CancellationToken token)
        {
            WriteLog("Sync loop started");
            while (!token.IsCancellationRequested)
            {
                try
                {
                    // ── Step 1: wait for at least one peer ──────────────────
                    Node peer = WaitForPeer(30, token);
                    if (peer == null || token.IsCancellationRequested) continue;

                    // ── Step 2: synchronise block headers ───────────────────
                    int peerCount = _group?.ConnectedNodes?.Count ?? 0;
                    UpdateStatus(
                        $"Syncing headers… ({peerCount} peer{(peerCount == 1 ? "" : "s")})",
                        SyncedBlocks, _slimChain.Height);
                    WriteLog($"Starting header sync at height {_slimChain.Height} with {peerCount} peer(s)");

                    try
                    {
                        // Guard against a stalled peer blocking indefinitely.
                        using (var headerCts = CancellationTokenSource.CreateLinkedTokenSource(token))
                        {
                            headerCts.CancelAfter(TimeSpan.FromMinutes(5));
                            peer.SynchronizeSlimChain(_slimChain, null, headerCts.Token);
                        }
                    }
                    catch (OperationCanceledException) when (token.IsCancellationRequested) { break; }
                    catch (OperationCanceledException)
                    {
                        // Peer disconnected or 5-minute timeout — try again with a fresh peer.
                        WriteLog("Header sync interrupted — retrying with new peer");
                        Thread.Sleep(5000);
                        continue;
                    }
                    catch (Exception ex) when (!token.IsCancellationRequested)
                    {
                        WriteLog("Header sync error: " + ex.Message);
                        Thread.Sleep(5000);
                        continue;
                    }

                    int headerHeight = _slimChain.Height;
                    ChainHeaders = headerHeight;
                    PersistHeaders();
                    WriteLog($"Headers synced to {headerHeight}");

                    // ── Step 3: download full blocks and index them ──────────
                    int startHeight = _addressIndex.ScannedHeight;
                    if (startHeight < headerHeight)
                    {
                        WriteLog($"Downloading blocks {startHeight + 1}–{headerHeight}");
                        peer = WaitForPeer(10, token);
                        if (peer != null)
                            DownloadBlocks(peer, startHeight, headerHeight, token);
                    }

                    if (!token.IsCancellationRequested)
                    {
                        int h = _addressIndex.ScannedHeight;
                        UpdateStatus($"Ready — {h} block{(h == 1 ? "" : "s")}", h, headerHeight);
                        // Wait one minute then check for new blocks
                        token.WaitHandle.WaitOne(TimeSpan.FromMinutes(1));
                    }
                }
                catch (OperationCanceledException) { break; }
                catch (Exception ex)
                {
                    WriteLog("SyncLoop error: " + ex.Message);
                    if (!token.IsCancellationRequested)
                    {
                        UpdateStatus("Reconnecting…", SyncedBlocks, ChainHeaders);
                        Thread.Sleep(15_000);
                    }
                }
            }

            PersistState();
            WriteLog("Sync loop stopped");
        }

        private Node WaitForPeer(int maxWaitSeconds, CancellationToken token)
        {
            for (int i = 0; i < maxWaitSeconds * 2 && !token.IsCancellationRequested; i++)
            {
                Node node = _group?.ConnectedNodes?.FirstOrDefault();
                if (node != null) return node;
                UpdateStatus(
                    $"Connecting… ({_group?.ConnectedNodes?.Count ?? 0} peers)",
                    SyncedBlocks, ChainHeaders);
                Thread.Sleep(500);
            }
            return null;
        }

        private void DownloadBlocks(
            Node peer, int fromHeight, int toHeight, CancellationToken token)
        {
            const int BatchSize = 500;
            int h = fromHeight + 1;

            while (h <= toHeight && !token.IsCancellationRequested)
            {
                var batchHashes  = new List<uint256>(BatchSize);
                var batchHeights = new List<int>(BatchSize);

                for (int i = 0; i < BatchSize && h <= toHeight; i++, h++)
                {
                    SlimChainedBlock sb = _slimChain.GetBlock(h);
                    if (sb == null) continue;
                    batchHashes.Add(sb.Hash);
                    batchHeights.Add(sb.Height);
                }

                if (batchHashes.Count == 0) break;

                try
                {
                    int idx = 0;
                    foreach (Block block in peer.GetBlocks(batchHashes, token))
                    {
                        if (token.IsCancellationRequested) break;

                        int blockHeight = idx < batchHeights.Count
                            ? batchHeights[idx]
                            : (fromHeight + 1 + idx);

                        _addressIndex.IndexBlock(block, blockHeight);

                        SyncedBlocks  = blockHeight;
                        ChainTxCount += block.Transactions?.Count ?? 0;
                        idx++;

                        SyncPercent = toHeight > 0
                            ? Math.Min(100.0, blockHeight * 100.0 / toHeight)
                            : 0;

                        int peers = _group?.ConnectedNodes?.Count ?? 0;
                        string syncText = SyncPercent >= 99.99
                            ? $"Ready — {blockHeight} blocks"
                            : $"Blocks {blockHeight}/{toHeight} ({SyncPercent:F1}%) — " +
                              $"{peers} peer{(peers == 1 ? "" : "s")}";
                        UpdateStatus(syncText, blockHeight, toHeight);

                        if (idx % 100 == 0)
                        {
                            PersistHeaders();
                            _addressIndex.FlushToDisk();
                            WriteLog($"Progress: {blockHeight}/{toHeight} ({SyncPercent:F1}%)");
                        }
                    }
                }
                catch (OperationCanceledException) { break; }
                catch (Exception ex) when (!token.IsCancellationRequested)
                {
                    WriteLog($"Block batch error near height {h}: " + ex.Message);
                    Thread.Sleep(5000);
                    peer = WaitForPeer(10, token);
                    if (peer == null) break;
                }
            }

            if (!token.IsCancellationRequested)
            {
                _addressIndex.FlushToDisk();
                PersistHeaders();
            }
        }

        // ── Persistence ───────────────────────────────────────────────────────

        private void PersistHeaders()
        {
            if (_slimChain == null) return;
            try
            {
                string tmp = HeadersFile + ".tmp";
                using (var fs = new FileStream(tmp, FileMode.Create, FileAccess.Write))
                    _slimChain.Save(fs);
                if (File.Exists(HeadersFile)) File.Delete(HeadersFile);
                File.Move(tmp, HeadersFile);
            }
            catch { }
        }

        private void PersistState()
        {
            PersistHeaders();
            try { _addressManager?.SavePeerFile(PeersFile, Config.Network); } catch { }
            try { _addressIndex.FlushToDisk(); } catch { }
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private void UpdateStatus(string text, int synced, int headers)
        {
            bool changed = StatusText != text
                        || SyncedBlocks != synced
                        || ChainHeaders != headers;
            StatusText   = text;
            SyncedBlocks = synced;
            if (headers > ChainHeaders) ChainHeaders = headers;
            if (changed) StatusChanged?.Invoke(this, EventArgs.Empty);
        }

        private void WriteLog(string message)
        {
            try
            {
                File.AppendAllText(
                    Path.Combine(_dataDir, "sync.log"),
                    $"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}] {message}\r\n");
            }
            catch { }
        }

        // ── Dispose ───────────────────────────────────────────────────────────

        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;
                try { Stop(); } catch { }
            }
        }
    }
}
