using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using TradeFeed.Contracts;
using static HubServer.Tests.HubTestHelpers;

namespace HubServer.Tests
{
    /// <summary>
    /// Contract and security tests for the trade hub. Viewers (the trade display app) must only
    /// ever be able to read; only connections presenting the publisher key may publish.
    /// </summary>
    public class TradeHubTests : IAsyncLifetime
    {
        private const string PublisherKey = "test-publisher-key";

        private readonly WebApplicationFactory<Program> _factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(b => b.UseSetting("TradeFeed:PublisherKey", PublisherKey));

        private HubConnection _viewer = null!;

        public async Task InitializeAsync()
        {
            _viewer = await ConnectAsync(_factory, TradeHubContract.Route);
        }

        public async Task DisposeAsync()
        {
            await _viewer.DisposeAsync();
            await _factory.DisposeAsync();
        }

        [Fact]
        public async Task Publisher_WithValidKey_BroadcastsToViewers()
        {
            await using HubConnection publisher = await ConnectPublisherAsync(PublisherKey);
            Task<Trade[]> received = Expect<Trade[]>(_viewer, TradeHubContract.TradesReceived);
            Trade trade = NewTrade("BTCUSDT", 95_123.45m, TradeSide.Buy);

            await publisher.InvokeAsync(TradeHubContract.PublishTrades, new[] { trade });

            Assert.Equal(trade, Assert.Single(await received));
        }

        [Fact]
        public async Task Viewer_CannotPublish()
        {
            var ex = await Assert.ThrowsAsync<HubException>(() =>
                _viewer.InvokeAsync(TradeHubContract.PublishTrades, new[] { NewTrade("BTCUSDT", 1m, TradeSide.Buy) }));

            Assert.Contains("not authorised", ex.Message);
        }

        [Fact]
        public async Task Publisher_WithWrongKey_CannotPublish()
        {
            await using HubConnection impostor = await ConnectPublisherAsync("wrong-key");

            await Assert.ThrowsAsync<HubException>(() =>
                impostor.InvokeAsync(TradeHubContract.PublishTrades, new[] { NewTrade("BTCUSDT", 1m, TradeSide.Buy) }));
        }

        [Fact]
        public async Task Publishing_IsDisabled_WhenNoKeyConfigured()
        {
            await using WebApplicationFactory<Program> factory = new WebApplicationFactory<Program>()
                .WithWebHostBuilder(b => b.UseSetting("TradeFeed:PublisherKey", string.Empty));
            await using HubConnection publisher = await ConnectAsync(factory, TradeHubContract.Route,
                new Dictionary<string, string> { [TradeHubContract.PublisherKeyHeader] = string.Empty });

            await Assert.ThrowsAsync<HubException>(() =>
                publisher.InvokeAsync(TradeHubContract.PublishTrades, new[] { NewTrade("BTCUSDT", 1m, TradeSide.Buy) }));
        }

        [Fact]
        public async Task Publisher_DoesNotReceiveItsOwnBroadcasts()
        {
            await using HubConnection publisher = await ConnectPublisherAsync(PublisherKey);
            Task<Trade[]> echoed = Expect<Trade[]>(publisher, TradeHubContract.TradesReceived);

            await publisher.InvokeAsync(TradeHubContract.PublishTrades, new[] { NewTrade("BTCUSDT", 1m, TradeSide.Buy) });

            await AssertNotReceived(echoed);
        }

        [Fact]
        public async Task TradesReceived_UsesCamelCaseAndStringSides()
        {
            await using HubConnection publisher = await ConnectPublisherAsync(PublisherKey);
            Task<JsonElement> received = Expect<JsonElement>(_viewer, TradeHubContract.TradesReceived);

            await publisher.InvokeAsync(TradeHubContract.PublishTrades, new[] { NewTrade("ETHUSDT", 3_500.5m, TradeSide.Sell) });

            JsonElement trade = (await received)[0];
            Assert.Equal("ETHUSDT", trade.GetProperty("symbol").GetString());
            Assert.Equal("Sell", trade.GetProperty("side").GetString());
            Assert.Equal(3_500.5m, trade.GetProperty("price").GetDecimal());
            Assert.True(trade.TryGetProperty("timestamp", out _));
        }

