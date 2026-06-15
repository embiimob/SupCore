namespace SupCore.Wallet
{
    /// <summary>
    /// Supported coin types for the internal wallet.
    /// </summary>
    public enum CoinType
    {
        BitcoinTestnet,
        Bitcoin,
        Litecoin,
        Dogecoin,
        Mazacoin
    }

    public static class CoinTypeExtensions
    {
        public static string GetDisplayName(this CoinType coin) => coin switch
        {
            CoinType.Bitcoin => "Bitcoin (BTC)",
            CoinType.BitcoinTestnet => "Bitcoin Testnet (BTCT)",
            CoinType.Litecoin => "Litecoin (LTC)",
            CoinType.Dogecoin => "Dogecoin (DOGE)",
            CoinType.Mazacoin => "Mazacoin (MZC)",
            _ => coin.ToString()
        };

        public static string GetShortName(this CoinType coin) => coin switch
        {
            CoinType.Bitcoin => "BTC",
            CoinType.BitcoinTestnet => "BTCT",
            CoinType.Litecoin => "LTC",
            CoinType.Dogecoin => "DOGE",
            CoinType.Mazacoin => "MZC",
            _ => coin.ToString()
        };

        /// <summary>Returns the wallet data file name for the given coin.</summary>
        public static string GetWalletFileName(this CoinType coin) =>
            $"wallet_{coin.GetShortName().ToLower()}.json";
    }
}
