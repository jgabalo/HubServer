using System.Text;
using TradeFeed.Contracts;
using TradeFeed.Publisher.Sources;

namespace TradeFeed.Publisher.Tests
{
    public class BinanceTradeParserTests
    {
        // Shape documented at https://developers.binance.com/docs/binance-spot-api-docs/web-socket-streams#trade-streams
        private const string CombinedEnvelope = """
            {"stream":"btcusdt@trade","data":{"e":"trade","E":1764032523100,"s":"BTCUSDT","t":5123456789,"p":"95123.45000000","q":"0.00125000","T":1764032523099,"m":true,"M":true}}
            """;

        [Fact]
        public void Parses_CombinedStreamEnvelope()
        {
            Assert.True(BinanceTradeParser.TryParse(Bytes(CombinedEnvelope), out Trade? trade));

            Assert.Equal("Binance", trade.Source);
            Assert.Equal("BTCUSDT", trade.Symbol);
            Assert.Equal("5123456789", trade.TradeId);
            Assert.Equal(95123.45m, trade.Price);
            Assert.Equal(0.00125m, trade.Quantity);
            Assert.Equal(DateTimeOffset.FromUnixTimeMilliseconds(1764032523099), trade.Timestamp);
        }

        [Theory]
        [InlineData(true, TradeSide.Sell)]
        [InlineData(false, TradeSide.Buy)]
        public void MapsBuyerIsMaker_ToAggressorSide(bool buyerIsMaker, TradeSide expected)
        {
            string json = $$"""{"e":"trade","s":"ETHUSDT","t":1,"p":"3500.1","q":"2","T":0,"m":{{buyerIsMaker.ToString().ToLowerInvariant()}}}""";

            Assert.True(BinanceTradeParser.TryParse(Bytes(json), out Trade? trade));
            Assert.Equal(expected, trade.Side);
        }

        [Theory]
        [InlineData("""{"result":null,"id":1}""")]
        [InlineData("""{"stream":"btcusdt@aggTrade","data":{"e":"aggTrade","s":"BTCUSDT"}}""")]
        [InlineData("""{"e":"trade","s":"BTCUSDT"}""")]
        [InlineData("""{"e":"trade","s":"BTCUSDT","t":1,"p":"not-a-number","q":"1","T":0,"m":false}""")]
        [InlineData("not json")]
        [InlineData("[]")]
        public void Rejects_NonTradeOrMalformedMessages(string json)
        {
            Assert.False(BinanceTradeParser.TryParse(Bytes(json), out _));
        }

        [Fact]
        public void BuildStreamUri_CombinesLowerCaseTradeStreams()
        {
            Uri uri = BinanceTradeSource.BuildStreamUri("wss://data-stream.binance.vision/", new[] { "BTCUSDT", "EthUsdt" });

            Assert.Equal("wss://data-stream.binance.vision/stream?streams=btcusdt@trade/ethusdt@trade", uri.ToString());
        }

        private static byte[] Bytes(string s) => Encoding.UTF8.GetBytes(s);
    }
}
