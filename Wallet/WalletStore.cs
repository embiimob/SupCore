using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;

namespace SUP.Wallet
{
    /// <summary>
    /// AES-256/CBC encrypted JSON wallet file.
    /// File layout (binary):
    ///   [4 bytes] magic "SUPW"
    ///   [1 byte]  version = 1
    ///   [16 bytes] AES IV
    ///   [32 bytes] PBKDF2 salt
    ///   [4 bytes]  PBKDF2 iterations (little-endian int32)
    ///   [remaining] AES-CBC ciphertext of UTF-8 JSON payload
    /// </summary>
    public class WalletStore
    {
        private const string WalletDirectory = "wallet";
        private const int Pbkdf2Iterations = 100_000;
        private static readonly byte[] Magic = Encoding.ASCII.GetBytes("SUPW");

        public string EncryptedMnemonic { get; set; }  // BIP39 mnemonic encrypted with wallet password
        public List<WalletEntry> Entries { get; set; } = new List<WalletEntry>();
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        // ─── Persistence ───────────────────────────────────────────────────

        private static string WalletPath(CoinNetworkId networkId)
        {
            string dir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, WalletDirectory);
            Directory.CreateDirectory(dir);
            return Path.Combine(dir, networkId.ToString().ToLower() + ".wallet");
        }

        /// <summary>
        /// Save this store to disk, encrypting the contents with <paramref name="password"/>.
        /// If the wallet was previously unencrypted (empty password) and is being encrypted,
        /// the per-entry WIF fields are encrypted before saving.
        /// </summary>
        public void Save(CoinNetworkId networkId, string password)
        {
            UpdatedAt = DateTime.UtcNow;
            string json = JsonConvert.SerializeObject(this, Formatting.Indented);
            byte[] plaintext = Encoding.UTF8.GetBytes(json);

            // Generate salt + IV
            byte[] salt = new byte[32];
            byte[] iv = new byte[16];
            using (var rng = new RNGCryptoServiceProvider())
            {
                rng.GetBytes(salt);
                rng.GetBytes(iv);
            }

            byte[] key = DeriveKey(password, salt, Pbkdf2Iterations);
            byte[] ciphertext = AesEncrypt(plaintext, key, iv);

            string path = WalletPath(networkId);
            string tmp = path + ".tmp." + Guid.NewGuid().ToString("N");
            try
            {
                using (var fs = new FileStream(tmp, FileMode.Create, FileAccess.Write))
                using (var bw = new BinaryWriter(fs))
                {
                    bw.Write(Magic);
                    bw.Write((byte)1);              // version
                    bw.Write(iv);
                    bw.Write(salt);
                    bw.Write(Pbkdf2Iterations);
                    bw.Write(ciphertext);
                }
                if (File.Exists(path)) File.Delete(path);
                File.Move(tmp, path);
            }
            finally
            {
                try { if (File.Exists(tmp)) File.Delete(tmp); } catch { }
            }
        }

