namespace TradeFeed.Contracts
{
    /// <summary>
    /// Wire contract for the trade hub, shared by the server and the publisher.
    /// The JavaScript display app mirrors these names in trade-display/src/hub.js.
    /// </summary>
    public static class TradeHubContract
    {
        /// <summary>Hub URL path on HubServer.</summary>
        public const string Route = "/signalr/tradeHub";

        /// <summary>HTTP header a publisher sends to prove it may publish.</summary>
        public const string PublisherKeyHeader = "X-Publisher-Key";

        /// <summary>Largest batch the hub accepts in one <see cref="PublishTrades"/> call.</summary>
        public const int MaxBatchSize = 200;

        // Server methods (client -> hub)

        /// <summary>Publisher only. Argument: Trade[]. Rejected unless the connection presented a valid publisher key.</summary>
        public const string PublishTrades = "PublishTrades";

        /// <summary>Returns the most recent trades (newest last). Argument: int max.</summary>
        public const string GetRecentTrades = "GetRecentTrades";

        /// <summary>Returns the current <see cref="FeedStatus"/>.</summary>
        public const string GetFeedStatus = "GetFeedStatus";

        // Client callbacks (hub -> viewers)

        /// <summary>Pushed to viewers with each published batch. Argument: Trade[].</summary>
        public const string TradesReceived = "tradesReceived";

        /// <summary>Pushed to viewers when a publisher connects or disconnects. Argument: FeedStatus.</summary>
        public const string FeedStatusChanged = "feedStatusChanged";
    }
}
