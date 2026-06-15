using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace SupCore.Wallet
{
    /// <summary>
    /// Lightweight UTXO and blockchain-info client backed by public REST APIs:
    /// <list type="bullet">
    ///   <item>BTC / BTCT  – blockstream.info</item>
    ///   <item>LTC / DOGE  – blockcypher.com</item>
    ///   <item>MZC         – basic fallback (balance returns 0 / always offline)</item>
    /// </list>
    /// All network I/O is async to keep the UI responsive.
    /// </summary>
    public static class BlockchainApiClient
    {
        private static readonly HttpClient _http = CreateHttpClient();
        private static readonly Dictionary<CoinType, DateTime> _daemonStartAttemptsUtc = new();

        private static HttpClient CreateHttpClient()
        {
            var client = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
            client.DefaultRequestHeaders.UserAgent.ParseAdd("SupCore/1.0");
            return client;
        }

        private sealed record NodeRpcConfig(
            int Port,
            string[] DaemonNames,
            string[] CookieSuffixes,
            string[] DaemonArgs,
            string[] DataDirectories);

        // ── API base URLs ──────────────────────────────────────────────────────────
        private static string BaseUrl(CoinType coin) => coin switch
        {
            CoinType.Bitcoin => "https://blockstream.info/api",
            CoinType.BitcoinTestnet => "https://blockstream.info/testnet/api",
            CoinType.Litecoin => "https://api.blockcypher.com/v1/ltc/main",
            CoinType.Dogecoin => "https://api.blockcypher.com/v1/doge/main",
            _ => string.Empty
        };

        private static NodeRpcConfig? GetNodeRpcConfig(CoinType coin)
        {
            string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

            return coin switch
            {
                CoinType.Bitcoin => new NodeRpcConfig(
                    8332,
                    new[] { "bitcoind", "bitcoind.exe" },
                    new[] { ".cookie" },
                    new[] { "-server", "-rpcbind=127.0.0.1", "-rpcallowip=127.0.0.1" },
                    new[]
                    {
                        Path.Combine(home, ".bitcoin"),
                        Path.Combine(appData, "Bitcoin")
                    }),
                CoinType.BitcoinTestnet => new NodeRpcConfig(
                    18332,
                    new[] { "bitcoind", "bitcoind.exe" },
                    new[] { Path.Combine("testnet4", ".cookie"), Path.Combine("testnet3", ".cookie"), ".cookie" },
                    new[] { "-server", "-testnet", "-rpcbind=127.0.0.1", "-rpcallowip=127.0.0.1" },
                    new[]
                    {
                        Path.Combine(home, ".bitcoin"),
                        Path.Combine(appData, "Bitcoin")
                    }),
                CoinType.Litecoin => new NodeRpcConfig(
                    9332,
                    new[] { "litecoind", "litecoind.exe" },
                    new[] { ".cookie" },
                    new[] { "-server", "-rpcbind=127.0.0.1", "-rpcallowip=127.0.0.1" },
                    new[]
                    {
                        Path.Combine(home, ".litecoin"),
                        Path.Combine(appData, "Litecoin")
                    }),
                CoinType.Dogecoin => new NodeRpcConfig(
                    22555,
                    new[] { "dogecoind", "dogecoind.exe" },
                    new[] { ".cookie" },
                    new[] { "-server", "-rpcbind=127.0.0.1", "-rpcallowip=127.0.0.1" },
                    new[]
                    {
                        Path.Combine(home, ".dogecoin"),
                        Path.Combine(appData, "Dogecoin")
                    }),
                _ => null
            };
        }

        public static bool TryStartDaemon(CoinType coin)
        {
            var cfg = GetNodeRpcConfig(coin);
            if (cfg == null) return false;

            if (_daemonStartAttemptsUtc.TryGetValue(coin, out var lastAttempt)
                && (DateTime.UtcNow - lastAttempt) < TimeSpan.FromSeconds(10))
                return false;

            _daemonStartAttemptsUtc[coin] = DateTime.UtcNow;

            foreach (var name in cfg.DaemonNames)
            {
                try
                {
                    var startInfo = new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = name,
                        Arguments = string.Join(" ", cfg.DaemonArgs),
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        RedirectStandardOutput = false,
                        RedirectStandardError = false
                    };
                    _ = System.Diagnostics.Process.Start(startInfo);
                    return true;
                }
                catch
                {
                    // try next candidate executable name
                }
            }
            return false;
        }

        private static async Task<SyncStatus?> TryGetLocalNodeStatusAsync(CoinType coin)
        {
            var cfg = GetNodeRpcConfig(coin);
            if (cfg == null) return null;

            string? cookie = TryReadCookie(cfg);
            if (string.IsNullOrWhiteSpace(cookie)) return null;
            var parts = cookie.Split(':', 2);
            if (parts.Length != 2) return null;

            try
            {
                using var req = new HttpRequestMessage(HttpMethod.Post, $"http://127.0.0.1:{cfg.Port}/");
                string auth = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{parts[0]}:{parts[1]}"));
                req.Headers.Authorization = new AuthenticationHeaderValue("Basic", auth);
                req.Content = new StringContent(
                    "{\"jsonrpc\":\"1.0\",\"id\":\"supcore\",\"method\":\"getblockchaininfo\",\"params\":[]}",
                    Encoding.UTF8,
                    "application/json");

                using var res = await _http.SendAsync(req).ConfigureAwait(false);
                if (!res.IsSuccessStatusCode) return null;

                string json = await res.Content.ReadAsStringAsync().ConfigureAwait(false);
                using var doc = JsonDocument.Parse(json);
                if (!doc.RootElement.TryGetProperty("result", out var result) || result.ValueKind != JsonValueKind.Object)
                    return null;

                var status = new SyncStatus
                {
                    Coin = coin,
                    Source = SyncSource.LocalNode,
                    IsOnline = true,
                    BestHeight = result.TryGetProperty("blocks", out var blocks) ? blocks.GetInt32() : 0,
                    BestHeaderHeight = result.TryGetProperty("headers", out var headers) ? headers.GetInt32() : 0,
                    VerificationProgress = result.TryGetProperty("verificationprogress", out var verify) ? verify.GetDouble() : null,
                    IsInitialBlockDownload = result.TryGetProperty("initialblockdownload", out var ibd) ? ibd.GetBoolean() : null
                };

                if (result.TryGetProperty("mediantime", out var mt) && mt.ValueKind == JsonValueKind.Number)
                {
                    long unix = mt.GetInt64();
                    if (unix > 0)
                        status.BestBlockTime = DateTimeOffset.FromUnixTimeSeconds(unix).UtcDateTime;
                }
                return status;
            }
            catch
            {
                return null;
            }
        }

        private static string? TryReadCookie(NodeRpcConfig cfg)
        {
            foreach (string dir in cfg.DataDirectories.Where(Directory.Exists))
            {
                foreach (string suffix in cfg.CookieSuffixes)
                {
                    string cookiePath = Path.Combine(dir, suffix);
                    if (!File.Exists(cookiePath)) continue;
                    try
                    {
                        return File.ReadAllText(cookiePath).Trim();
                    }
                    catch
                    {
                        // ignore and continue
                    }
                }
            }
            return null;
        }

        // ── Public API ─────────────────────────────────────────────────────────────

        public static async Task<SyncStatus> GetSyncStatusAsync(CoinType coin)
        {
            var local = await TryGetLocalNodeStatusAsync(coin).ConfigureAwait(false);
            if (local != null) return local;

            var status = new SyncStatus { Coin = coin };
            try
            {
                if (coin == CoinType.Mazacoin)
                {
                    status.IsOnline = false;
                    status.ErrorMessage = "No public API available – local node required";
                    return status;
                }

                if (coin is CoinType.Bitcoin or CoinType.BitcoinTestnet)
                {
                    string baseUrl = BaseUrl(coin);
                    // tip height
                    string heightStr = await _http.GetStringAsync($"{baseUrl}/blocks/tip/height").ConfigureAwait(false);
                    int height = int.Parse(heightStr.Trim());

                    // tip hash → block stats
                    string hash = await _http.GetStringAsync($"{baseUrl}/blocks/tip/hash").ConfigureAwait(false);
                    hash = hash.Trim().Trim('"');
                    string blockJson = await _http.GetStringAsync($"{baseUrl}/block/{hash}").ConfigureAwait(false);
                    using var doc = JsonDocument.Parse(blockJson);
                    long timestamp = doc.RootElement.GetProperty("timestamp").GetInt64();
                    status.BestBlockTime = DateTimeOffset.FromUnixTimeSeconds(timestamp).UtcDateTime;
                    status.BestHeight = height;
                    status.BestHeaderHeight = height;
                    status.Source = SyncSource.RemoteApi;
                    status.IsOnline = true;
                }
                else
                {
                    // BlockCypher chain endpoint
                    string chainJson = await _http.GetStringAsync(BaseUrl(coin)).ConfigureAwait(false);
                    using var doc = JsonDocument.Parse(chainJson);
                    status.BestHeight = doc.RootElement.GetProperty("height").GetInt32();
                    string timeStr = doc.RootElement.GetProperty("time").GetString() ?? string.Empty;
                    if (DateTime.TryParse(timeStr, null,
                            System.Globalization.DateTimeStyles.RoundtripKind, out var parsedTime))
                        status.BestBlockTime = parsedTime.ToUniversalTime();
                    status.BestHeaderHeight = status.BestHeight;
                    status.Source = SyncSource.RemoteApi;
                    status.IsOnline = true;
                }
            }
            catch (Exception ex)
            {
                status.IsOnline = false;
                status.ErrorMessage = ex.Message;
            }
            return status;
        }

        /// <summary>Returns the confirmed + unconfirmed balance in the coin's base unit.</summary>
        public static async Task<decimal> GetBalanceAsync(CoinType coin, string address)
        {
            try
            {
                if (coin is CoinType.Bitcoin or CoinType.BitcoinTestnet)
                {
                    string json = await _http.GetStringAsync(
                        $"{BaseUrl(coin)}/address/{address}").ConfigureAwait(false);
                    using var doc = JsonDocument.Parse(json);
                    var stats = doc.RootElement.GetProperty("chain_stats");
                    long funded = stats.GetProperty("funded_txo_sum").GetInt64();
                    long spent = stats.GetProperty("spent_txo_sum").GetInt64();
                    // blockstream returns values in satoshis
                    return (funded - spent) / 100_000_000m;
                }
                else if (coin is CoinType.Litecoin or CoinType.Dogecoin)
                {
                    string json = await _http.GetStringAsync(
                        $"{BaseUrl(coin)}/addrs/{address}/balance").ConfigureAwait(false);
                    using var doc = JsonDocument.Parse(json);
                    long satoshis = doc.RootElement.GetProperty("balance").GetInt64();
                    return satoshis / 100_000_000m;
                }
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    $"Failed to retrieve balance for {address} on {coin}: {ex.Message}", ex);
            }
            return 0m;
        }

        /// <summary>
        /// Returns UTXOs for the given address as (txid, vout, value-in-satoshis) tuples.
        /// </summary>
        public static async Task<List<(string txid, int vout, long satoshis)>> GetUtxosAsync(
            CoinType coin, string address)
        {
            var result = new List<(string, int, long)>();
            try
            {
                if (coin is CoinType.Bitcoin or CoinType.BitcoinTestnet)
                {
                    string json = await _http.GetStringAsync(
                        $"{BaseUrl(coin)}/address/{address}/utxo").ConfigureAwait(false);
                    using var doc = JsonDocument.Parse(json);
                    foreach (var el in doc.RootElement.EnumerateArray())
                    {
                        result.Add((
                            el.GetProperty("txid").GetString()!,
                            el.GetProperty("vout").GetInt32(),
                            el.GetProperty("value").GetInt64()));
                    }
                }
                else if (coin is CoinType.Litecoin or CoinType.Dogecoin)
                {
                    string json = await _http.GetStringAsync(
                        $"{BaseUrl(coin)}/addrs/{address}?unspentOnly=true&includeScript=true").ConfigureAwait(false);
                    using var doc = JsonDocument.Parse(json);
                    if (doc.RootElement.TryGetProperty("txrefs", out var txrefs))
                    {
                        foreach (var el in txrefs.EnumerateArray())
                        {
                            result.Add((
                                el.GetProperty("tx_hash").GetString()!,
                                el.GetProperty("tx_output_n").GetInt32(),
                                el.GetProperty("value").GetInt64()));
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    $"Failed to retrieve UTXOs for {address} on {coin}: {ex.Message}", ex);
            }
            return result;
        }

        /// <summary>Broadcasts a signed raw transaction. Returns the txid on success.</summary>
        public static async Task<string> BroadcastAsync(CoinType coin, string rawTxHex)
        {
            if (coin is CoinType.Bitcoin or CoinType.BitcoinTestnet)
            {
                var content = new StringContent(rawTxHex, Encoding.UTF8, "text/plain");
                var response = await _http.PostAsync($"{BaseUrl(coin)}/tx", content).ConfigureAwait(false);
                response.EnsureSuccessStatusCode();
                return await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            }
            else if (coin is CoinType.Litecoin or CoinType.Dogecoin)
            {
                var payload = new { tx = rawTxHex };
                var json = JsonSerializer.Serialize(payload);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                var response = await _http.PostAsync($"{BaseUrl(coin)}/txs/push", content).ConfigureAwait(false);
                response.EnsureSuccessStatusCode();
                return await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            }
            throw new NotSupportedException($"Broadcast not supported for {coin}");
        }
    }
}
