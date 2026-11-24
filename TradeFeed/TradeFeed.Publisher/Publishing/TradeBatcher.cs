using System.Threading.Channels;
using TradeFeed.Contracts;

namespace TradeFeed.Publisher.Publishing
{
    /// <summary>
    /// Groups a high-frequency trade stream into batches so the hub receives a few calls per
    /// second instead of one per trade.
    /// </summary>
    public static class TradeBatcher
    {
        /// <summary>
        /// Reads <paramref name="source"/> and, every <paramref name="interval"/>, publishes everything
        /// buffered in chunks of at most <paramref name="maxBatchSize"/>. Completes when the source
        /// ends (after a final flush) or throws when cancelled.
        /// </summary>
        public static async Task RunAsync(
            IAsyncEnumerable<Trade> source,
            Func<IReadOnlyList<Trade>, CancellationToken, Task> publish,
            int maxBatchSize,
            TimeSpan interval,
            CancellationToken cancellationToken)
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(maxBatchSize, 1);

            // Bounded so a stalled hub cannot exhaust memory; for live data the oldest trades are the least useful.
            Channel<Trade> buffer = Channel.CreateBounded<Trade>(new BoundedChannelOptions(10_000)
            {
                FullMode = BoundedChannelFullMode.DropOldest,
                SingleReader = true,
                SingleWriter = true,
            });

            Task pump = Task.Run(async () =>
            {
                try
                {
                    await foreach (Trade trade in source.WithCancellation(cancellationToken))
                    {
                        buffer.Writer.TryWrite(trade);
                    }

                    buffer.Writer.TryComplete();
                }
                catch (Exception ex)
                {
                    buffer.Writer.TryComplete(ex);
                }
            }, CancellationToken.None);

            using var timer = new PeriodicTimer(interval);
            var batch = new List<Trade>(maxBatchSize);

            while (await timer.WaitForNextTickAsync(cancellationToken))
            {
                while (buffer.Reader.TryRead(out Trade? trade))
                {
                    batch.Add(trade);
                    if (batch.Count == maxBatchSize)
                    {
                        await publish(batch.ToArray(), cancellationToken);
                        batch.Clear();
                    }
                }

                if (batch.Count > 0)
                {
                    await publish(batch.ToArray(), cancellationToken);
                    batch.Clear();
                }

                if (buffer.Reader.Completion.IsCompleted)
                {
                    break;
                }
            }

            await pump;
            await buffer.Reader.Completion;
        }
    }
}
