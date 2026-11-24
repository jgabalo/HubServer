namespace WebCamHandler.Trading
{
    /// <summary>
    /// Configuration section "TradeFeed".
    /// </summary>
    public class TradeFeedOptions
    {
        public const string SectionName = "TradeFeed";

        /// <summary>
        /// Shared secret a publisher must send in the X-Publisher-Key header.
        /// When empty, publishing is disabled. Set it through an environment variable
        /// (TradeFeed__PublisherKey) or user secrets outside Development.
        /// </summary>
        public string PublisherKey { get; set; } = string.Empty;

        /// <summary>
        /// How many recent trades the hub keeps for viewers that connect mid-stream.
        /// </summary>
        public int RecentTradeCapacity { get; set; } = 500;
    }
}
