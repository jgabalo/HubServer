using TradeFeed.Contracts;
using TradeFeed.Publisher.Sources;

namespace TradeFeed.Publisher.Tests
{
    public class SimulatedTradeSourceTests
    {
        [Fact]
        public async Task ProducesValidTrades_ForConfiguredSymbols()
        {
            var options = new PublisherOptions { Symbols = ["BTCUSDT", "XYZUSDT"], SimulatedTradesPerSecond = 1000 };
            var source = new SimulatedTradeSource(options, new Random(42));
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));

            var trades = new List<Trade>();
            await foreach (Trade trade in source.ReadAsync(cts.Token))
            {
                trades.Add(trade);
                if (trades.Count == 50)
                {
                    break;
                }
            }

            Assert.All(trades, t =>
            {
                Assert.Contains(t.Symbol, options.Symbols);
                Assert.True(t.Price > 0);
                Assert.True(t.Quantity > 0);
                Assert.Equal(SimulatedTradeSource.SourceName, t.Source);
            });
            Assert.Equal(trades.Count, trades.Select(t => t.TradeId).Distinct().Count());
        }
    }
}
