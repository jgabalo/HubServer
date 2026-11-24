using System.Net.WebSockets;
using System.Runtime.CompilerServices;
using System.Threading.Channels;
using Microsoft.Extensions.Options;
using TradeFeed.Contracts;

namespace TradeFeed.Publisher.Sources
{
    /// <summary>
    /// Streams live trades from Binance's public market data WebSocket. Read-only market data:
    /// no account, API key or order capability is involved. Reconnects with exponential back-off,
    /// including after Binance's scheduled 24-hour disconnects.
    /// </summary>
    public sealed class BinanceTradeSource : ITradeSource
    {
        private static readonly TimeSpan MaxBackoff = TimeSpan.FromSeconds(30);

        private readonly PublisherOptions _options;
        private readonly ILogger<BinanceTradeSource> _logger;

        public BinanceTradeSource(IOptions<PublisherOptions> options, ILogger<BinanceTradeSource> logger)
        {
            _options = options.Value;
            _logger = logger;
        }

        public string Name => BinanceTradeParser.SourceName;

        public async IAsyncEnumerable<Trade> ReadAsync([EnumeratorCancellation] CancellationToken cancellationToken)
        {
            Channel<Trade> channel = Channel.CreateBounded<Trade>(new BoundedChannelOptions(10_000)
            {
                FullMode = BoundedChannelFullMode.DropOldest,
                SingleReader = true,
                SingleWriter = true,
            });

            Task pump = PumpAsync(channel.Writer, cancellationToken);

            await foreach (Trade trade in channel.Reader.ReadAllAsync(cancellationToken))
            {
                yield return trade;
            }

            await pump;
        }

        public static Uri BuildStreamUri(string baseUrl, IEnumerable<string> symbols)
        {
            string streams = string.Join('/', symbols.Select(s => s.ToLowerInvariant() + "@trade"));
            return new Uri($"{baseUrl.TrimEnd('/')}/stream?streams={streams}");
        }

        private async Task PumpAsync(ChannelWriter<Trade> writer, CancellationToken cancellationToken)
        {
            Uri uri = BuildStreamUri(_options.BinanceStreamUrl, _options.Symbols);
            TimeSpan backoff = TimeSpan.FromSeconds(1);

            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    try
                    {
                        using var socket = new ClientWebSocket();
                        socket.Options.KeepAliveInterval = TimeSpan.FromSeconds(20);

                        _logger.LogInformation("Connecting to {Uri}", uri);
                        await socket.ConnectAsync(uri, cancellationToken);
                        _logger.LogInformation("Connected to Binance trade stream for {Symbols}", string.Join(", ", _options.Symbols));
                        backoff = TimeSpan.FromSeconds(1);

                        await ReceiveAsync(socket, writer, cancellationToken);
                        _logger.LogWarning("Binance closed the stream ({Status}: {Description}). Reconnecting.", socket.CloseStatus, socket.CloseStatusDescription);
                    }
                    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                    {
                        break;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Binance stream error. Reconnecting in {Delay}.", backoff);
                    }

                    await Task.Delay(backoff, cancellationToken);
                    backoff = TimeSpan.FromTicks(Math.Min(backoff.Ticks * 2, MaxBackoff.Ticks));
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // Shutting down.
            }
            finally
            {
                writer.TryComplete();
            }
        }

        private async Task ReceiveAsync(ClientWebSocket socket, ChannelWriter<Trade> writer, CancellationToken cancellationToken)
        {
            byte[] buffer = new byte[16 * 1024];
            using var message = new MemoryStream();

            while (socket.State == WebSocketState.Open)
            {
                WebSocketReceiveResult result = await socket.ReceiveAsync(buffer, cancellationToken);

                if (result.MessageType == WebSocketMessageType.Close)
                {
                    return;
                }

                message.Write(buffer, 0, result.Count);

                if (!result.EndOfMessage)
                {
                    continue;
                }

                if (BinanceTradeParser.TryParse(message.GetBuffer().AsSpan(0, (int)message.Length), out Trade? trade))
                {
                    writer.TryWrite(trade);
                }
                else
                {
                    _logger.LogDebug("Ignored non-trade Binance message.");
                }

                message.SetLength(0);
            }
        }
    }
}
