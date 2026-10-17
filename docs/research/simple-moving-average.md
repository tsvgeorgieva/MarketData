# Simple Moving Average (SMA): research for the VSG assessment

Researched 2026-10-17. Labels: **[SOURCED]** = taken from a cited source, **[OBSERVED]** = my own live request to Binance, **[RECOMMENDATION]** = my design choice, not a fact. Companion note: `binance-websocket-market-data.md` (data is stored as closed 1m klines, key `(symbol, open_time)`).

## 1. What the PDF requires

Extracted from "Technical Assessment at VSG.pdf", section 4.2 (paraphrased):

- `GET /api/{symbol}/SimpleMovingAverage?n={numberOfDataPoints}&p={timePeriod}&s={startDateTime}` returns the SMA for the symbol.
- `symbol`: trading symbol.
- `n`: number of data points.
- `p`: the time period represented by each data point. Supported: `1w`, `1d`, `30m`, `5m`, `1m`.
- `s`: optional "starting date and time for the calculation". If omitted, use the current time as the reference point.
- Reference link: https://www.investopedia.com/terms/s/sma.asp
- The output shape is not specified, and there are no examples, no error cases, and no mention of gaps or of having fewer than `n` points. Section 4 also requires JSON and XML via content negotiation, and a console client offering the same analysis.

## 2. Definition and conventions

| Topic | Finding | Source |
|---|---|---|
| Definition | The moving average is `M_t = (X_t + X_{t-1} + ... + X_{t-N+1}) / N`: the mean of the N most recent consecutive observations. As time advances, the oldest value drops out and the newest enters. [SOURCED] | NIST/SEMATECH e-Handbook 6.4.2.1 https://www.itl.nist.gov/div898/handbook/pmc/section4/pmc421.htm |
| Trailing vs centered | NIST's formula is trailing (the window ends at t). pandas defaults to trailing (`center=False`: labels are the right edge of the window), with centered as an opt-in. [SOURCED] | https://pandas.pydata.org/docs/reference/api/pandas.DataFrame.rolling.html |
| Warm-up | pandas: for an integer window, `min_periods` defaults to the window size, so the first N-1 outputs are NaN. Setting `min_periods` lower yields partial-window averages. [SOURCED] | same pandas URL |
| Price input | Close is the conventional input. I could not fetch the TradingView `ta.sma` reference (the page is JS-rendered), so this is **not verified from a first-party doc**. The PDF's own link (Investopedia, a secondary source) is the assessment's stated reference. The `source` argument of `ta.sma` defaults to close in Pine, per my recollection only. | https://www.tradingview.com/pine-script-reference/v6/#fun_ta.sma (not retrievable) |
| SQL window | `ROWS BETWEEN n PRECEDING AND CURRENT ROW` frames the current row plus up to n preceding rows; near the partition start the frame is smaller. [SOURCED] | https://www.postgresql.org/docs/current/sql-expressions.html |

Note: a NIST page was fetched at section 6.4.2 (pmc42.htm) and does not define the moving average; the definition is on pmc421.htm.

## 3. Efficient computation and numeric type

[RECOMMENDATION] unless stated.

