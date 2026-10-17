# Binance Spot WebSocket market data: research for the VSG assessment

Researched 2026-10-17. Sources are primary only:

- **[GH-WSS]** https://github.com/binance/binance-spot-api-docs/blob/master/web-socket-streams.md (raw: https://raw.githubusercontent.com/binance/binance-spot-api-docs/master/web-socket-streams.md). This is the document the assessment links to.
- **[GH-WSS-TN]** https://github.com/binance/binance-spot-api-docs/blob/master/testnet/web-socket-streams.md
- **[GH-REST]** https://github.com/binance/binance-spot-api-docs/blob/master/rest-api.md
- **[GH-CL]** https://github.com/binance/binance-spot-api-docs/blob/master/CHANGELOG.md (last updated 2026-09-18)
- **[DEV]** https://developers.binance.com/docs/binance-spot-api-docs/web-socket-streams (official docs site; the fetched copy lists endpoints and stream names, the limits are taken from [GH-WSS])
- **[LIVE]** My own live connection to `wss://stream.binance.com:9443` on 2026-10-17. These are observations, not documentation.

## 1. What the assessment PDF asks for

I extracted the PDF text by decoding its compressed streams, because the Read tool's PDF mode was unavailable.

| Area | Requirement (paraphrased) |
|---|---|
| Stack | A .NET service that collects cryptocurrency price data from Binance, stores it in a **relational database**, and exposes an **HTTP API**. The database technology and data access approach are the candidate's choice. |
| Collection (section 2) | Use the **Binance Spot WebSocket API** to collect "price updates" for trading symbols. The PDF links to `web-socket-streams.md` in the binance-spot-api-docs repo. It names **no specific stream, field or frequency**. |
| Symbols (section 3) | The monitored symbols are managed **at runtime** through administrative API endpoints. The initial configuration is **BTCUSDT, ADAUSDT, ETHUSDT**. |
| API format (section 4) | JSON and XML through HTTP content negotiation. |
| 4.1 | `GET /api/{symbol}/24hAvgPrice` returns the average price over the last 24 hours of **available data**. If less than 24h of data exists, average over what is available. |
| 4.2 | `GET /api/{symbol}/SimpleMovingAverage?n={numberOfDataPoints}&p={timePeriod}&s={startDateTime}`. `n` is the number of data points. `p` is the period of each data point, one of `1w`, `1d`, `30m`, `5m`, `1m`. `s` is an optional start; if omitted, "now" is the reference point. The PDF links to the Investopedia SMA article. |
| Console client | Must offer the same price analysis capabilities through the HTTP API. |
| Delivery | A README, containerization or a deployment proposal (a live deployment is optional), AI-usage notes (a prompt record and an `engineering-notes.md`), and a Git repo with assumptions and trade-offs documented. |

The PDF names "Spot" explicitly, so USD-M futures streams are out of scope and are not covered here.

## 2. Recommended data set

The assessment only needs a price time series per symbol. The required analytics are a 24h average and an SMA over fixed buckets (`1m`, `5m`, `30m`, `1d`, `1w`). Those bucket names are exactly Binance kline interval names ([GH-WSS] "Kline/Candlestick chart intervals").

### Recommended (minimal and sufficient)

Subscribe to `<symbol>@kline_1m` for every monitored symbol, through one combined-stream connection. Persist a row only when `k.x == true`, meaning the candle is closed.

- A closed 1m candle gives a gap-detectable, idempotent, bounded-volume record. The natural key is `(symbol, k.t)`. Its volume is about 1,440 rows per symbol per day.
- Every required `p` value (5m, 30m, 1d, 1w) is a multiple of 1m, so those buckets can be built by aggregating 1m candles in SQL. This avoids four extra streams.
- Other kline streams (`kline_5m`, `kline_30m`, `kline_1d`, `kline_1w`) exist ([GH-WSS]) and could be stored directly. That costs storage and complexity, and results might differ at bucket boundaries. Aggregating from 1m is simpler.
- Observed in [LIVE]: kline events arrive about every 2s per symbol while a candle is open, which matches "2000ms for the other intervals" ([GH-WSS] "Kline/Candlestick Streams for UTC"). Only the final event with `x:true` per minute is persisted.

For the 24h average, the candidate options are:
1. The mean of stored 1m close prices over the window, using only the data available. This follows the PDF wording and is the simplest.
2. A volume-weighted mean, computed from `q` and `v` (quote and base volume) in the same candle rows.

### Optional additions

| Stream | Why |
|---|---|
| `<symbol>@miniTicker` | A 1000ms last price with 24h open/high/low/volume. It is cheap, but it is a 24h rolling window, not the UTC day. Useful as a "latest price" snapshot. |
| `<symbol>@ticker` | Includes `w`, the Binance-computed **24h rolling weighted average price**. It is a handy cross-check for `24hAvgPrice`, but it is not the same quantity as "average over available data". |
| `<symbol>@aggTrade` or `@trade` | Tick-level data. It is high volume (see the counts below) and is not needed by the stated analytics. |

