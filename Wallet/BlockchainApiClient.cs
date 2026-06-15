using System.Net.Http;
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

        private static HttpClient CreateHttpClient()
        {
            var client = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
            client.DefaultRequestHeaders.UserAgent.ParseAdd("SupCore/1.0");
            return client;
        }

        // ── API base URLs ──────────────────────────────────────────────────────────
        private static string BaseUrl(CoinType coin) => coin switch
        {
            CoinType.Bitcoin => "https://blockstream.info/api",
            CoinType.BitcoinTestnet => "https://blockstream.info/testnet/api",
            CoinType.Litecoin => "https://api.blockcypher.com/v1/ltc/main",
            CoinType.Dogecoin => "https://api.blockcypher.com/v1/doge/main",
            _ => string.Empty
        };

        // ── Public API ─────────────────────────────────────────────────────────────

        public static async Task<SyncStatus> GetSyncStatusAsync(CoinType coin)
        {
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
