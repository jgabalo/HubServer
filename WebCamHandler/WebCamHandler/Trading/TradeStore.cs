using Microsoft.Extensions.Options;
using TradeFeed.Contracts;

namespace WebCamHandler.Trading
{
    /// <summary>
    /// Process-wide, thread-safe buffer of recent trades and publisher presence.
    /// Registered as a DI singleton.
    /// </summary>
    public class TradeStore
    {
        private readonly object _lock = new object();
        private readonly Queue<Trade> _recent;
        private readonly int _capacity;
        private int _activePublishers;

        public TradeStore(IOptions<TradeFeedOptions> options)
        {
            _capacity = Math.Max(1, options.Value.RecentTradeCapacity);
            _recent = new Queue<Trade>(_capacity);
        }

        /// <summary>
        /// Appends trades, discarding the oldest once capacity is reached.
        /// </summary>
        public void Add(IEnumerable<Trade> trades)
        {
            lock (_lock)
            {
                foreach (Trade trade in trades)
                {
                    if (_recent.Count == _capacity)
                    {
                        _recent.Dequeue();
                    }

                    _recent.Enqueue(trade);
                }
            }
        }

        /// <summary>
        /// Returns up to <paramref name="max"/> of the most recent trades, oldest first.
        /// </summary>
        public IReadOnlyList<Trade> GetRecent(int max)
        {
            lock (_lock)
            {
                int take = Math.Clamp(max, 0, _recent.Count);
                return _recent.Skip(_recent.Count - take).ToArray();
            }
        }

        public FeedStatus GetFeedStatus()
        {
            return new FeedStatus(Volatile.Read(ref _activePublishers));
        }

        public FeedStatus PublisherConnected()
        {
            return new FeedStatus(Interlocked.Increment(ref _activePublishers));
        }

        public FeedStatus PublisherDisconnected()
        {
            return new FeedStatus(Interlocked.Decrement(ref _activePublishers));
        }
    }
}
