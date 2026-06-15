using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SUP.RPCClient;

namespace SUP.Wallet
{
    /// <summary>
    /// Optional JSON-RPC 1.0 server exposed on a configurable port for CLI tool compatibility.
    /// Supports the same wire format as Bitcoin Core's RPC interface.
    ///
    /// Supported methods (all networks use the same server; the "wallet" parameter selects the
    /// active coin when specified, otherwise Bitcoin Testnet is used as the default for legacy compat):
    ///   searchrawtransactions — address-indexed tx history (proxied to local daemon)
    ///   getrawtransaction     — raw tx by ID (proxied to local daemon)
    ///   sendmany              — build + broadcast tx with NO output shuffle
    ///   getbalance            — wallet balance (proxied to local daemon)
    ///   dumpprivkey           — WIF export from internal wallet
    ///   getblockchaininfo     — sync status (proxied to local daemon)
    ///   stop                  — graceful daemon stop (proxied to local daemon)
    /// </summary>
    public class InternalRpcServer
    {
        private readonly string _rpcUser;
        private readonly string _rpcPassword;
        private HttpListener _listener;
        private Thread _thread;
        private volatile bool _running;

        // Ports: one listener per active coin RPC port
        private readonly int[] _ports;

        public bool IsRunning => _running;

        public InternalRpcServer(string rpcUser = "good-user", string rpcPassword = "better-password")
        {
            _rpcUser = rpcUser;
            _rpcPassword = rpcPassword;
            // Expose on all coin RPC ports + one extra port (8334) for unified CLI access
            _ports = new int[] { 8334 };
        }

        public void Start()
        {
            if (_running) return;
            _listener = new HttpListener();
            foreach (int port in _ports)
                _listener.Prefixes.Add($"http://127.0.0.1:{port}/");
            _listener.AuthenticationSchemes = AuthenticationSchemes.Basic;
            _listener.Start();
            _running = true;
            _thread = new Thread(ListenLoop) { IsBackground = true, Name = "InternalRpcServer" };
            _thread.Start();
        }

        public void Stop()
        {
            _running = false;
            try { _listener?.Stop(); } catch { }
            _thread?.Join(2000);
            _listener = null;
        }

        private void ListenLoop()
        {
            while (_running)
            {
                try
                {
                    var ctx = _listener.GetContext();
                    Task.Run(() => HandleRequest(ctx));
                }
                catch (HttpListenerException) { break; }
                catch { }
            }
        }

        private void HandleRequest(HttpListenerContext ctx)
        {
            try
            {
                // Authenticate
                var identity = ctx.User?.Identity as HttpListenerBasicIdentity;
                if (identity == null ||
                    identity.Name != _rpcUser ||
                    identity.Password != _rpcPassword)
                {
                    ctx.Response.StatusCode = 401;
                    ctx.Response.Close();
                    return;
                }

                string body;
                using (var sr = new StreamReader(ctx.Request.InputStream, Encoding.UTF8))
                    body = sr.ReadToEnd();

                JObject req;
                try { req = JObject.Parse(body); }
                catch
                {
                    SendError(ctx, -32700, "Parse error", null);
                    return;
                }

                string method = req["method"]?.Value<string>() ?? "";
                var @params = req["params"] as JArray ?? new JArray();
                var id = req["id"];

                // Determine which coin network to route to
                // The port the request arrived on tells us which coin
                int port = ctx.Request.LocalEndPoint.Port;
                var coinCfg = FindConfigByPort(port) ?? CoinNetworkConfig.BitcoinTestnet;

                string result;
                try { result = Dispatch(method, @params, id, coinCfg); }
                catch (Exception ex)
                {
                    SendError(ctx, -32603, ex.Message, id);
                    return;
                }

                byte[] data = Encoding.UTF8.GetBytes(result);
                ctx.Response.ContentType = "application/json";
                ctx.Response.ContentLength64 = data.Length;
                ctx.Response.OutputStream.Write(data, 0, data.Length);
                ctx.Response.OutputStream.Close();
            }
            catch { try { ctx.Response.Close(); } catch { } }
        }

