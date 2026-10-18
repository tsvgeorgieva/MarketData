# Market Data

Collects cryptocurrency price data from Binance for a set of trading symbols, stores it, and serves price analytics (averages) over it. For plain-language background on the market terms, see [docs/primer-market-data.md](docs/primer-market-data.md).

## Language

**Symbol**:
A tradable pair identified by a code such as `BTCUSDT` (the asset on the left, priced in the asset on the right).
_Avoid_: Ticker, pair, instrument

**Monitored Symbol**:
A Symbol whose data the system currently collects. An administrator adds and removes them at runtime.
_Avoid_: Subscribed symbol, tracked symbol

**Removed Symbol**:
A Symbol that was a Monitored Symbol and no longer is. Collection stops but its stored Candles are kept, and the price endpoints treat it as unknown until it is added again, when it resumes with its history.
_Avoid_: Deleted symbol, archived symbol

**Candle**:
A stored, closed one-minute summary of a Symbol's trading: open, high, low, close and volume. Only closed Candles are stored. A Candle is identified by its Symbol and the time it opened, and is never changed once stored; Candles are kept indefinitely.
_Avoid_: Kline, price update, tick, sample

**Backfill**:
Fetching recently closed Candles from Binance's history to fill minutes the system did not collect live, for example after a disconnect or when a Symbol is added. It only adds Candles that are missing.
_Avoid_: Replay, resync, repair

**Gap**:
A minute with no stored Candle for a Monitored Symbol. It can be a minute the system missed or a minute with no trades; either way it is never filled in when reading.
_Avoid_: Hole, missing data

**Price**:
The close value of a Candle. It is the only price the system stores and serves.
_Avoid_: Last price, quote, rate

**Period**:
The length of time one data point represents in a moving average: one of 1m, 5m, 30m, 1d, 1w.
_Avoid_: Interval, timeframe, resolution

**Bucket**:
The group of consecutive Candles that make up one data point of a given Period, aligned to UTC (weeks start Monday 00:00). Its value is the Price of its last Candle. A Bucket with no Candles is missing and contributes nothing; it is never filled in.
_Avoid_: Window, bar

**Reference Instant**:
The moment a moving average is calculated as of: the requested end time (the `e` parameter), or now when none is given. The newest data point is the Bucket containing the last minute that had closed by then, and a Candle counts only if it had closed by then.
_Avoid_: Start time, as-of date

**In-progress Bucket**:
The newest Bucket of a moving average when its Period has not yet fully elapsed; its value is the Price of the latest closed Candle so far. It never exists for a `1m` Period, where each Bucket is one closed Candle.
_Avoid_: Open bucket, partial bucket

**24h Average Price**:
The simple arithmetic mean of the Prices of all Candles in the last 24 hours, using only the Candles available.
_Avoid_: Daily average, 24h ticker price

**Simple Moving Average**:
The arithmetic mean of the Prices of the `n` consecutive Buckets of a given Period that end with the Bucket containing the last minute closed by the Reference Instant, using only the Buckets that exist. It is partial when fewer than `n` exist.
_Avoid_: SMA line, rolling average
