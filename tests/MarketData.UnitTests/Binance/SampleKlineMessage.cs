using System.Text;

namespace MarketData.UnitTests.Binance;

internal static class SampleKlineMessage
{
    /// <summary>A real-shaped combined-stream message. Binance uses keys that differ only by case (v and V, t and T): the volume is "v".</summary>
    public static ReadOnlyMemory<byte> Json(bool closed) => Utf8(
        """
        {"stream":"btcusdt@kline_1m","data":{"e":"kline","E":1767268860000,"s":"BTCUSDT","k":{
        "t":1767268800000,"T":1767268859999,"s":"BTCUSDT","i":"1m","f":1,"L":2,
        "o":"100.50000000","c":"101.12500000","h":"102.25000000","l":"99.75000000",
        "v":"2.50000000","n":398,"x":CLOSED,"q":"63213.0","V":"0.21","Q":"17149.2","B":"0"}}}
        """.Replace("CLOSED", closed ? "true" : "false", StringComparison.Ordinal));

    public static ReadOnlyMemory<byte> Utf8(string text) => Encoding.UTF8.GetBytes(text);
}
