using System.Runtime.CompilerServices;
using TradeFeed.Contracts;
using TradeFeed.Publisher.Publishing;

namespace TradeFeed.Publisher.Tests
{
    public class TradeBatcherTests
    {
        [Fact]
        public async Task PublishesEveryTrade_InOrder_WithinMaxBatchSize()
        {
            var batches = new List<IReadOnlyList<Trade>>();

            await TradeBatcher.RunAsync(
                Trades(25),
                (batch, _) => { batches.Add(batch); return Task.CompletedTask; },
                maxBatchSize: 10,
                interval: TimeSpan.FromMilliseconds(20),
                CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(10));

            Assert.All(batches, b => Assert.InRange(b.Count, 1, 10));
            Assert.Equal(Enumerable.Range(0, 25).Select(i => i.ToString()), batches.SelectMany(b => b).Select(t => t.TradeId));
        }

        [Fact]
        public async Task Stops_WhenCancelled()
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));

            Task run = TradeBatcher.RunAsync(
                Endless(),
                (_, _) => Task.CompletedTask,
                maxBatchSize: 10,
                interval: TimeSpan.FromMilliseconds(20),
                cts.Token);

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => run.WaitAsync(TimeSpan.FromSeconds(10)));
        }

        [Fact]
        public async Task SurfacesSourceFailures()
        {
            await Assert.ThrowsAsync<InvalidOperationException>(() => TradeBatcher.RunAsync(
                Failing(),
                (_, _) => Task.CompletedTask,
                maxBatchSize: 10,
                interval: TimeSpan.FromMilliseconds(20),
                CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(10)));
        }

        private static async IAsyncEnumerable<Trade> Trades(int count)
        {
            for (int i = 0; i < count; i++)
            {
                if (i % 7 == 0)
                {
                    await Task.Delay(5);
                }

                yield return new Trade("Test", "BTCUSDT", i.ToString(), 1m, 1m, TradeSide.Buy, DateTimeOffset.UnixEpoch);
            }
        }

        private static async IAsyncEnumerable<Trade> Endless([EnumeratorCancellation] CancellationToken ct = default)
        {
            while (true)
            {
                await Task.Delay(1, ct);
                yield return new Trade("Test", "BTCUSDT", "1", 1m, 1m, TradeSide.Buy, DateTimeOffset.UnixEpoch);
            }
        }

        private static async IAsyncEnumerable<Trade> Failing()
        {
            await Task.Yield();
            yield return new Trade("Test", "BTCUSDT", "1", 1m, 1m, TradeSide.Buy, DateTimeOffset.UnixEpoch);
            throw new InvalidOperationException("source failed");
        }
    }
}
