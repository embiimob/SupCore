namespace SupCore.Wallet
{
    public enum SyncSource
    {
        None,
        LocalNode,
        RemoteApi
    }

    /// <summary>
    /// Blockchain synchronisation / connectivity status for a given coin.
    /// Because SupCore uses API providers rather than running a local full-node,
    /// "sync" here means "are we connected to the API and how fresh is the tip?"
    /// </summary>
    public class SyncStatus
    {
        public CoinType Coin { get; init; }
        public SyncSource Source { get; set; } = SyncSource.None;

        /// <summary>True when the API is reachable and returning valid data.</summary>
        public bool IsOnline { get; set; }

        /// <summary>Best chain height as reported by the API.</summary>
        public int BestHeight { get; set; }
        public int BestHeaderHeight { get; set; }
        public double? VerificationProgress { get; set; }
        public bool? IsInitialBlockDownload { get; set; }

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

        public int BlocksBehind =>
            Math.Max(0, (BestHeaderHeight > 0 ? BestHeaderHeight : BestHeight) - BestHeight);

        public bool IsFullySynced =>
            IsOnline
            && Source == SyncSource.LocalNode
            && !IsInitialBlockDownload.GetValueOrDefault()
            && BlocksBehind <= 1;

        private double BlockMinutes => Coin switch
        {
            CoinType.Bitcoin => 10d,
            CoinType.BitcoinTestnet => 10d,
            CoinType.Litecoin => 2.5d,
            CoinType.Dogecoin => 1d,
            _ => 10d
        };

        public TimeSpan? EstimatedSyncTimeRemaining =>
            Source == SyncSource.LocalNode && BlocksBehind > 1
                ? TimeSpan.FromMinutes(BlocksBehind * BlockMinutes)
                : null;

        /// <summary>Human-readable one-line status string.</summary>
        public string StatusText
        {
            get
            {
                if (!IsOnline) return "Offline";
                if (Source == SyncSource.RemoteApi)
                    return $"API online – remote tip {BestHeight} (local node sync not detected)";

                if (IsFullySynced)
                    return $"Local node online – fully synced (block {BestHeight})";

                string pct = VerificationProgress.HasValue
                    ? $"{VerificationProgress.Value * 100:F2}%"
                    : "unknown";

                if (EstimatedSyncTimeRemaining.HasValue)
                {
                    var eta = EstimatedSyncTimeRemaining.Value;
                    if (eta.TotalHours >= 1)
                        return $"Local node syncing – {pct}, {BlocksBehind:N0} blocks behind (~{eta.TotalHours:F1}h left)";
                    return $"Local node syncing – {pct}, {BlocksBehind:N0} blocks behind (~{Math.Max(1, eta.TotalMinutes):F0}m left)";
                }

                if (!double.IsNaN(DaysBehind))
                    return $"Local node syncing – {pct}, {DaysBehind:F1} day(s) behind";

                return $"Local node online – block {BestHeight}";
            }
        }

        /// <summary>Optional error message when IsOnline == false.</summary>
        public string? ErrorMessage { get; set; }
    }
}
