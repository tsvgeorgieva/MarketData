using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MarketData.Infrastructure.Binance.Models;

/// <summary>
/// Reads <c>[openTime, open, high, low, close, volume, closeTime, ...]</c>: the open time is a number, the prices and volume are strings,
/// and the fields after the volume are not needed.
/// </summary>
internal sealed class KlineHistoryEntryConverter : JsonConverter<KlineHistoryEntry>
{
    public override KlineHistoryEntry Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartArray)
        {
            throw new JsonException("Binance sent a kline that is not an array.");
        }

        var openTime = ReadNext(ref reader, static (ref Utf8JsonReader r) => r.GetInt64());
        var open = ReadNext(ref reader, ReadDecimalString);
        var high = ReadNext(ref reader, ReadDecimalString);
        var low = ReadNext(ref reader, ReadDecimalString);
        var close = ReadNext(ref reader, ReadDecimalString);
        var volume = ReadNext(ref reader, ReadDecimalString);

        while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
        {
            reader.Skip();
        }

        return new KlineHistoryEntry(openTime, open, high, low, close, volume);
    }

    public override void Write(Utf8JsonWriter writer, KlineHistoryEntry value, JsonSerializerOptions options) =>
        throw new NotSupportedException();

    private static T ReadNext<T>(ref Utf8JsonReader reader, ReaderFunc<T> read)
    {
        if (!reader.Read() || reader.TokenType == JsonTokenType.EndArray)
        {
            throw new JsonException("Binance sent a kline with too few fields.");
        }

        return read(ref reader);
    }

    private static decimal ReadDecimalString(ref Utf8JsonReader reader) =>
        decimal.Parse(reader.GetString() ?? throw new JsonException("Binance sent a kline number that is not a string."), CultureInfo.InvariantCulture);

    private delegate T ReaderFunc<out T>(ref Utf8JsonReader reader);
}
