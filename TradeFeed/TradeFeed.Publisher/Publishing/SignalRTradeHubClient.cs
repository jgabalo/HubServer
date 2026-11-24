using Microsoft.AspNetCore.Http.Connections.Client;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using TradeFeed.Contracts;

namespace TradeFeed.Publisher.Publishing
{
    public interface ITradeHubClient : IAsyncDisposable
    {
        /// <summary>Connects to the hub, retrying until it succeeds or is cancelled.</summary>
        Task StartAsync(CancellationToken cancellationToken);

        /// <summary>Publishes a batch. Batches are dropped (not queued) while disconnected, as they are live data.</summary>
        Task PublishAsync(IReadOnlyList<Trade> trades, CancellationToken cancellationToken);
    }

    /// <summary>
    /// Publishes trades to HubServer's trade hub, authenticating with the publisher key header.
    /// </summary>
    public sealed class SignalRTradeHubClient : ITradeHubClient
    {
        private static readonly TimeSpan MaxRetryDelay = TimeSpan.FromSeconds(30);

        private readonly HubConnection _connection;
        private readonly ILogger<SignalRTradeHubClient> _logger;
        private long _droppedWhileDisconnected;

        /// <param name="options">Publisher settings.</param>
        /// <param name="logger">Logger.</param>
        /// <param name="configureHttp">Optional transport customisation, used by tests to route through an in-memory server.</param>
        public SignalRTradeHubClient(PublisherOptions options, ILogger<SignalRTradeHubClient> logger, Action<HttpConnectionOptions>? configureHttp = null)
        {
            _logger = logger;
            _connection = new HubConnectionBuilder()
                .WithUrl(options.HubUrl, http =>
                {
                    http.Headers[TradeHubContract.PublisherKeyHeader] = options.PublisherKey;
                    configureHttp?.Invoke(http);
                })
                .WithAutomaticReconnect(new ForeverRetryPolicy())
                .Build();

            _connection.Reconnecting += ex =>
            {
                _logger.LogWarning(ex, "Lost connection to trade hub. Reconnecting.");
                return Task.CompletedTask;
            };
            _connection.Reconnected += _ =>
            {
                _logger.LogInformation("Reconnected to trade hub. {Dropped} trades were dropped while disconnected.", Interlocked.Exchange(ref _droppedWhileDisconnected, 0));
                return Task.CompletedTask;
            };
        }

        public HubConnectionState State => _connection.State;

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            TimeSpan delay = TimeSpan.FromSeconds(1);

            while (true)
            {
                try
                {
                    await _connection.StartAsync(cancellationToken);
                    _logger.LogInformation("Connected to trade hub as publisher.");
                    return;
                }
                catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
                {
                    _logger.LogWarning("Could not connect to trade hub ({Message}). Retrying in {Delay}.", ex.Message, delay);
                    await Task.Delay(delay, cancellationToken);
                    delay = TimeSpan.FromTicks(Math.Min(delay.Ticks * 2, MaxRetryDelay.Ticks));
                }
            }
        }

        public async Task PublishAsync(IReadOnlyList<Trade> trades, CancellationToken cancellationToken)
        {
            if (_connection.State != HubConnectionState.Connected)
            {
                Interlocked.Add(ref _droppedWhileDisconnected, trades.Count);
                return;
            }

            try
            {
                await _connection.InvokeAsync(TradeHubContract.PublishTrades, trades, cancellationToken);
            }
            catch (HubException ex)
            {
                // The hub rejected the call, for example an invalid publisher key or trade. Retrying will not help.
                _logger.LogError("Trade hub rejected a batch of {Count} trades: {Message}", trades.Count, ex.Message);
            }
            catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
            {
                Interlocked.Add(ref _droppedWhileDisconnected, trades.Count);
                _logger.LogDebug(ex, "Failed to publish a batch of {Count} trades.", trades.Count);
            }
        }

        public ValueTask DisposeAsync()
        {
            return _connection.DisposeAsync();
        }

        private sealed class ForeverRetryPolicy : IRetryPolicy
        {
            public TimeSpan? NextRetryDelay(RetryContext retryContext)
            {
                double seconds = Math.Min(Math.Pow(2, retryContext.PreviousRetryCount), MaxRetryDelay.TotalSeconds);
                return TimeSpan.FromSeconds(seconds);
            }
        }
    }
}