        [Fact]
        public async Task GetRecentTrades_ReturnsNewestTradesOldestFirst()
        {
            await using HubConnection publisher = await ConnectPublisherAsync(PublisherKey);
            await publisher.InvokeAsync(TradeHubContract.PublishTrades, new[]
            {
                NewTrade("BTCUSDT", 1m, TradeSide.Buy, "1"),
                NewTrade("BTCUSDT", 2m, TradeSide.Buy, "2"),
                NewTrade("BTCUSDT", 3m, TradeSide.Buy, "3"),
            });

            Trade[] recent = await _viewer.InvokeAsync<Trade[]>(TradeHubContract.GetRecentTrades, 2);

            Assert.Equal(new[] { "2", "3" }, recent.Select(t => t.TradeId));
        }

        [Fact]
        public async Task FeedStatus_TracksPublisherConnections()
        {
            Assert.Equal(0, (await _viewer.InvokeAsync<FeedStatus>(TradeHubContract.GetFeedStatus)).ActivePublishers);

            Task<FeedStatus> online = Expect<FeedStatus>(_viewer, TradeHubContract.FeedStatusChanged);
            HubConnection publisher = await ConnectPublisherAsync(PublisherKey);
            Assert.Equal(1, (await online).ActivePublishers);

            var offline = new TaskCompletionSource<FeedStatus>(TaskCreationOptions.RunContinuationsAsynchronously);
            _viewer.On<FeedStatus>(TradeHubContract.FeedStatusChanged, s =>
            {
                if (s.ActivePublishers == 0)
                {
                    offline.TrySetResult(s);
                }
            });
            await publisher.DisposeAsync();

            Assert.Equal(0, (await offline.Task.WaitAsync(HubTestHelpers.Timeout)).ActivePublishers);
        }

        [Fact]
        public async Task PublishTrades_RejectsInvalidTrades()
        {
            await using HubConnection publisher = await ConnectPublisherAsync(PublisherKey);

            await Assert.ThrowsAsync<HubException>(() => publisher.InvokeAsync(TradeHubContract.PublishTrades,
                new[] { NewTrade("BTCUSDT", -1m, TradeSide.Buy) }));
            await Assert.ThrowsAsync<HubException>(() => publisher.InvokeAsync(TradeHubContract.PublishTrades,
                new[] { NewTrade("<script>", 1m, TradeSide.Buy) }));
        }

        [Fact]
        public async Task PublishTrades_RejectsOversizedBatch()
        {
            await using HubConnection publisher = await ConnectPublisherAsync(PublisherKey);
            Trade[] batch = Enumerable.Range(0, TradeHubContract.MaxBatchSize + 1)
                .Select(i => NewTrade("BTCUSDT", 1m, TradeSide.Buy, i.ToString()))
                .ToArray();

            var ex = await Assert.ThrowsAsync<HubException>(() => publisher.InvokeAsync(TradeHubContract.PublishTrades, batch));
            Assert.Contains("too large", ex.Message);
        }

        private Task<HubConnection> ConnectPublisherAsync(string key)
        {
            return ConnectAsync(_factory, TradeHubContract.Route,
                new Dictionary<string, string> { [TradeHubContract.PublisherKeyHeader] = key });
        }

        private static Trade NewTrade(string symbol, decimal price, TradeSide side, string id = "1")
        {
            return new Trade("Test", symbol, id, price, 0.5m, side, new DateTimeOffset(2026, 11, 25, 1, 2, 3, TimeSpan.Zero));
        }
    }
}
