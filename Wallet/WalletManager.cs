using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using NBitcoin;
using NBitcoin.RPC;
using Newtonsoft.Json.Linq;
using SUP.RPCClient;

namespace SUP.Wallet
{
    /// <summary>
    /// Internal wallet manager backed by NBitcoin.
    /// Handles key derivation, WIF import/export, transaction building (with no output shuffle),
    /// wallet encryption/decryption, and UTXO-based sending.
    ///
    /// Uses the local running daemon (managed by NodeHostManager) for UTXO queries
    /// and transaction broadcasting via the existing CoinRPC infrastructure.
    /// </summary>
    public class WalletManager
    {
        private readonly CoinNetworkConfig _config;
        private WalletStore _store;
        private string _password;   // null = no encryption / unlocked
        private bool _locked = false;

        // RPC credentials (same defaults used everywhere in the project)
        private const string RpcUser = "good-user";
        private const string RpcPassword = "better-password";

        public WalletManager(CoinNetworkConfig config)
        {
            _config = config;
        }

        // ── Lifecycle ──────────────────────────────────────────────────────

        /// <summary>
        /// Open or create the wallet.  If no wallet file exists, a new HD wallet is created
        /// (BIP39 mnemonic) and saved with the given password (may be empty).
        /// </summary>
        public void Open(string password = "")
        {
            _password = password ?? string.Empty;
            if (WalletStore.Exists(_config.Id))
            {
                _store = WalletStore.Load(_config.Id, _password);
                // Decrypt per-entry WIFs into memory.
                // A decryption failure (wrong password) is surfaced immediately rather than silently
                // skipped, preventing the wallet from appearing open with inaccessible keys.
                foreach (var entry in _store.Entries)
                {
                    if (!string.IsNullOrEmpty(entry.EncryptedWif) && !string.IsNullOrEmpty(_password))
                    {
                        try
                        {
                            entry.PrivateKeyWif = WalletStore.DecryptWif(entry.EncryptedWif, _password);
                        }
                        catch (Exception ex)
                        {
                            throw new InvalidOperationException(
                                "Wrong password or corrupted wallet entry for address " + entry.Address, ex);
                        }
                    }
                    else if (!string.IsNullOrEmpty(entry.EncryptedWif) && string.IsNullOrEmpty(_password))
                    {
                        // Entry was saved without encryption — treat the stored value as plaintext WIF.
                        entry.PrivateKeyWif = entry.EncryptedWif;
                    }
                }
            }
            else
            {
                _store = new WalletStore();
                // Create first HD address
                var mnemonic = new Mnemonic(Wordlist.English, WordCount.Twelve);
                _store.EncryptedMnemonic = string.IsNullOrEmpty(_password)
                    ? mnemonic.ToString()
                    : WalletStore.EncryptWif(mnemonic.ToString(), _password);
                DeriveHDAddresses(mnemonic, 10);
                Save();
            }
            _locked = false;

            // Register all wallet addresses with the embedded node's address index
            // so that sync will capture their transactions.
            RegisterAddressesWithIndex();
        }

        /// <summary>
        /// Lock the wallet — wipes in-memory private keys.
        /// </summary>
        public void Lock()
        {
            if (_store != null)
                foreach (var e in _store.Entries)
                    e.PrivateKeyWif = null;
            _locked = true;
            _password = null;
        }

        /// <summary>
        /// Unlock the wallet with password.
        /// </summary>
        public void Unlock(string password)
        {
            _password = password ?? string.Empty;
            if (_store == null) return;
            foreach (var entry in _store.Entries)
            {
                if (!string.IsNullOrEmpty(entry.EncryptedWif))
                    try { entry.PrivateKeyWif = WalletStore.DecryptWif(entry.EncryptedWif, _password); } catch { }
            }
            _locked = false;
        }

        public bool IsLocked => _locked;
        public bool IsOpen => _store != null && !_locked;

        // ── Addresses ──────────────────────────────────────────────────────

        /// <summary>Return all wallet addresses.</summary>
        public IReadOnlyList<WalletEntry> GetAddresses() => _store?.Entries ?? new List<WalletEntry>();