Do not collect `@bookTicker` or the depth streams. They are not price-update requirements. In [LIVE], `btcusdt@bookTicker` alone produced about 65 messages/s. Maintaining a local order book is complex ([GH-WSS] "How to manage a local order book correctly").

### Observed message rates [LIVE]

Eight streams over one combined connection for 10s (1001 messages, about 100 messages/s total):

| Stream | Messages in 10s |
|---|---|
| btcusdt@bookTicker | 654 |
| btcusdt@trade | 269 |
| btcusdt@aggTrade | 31 |
| ethusdt@trade | 21 |
| btcusdt@ticker | 10 |
| btcusdt@miniTicker | 10 |
| btcusdt@kline_1m | 4 |
| adausdt@trade | 2 |

## 3. Endpoints and stream naming

All claims in this section are from [GH-WSS] "General WSS information" unless noted.

| Item | Value |
|---|---|
| Base endpoints | `wss://stream.binance.com:9443` or `wss://stream.binance.com:443`. |
| Market-data-only endpoint | `wss://data-stream.binance.vision`. The user data stream is not available there. It is a good fit for this project, which has no private data. I did not test this host live. |
| Raw stream | `/ws/<streamName>`. |
| Combined streams | `/stream?streams=<s1>/<s2>/<s3>`. Events are wrapped as `{"stream":"<streamName>","data":<rawPayload>}`. [LIVE] confirmed this. |
| Symbols in stream names | **Lowercase**, for example `btcusdt@kline_1m`. Event payloads carry the symbol in uppercase (`"s":"BTCUSDT"`) [LIVE]. |
| Testnet | `wss://stream.testnet.binance.vision/ws` ([GH-WSS-TN]). [DEV] also lists `wss://demo-stream.binance.com:9443` and `:443`. |
| Timestamps | Milliseconds by default. Append `timeUnit=MICROSECOND` to the URL for microseconds. |
| Example for this assessment | `wss://stream.binance.com:9443/stream?streams=btcusdt@kline_1m/ethusdt@kline_1m/adausdt@kline_1m` |
| Runtime symbol changes | Live subscribe, unsubscribe and listing are supported by sending JSON text frames on the same connection: `{"method":"SUBSCRIBE","params":["btcusdt@kline_1m"],"id":1}`. `UNSUBSCRIBE`, `LIST_SUBSCRIPTIONS`, `SET_PROPERTY` and `GET_PROPERTY` also exist. A successful reply is `{"result":null,"id":1}` ([GH-WSS] "Live Subscribing/Unsubscribing to streams"). This maps directly onto the "manage symbols at runtime" requirement, so the admin API can issue SUBSCRIBE or UNSUBSCRIBE without reconnecting. I did not exercise this live. |
| `/ws` vs `/stream` | The `combined` property defaults to `false` on `/ws/` and `true` on `/stream`. `SET_PROPERTY ["combined", true]` changes it ([GH-WSS] "Setting Properties"). So a live SUBSCRIBE on a raw `/ws` connection yields unwrapped payloads, and the message shape depends on how you connected. |

## 4. Payload schemas

All prices and quantities are **strings** (decimal), so parse to `decimal`, not `double`. Timestamps and IDs are JSON numbers.

JSON property names are **case-sensitive and differ only by case** (for example `b` vs `B`, `e` vs `E`, `t` vs `T`, `v` vs `V`). I hit this directly in [LIVE]: PowerShell's `ConvertFrom-Json` rejected the payloads with a duplicate-key error. In .NET, `System.Text.Json` is case-sensitive by default (do not enable `PropertyNameCaseInsensitive`); explicit `[JsonPropertyName]` attributes are the safe approach.

### 4.1 Kline: `<symbol>@kline_<interval>` (recommended)

Source: [GH-WSS] "Kline/Candlestick Streams for UTC". Update speed: 1000ms for `1s`, 2000ms for other intervals. The `k` object holds the candle. Events carry `e`, `E`, `s`, `k`.

Supported intervals: 1s, 1m, 3m, 5m, 15m, 30m, 1h, 2h, 4h, 6h, 8h, 12h, 1d, 3d, 1w, 1M.

| Field | Meaning | Type |
|---|---|---|
| `e` | Event type (`kline`) | string |
| `E` | Event time (ms) | number |
| `s` | Symbol | string |
| `k.t` | Kline start time (ms) | number |
| `k.T` | Kline close time (ms) | number |
| `k.s` | Symbol | string |
| `k.i` | Interval | string |
| `k.f` | First trade ID | number |
| `k.L` | Last trade ID | number |
| `k.o`, `k.c`, `k.h`, `k.l` | Open, close, high, low price | string |
| `k.v` | Base asset volume | string |
| `k.n` | Number of trades | number |
| `k.x` | Is this kline closed? | bool |
| `k.q` | Quote asset volume | string |
| `k.V` | Taker buy base asset volume | string |
| `k.Q` | Taker buy quote asset volume | string |
| `k.B` | Ignore | string |

