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
                // Decrypt per-entry WIFs into memory
                foreach (var entry in _store.Entries)
                {
                    if (!string.IsNullOrEmpty(entry.EncryptedWif) && !string.IsNullOrEmpty(_password))
                        try { entry.PrivateKeyWif = WalletStore.DecryptWif(entry.EncryptedWif, _password); } catch { }
                    else if (!string.IsNullOrEmpty(entry.EncryptedWif) && string.IsNullOrEmpty(_password))
                        entry.PrivateKeyWif = entry.EncryptedWif; // stored unencrypted
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

            // Get UTXOs from local node
            var rpc = new CoinRPC(new Uri(_config.RpcUrl), new NetworkCredential(RpcUser, RpcPassword));
            var unspent = GetUnspentOutputs(rpc, fundingEntry.Address);
            if (unspent.Count == 0)
                throw new InvalidOperationException("No unspent outputs available.");

            // Build transaction — ShuffleRandom = false guarantees output order
            var builder = _config.Network.CreateTransactionBuilder();
            builder.ShuffleRandom = false;

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
            var hex = tx.ToHex();

            // Broadcast via local node
            rpc.SendRawTransaction(hex);
            return tx.GetHash().ToString();
        }

        // ── Balance ────────────────────────────────────────────────────────

        /// <summary>Query the confirmed balance for a given address from the local node.</summary>
        public decimal GetBalance(string address)
        {
            try
            {
                var rpc = new CoinRPC(new Uri(_config.RpcUrl), new NetworkCredential(RpcUser, RpcPassword));
                var results = rpc.SearchRawDataTransaction(address, 1, 0, 1000);
                if (results == null || results.Count == 0) return 0m;

                // Sum outputs to this address, subtract inputs spent from this address
                decimal balance = 0m;
                var spent = new System.Collections.Generic.HashSet<string>();

                foreach (var tx in results)
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
            catch { return 0m; }
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

        /// <summary>Get unspent outputs for an address by scanning searchrawtransactions.</summary>
        private List<Coin> GetUnspentOutputs(CoinRPC rpc, string address)
        {
            var coins = new List<Coin>();
            try
            {
                var txs = rpc.SearchRawDataTransaction(address, 1, 0, 1000);
                if (txs == null) return coins;

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
                            string.Equals(a, address, StringComparison.OrdinalIgnoreCase))) continue;
                        string key = tx.txid + ":" + out_.n;
                        if (spentKeys.Contains(key)) continue;
                        var outPoint = new OutPoint(uint256.Parse(tx.txid), (uint)out_.n);
                        var txOut = new TxOut(Money.Coins(out_.value),
                            BitcoinAddress.Create(address, _config.Network).ScriptPubKey);
                        coins.Add(new Coin(outPoint, txOut));
                    }
                }
            }
            catch { }
            return coins;
        }
    }
}