        /// <summary>Derive the next unused HD address.</summary>
        public WalletEntry NewAddress(string label = "")
        {
            EnsureOpen();
            var mnemonic = GetMnemonic();
            int hdIndex = _store.Entries.Count(e => e.IsHD);
            var path = new KeyPath($"m/44'/0'/0'/0/{hdIndex}");
            var extKey = mnemonic.DeriveExtKey();
            var key = extKey.Derive(path).PrivateKey;
            var addr = key.PubKey.GetAddress(ScriptPubKeyType.Legacy, _config.Network).ToString();
            var entry = new WalletEntry
            {
                Address = addr,
                Label = label,
                IsHD = true,
                DerivationPath = path.ToString(),
                NetworkId = _config.Id,
                PrivateKeyWif = key.GetBitcoinSecret(_config.Network).ToWif(),
                EncryptedWif = string.IsNullOrEmpty(_password)
                    ? key.GetBitcoinSecret(_config.Network).ToWif()
                    : WalletStore.EncryptWif(key.GetBitcoinSecret(_config.Network).ToWif(), _password)
            };
            _store.Entries.Add(entry);
            Save();
            WatchAddressInIndex(addr);
            return entry;
        }

        /// <summary>Import a WIF private key and return the derived address entry.</summary>
        public WalletEntry ImportWIF(string wif, string label = "")
        {
            EnsureOpen();
            var secret = new BitcoinSecret(wif, _config.Network);
            var addr = secret.GetAddress(ScriptPubKeyType.Legacy).ToString();
            if (_store.Entries.Any(e => e.Address == addr))
                return _store.Entries.First(e => e.Address == addr);
            var entry = new WalletEntry
            {
                Address = addr,
                Label = label,
                IsHD = false,
                NetworkId = _config.Id,
                PrivateKeyWif = wif,
                EncryptedWif = string.IsNullOrEmpty(_password)
                    ? wif
                    : WalletStore.EncryptWif(wif, _password)
            };
            _store.Entries.Add(entry);
            Save();
            WatchAddressInIndex(addr);
            return entry;
        }

        /// <summary>Export the WIF private key for a given address.  Wallet must be unlocked.</summary>
        public string ExportWIF(string address)
        {
            EnsureOpen();
            var entry = _store.Entries.FirstOrDefault(e =>
                string.Equals(e.Address, address, StringComparison.OrdinalIgnoreCase));
            if (entry == null)
                throw new InvalidOperationException("Address not found in wallet: " + address);
            if (string.IsNullOrEmpty(entry.PrivateKeyWif))
                throw new InvalidOperationException("Wallet is locked or private key unavailable.");
            return entry.PrivateKeyWif;
        }

        // ── Wallet encryption ──────────────────────────────────────────────

        /// <summary>
        /// Re-encrypt all keys with a new password.  Wallet must be open/unlocked.
        /// </summary>
        public void EncryptWallet(string newPassword)
        {
            EnsureOpen();
            _password = newPassword ?? string.Empty;
            foreach (var entry in _store.Entries)
            {
                if (string.IsNullOrEmpty(entry.PrivateKeyWif)) continue;
                entry.EncryptedWif = string.IsNullOrEmpty(_password)
                    ? entry.PrivateKeyWif
                    : WalletStore.EncryptWif(entry.PrivateKeyWif, _password);
            }
            if (!string.IsNullOrEmpty(_store.EncryptedMnemonic))
            {
                // Re-encrypt the mnemonic
                string plainMnemonic = GetMnemonicString();
                _store.EncryptedMnemonic = string.IsNullOrEmpty(_password)
                    ? plainMnemonic
                    : WalletStore.EncryptWif(plainMnemonic, _password);
            }
            Save();
        }

        // ── Send ───────────────────────────────────────────────────────────

