using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NBitcoin;
using Newtonsoft.Json;
using SUP.RPCClient;

namespace SUP.Wallet
{
    /// <summary>
    /// Persistent, address-indexed transaction store built from downloaded blocks.
    ///
    /// Usage pattern:
    ///   1. Call <see cref="WatchAddress"/> for every wallet address that should be tracked.
    ///   2. Call <see cref="IndexBlock"/> for every downloaded block (in order).
    ///   3. Call <see cref="FlushToDisk"/> periodically and on shutdown.
    ///   4. Call <see cref="GetTransactions"/> to retrieve indexed transactions.
    ///
    /// Files written under the index directory:
    ///   &lt;address&gt;.json       — serialised transaction list per address
    ///   scanned_height.txt  — highest block height fully processed
    /// </summary>
    public class AddressIndex
    {
        private const string HeightFile = "scanned_height.txt";

        private readonly string _indexDir;
        private readonly CoinNetworkConfig _config;

        private readonly object _cacheLock = new object();

        private readonly HashSet<string> _watched =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        private readonly Dictionary<string, List<GetRawDataTransactionResponse>> _cache =
            new Dictionary<string, List<GetRawDataTransactionResponse>>(StringComparer.OrdinalIgnoreCase);

        private bool _dirty;

        /// <summary>Highest block height that has been fully indexed.</summary>
        public int ScannedHeight { get; private set; }

        public AddressIndex(string indexDir, CoinNetworkConfig config)
        {
            _indexDir = indexDir;
            _config   = config;
        }

        // ── Watch list ────────────────────────────────────────────────────────

        /// <summary>Register an address so that its transactions are indexed.</summary>
        public void WatchAddress(string address)
        {
            if (string.IsNullOrWhiteSpace(address)) return;
            lock (_cacheLock)
            {
                if (_watched.Add(address) && !_cache.ContainsKey(address))
                    _cache[address] = new List<GetRawDataTransactionResponse>();
            }
        }

        /// <summary>Stop tracking an address (existing indexed data is kept).</summary>
        public void UnwatchAddress(string address)
        {
            if (string.IsNullOrWhiteSpace(address)) return;
            lock (_cacheLock)
                _watched.Remove(address);
        }

        // ── Block indexing ────────────────────────────────────────────────────

        /// <summary>
        /// Scan a downloaded block for transactions touching any watched address
        /// and update the in-memory cache.  Call in block-height order.
        /// </summary>
        public void IndexBlock(Block block, int height)
        {
            if (block?.Transactions == null) return;

            string blockHash = block.GetHash().ToString();
            long blockTime   = block.Header?.BlockTime.ToUnixTimeSeconds() ?? 0;

            lock (_cacheLock)
            {
                foreach (Transaction tx in block.Transactions)
                {
                    GetRawDataTransactionResponse mapped =
                        MapTransaction(tx, blockHash, height, blockTime);
                    if (mapped == null) continue;

                    bool relevant = false;

                    // ── Check outputs ─────────────────────────────────────
                    if (mapped.vout != null)
                    {
                        foreach (var output in mapped.vout)
                        {
                            if (output.scriptPubKey?.addresses == null) continue;
                            foreach (string addr in output.scriptPubKey.addresses)
                            {
                                if (!_watched.Contains(addr)) continue;
                                AppendTx(addr, mapped);
                                relevant = true;
                            }
                        }
                    }

                    // ── Check inputs (spending side) ──────────────────────
                    // If an input spends an output we have already indexed for
                    // a watched address, record the spending TX for that address
                    // so that balance calculation works correctly.
                    if (!relevant && mapped.vin != null)
                    {
                        foreach (var input in mapped.vin)
                        {
                            if (string.IsNullOrEmpty(input.txid)) continue;
                            foreach (string addr in _watched)
                            {
                                if (!_cache.TryGetValue(addr, out var txList)) continue;
                                if (txList.Any(t => t.txid == input.txid))
                                {
                                    AppendTx(addr, mapped);
                                    break;
                                }
                            }
                        }
                    }
                }

                ScannedHeight = height;
                _dirty        = true;
            }
        }

        private void AppendTx(string address, GetRawDataTransactionResponse tx)
        {
            if (!_cache.TryGetValue(address, out var list))
            {
                list = new List<GetRawDataTransactionResponse>();
                _cache[address] = list;
            }
            // Deduplicate by txid
            if (!list.Any(t => t.txid == tx.txid))
                list.Add(tx);
        }

        // ── Query ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Return indexed transactions for <paramref name="address"/>.
        /// Returns an empty list if the address has not been indexed.
        /// </summary>
        public List<GetRawDataTransactionResponse> GetTransactions(
            string address, int skip = 0, int count = 100)
        {
            lock (_cacheLock)
            {
                if (!_cache.TryGetValue(address, out var all))
                    return new List<GetRawDataTransactionResponse>();
                return all.Skip(skip).Take(count).ToList();
            }
        }

