# Market Data

Collects cryptocurrency price data from Binance for a set of trading symbols, stores it, and serves price analytics (averages) over it. For plain-language background on the market terms, see [docs/primer-market-data.md](docs/primer-market-data.md).

## Language

**Symbol**:
A tradable pair identified by a code such as `BTCUSDT` (the asset on the left, priced in the asset on the right).
_Avoid_: Ticker, pair, instrument

**Monitored Symbol**:
A Symbol whose data the system currently collects.
_Avoid_: Subscribed symbol, tracked symbol

**Candle**:
A stored, closed one-minute summary of a Symbol's trading: open, high, low, close and volume. Only closed Candles are stored.
_Avoid_: Kline, price update, tick, sample

**Price**:
The close value of a Candle. It is the only price the system stores and serves.
_Avoid_: Last price, quote, rate

**Period**:
The length of time one data point represents in a moving average: one of 1m, 5m, 30m, 1d, 1w.
_Avoid_: Interval, timeframe, resolution

**Bucket**:
The group of consecutive Candles that make up one data point of a given Period; its value is the Price of its last Candle.
_Avoid_: Window, bar

**24h Average Price**:
The simple arithmetic mean of the Prices of all Candles in the last 24 hours, using only the Candles available.
_Avoid_: Daily average, 24h ticker price

**Simple Moving Average**:
The arithmetic mean of the Prices of the latest `n` Buckets of a given Period.
_Avoid_: SMA line, rolling average