        /// <summary>
        /// Build, sign and broadcast a transaction paying multiple outputs.
        /// Output ORDER IS PRESERVED — no shuffling.
        /// Returns the transaction ID (hex).
        /// </summary>
        public string SendMany(IDictionary<string, decimal> outputs, string fromAddress = null, decimal feeRate = 0.001m)
        {
            EnsureOpen();

            // Choose a funding address
            var fundingEntry = string.IsNullOrEmpty(fromAddress)
                ? _store.Entries.FirstOrDefault(e => !string.IsNullOrEmpty(e.PrivateKeyWif))
                : _store.Entries.FirstOrDefault(e =>
                      string.Equals(e.Address, fromAddress, StringComparison.OrdinalIgnoreCase));

            if (fundingEntry == null || string.IsNullOrEmpty(fundingEntry.PrivateKeyWif))
                throw new InvalidOperationException("No usable key found in wallet.");

            var secret = new BitcoinSecret(fundingEntry.PrivateKeyWif, _config.Network);
            var changeAddress = secret.GetAddress(ScriptPubKeyType.Legacy);

            // Get UTXOs — prefer embedded index, fall back to external RPC
            List<Coin> unspent = GetUnspentOutputs(fundingEntry.Address);
            if (unspent.Count == 0)
                throw new InvalidOperationException("No unspent outputs available.");

            // Build transaction.
            // IMPORTANT: ShuffleRandom = false preserves the caller-supplied output order.
            // The Sup!? state engine encodes semantic meaning into the position of outputs in a
            // sendmany transaction — shuffling would break state-machine decoding.
            var builder = _config.Network.CreateTransactionBuilder();
            builder.ShuffleRandom = null;

            foreach (var coin in unspent)
                builder.AddCoins(coin);
            builder.AddKeys(secret.PrivateKey);

            // Add outputs in dictionary insertion order
            foreach (var kvp in outputs)
            {
                var addr = BitcoinAddress.Create(kvp.Key, _config.Network);
                builder.Send(addr, Money.Coins(kvp.Value));
            }

            builder.SetChange(changeAddress);
            builder.SendFees(Money.Coins(feeRate));

            var tx = builder.BuildTransaction(sign: true);

            // Broadcast — prefer embedded P2P, fall back to external RPC
            var node = NodeHostManager.GetNode(_config.Id);
            if (node != null && node.IsRunning)
            {
                bool ok = node.BroadcastAsync(tx).GetAwaiter().GetResult();
                if (!ok)
                    throw new InvalidOperationException(
                        "Failed to broadcast transaction via the embedded P2P node.");
            }
            else
            {
                var rpc = new CoinRPC(new Uri(_config.RpcUrl),
                    new NetworkCredential(RpcUser, RpcPassword));
                rpc.SendRawTransaction(tx.ToHex());
            }

            return tx.GetHash().ToString();
        }

        // ── Balance ────────────────────────────────────────────────────────

        /// <summary>
        /// Query the confirmed balance for a given address.
        /// Uses the embedded address index when the local node is running;
        /// otherwise falls back to external RPC.
        /// </summary>
        public decimal GetBalance(string address)
        {
            try
            {
                // Try embedded index first
                var node = NodeHostManager.GetNode(_config.Id);
                var idx  = node?.GetAddressIndex();
                if (idx != null)
                {
                    var results = idx.GetTransactions(address, 0, 10000);
                    return ComputeBalance(address, results);
                }

                // Fallback to external RPC
                var rpc = new CoinRPC(new Uri(_config.RpcUrl),
                    new NetworkCredential(RpcUser, RpcPassword));
                var rpcResults = rpc.SearchRawDataTransaction(address, 1, 0, 1000);
                return ComputeBalance(address, rpcResults);
            }
            catch { return 0m; }
        }

        private static decimal ComputeBalance(
            string address,
            IList<SUP.RPCClient.GetRawDataTransactionResponse> txs)
        {
            if (txs == null || txs.Count == 0) return 0m;

            decimal balance = 0m;
            var spent = new System.Collections.Generic.HashSet<string>();

            foreach (var tx in txs)
            {
                if (tx.vin != null)
                    foreach (var inp in tx.vin)
                        if (!string.IsNullOrEmpty(inp.txid))
                            spent.Add(inp.txid + ":" + inp.vout);

                if (tx.vout != null)
                    foreach (var out_ in tx.vout)
                        if (out_.scriptPubKey?.addresses != null &&
                            out_.scriptPubKey.addresses.Any(a =>
                                string.Equals(a, address, StringComparison.OrdinalIgnoreCase)))
                        {
                            string key = tx.txid + ":" + out_.n;
                            if (!spent.Contains(key))
                                balance += out_.value;
                        }
            }
            return balance;
        }

        // ── Helpers ────────────────────────────────────────────────────────

        private void EnsureOpen()
        {
            if (_store == null || _locked)
                throw new InvalidOperationException("Wallet is not open or is locked.");
        }

        private Mnemonic GetMnemonic()
        {
            string mnemonicStr = GetMnemonicString();
            return new Mnemonic(mnemonicStr, Wordlist.English);
        }