- Use `decimal` (C#) and `numeric` (SQL). Binance sends prices as decimal strings, e.g. `"81589.69000000"` (companion note, section 4; https://github.com/binance/binance-spot-api-docs/blob/master/web-socket-streams.md). Binary doubles introduce rounding error in sums of such values.
- Running-sum update is O(1) per point: `sum += new - oldest; sma = sum / n`. With `decimal` it is exact (up to 28-29 significant digits), so there is no drift. With `double` a running sum accumulates error, so recompute periodically or use Kahan/Neumaier summation.
- For this API, n is small and each request computes one value (or n values), so a plain `AVG` over the last n buckets is enough. A running sum only matters if returning a series of many windows.
- Division: `sum / n` in `decimal` is fine. Round only at presentation (for example 8 decimal places, matching Binance's price precision).

## 4. Building p-sized buckets from 1m klines

### What Binance offers natively

- Intervals supported by REST klines include `1m`, `5m`, `30m`, `1d`, `1w` (also 1s, 3m, 15m, 1h, 2h, 4h, 6h, 8h, 12h, 3d, 1M). [SOURCED] https://github.com/binance/binance-spot-api-docs/blob/master/rest-api.md ("Kline/Candlestick data", "Supported kline intervals"). The WebSocket kline streams list the same set (companion note).
- Klines are uniquely identified by open time. UTC is the default for kline intervals; `startTime`/`endTime` are always UTC; the optional `timeZone` parameter (-12:00 to +14:00) shifts interval boundaries. [SOURCED] same rest-api.md.
- The docs do **not** state which weekday a `1w` candle opens on, nor how gaps (minutes without trades) are represented. [SOURCED as absence: searched rest-api.md and web-socket-streams.md.]

### 1w week start (the open question from the earlier note)

[OBSERVED] by calling `GET https://data-api.binance.vision/api/v3/klines?symbol=BTCUSDT&interval=1w&limit=3` on 2026-10-17:

- Open times were 1790553600000, 1791158400000, 1791763200000. Each is exactly 7 days (604,800,000 ms) apart, and 1790553600000 / 86400000 = 20724 days since the Unix epoch. 20724 mod 7 = 4; the epoch day (1970-01-01) is a Thursday, so the day is a **Monday, 00:00:00 UTC**.
- The earliest 1w candle (`startTime=0`, BTCUSDT) opens at 1502668800000 = 17392 days, 17392 mod 7 = 4, also Monday 00:00 UTC (2017-08-14).
- With `timeZone=8` the weekly open shifted to 1790524800000 (Monday 00:00 at UTC+8, i.e. Sunday 16:00 UTC), confirming the timezone parameter moves boundaries.
- Daily candles open at 00:00:00 UTC (for example 1792108800000 = 20378 days exactly).
- Close time = next open - 1 ms (for example 1791158399999).

Conclusion: weeks are Monday-00:00-UTC aligned, ISO-style. This is empirical, not documented; cite it as observed behavior.

### Alignment rules

[RECOMMENDATION] Bucket start (ms) for a 1m candle opening at `t`:

| p | bucket start |
|---|---|
| 1m | `t` |
| 5m | `t - t % 300000` |
| 30m | `t - t % 1800000` |
| 1d | `t - t % 86400000` |
| 1w | `t - ((t + 3*86400000) % 604800000)` (the epoch is a Thursday; shift by 3 days so Monday is the origin) |

Check: for Monday 00:00 UTC, `(t + 3 days) % 7 days = 0` since Monday is 3 days after Thursday... Verify: 1790553600000 = 20724 days; (20724 + 3) mod 7 = 20727 mod 7 = 0. Good.

Value per bucket: the **close of the last 1m candle in the bucket** (the one with the greatest open time). This equals the native Binance kline close for that interval, as long as the minute candles are complete. [RECOMMENDATION]; the equivalence follows from the kline definition (a larger kline's close is the last trade price in its window).

### Open (in-progress) bucket

The newest bucket is incomplete (for example the current day). Options: (a) include it using the latest stored 1m close, which makes "now" meaningful for `s` omitted; or (b) use only completed buckets. [RECOMMENDATION] Default to (a), because the PDF says "use the current time as the reference point", and document it (the latest data point is the partial current bucket). Make this explicit in the response (`isPartial`).

## 5. Gaps and missing candles

- Not documented by Binance (see above). Missing 1m rows can arise from collector downtime or disconnects. Backfill with REST `GET /api/v3/klines` (companion note, section 5; https://github.com/binance/binance-spot-api-docs/blob/master/rest-api.md).
- [RECOMMENDATION] Do not synthesize values. Compute each bucket from the 1m rows that exist in it; a bucket with zero rows is "missing". Take the last n buckets *by time* (not the last n existing rows), so a gap is visible rather than silently stretching the window. If any of the n slots is missing, either (a) return what exists with `dataPoints` < n and a `complete: false` flag, or (b) forward-fill. Prefer (a). A forward-fill option can be mentioned in the README as a trade-off.
- This differs from pandas semantics (row-count windows over whatever rows exist; https://pandas.pydata.org/docs/reference/api/pandas.DataFrame.rolling.html) and matches its time-based `"Ns"` offset windows.

## 6. Resolving the PDF's ambiguities

All are [RECOMMENDATION]; document them in the README as assumptions.

| Ambiguity | Recommended resolution | Reasoning |
|---|---|---|
| Meaning of `s` | `s` is the **end (reference) instant** of the series: the window of n buckets ends with the bucket containing `s`. Omitted means "now". Parse as ISO-8601 and treat as UTC when no offset is given. | The PDF says that if `s` is omitted, "the current time is the reference point". A reference point that defaults to now is naturally the right edge of a trailing SMA, and a start time that defaults to now would have no data after it. The literal word "starting" reads as window start, so state the choice explicitly. Optionally accept `s` as the first bucket if documented, but pick one. |
| `s` in the future / before data | Future: clamp to now (or 400). Before earliest data: return an empty or incomplete result. | |
| Fewer than n data points | Return the SMA over the available points with `dataPoints` and `requested` fields and `complete: false`; return 404/empty when zero points. | Mirrors the PDF's own 24hAvgPrice rule ("calculate the average over the available period"), which gives an internal-consistency argument. The strict alternative (pandas default `min_periods=n` gives NaN) is a defensible choice but less helpful. |
| Price input | Close of each bucket. | Convention; section 2. |
| Output shape | A single SMA value plus metadata. Optionally the contributing points. Example: `{ "symbol":"BTCUSDT","n":5,"p":"5m","asOf":"2026-10-17T10:12:00Z","windowStart":"...","windowEnd":"...","sma":81587.123,"dataPoints":5,"complete":true }`. | The PDF says "Returns the SMA", singular. |
| Validation | `n` integer 1..some cap (for example 1000); `p` in the five allowed values (case-sensitive; reject others with 400); unknown symbol 404. | |
| Window vs 1w and `n` | n weeks reach back n*7 days; with ~1,440 1m rows per day per symbol, `n=52, p=1w` means ~524k rows, which is fine with an index on `(symbol, open_time)`. | Companion note volumes. |

## 7. SQL sketch (PostgreSQL)

[RECOMMENDATION]. Table `candle(symbol text, open_time bigint /*ms UTC*/, close numeric(28,10), primary key(symbol, open_time))`.

```sql
-- params: @symbol, @periodMs (60000 / 300000 / 1800000 / 86400000 / 604800000),
--         @weekShiftMs (0 for all but 1w; 259200000 for 1w), @endMs (s or now), @n
WITH last_per_bucket AS (
  SELECT DISTINCT ON (bucket)
         ((open_time + @weekShiftMs) / @periodMs) * @periodMs - @weekShiftMs AS bucket,
         close
  FROM candle
  WHERE symbol = @symbol
    AND open_time <= @endMs                            -- 1m candle opened at/before reference
    AND open_time >= (((@endMs + @weekShiftMs) / @periodMs) - (@n - 1)) * @periodMs - @weekShiftMs
  ORDER BY bucket DESC, open_time DESC                -- picks the last 1m close per bucket
)
SELECT count(*) AS data_points, avg(close) AS sma, min(bucket) AS window_start
FROM last_per_bucket;
```

Notes: integer division on `bigint` floors for non-negative values. Bucket start for week uses the shift from section 4. Compute `@endMs` from `s` in UTC. `DISTINCT ON` requires the `ORDER BY` to begin with the same expression. For SQLite or SQL Server, replace `DISTINCT ON` with `ROW_NUMBER() OVER (PARTITION BY bucket ORDER BY open_time DESC) = 1`.

## 8. C# sketch

[RECOMMENDATION]

```csharp
public sealed record SmaResult(string Symbol, int Requested, int DataPoints, bool Complete,
                               DateTimeOffset WindowStart, DateTimeOffset WindowEnd, decimal? Sma);

static long BucketStart(long tMs, string p) => p switch
{
    "1m"  => tMs - tMs % 60_000,
    "5m"  => tMs - tMs % 300_000,
    "30m" => tMs - tMs % 1_800_000,
    "1d"  => tMs - tMs % 86_400_000,
    "1w"  => tMs - (tMs + 3 * 86_400_000L) % 604_800_000L, // Monday 00:00 UTC
    _ => throw new ArgumentException("unsupported p")
};
static long PeriodMs(string p) => p switch { "1m" => 60_000, "5m" => 300_000, "30m" => 1_800_000,
                                             "1d" => 86_400_000, "1w" => 604_800_000, _ => throw new ArgumentException() };

// candles: ascending 1m closes (openTimeMs, close) covering the window
public static SmaResult Compute(string symbol, string p, int n, long endMs, IEnumerable<(long t, decimal close)> candles)
{
    long period = PeriodMs(p);
    long lastBucket = BucketStart(endMs, p);
    long firstBucket = lastBucket - (n - 1) * period;
    var closes = new Dictionary<long, decimal>();               // last-writer-wins on ascending input
    foreach (var (t, c) in candles)
        if (t <= endMs && t >= firstBucket) closes[BucketStart(t, p)] = c;
    decimal sum = 0m; foreach (var v in closes.Values) sum += v;
    int k = closes.Count;
    return new(symbol, n, k, k == n,
               DateTimeOffset.FromUnixTimeMilliseconds(firstBucket),
               DateTimeOffset.FromUnixTimeMilliseconds(lastBucket),
               k == 0 ? null : sum / k);
}
```

Use `decimal` throughout and parse Binance strings with `decimal.Parse(s, CultureInfo.InvariantCulture)`.

## 9. Test cases worth writing

- Window of 3 on closes 9, 8, 9 gives 8.6666... (NIST example, https://www.itl.nist.gov/div898/handbook/pmc/section4/pmc421.htm).
- Fewer than n points returns the available average with `complete=false`.
- 1w boundary: a candle at Sunday 23:59 UTC and one at Monday 00:00 UTC fall into different weeks. Use the observed open times above as fixtures (1790553600000 is a Monday).
- Gap in the middle: the missing bucket is not filled and `dataPoints < n`.
- `s` omitted vs explicit `s` equal to now produce the same result.
- Invalid `p`, `n <= 0` give 400.

## 10. Sources

- NIST/SEMATECH e-Handbook, Single Moving Average: https://www.itl.nist.gov/div898/handbook/pmc/section4/pmc421.htm
- pandas `DataFrame.rolling`: https://pandas.pydata.org/docs/reference/api/pandas.DataFrame.rolling.html
- PostgreSQL window frames: https://www.postgresql.org/docs/current/sql-expressions.html
- Binance Spot REST docs: https://github.com/binance/binance-spot-api-docs/blob/master/rest-api.md
- Binance Spot WebSocket streams docs: https://github.com/binance/binance-spot-api-docs/blob/master/web-socket-streams.md
- Observations: Binance public market-data REST host `https://data-api.binance.vision/api/v3/klines` (live calls 2026-10-17).
- Not retrieved: TradingView Pine `ta.sma` reference (https://www.tradingview.com/pine-script-reference/v6/#fun_ta.sma) rendered no content to my fetcher; claims about it are unverified.
