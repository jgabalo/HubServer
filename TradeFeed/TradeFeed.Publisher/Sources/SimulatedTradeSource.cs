using System.Globalization;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.Options;
using TradeFeed.Contracts;

namespace TradeFeed.Publisher.Sources
{
    /// <summary>
    /// Generates plausible random-walk trades. For offline development, demos and tests only.
    /// </summary>
    public sealed class SimulatedTradeSource : ITradeSource
    {
        public const string SourceName = "Simulated";

        private static readonly Dictionary<string, decimal> StartingPrices = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase)
        {
            ["BTCUSDT"] = 95_000m,
            ["ETHUSDT"] = 3_500m,
            ["SOLUSDT"] = 180m,
        };

        private readonly PublisherOptions _options;
        private readonly Random _random;

        public SimulatedTradeSource(IOptions<PublisherOptions> options)
            : this(options.Value, Random.Shared)
        {
        }

        public SimulatedTradeSource(PublisherOptions options, Random random)
        {
            _options = options;
            _random = random;
        }

        public string Name => SourceName;

        public async IAsyncEnumerable<Trade> ReadAsync([EnumeratorCancellation] CancellationToken cancellationToken)
        {
            string[] symbols = _options.Symbols.Select(s => s.ToUpperInvariant()).ToArray();
            Dictionary<string, decimal> prices = symbols.ToDictionary(s => s, s => StartingPrices.GetValueOrDefault(s, 100m));
            double meanDelayMs = 1000.0 / _options.SimulatedTradesPerSecond;
            long tradeId = 0;

            while (!cancellationToken.IsCancellationRequested)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(meanDelayMs * (0.5 + _random.NextDouble())), cancellationToken);

                string symbol = symbols[_random.Next(symbols.Length)];
                decimal price = prices[symbol];

                // Random walk of up to +/-0.05% per trade.
                decimal move = price * (decimal)((_random.NextDouble() - 0.5) * 0.001);
                decimal next = Math.Max(0.01m, Math.Round(price + move, price >= 1000 ? 2 : 4));
                prices[symbol] = next;

                decimal quantity = Math.Round((decimal)(_random.NextDouble() * (double)(1000m / next)) + 0.0001m, 4);

                yield return new Trade(
                    SourceName,
                    symbol,
                    (++tradeId).ToString(CultureInfo.InvariantCulture),
                    next,
                    quantity,
                    move >= 0 ? TradeSide.Buy : TradeSide.Sell,
                    DateTimeOffset.UtcNow);
            }
        }
    }
}
