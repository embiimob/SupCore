namespace SupCore.Wallet
{
    /// <summary>
    /// Blockchain synchronisation / connectivity status for a given coin.
    /// Because SupCore uses API providers rather than running a local full-node,
    /// "sync" here means "are we connected to the API and how fresh is the tip?"
    /// </summary>
    public class SyncStatus
    {
        public CoinType Coin { get; init; }

        /// <summary>True when the API is reachable and returning valid data.</summary>
        public bool IsOnline { get; set; }

        /// <summary>Best chain height as reported by the API.</summary>
        public int BestHeight { get; set; }

        /// <summary>UTC timestamp of the best block.</summary>
        public DateTime? BestBlockTime { get; set; }

        /// <summary>
        /// How many days behind the chain tip the client currently is.
        /// Zero when fully current; negative values mean we have more recent data
        /// than expected (clamp to 0 for display).
        /// </summary>
        public double DaysBehind =>
            BestBlockTime.HasValue
                ? Math.Max(0, (DateTime.UtcNow - BestBlockTime.Value).TotalDays)
                : double.NaN;

        /// <summary>Human-readable one-line status string.</summary>
        public string StatusText
        {
            get
            {
                if (!IsOnline) return "Offline";
                if (double.IsNaN(DaysBehind)) return $"Online – block {BestHeight}";
                if (DaysBehind < 0.01) return $"Online – fully synced (block {BestHeight})";
                return $"Online – {DaysBehind:F1} day(s) behind (block {BestHeight})";
            }
        }

        /// <summary>Optional error message when IsOnline == false.</summary>
        public string? ErrorMessage { get; set; }
    }
}
