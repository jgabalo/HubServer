using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text.Json;
using TradeFeed.Contracts;

namespace TradeFeed.Publisher.Sources
{
    /// <summary>
    /// Parses Binance "trade" stream events into <see cref="Trade"/>s.
    /// Accepts both raw events and combined-stream envelopes ({"stream": ..., "data": {...}}).
    /// See https://developers.binance.com/docs/binance-spot-api-docs/web-socket-streams#trade-streams
    /// </summary>
    public static class BinanceTradeParser
    {
        public const string SourceName = "Binance";

        public static bool TryParse(ReadOnlySpan<byte> json, [NotNullWhen(true)] out Trade? trade)
        {
            trade = null;

            try
            {
                var reader = new Utf8JsonReader(json);
                using JsonDocument document = JsonDocument.ParseValue(ref reader);
                JsonElement data = document.RootElement;

                if (data.ValueKind == JsonValueKind.Object && data.TryGetProperty("data", out JsonElement inner))
                {
                    data = inner;
                }

                if (data.ValueKind != JsonValueKind.Object
                    || !data.TryGetProperty("e", out JsonElement eventType)
                    || eventType.GetString() != "trade")
                {
                    return false;
                }

                string symbol = data.GetProperty("s").GetString()!;
                long tradeId = data.GetProperty("t").GetInt64();
                decimal price = decimal.Parse(data.GetProperty("p").GetString()!, NumberStyles.Float, CultureInfo.InvariantCulture);
                decimal quantity = decimal.Parse(data.GetProperty("q").GetString()!, NumberStyles.Float, CultureInfo.InvariantCulture);
                long tradeTimeMs = data.GetProperty("T").GetInt64();

                // "m" = buyer is the market maker, so the aggressor (taker) was the seller.
                bool buyerIsMaker = data.GetProperty("m").GetBoolean();

                trade = new Trade(
                    SourceName,
                    symbol.ToUpperInvariant(),
                    tradeId.ToString(CultureInfo.InvariantCulture),
                    price,
                    quantity,
                    buyerIsMaker ? TradeSide.Sell : TradeSide.Buy,
                    DateTimeOffset.FromUnixTimeMilliseconds(tradeTimeMs));
                return true;
            }
            catch (Exception ex) when (ex is JsonException or KeyNotFoundException or FormatException or InvalidOperationException or OverflowException)
            {
                return false;
            }
        }
    }
}
