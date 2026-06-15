using System.Security.Cryptography;
using System.Text;
using NBitcoin;
using Newtonsoft.Json;
using AltcoinsLib = NBitcoin.Altcoins;

namespace SupCore.Wallet
{
    /// <summary>
    /// Manages all internal wallet operations for every supported coin.
    /// Private keys are stored AES-256 encrypted (PBKDF2 key derivation) in
    /// per-coin JSON files in the application base directory.
    /// </summary>
    public class WalletManager
    {
        // ── Configuration ──────────────────────────────────────────────────────────
        private const int PbkdfIterations = 100_000;
        private const int AesKeySize = 32; // 256-bit
        private const int AesIvSize = 16;  // 128-bit

        // ── State ──────────────────────────────────────────────────────────────────
        private readonly string _walletDir;
        private readonly Dictionary<CoinType, WalletFile> _wallets = new();
        private byte[]? _aesKey;   // derived when wallet is unlocked
        private byte[]? _aesSalt;  // from first wallet that has a salt

        public bool IsLocked => _aesKey == null;

        // ── Constructor ────────────────────────────────────────────────────────────
        public WalletManager(string? walletDirectory = null)
        {
            _walletDir = walletDirectory
                ?? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "wallets");
            Directory.CreateDirectory(_walletDir);

            foreach (CoinType coin in Enum.GetValues<CoinType>())
                LoadOrCreateWallet(coin);
        }

        // ── Wallet file I/O ────────────────────────────────────────────────────────
        private string WalletPath(CoinType coin) =>
            Path.Combine(_walletDir, coin.GetWalletFileName());

        private void LoadOrCreateWallet(CoinType coin)
        {
            string path = WalletPath(coin);
            if (File.Exists(path))
            {
                string json = File.ReadAllText(path);
                var wf = JsonConvert.DeserializeObject<WalletFile>(json) ?? new WalletFile();
                _wallets[coin] = wf;
                if (_aesSalt == null && wf.Salt != null)
                    _aesSalt = Convert.FromBase64String(wf.Salt);
            }
            else
            {
                _wallets[coin] = new WalletFile { Coin = coin.GetShortName() };
                SaveWallet(coin);
            }
        }

        private void SaveWallet(CoinType coin)
        {
            string path = WalletPath(coin);
            string json = JsonConvert.SerializeObject(_wallets[coin], Formatting.Indented);
            File.WriteAllText(path, json);
        }

        // ── NBitcoin network resolution ────────────────────────────────────────────
        private static Network GetNetwork(CoinType coin) => coin switch
        {
            CoinType.Bitcoin => Network.Main,
            CoinType.BitcoinTestnet => Network.TestNet,
            CoinType.Litecoin => AltcoinsLib.Litecoin.Instance.Mainnet,
            CoinType.Dogecoin => AltcoinsLib.Dogecoin.Instance.Mainnet,
            CoinType.Mazacoin => Network.Main,  // MZC uses same base58 prefixes as BTC
            _ => throw new NotSupportedException(coin.ToString())
        };

        // ── Encryption helpers ─────────────────────────────────────────────────────
        private byte[] DeriveKey(string password, byte[] salt)
        {
            using var pbkdf2 = new Rfc2898DeriveBytes(password, salt, PbkdfIterations, HashAlgorithmName.SHA256);
            return pbkdf2.GetBytes(AesKeySize);
        }

        private string Encrypt(string plainText, byte[] key)
        {
            byte[] iv = RandomNumberGenerator.GetBytes(AesIvSize);
            using var aes = Aes.Create();
            aes.Key = key;
            aes.IV = iv;
            using var ms = new MemoryStream();
            ms.Write(iv, 0, iv.Length);
            using (var cs = new CryptoStream(ms, aes.CreateEncryptor(), CryptoStreamMode.Write))
            using (var sw = new StreamWriter(cs))
                sw.Write(plainText);
            return Convert.ToBase64String(ms.ToArray());
        }

        private string Decrypt(string cipherBase64, byte[] key)
        {
            byte[] data = Convert.FromBase64String(cipherBase64);
            byte[] iv = data[..AesIvSize];
            byte[] cipher = data[AesIvSize..];
            using var aes = Aes.Create();
            aes.Key = key;
            aes.IV = iv;
            using var ms = new MemoryStream(cipher);
            using var cs = new CryptoStream(ms, aes.CreateDecryptor(), CryptoStreamMode.Read);
            using var sr = new StreamReader(cs);
            return sr.ReadToEnd();
        }

        // ── Wallet encryption / locking ────────────────────────────────────────────

