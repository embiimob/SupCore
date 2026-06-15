using Newtonsoft.Json;

namespace SupCore.Wallet
{
    /// <summary>
    /// Represents a single wallet key entry (address + encrypted private key).
    /// </summary>
    public class WalletEntry
    {
        [JsonProperty("address")]
        public string Address { get; set; } = string.Empty;

        /// <summary>
        /// WIF private key, AES-256 encrypted and Base64-encoded when wallet is locked.
        /// Plain WIF when wallet is unlocked (never persisted in plain form).
        /// </summary>
        [JsonProperty("encryptedKey")]
        public string EncryptedKey { get; set; } = string.Empty;

        /// <summary>User-supplied label for this address.</summary>
        [JsonProperty("label")]
        public string Label { get; set; } = string.Empty;

        [JsonProperty("createdAt")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// On-disk representation of a coin wallet.
    /// </summary>
    public class WalletFile
    {
        [JsonProperty("version")]
        public int Version { get; set; } = 1;

        [JsonProperty("coin")]
        public string Coin { get; set; } = string.Empty;

        /// <summary>
        /// PBKDF2 salt (Base64) used to derive the AES key from the password.
        /// Null when the wallet has no password set.
        /// </summary>
        [JsonProperty("salt")]
        public string? Salt { get; set; }

        [JsonProperty("entries")]
        public List<WalletEntry> Entries { get; set; } = new();
    }
}
