using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Options;
using TradeFeed.Contracts;

namespace WebCamHandler.Trading
{
    /// <summary>
    /// Relays trades from authorised publishers to read-only viewers.
    /// Publishers authenticate with the X-Publisher-Key header; every other connection is a viewer
    /// and can only read (GetRecentTrades, GetFeedStatus) and receive pushes.
    /// </summary>
    public partial class TradeHub : Hub
    {
        internal const string ViewersGroup = "viewers";
        private const string IsPublisherKey = "isPublisher";

        private readonly TradeStore _store;
        private readonly TradeFeedOptions _options;
        private readonly ILogger<TradeHub> _logger;

        public TradeHub(TradeStore store, IOptions<TradeFeedOptions> options, ILogger<TradeHub> logger)
        {
            _store = store;
            _options = options.Value;
            _logger = logger;
        }

        public override async Task OnConnectedAsync()
        {
            if (IsValidPublisherKey(Context.GetHttpContext()?.Request.Headers[TradeHubContract.PublisherKeyHeader].ToString()))
            {
                Context.Items[IsPublisherKey] = true;
                FeedStatus status = _store.PublisherConnected();
                _logger.LogInformation("Trade publisher {ConnectionId} connected. Active publishers: {Count}", Context.ConnectionId, status.ActivePublishers);
                await Clients.Group(ViewersGroup).SendAsync(TradeHubContract.FeedStatusChanged, status);
            }
            else
            {
                await Groups.AddToGroupAsync(Context.ConnectionId, ViewersGroup);
            }

            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            if (IsPublisher)
            {
                FeedStatus status = _store.PublisherDisconnected();
                _logger.LogInformation("Trade publisher {ConnectionId} disconnected. Active publishers: {Count}", Context.ConnectionId, status.ActivePublishers);
                await Clients.Group(ViewersGroup).SendAsync(TradeHubContract.FeedStatusChanged, status);
            }

            await base.OnDisconnectedAsync(exception);
        }

        /// <summary>
        /// Publisher only: stores a batch of trades and pushes it to all viewers.
        /// </summary>
        public async Task PublishTrades(Trade[] trades)
        {
            if (!IsPublisher)
            {
                throw new HubException("This connection is not authorised to publish trades.");
            }

            if (trades is null || trades.Length == 0)
            {
                return;
            }

            if (trades.Length > TradeHubContract.MaxBatchSize)
            {
                throw new HubException($"Batch too large: {trades.Length} trades (maximum {TradeHubContract.MaxBatchSize}).");
            }

            foreach (Trade trade in trades)
            {
                string? error = Validate(trade);
                if (error is not null)
                {
                    throw new HubException($"Invalid trade: {error}");
                }
            }

            _store.Add(trades);
            await Clients.Group(ViewersGroup).SendAsync(TradeHubContract.TradesReceived, trades);
        }

        /// <summary>
        /// Returns up to <paramref name="max"/> recent trades (oldest first) so a new viewer can fill its screen.
        /// </summary>
        public IReadOnlyList<Trade> GetRecentTrades(int max)
        {
            return _store.GetRecent(Math.Clamp(max, 0, _options.RecentTradeCapacity));
        }

        public FeedStatus GetFeedStatus()
        {
            return _store.GetFeedStatus();
        }

        private bool IsPublisher => Context.Items.TryGetValue(IsPublisherKey, out object? value) && value is true;

        private bool IsValidPublisherKey(string? presented)
        {
            if (string.IsNullOrEmpty(_options.PublisherKey) || string.IsNullOrEmpty(presented))
            {
                return false;
            }

            return CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(presented),
                Encoding.UTF8.GetBytes(_options.PublisherKey));
        }

        internal static string? Validate(Trade? trade)
        {
            if (trade is null)
            {
                return "trade is null.";
            }

            if (string.IsNullOrWhiteSpace(trade.Symbol) || !SymbolPattern().IsMatch(trade.Symbol))
            {
                return $"symbol '{trade.Symbol}' must be 1-20 upper-case letters, digits, '-', '/' or '_'.";
            }

            if (string.IsNullOrWhiteSpace(trade.Source) || trade.Source.Length > 32)
            {
                return "source must be 1-32 characters.";
            }

            if (string.IsNullOrWhiteSpace(trade.TradeId) || trade.TradeId.Length > 64)
            {
                return "tradeId must be 1-64 characters.";
            }

            if (trade.Price <= 0 || trade.Quantity <= 0)
            {
                return "price and quantity must be positive.";
            }

            if (!Enum.IsDefined(trade.Side))
            {
                return "side must be Buy or Sell.";
            }

            return null;
        }

        [GeneratedRegex("^[A-Z0-9/_-]{1,20}$")]
        private static partial Regex SymbolPattern();
    }
}
