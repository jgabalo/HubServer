using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Logging.Abstractions;
using TradeFeed.Contracts;
using TradeFeed.Publisher.Publishing;
using TradeFeed.Publisher.Sources;

namespace TradeFeed.Publisher.Tests
{
    /// <summary>
    /// Runs the real publisher pipeline (simulated source, batcher, SignalR client) against an
    /// in-memory HubServer and checks that a read-only viewer receives the trades.
    /// </summary>
    public class PublisherEndToEndTests : IAsyncLifetime
    {
        private const string Key = "e2e-publisher-key";

        private readonly WebApplicationFactory<Program> _server = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(b => b.UseSetting("TradeFeed:PublisherKey", Key));

        public Task InitializeAsync() => Task.CompletedTask;

        public async Task DisposeAsync() => await _server.DisposeAsync();

        [Fact]
        public async Task Viewer_ReceivesTrades_RelayedByPublisher()
        {
            await using HubConnection viewer = await ConnectViewerAsync();
            var received = new TaskCompletionSource<Trade[]>(TaskCreationOptions.RunContinuationsAsynchronously);
            viewer.On<Trade[]>(TradeHubContract.TradesReceived, trades => received.TrySetResult(trades));

            var options = new PublisherOptions
            {
                HubUrl = new Uri(_server.Server.BaseAddress, TradeHubContract.Route).ToString(),
                PublisherKey = Key,
                Symbols = ["BTCUSDT", "ETHUSDT"],
                SimulatedTradesPerSecond = 200,
            };
            await using var client = new SignalRTradeHubClient(options, NullLogger<SignalRTradeHubClient>.Instance, InMemory);
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));

            await client.StartAsync(cts.Token);
            Task publishing = TradeBatcher.RunAsync(
                new SimulatedTradeSource(options, new Random(1)).ReadAsync(cts.Token),
                client.PublishAsync,
                maxBatchSize: 50,
                interval: TimeSpan.FromMilliseconds(50),
                cts.Token);

            Trade[] batch = await received.Task.WaitAsync(TimeSpan.FromSeconds(10));
            await cts.CancelAsync();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => publishing);

            Assert.NotEmpty(batch);
            Assert.All(batch, t => Assert.Contains(t.Symbol, options.Symbols));
        }

        [Fact]
        public async Task Publisher_WithWrongKey_DoesNotReachViewers()
        {
            await using HubConnection viewer = await ConnectViewerAsync();
            var received = new TaskCompletionSource<Trade[]>(TaskCreationOptions.RunContinuationsAsynchronously);
            viewer.On<Trade[]>(TradeHubContract.TradesReceived, trades => received.TrySetResult(trades));

            var options = new PublisherOptions
            {
                HubUrl = new Uri(_server.Server.BaseAddress, TradeHubContract.Route).ToString(),
                PublisherKey = "wrong",
            };
            await using var client = new SignalRTradeHubClient(options, NullLogger<SignalRTradeHubClient>.Instance, InMemory);
            await client.StartAsync(CancellationToken.None);

            await client.PublishAsync(new[] { new Trade("Test", "BTCUSDT", "1", 1m, 1m, TradeSide.Buy, DateTimeOffset.UtcNow) }, CancellationToken.None);

            Task winner = await Task.WhenAny(received.Task, Task.Delay(500));
            Assert.NotSame(received.Task, winner);
        }

        private async Task<HubConnection> ConnectViewerAsync()
        {
            HubConnection viewer = new HubConnectionBuilder()
                .WithUrl(new Uri(_server.Server.BaseAddress, TradeHubContract.Route), InMemory)
                .Build();
            await viewer.StartAsync();
            return viewer;
        }

        private void InMemory(Microsoft.AspNetCore.Http.Connections.Client.HttpConnectionOptions options)
        {
            options.HttpMessageHandlerFactory = _ => _server.Server.CreateHandler();
            options.Transports = HttpTransportType.LongPolling;
        }
    }
}
