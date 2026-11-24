using System.Diagnostics;
using Microsoft.Extensions.Options;
using TradeFeed.Contracts;
using TradeFeed.Publisher.Publishing;
using TradeFeed.Publisher.Sources;

namespace TradeFeed.Publisher
{
    /// <summary>
    /// Reads trades from the configured source and relays them to HubServer in batches.
    /// </summary>
    public sealed class TradePublisherWorker : BackgroundService
    {
        private static readonly TimeSpan StatsInterval = TimeSpan.FromSeconds(30);

        private readonly ITradeSource _source;
        private readonly ITradeHubClient _hub;
        private readonly PublisherOptions _options;
        private readonly ILogger<TradePublisherWorker> _logger;

        public TradePublisherWorker(ITradeSource source, ITradeHubClient hub, IOptions<PublisherOptions> options, ILogger<TradePublisherWorker> logger)
        {
            _source = source;
            _hub = hub;
            _options = options.Value;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Relaying {Source} trades for {Symbols} to {HubUrl}", _source.Name, string.Join(", ", _options.Symbols), _options.HubUrl);

            await _hub.StartAsync(stoppingToken);

            long published = 0;
            var sinceLastLog = Stopwatch.StartNew();

            async Task PublishAndCount(IReadOnlyList<Trade> batch, CancellationToken ct)
            {
                await _hub.PublishAsync(batch, ct);
                published += batch.Count;

                if (sinceLastLog.Elapsed >= StatsInterval)
                {
                    _logger.LogInformation("Relayed {Count} trades in the last {Seconds:F0}s.", published, sinceLastLog.Elapsed.TotalSeconds);
                    published = 0;
                    sinceLastLog.Restart();
                }
            }

            try
            {
                await TradeBatcher.RunAsync(
                    _source.ReadAsync(stoppingToken),
                    PublishAndCount,
                    _options.MaxBatchSize,
                    TimeSpan.FromMilliseconds(_options.BatchIntervalMilliseconds),
                    stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                // Normal shutdown.
            }
        }

        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            await base.StopAsync(cancellationToken);
            await _hub.DisposeAsync();
        }
    }
}