        // ── Reset ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Clear all indexed transactions and reset the scanned height to zero
        /// (use before a rescan).
        /// </summary>
        public void Reset()
        {
            lock (_cacheLock)
            {
                foreach (string key in _cache.Keys.ToList())
                    _cache[key] = new List<GetRawDataTransactionResponse>();
                ScannedHeight = 0;
                _dirty        = true;
            }
        }

        // ── Persistence ───────────────────────────────────────────────────────

        /// <summary>Load index data from disk for all currently watched addresses.</summary>
        public void LoadFromDisk()
        {
            lock (_cacheLock)
            {
                string heightPath = Path.Combine(_indexDir, HeightFile);
                if (File.Exists(heightPath))
                {
                    if (int.TryParse(File.ReadAllText(heightPath).Trim(), out int h))
                        ScannedHeight = h;
                }

                foreach (string addr in _watched.ToList())
                    LoadAddressFromDisk(addr);
            }
        }

        private void LoadAddressFromDisk(string address)
        {
            string path = AddressFilePath(address);
            if (!File.Exists(path)) return;
            try
            {
                var txs = JsonConvert.DeserializeObject<List<GetRawDataTransactionResponse>>(
                    File.ReadAllText(path));
                if (txs != null)
                    _cache[address] = txs;
            }
            catch { }
        }

        /// <summary>Write any pending changes to disk.</summary>
        public void FlushToDisk()
        {
            lock (_cacheLock)
            {
                if (!_dirty) return;

                try { Directory.CreateDirectory(_indexDir); } catch { }

                // Save scanned height
                try
                {
                    File.WriteAllText(
                        Path.Combine(_indexDir, HeightFile),
                        ScannedHeight.ToString());
                }
                catch { }

                // Save each address cache file
                foreach (var kvp in _cache)
                {
                    string path = AddressFilePath(kvp.Key);
                    string tmp  = path + ".tmp";
                    try
                    {
                        File.WriteAllText(tmp,
                            JsonConvert.SerializeObject(kvp.Value, Formatting.None));
                        if (File.Exists(path)) File.Delete(path);
                        File.Move(tmp, path);
                    }
                    catch
                    {
                        try { if (File.Exists(tmp)) File.Delete(tmp); } catch { }
                    }
                }

                _dirty = false;
            }
        }

        private string AddressFilePath(string address)
        {
            // Sanitise the address so it is safe to use as a file name.
            string safe = address
                .Replace('/', '_')
                .Replace('\\', '_')
                .Replace(':', '_');
            return Path.Combine(_indexDir, safe + ".json");
        }

        // ── Mapping: NBitcoin → GetRawDataTransactionResponse ─────────────────

        private GetRawDataTransactionResponse MapTransaction(
            Transaction tx, string blockHash, int blockHeight, long blockTime)
        {
            if (tx == null) return null;
            try
            {
                return new GetRawDataTransactionResponse
                {
                    txid      = tx.GetHash().ToString(),
                    hex       = tx.ToHex(),
                    size      = tx.GetVirtualSize(),
                    version   = (int)tx.Version,
                    locktime  = (int)tx.LockTime.Value,
                    blockhash = blockHash,
                    blocktime = blockTime,
                    vin = tx.Inputs?.Select(inp => new GetRawDataTransactionResponse.Input
                    {
                        txid = inp.PrevOut.Hash.ToString(),
                        vout = (int)inp.PrevOut.N,
                        scriptSig = new GetRawDataTransactionResponse.Input.ScriptSig
                        {
                            asm = inp.ScriptSig?.ToString() ?? string.Empty,
                            hex = inp.ScriptSig?.ToHex()    ?? string.Empty
                        },
                        sequence = (long)inp.Sequence.Value
                    }).ToArray(),
                    vout = tx.Outputs?.Select((output, n) =>
                    {
                        string[] addresses = ExtractAddresses(output.ScriptPubKey);
                        return new GetRawDataTransactionResponse.Output
                        {
                            value = output.Value?.ToDecimal(MoneyUnit.BTC) ?? 0m,
                            n     = n,
                            scriptPubKey = new GetRawDataTransactionResponse.Output.ScriptPubKey
                            {
                                asm       = output.ScriptPubKey?.ToString() ?? string.Empty,
                                hex       = output.ScriptPubKey?.ToHex()    ?? string.Empty,
                                addresses = addresses
                            }
                        };
                    }).ToArray()
                };
            }
            catch { return null; }
        }

        private string[] ExtractAddresses(Script script)
        {
            if (script == null) return new string[0];
            try
            {
                BitcoinAddress addr = script.GetDestinationAddress(_config.Network);
                return addr != null ? new[] { addr.ToString() } : new string[0];
            }
            catch { return new string[0]; }
        }
    }
}
