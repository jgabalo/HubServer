using TradeFeed.Contracts;

namespace TradeFeed.Publisher.Sources
{
    /// <summary>
    /// A stream of executed trades from a market data provider.
    /// Implementations handle their own reconnection and only finish when cancelled.
    /// </summary>
    public interface ITradeSource
    {
        string Name { get; }

        IAsyncEnumerable<Trade> ReadAsync(CancellationToken cancellationToken);
    }
}