        /// <summary>
        /// Load the wallet for <paramref name="networkId"/>, decrypting with <paramref name="password"/>.
        /// Returns null if the file does not exist.
        /// Throws <see cref="InvalidOperationException"/> if the password is wrong.
        /// </summary>
        public static WalletStore Load(CoinNetworkId networkId, string password)
        {
            string path = WalletPath(networkId);
            if (!File.Exists(path)) return null;

            using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read))
            using (var br = new BinaryReader(fs))
            {
                byte[] magic = br.ReadBytes(4);
                if (magic.Length < 4 ||
                    magic[0] != Magic[0] || magic[1] != Magic[1] ||
                    magic[2] != Magic[2] || magic[3] != Magic[3])
                    throw new InvalidOperationException("Invalid wallet file format.");

                byte version = br.ReadByte();
                if (version != 1)
                    throw new InvalidOperationException("Unsupported wallet file version: " + version);

                byte[] iv = br.ReadBytes(16);
                byte[] salt = br.ReadBytes(32);
                int iterations = br.ReadInt32();
                byte[] ciphertext = br.ReadBytes((int)(fs.Length - fs.Position));

                byte[] key = DeriveKey(password, salt, iterations);
                byte[] plaintext;
                try { plaintext = AesDecrypt(ciphertext, key, iv); }
                catch { throw new InvalidOperationException("Incorrect wallet password."); }

                string json = Encoding.UTF8.GetString(plaintext);
                return JsonConvert.DeserializeObject<WalletStore>(json);
            }
        }

        /// <summary>Returns true if a wallet file exists for the given network.</summary>
        public static bool Exists(CoinNetworkId networkId) => File.Exists(WalletPath(networkId));

        // ─── Crypto helpers ────────────────────────────────────────────────

        private static byte[] DeriveKey(string password, byte[] salt, int iterations)
        {
            using (var pbkdf2 = new Rfc2898DeriveBytes(
                password ?? string.Empty,
                salt,
                iterations,
                HashAlgorithmName.SHA256))
            {
                return pbkdf2.GetBytes(32);
            }
        }

        private static byte[] AesEncrypt(byte[] plaintext, byte[] key, byte[] iv)
        {
            using (var aes = Aes.Create())
            {
                aes.Key = key;
                aes.IV = iv;
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.PKCS7;
                using (var ms = new MemoryStream())
                using (var cs = new CryptoStream(ms, aes.CreateEncryptor(), CryptoStreamMode.Write))
                {
                    cs.Write(plaintext, 0, plaintext.Length);
                    cs.FlushFinalBlock();
                    return ms.ToArray();
                }
            }
        }

        private static byte[] AesDecrypt(byte[] ciphertext, byte[] key, byte[] iv)
        {
            using (var aes = Aes.Create())
            {
                aes.Key = key;
                aes.IV = iv;
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.PKCS7;
                using (var ms = new MemoryStream(ciphertext))
                using (var cs = new CryptoStream(ms, aes.CreateDecryptor(), CryptoStreamMode.Read))
                using (var result = new MemoryStream())
                {
                    cs.CopyTo(result);
                    return result.ToArray();
                }
            }
        }

        /// <summary>
        /// Encrypt a WIF string with the given password (for per-key storage).
        /// Returns a Base64-encoded encrypted blob.
        /// </summary>
        public static string EncryptWif(string wif, string password)
        {
            byte[] salt = new byte[32];
            byte[] iv = new byte[16];
            using (var rng = new RNGCryptoServiceProvider())
            {
                rng.GetBytes(salt);
                rng.GetBytes(iv);
            }
            byte[] key = DeriveKey(password, salt, Pbkdf2Iterations);
            byte[] cipher = AesEncrypt(Encoding.UTF8.GetBytes(wif), key, iv);
            byte[] blob = new byte[48 + cipher.Length];
            Buffer.BlockCopy(iv, 0, blob, 0, 16);
            Buffer.BlockCopy(salt, 0, blob, 16, 32);
            Buffer.BlockCopy(cipher, 0, blob, 48, cipher.Length);
            return Convert.ToBase64String(blob);
        }

        /// <summary>Decrypt a WIF blob produced by <see cref="EncryptWif"/>.</summary>
        public static string DecryptWif(string encryptedBase64, string password)
        {
            byte[] blob = Convert.FromBase64String(encryptedBase64);
            byte[] iv = new byte[16];
            byte[] salt = new byte[32];
            Buffer.BlockCopy(blob, 0, iv, 0, 16);
            Buffer.BlockCopy(blob, 16, salt, 0, 32);
            byte[] cipher = new byte[blob.Length - 48];
            Buffer.BlockCopy(blob, 48, cipher, 0, cipher.Length);
            byte[] key = DeriveKey(password, salt, Pbkdf2Iterations);
            byte[] plain = AesDecrypt(cipher, key, iv);
            return Encoding.UTF8.GetString(plain);
        }
    }
}
