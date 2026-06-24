using System;
using System.Collections.Generic;

namespace SUP.Wallet
{
    /// <summary>
    /// Represents a single key/address entry stored in the wallet.
    /// </summary>
    [Serializable]
    public class WalletEntry
    {
        /// <summary>Bitcoin / altcoin address (Base58Check).</summary>
        public string Address { get; set; }
        /// <summary>Optional human-readable label.</summary>
        public string Label { get; set; }
        /// <summary>
        /// AES-256/CBC encrypted WIF private key.  Null when the wallet is unlocked —
        /// in that case <see cref="PrivateKeyWif"/> holds the plaintext WIF.
        /// </summary>
        public string EncryptedWif { get; set; }
        /// <summary>
        /// Plaintext WIF while the wallet is unlocked.  Never persisted to disk.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public string PrivateKeyWif { get; set; }
        /// <summary>True when this entry was derived from the HD root (BIP44).</summary>
        public bool IsHD { get; set; }
        /// <summary>BIP44 derivation path, e.g. "m/44'/0'/0'/0/0".</summary>
        public string DerivationPath { get; set; }
        /// <summary>Network this entry belongs to.</summary>
        public CoinNetworkId NetworkId { get; set; }
        /// <summary>UTC creation time.</summary>
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