Notes:

- The default kline boundaries are UTC. A variant `<symbol>@kline_<interval>@+08:00` uses UTC+8 boundaries ([GH-WSS] "Kline/Candlestick Streams with timezone offset"). Do not use it.
- [LIVE] sample (`x:false`, an in-progress candle): `{"stream":"btcusdt@kline_1m","data":{"e":"kline","E":1792237052043,"s":"BTCUSDT","k":{"t":1792237020000,"T":1792237079999,"s":"BTCUSDT","i":"1m","f":6770658432,"L":6770658829,"o":"81589.69000000","c":"81587.77000000","h":"81589.70000000","l":"81584.82000000","v":"0.77477000","n":398,"x":false,"q":"63213.00348210","V":"0.21019000","Q":"17149.26411680","B":"0"}}}`
- The close time `T` equals `t + 60000 - 1` for `1m` [LIVE].

### 4.2 Mini ticker: `<symbol>@miniTicker` (1000ms)

Source: [GH-WSS] "Individual Symbol Mini Ticker Stream". It is a 24h rolling window, not the UTC day.

| Field | Meaning |
|---|---|
| `e` | `24hrMiniTicker` |
| `E` | Event time |
| `s` | Symbol |
| `c` | Close (last) price |
| `o` | Open price (24h ago) |
| `h` | High price |
| `l` | Low price |
| `v` | Total base asset volume |
| `q` | Total quote asset volume |

[LIVE] sample: `{"e":"24hrMiniTicker","E":1792237049016,"s":"BTCUSDT","c":"81589.29000000","o":"82717.54000000","h":"82773.00000000","l":"80809.25000000","v":"11785.78163000","q":"962275580.79557600"}`

### 4.3 24hr ticker: `<symbol>@ticker` (1000ms)

Source: [GH-WSS] "Individual Symbol Ticker Streams".

| Field | Meaning |
|---|---|
| `e`, `E`, `s` | Event type `24hrTicker`, event time, symbol |
| `p`, `P` | Price change, price change percent |
| `w` | Weighted average price (24h rolling) |
| `x` | Price of the trade before the first one in the window |
| `c`, `Q` | Last price, last quantity |
| `b`, `B` | Best bid price, quantity |
| `a`, `A` | Best ask price, quantity |
| `o`, `h`, `l` | Open, high, low |
| `v`, `q` | Base and quote volume |
| `O`, `C` | Statistics open and close time |
| `F`, `L` | First and last trade ID |
| `n` | Number of trades |

[LIVE] confirmed this layout (for example `"w":"81647.15850039"`).

### 4.4 Trade: `<symbol>@trade` (real-time)

Source: [GH-WSS] "Trade Streams".

| Field | Meaning |
|---|---|
| `e`, `E`, `s` | `trade`, event time, symbol |
| `t` | Trade ID |
| `p`, `q` | Price, quantity |
| `T` | Trade time |
| `m` | Is the buyer the market maker? |
| `M` | Ignore |

### 4.5 Aggregate trade: `<symbol>@aggTrade` (real-time)

Source: [GH-WSS] "Aggregate Trade Streams". It aggregates trades from a single taker order.

| Field | Meaning |
|---|---|
| `e`, `E`, `s` | `aggTrade`, event time, symbol |
| `a` | Aggregate trade ID |
| `p`, `q` | Price, quantity |
| `f`, `l` | First and last trade ID |
| `T` | Trade time |
| `m` | Is the buyer the market maker? |
| `M` | Ignore |

### 4.6 Other streams (not recommended for this task)

- `<symbol>@bookTicker`: real-time best bid and ask. Payload fields are `u` (order book updateId), `s`, `b`, `B`, `a`, `A`. There is no event-time field ([GH-WSS] "Individual Symbol Book Ticker Streams"; [LIVE] confirmed).
- `<symbol>@avgPrice`: a 1000ms stream. The documented payload shows interval `"i":"5m"`, with fields `w` (average price) and `T` (last trade time). The docs describe it as an average over a fixed time interval, so it is not the 24h average the assessment wants ([GH-WSS] "Average Price").
- `<symbol>@depth<5|10|20>[@100ms]` (partial book) and `<symbol>@depth[@100ms]` (diff depth, with `U`/`u` update IDs): update speed 1000ms or 100ms ([GH-WSS]).
- `<symbol>@ticker_<1h|4h|1d>`, `!miniTicker@arr`, `!ticker_<window>@arr` (all-market arrays), `<symbol>@referencePrice` and `<symbol>@blockTrade`: also exist ([GH-WSS]; [DEV] lists the same names).

