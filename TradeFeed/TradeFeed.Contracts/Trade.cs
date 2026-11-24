using System.Text.Json.Serialization;

namespace TradeFeed.Contracts
{
    /// <summary>
    /// The aggressor side of a trade: whether the taker bought or sold.
    /// Serialised as "Buy" / "Sell" so JavaScript clients receive readable values.
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter<TradeSide>))]
    public enum TradeSide
    {
        Buy,
        Sell,
    }

    /// <summary>
    /// A single executed trade as reported by a market data source.
    /// </summary>
    /// <param name="Source">Where the trade came from, for example "Binance" or "Simulated".</param>
    /// <param name="Symbol">Instrument symbol in upper case, for example "BTCUSDT".</param>
    /// <param name="TradeId">Source-specific trade identifier.</param>
    /// <param name="Price">Execution price in the quote currency.</param>
    /// <param name="Quantity">Executed quantity in the base currency.</param>
    /// <param name="Side">Aggressor side.</param>
    /// <param name="Timestamp">Execution time reported by the source (UTC).</param>
    public sealed record Trade(
        string Source,
        string Symbol,
        string TradeId,
        decimal Price,
        decimal Quantity,
        TradeSide Side,
        DateTimeOffset Timestamp);

    /// <summary>
    /// Whether any publisher is currently feeding trades into the hub.
    /// </summary>
    /// <param name="ActivePublishers">Number of connected, authorised publishers.</param>
    public sealed record FeedStatus(int ActivePublishers);
}