        private string Dispatch(string method, JArray @params, JToken id, CoinNetworkConfig cfg)
        {
            switch (method.ToLowerInvariant())
            {
                case "searchrawtransactions":
                {
                    string address = @params.Count > 0 ? @params[0].Value<string>() : null;
                    int verbose = @params.Count > 1 ? @params[1].Value<int>() : 0;
                    int skip = @params.Count > 2 ? @params[2].Value<int>() : 0;
                    int qty = @params.Count > 3 ? @params[3].Value<int>() : 100;
                    var rpc = MakeRpc(cfg);
                    var txs = rpc.SearchRawDataTransaction(address, verbose, skip, qty);
                    return OkResponse(JToken.FromObject(txs ?? new List<GetRawDataTransactionResponse>()), id);
                }
                case "getrawtransaction":
                {
                    string txid = @params.Count > 0 ? @params[0].Value<string>() : null;
                    int verbose = @params.Count > 1 ? @params[1].Value<int>() : 0;
                    var rpc = MakeRpc(cfg);
                    var tx = rpc.GetRawDataTransaction(txid, verbose);
                    JToken result = verbose == 0 ? (JToken)(tx?.hex ?? "") : JToken.FromObject(tx);
                    return OkResponse(result, id);
                }
                case "sendmany":
                {
                    // sendmany(fromaccount, amounts, [minconf])
                    // amounts is an object {"address": amount, ...}
                    string from = @params.Count > 0 ? @params[0].Value<string>() : "";
                    var amountsObj = @params.Count > 1 ? @params[1] as JObject : null;
                    if (amountsObj == null) throw new InvalidOperationException("sendmany: amounts required");

                    // Build ordered dictionary preserving JSON key order
                    var outputs = new System.Collections.Specialized.OrderedDictionary();
                    foreach (var kv in amountsObj)
                        outputs[kv.Key] = kv.Value.Value<decimal>();

                    // Route through WalletManager for no-shuffle guarantee
                    var wallet = NodeHostManager.GetWallet(cfg.Id);
                    // Convert to generic IDictionary<string,decimal> preserving order
                    var ordered = new System.Collections.Generic.Dictionary<string, decimal>();
                    foreach (System.Collections.DictionaryEntry kv in outputs)
                        ordered[(string)kv.Key] = (decimal)kv.Value;
                    string txid = wallet.SendMany(ordered, from);
                    return OkResponse(JToken.FromObject(txid), id);
                }
                case "getbalance":
                {
                    var rpc = MakeRpc(cfg);
                    // Proxy getbalance to local daemon
                    string raw = rpc.HttpCall(JsonConvert.SerializeObject(
                        new { method = "getbalance", @params = new object[] { "*", 1 }, id = 1 }));
                    return ProxyRawResponse(raw, id);
                }
                case "dumpprivkey":
                {
                    string addr = @params.Count > 0 ? @params[0].Value<string>() : null;
                    var wallet = NodeHostManager.GetWallet(cfg.Id);
                    string wif = wallet.ExportWIF(addr);
                    return OkResponse(JToken.FromObject(wif), id);
                }
                case "getblockchaininfo":
                {
                    var rpc = MakeRpc(cfg);
                    var info = rpc.GetBlockchainInfo();
                    return OkResponse(JToken.FromObject(info), id);
                }
                case "stop":
                {
                    var node = NodeHostManager.GetNode(cfg.Id);
                    node.Stop();
                    return OkResponse(JToken.FromObject("Bitcoin server stopping"), id);
                }
                case "sendrawtransaction":
                {
                    string hex = @params.Count > 0 ? @params[0].Value<string>() : null;
                    var rpc = MakeRpc(cfg);
                    string txid = rpc.SendRawTransaction(hex);
                    return OkResponse(JToken.FromObject(txid), id);
                }
                default:
                {
                    // Proxy unknown methods directly to the local daemon
                    var rpc = MakeRpc(cfg);
                    string raw = rpc.HttpCall(JsonConvert.SerializeObject(
                        new { method = method, @params = @params, id = 1 }));
                    return ProxyRawResponse(raw, id);
                }
            }
        }

        // ── Helpers ────────────────────────────────────────────────────────

        private static CoinRPC MakeRpc(CoinNetworkConfig cfg)
            => new CoinRPC(new Uri(cfg.RpcUrl), new NetworkCredential("good-user", "better-password"));

        private static CoinNetworkConfig FindConfigByPort(int port)
        {
            foreach (var cfg in CoinNetworkConfig.All)
                if (cfg.RpcPort == port) return cfg;
            return null;
        }

        private static string OkResponse(JToken result, JToken id)
        {
            var obj = new JObject
            {
                ["result"] = result,
                ["error"] = null,
                ["id"] = id
            };
            return obj.ToString(Formatting.None);
        }

        private static void SendError(HttpListenerContext ctx, int code, string message, JToken id)
        {
            var obj = new JObject
            {
                ["result"] = null,
                ["error"] = new JObject { ["code"] = code, ["message"] = message },
                ["id"] = id
            };
            byte[] data = Encoding.UTF8.GetBytes(obj.ToString(Formatting.None));
            ctx.Response.StatusCode = 500;
            ctx.Response.ContentType = "application/json";
            ctx.Response.ContentLength64 = data.Length;
            try { ctx.Response.OutputStream.Write(data, 0, data.Length); ctx.Response.OutputStream.Close(); }
            catch { ctx.Response.Close(); }
        }

        private static string ProxyRawResponse(string raw, JToken id)
        {
            // The daemon response already has result/error/id — just return it
            return raw ?? OkResponse(JValue.CreateNull(), id);
        }
    }
}