### 4.7 Local order book (only if depth is ever added)

The rules are in [GH-WSS] "How to manage a local order book correctly":

1. Open `.../ws/bnbbtc@depth` and buffer the events.
2. Fetch the snapshot from `https://api.binance.com/api/v3/depth?symbol=BNBBTC&limit=5000`.
3. If the snapshot `lastUpdateId` is below the first buffered `U`, refetch.
4. Discard buffered events with `u <= lastUpdateId`.
5. Apply the rest in order. Ignore an event if `u` is below the local update ID. If `U` is greater than the local ID + 1, you missed events and must restart.
6. A quantity of zero removes the level.

Not needed for this assessment.

## 5. Operational limits (all from [GH-WSS] unless noted)

| Topic | Rule |
|---|---|
| Connection lifetime | A single connection is valid for **24 hours** and is then disconnected. |
| `serverShutdown` | The server sends `{"e":"serverShutdown","E":...}` (raw) or `{"stream":"!serverShutdown","data":{...}}` (combined) before shutdown. The changelog says it is sent **10 minutes** before disconnection ([GH-CL], 2026-05-06; that entry covers the WebSocket API and Streams). |
| Planned maintenance | [GH-CL] (2026-06-24) announced an infrastructure upgrade on 2026-07-09 that could disconnect clients. The lesson is that reconnect logic is mandatory. |
| Ping/pong | The server sends a ping frame every **20 seconds**. The client must reply with a pong carrying a copy of the ping payload. If no pong arrives within a minute, the connection is dropped. Unsolicited pongs are allowed but do not keep the connection alive. My [LIVE] test lasted only about 10s, so it did not exercise a full ping cycle. Check that the chosen .NET WebSocket client answers pings (this is not verified here). |
| Inbound message limit | **5 incoming messages per second per connection**. A message is a PING frame, a PONG frame, or a JSON control message (SUBSCRIBE and so on). Exceeding it disconnects, and repeat offenders may be IP-banned. Put multiple streams in one SUBSCRIBE `params` array when changing symbols. |
| Streams per connection | A maximum of **1024 streams** per connection. |
| Connection attempts | **300 connections per attempt every 5 minutes per IP**. A reconnect loop needs backoff. |
| Data gaps | Streams are a live feed and the docs describe no replay. After a disconnect, missed data can be backfilled with `GET /api/v3/klines` (weight 2, default 500 rows, max 1000; params `symbol`, `interval`, `startTime`, `endTime`). Response rows are arrays: open time, open, high, low, close, volume, close time, quote volume, trades, taker buy base, taker buy quote, ignore ([GH-REST] "Kline/Candlestick data"). The REST docs list the same interval set, including `1m`, `5m`, `30m`, `1d`, `1w`. |

## 6. Open questions and ambiguities in the assessment

1. **Which "price"?** The PDF says "price updates" but does not say which one. Options are last trade price, kline close, or mid price from the book. The recommended choice is the kline close, since the SMA buckets are kline-shaped. Document this assumption.
2. **What is the 24h "average price"?** It could be a simple mean of stored samples, a time-weighted mean, a volume-weighted mean, or Binance's own `w` field. The mean of stored 1m closes is the simplest reading of "average over available data". State the choice.
3. **SMA definition.** The PDF says `n` data points of period `p`, but not which value of each bucket to use (close is conventional, per the Investopedia link). It also does not say whether `s` is the window start or end. "Starting date and time" suggests the start, yet "if omitted use the current time as the reference point" suggests a window ending at now. It is also silent on what to do when fewer than `n` points exist (error, or compute over what exists), and on gaps in the data.
4. **Bucket boundaries.** Binance kline boundaries are UTC by default ([GH-WSS]). I did not verify from a primary source which weekday a `1w` candle starts on. If SMA buckets are built by aggregation, define the alignment (for example UTC midnight, or Monday).
5. **Symbol removal.** When a symbol is removed at runtime, should its stored history be kept, should collection stop, and should the API still serve its data?
6. **Retention.** There is no stated retention period or data volume. Closed 1m klines for 3 symbols is about 4,300 rows/day, which is small. Tick data would be orders of magnitude larger (see section 2).
7. **Symbol validation.** The PDF does not say whether an added symbol must be validated against Binance (for example via the REST exchange info endpoint) or whether failures should just be logged.
8. **Spot only.** The PDF says Spot explicitly, so there is no Spot vs futures ambiguity to resolve.
9. **Testnet.** Testnet prices are not real market prices. Use `wss://stream.binance.com:9443` (or the market-data-only `wss://data-stream.binance.vision`) for real data. No API key is needed for public market streams, and I used none in the [LIVE] test.