        private string GetMnemonicString()
        {
            if (string.IsNullOrEmpty(_store.EncryptedMnemonic))
                throw new InvalidOperationException("No mnemonic in wallet.");
            if (!string.IsNullOrEmpty(_password))
            {
                try { return WalletStore.DecryptWif(_store.EncryptedMnemonic, _password); }
                catch { return _store.EncryptedMnemonic; } // stored unencrypted
            }
            return _store.EncryptedMnemonic;
        }

        private void DeriveHDAddresses(Mnemonic mnemonic, int count)
        {
            var extKey = mnemonic.DeriveExtKey();
            for (int i = 0; i < count; i++)
            {
                var path = new KeyPath($"m/44'/0'/0'/0/{i}");
                var key = extKey.Derive(path).PrivateKey;
                var addr = key.PubKey.GetAddress(ScriptPubKeyType.Legacy, _config.Network).ToString();
                _store.Entries.Add(new WalletEntry
                {
                    Address = addr,
                    Label = i == 0 ? "default" : "",
                    IsHD = true,
                    DerivationPath = path.ToString(),
                    NetworkId = _config.Id,
                    PrivateKeyWif = key.GetBitcoinSecret(_config.Network).ToWif(),
                    EncryptedWif = string.IsNullOrEmpty(_password)
                        ? key.GetBitcoinSecret(_config.Network).ToWif()
                        : WalletStore.EncryptWif(key.GetBitcoinSecret(_config.Network).ToWif(), _password)
                });
            }
        }

        private void Save() => _store.Save(_config.Id, _password ?? string.Empty);

        /// <summary>
        /// Register all wallet addresses with the embedded node's address index
        /// so that the sync captures their transactions.
        /// </summary>
        private void RegisterAddressesWithIndex()
        {
            if (_store == null) return;
            var node = NodeHostManager.GetNode(_config.Id);
            var idx = node?.GetAddressIndex();
            if (idx == null) return;
            foreach (var entry in _store.Entries)
                idx.WatchAddress(entry.Address);
        }

        /// <summary>Register a single address with the embedded node's address index.</summary>
        private void WatchAddressInIndex(string address)
        {
            var node = NodeHostManager.GetNode(_config.Id);
            node?.GetAddressIndex()?.WatchAddress(address);
        }

        /// <summary>
        /// Get unspent outputs for an address.
        /// Uses the embedded address index when available; falls back to external RPC.
        /// </summary>
        private List<Coin> GetUnspentOutputs(string address)
        {
            // Try embedded index first
            var node = NodeHostManager.GetNode(_config.Id);
            var idx  = node?.GetAddressIndex();
            if (idx != null)
                return BuildCoinsFromIndex(address, idx.GetTransactions(address, 0, 10000));

            // Fallback to external RPC
            try
            {
                var rpc = new CoinRPC(new Uri(_config.RpcUrl),
                    new NetworkCredential(RpcUser, RpcPassword));
                return BuildCoinsFromIndex(address, rpc.SearchRawDataTransaction(address, 1, 0, 1000));
            }
            catch { return new List<Coin>(); }
        }

        private List<Coin> BuildCoinsFromIndex(
            string address,
            IList<SUP.RPCClient.GetRawDataTransactionResponse> txs)
        {
            var coins = new List<Coin>();
            if (txs == null || txs.Count == 0) return coins;

            var spentKeys = new System.Collections.Generic.HashSet<string>();
            foreach (var tx in txs)
                if (tx.vin != null)
                    foreach (var inp in tx.vin)
                        if (!string.IsNullOrEmpty(inp.txid))
                            spentKeys.Add(inp.txid + ":" + inp.vout);

            foreach (var tx in txs)
            {
                if (tx.vout == null || string.IsNullOrEmpty(tx.txid)) continue;
                foreach (var out_ in tx.vout)
                {
                    if (out_.scriptPubKey?.addresses == null) continue;
                    if (!out_.scriptPubKey.addresses.Any(a =>
                            string.Equals(a, address, StringComparison.OrdinalIgnoreCase)))
                        continue;
                    string key = tx.txid + ":" + out_.n;
                    if (spentKeys.Contains(key)) continue;
                    var outPoint = new OutPoint(uint256.Parse(tx.txid), (uint)out_.n);
                    var txOut = new TxOut(
                        Money.Coins(out_.value),
                        BitcoinAddress.Create(address, _config.Network).ScriptPubKey);
                    coins.Add(new Coin(outPoint, txOut));
                }
            }
            return coins;
        }
    }
}
