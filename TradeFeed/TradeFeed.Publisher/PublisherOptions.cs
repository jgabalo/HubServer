using System.ComponentModel.DataAnnotations;

namespace TradeFeed.Publisher
{
    public enum TradeSourceType
    {
        /// <summary>Live trades from Binance's public market data WebSocket (no API key needed).</summary>
        Binance,

        /// <summary>Randomly generated trades, for offline development and demos.</summary>
        Simulated,
    }

    /// <summary>
    /// Configuration section "Publisher".
    /// </summary>
    public class PublisherOptions
    {
        public const string SectionName = "Publisher";

        /// <summary>Full URL of HubServer's trade hub.</summary>
        [Required]
        [Url]
        public string HubUrl { get; set; } = string.Empty;

        /// <summary>
        /// Must match HubServer's TradeFeed:PublisherKey. Set it through user secrets or the
        /// Publisher__PublisherKey environment variable outside Development.
        /// </summary>
        [Required]
        public string PublisherKey { get; set; } = string.Empty;

        public TradeSourceType Source { get; set; } = TradeSourceType.Binance;

        /// <summary>Upper-case symbols such as BTCUSDT. Defaults live in appsettings.json.</summary>
        [MinLength(1)]
        public string[] Symbols { get; set; } = [];

        /// <summary>How often buffered trades are sent to the hub.</summary>
        [Range(50, 5000)]
        public int BatchIntervalMilliseconds { get; set; } = 250;

        /// <summary>Largest batch sent in one hub call (the hub accepts up to 200).</summary>
        [Range(1, 200)]
        public int MaxBatchSize { get; set; } = 100;

        /// <summary>Base WebSocket URL of Binance's public market data streams.</summary>
        [Required]
        public string BinanceStreamUrl { get; set; } = "wss://data-stream.binance.vision";

        /// <summary>Average trades per second the simulated source produces across all symbols.</summary>
        [Range(1, 1000)]
        public int SimulatedTradesPerSecond { get; set; } = 20;
    }
}