        /// <summary>
        /// Encrypts all private keys with <paramref name="password"/> and locks the wallet.
        /// Idempotent – calling again with a new password re-encrypts with the new password.
        /// </summary>
        public void EncryptWallet(string password)
        {
            if (string.IsNullOrWhiteSpace(password)) throw new ArgumentException("Password required.");

            byte[] salt = _aesSalt ?? RandomNumberGenerator.GetBytes(32);
            _aesSalt = salt;
            byte[] key = DeriveKey(password, salt);
            string saltB64 = Convert.ToBase64String(salt);

            foreach (CoinType coin in Enum.GetValues<CoinType>())
            {
                var wf = _wallets[coin];
                wf.Salt = saltB64;
                foreach (var entry in wf.Entries)
                {
                    // If the entry looks like plain WIF (not encrypted) re-encrypt it.
                    // Encrypted values are base64-encoded binary so they won't parse as WIF.
                    if (IsPlainWif(entry.EncryptedKey))
                        entry.EncryptedKey = Encrypt(entry.EncryptedKey, key);
                }
                SaveWallet(coin);
            }
            _aesKey = null; // lock after encrypting
        }

        /// <summary>Unlocks the wallet by verifying the password against a known key.</summary>
        /// <returns>True if the password is correct.</returns>
        public bool Unlock(string password)
        {
            if (_aesSalt == null) { _aesKey = null; return true; } // unencrypted wallet

            byte[] key = DeriveKey(password, _aesSalt);
            // Verify by trying to decrypt the first encrypted entry we can find.
            foreach (CoinType coin in Enum.GetValues<CoinType>())
            {
                foreach (var entry in _wallets[coin].Entries)
                {
                    if (!IsPlainWif(entry.EncryptedKey))
                    {
                        try { Decrypt(entry.EncryptedKey, key); }
                        catch { return false; }
                        _aesKey = key;
                        return true;
                    }
                }
            }
            // No encrypted entries – accept any password and mark unlocked.
            _aesKey = key;
            return true;
        }

        public void Lock() => _aesKey = null;

        private static bool IsPlainWif(string value)
        {
            if (string.IsNullOrEmpty(value)) return false;
            // WIF starts with 5/K/L (mainnet) or c/9 (testnet) and is 51-52 chars
            return (value[0] is '5' or 'K' or 'L' or 'c' or '9') && value.Length is >= 51 and <= 53;
        }

        private string GetPlainKey(WalletEntry entry)
        {
            if (IsPlainWif(entry.EncryptedKey)) return entry.EncryptedKey;
            if (_aesKey == null) throw new InvalidOperationException("Wallet is locked.");
            return Decrypt(entry.EncryptedKey, _aesKey);
        }

        // ── Key generation ─────────────────────────────────────────────────────────

        /// <summary>Generates a new private key for <paramref name="coin"/> and returns the address.</summary>
        public string GenerateNewKey(CoinType coin, string label = "")
        {
            var network = GetNetwork(coin);
            var key = new Key();
            string wif = key.GetWif(network).ToString();
            string address = key.PubKey.GetAddress(ScriptPubKeyType.Legacy, network).ToString();

            AddEntryInternal(coin, address, wif, label);
            return address;
        }

        // ── Import / Export ────────────────────────────────────────────────────────

        /// <summary>Imports a WIF private key. Returns the derived address.</summary>
        public string ImportPrivateKey(CoinType coin, string wif, string label = "")
        {
            var network = GetNetwork(coin);
            BitcoinSecret secret;
            try { secret = new BitcoinSecret(wif, network); }
            catch (Exception ex) { throw new ArgumentException("Invalid WIF private key: " + ex.Message, ex); }

            string address = secret.PubKey.GetAddress(ScriptPubKeyType.Legacy, network).ToString();

            // De-duplicate
            var wf = _wallets[coin];
            if (wf.Entries.Any(e => e.Address == address))
                throw new InvalidOperationException($"Address {address} already in wallet.");

            AddEntryInternal(coin, address, wif, label);
            return address;
        }

        /// <summary>Exports the WIF private key for <paramref name="address"/>.</summary>
        public string ExportPrivateKey(CoinType coin, string address)
        {
            var entry = FindEntry(coin, address)
                ?? throw new KeyNotFoundException($"Address {address} not found in wallet.");
            return GetPlainKey(entry);
        }

        // ── Address listing / balance ──────────────────────────────────────────────

        public IReadOnlyList<WalletEntry> GetEntries(CoinType coin) =>
            _wallets[coin].Entries.AsReadOnly();

        public async Task<decimal> GetBalanceAsync(CoinType coin, string address) =>
            await BlockchainApiClient.GetBalanceAsync(coin, address).ConfigureAwait(false);

        /// <summary>Returns the sum of balances for all addresses in the coin wallet.</summary>
        public async Task<decimal> GetTotalBalanceAsync(CoinType coin)
        {
            var entries = _wallets[coin].Entries;
            if (entries.Count == 0) return 0m;
            var tasks = entries.Select(e => GetBalanceAsync(coin, e.Address));
            decimal[] results = await Task.WhenAll(tasks).ConfigureAwait(false);
            return results.Sum();
        }

        // ── Send transaction ───────────────────────────────────────────────────────

        /// <summary>
        /// Builds, signs, and broadcasts a transaction.
        /// </summary>
        /// <param name="coin">Target coin.</param>
        /// <param name="fromAddress">Source address (must be in wallet).</param>
        /// <param name="toAddress">Recipient address.</param>
        /// <param name="amountBtc">Amount to send (in coin units, not satoshis).</param>
        /// <param name="feeSatoshis">Network fee in satoshis. Defaults to 1000.</param>
        /// <returns>The broadcast transaction ID.</returns>
        public async Task<string> SendAsync(
            CoinType coin,
            string fromAddress,
            string toAddress,
            decimal amountBtc,
            long feeSatoshis = 1000)
        {
            if (coin == CoinType.Mazacoin)
                throw new NotSupportedException("Mazacoin transactions require a local node.");

            var network = GetNetwork(coin);
            string wif = ExportPrivateKey(coin, fromAddress);
            var secret = new BitcoinSecret(wif, network);
            var key = secret.PrivateKey;

            // Fetch UTXOs
            var utxos = await BlockchainApiClient.GetUtxosAsync(coin, fromAddress).ConfigureAwait(false);
            if (utxos.Count == 0) throw new InvalidOperationException("No spendable UTXOs found.");

            long amountSatoshis = (long)(amountBtc * 100_000_000m);
            long total = 0;
            var selectedUtxos = new List<(string txid, int vout, long sats)>();
            foreach (var utxo in utxos.OrderByDescending(u => u.satoshis))
            {
                selectedUtxos.Add(utxo);
                total += utxo.satoshis;
                if (total >= amountSatoshis + feeSatoshis) break;
            }
            if (total < amountSatoshis + feeSatoshis)
                throw new InvalidOperationException("Insufficient funds.");

            // Build transaction
            var txBuilder = network.CreateTransactionBuilder();
            foreach (var (txid, vout, sats) in selectedUtxos)
            {
                var outPoint = new OutPoint(uint256.Parse(txid), (uint)vout);
                var coin2 = new NBitcoin.Coin(outPoint, new TxOut(Money.Satoshis(sats),
                    secret.PubKey.GetAddress(ScriptPubKeyType.Legacy, network)));
                txBuilder.AddCoins(coin2);
            }

            var destAddress = BitcoinAddress.Create(toAddress, network);
            var changeAddress = secret.PubKey.GetAddress(ScriptPubKeyType.Legacy, network);

            txBuilder
                .AddKeys(key)
                .Send(destAddress, Money.Satoshis(amountSatoshis))
                .SetChange(changeAddress)
                .SendFees(Money.Satoshis(feeSatoshis));

            var tx = txBuilder.BuildTransaction(sign: true);
            string rawHex = tx.ToHex();
            return await BlockchainApiClient.BroadcastAsync(coin, rawHex).ConfigureAwait(false);
        }

        // ── Internals ──────────────────────────────────────────────────────────────
        private void AddEntryInternal(CoinType coin, string address, string plainWif, string label)
        {
            string storedKey = (_aesKey != null) ? Encrypt(plainWif, _aesKey) : plainWif;
            var entry = new WalletEntry
            {
                Address = address,
                EncryptedKey = storedKey,
                Label = label,
                CreatedAt = DateTime.UtcNow
            };
            _wallets[coin].Entries.Add(entry);
            if (_aesSalt != null)
                _wallets[coin].Salt = Convert.ToBase64String(_aesSalt);
            SaveWallet(coin);
        }

        private WalletEntry? FindEntry(CoinType coin, string address) =>
            _wallets[coin].Entries.FirstOrDefault(e => e.Address == address);

        /// <summary>Checks whether a salt / password has already been set for any coin wallet.</summary>
        public bool HasPassword =>
            Enum.GetValues<CoinType>().Any(c => _wallets[c].Salt != null);
    }
}
