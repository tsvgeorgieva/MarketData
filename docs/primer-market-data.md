# Primer: market data terms and the Simple Moving Average

Plain-language background for readers with little finance knowledge. The project's own vocabulary is in [GLOSSARY.md](../GLOSSARY.md).

## Binance and market-data terms

**Exchange and symbol.** Binance is a marketplace where people trade cryptocurrencies. Each thing you can trade is a **pair** with a **symbol**, such as `BTCUSDT`. That means "Bitcoin priced in USDT". USDT is a stablecoin, a coin designed to stay worth about one US dollar. So `BTCUSDT = 81,589` roughly means one Bitcoin costs 81,589 dollars.

**Base and quote.** In `BTCUSDT`, BTC is the *base* (the thing being bought) and USDT is the *quote* (the currency its price is measured in). The assessment's three symbols are Bitcoin (BTC), Cardano (ADA) and Ethereum (ETH), each priced in USDT.

**Spot.** Buying or selling the real asset right now at the current price. Binance also has futures and other products. The assessment uses the Spot market.

**Trade.** One purchase matched with one sale: a price, a quantity and a time. Each trade is a single data point.

**Order book, bid and ask.** The exchange keeps a list of people waiting to buy (*bids*) and sell (*asks*) at stated prices. The best bid is the highest price a buyer will pay, and the best ask is the lowest price a seller will accept. This project does not need the order book, which is why it skips the depth and `bookTicker` streams: they send many updates per second.

**Last price.** The price of the most recent trade. It is what most people mean by "the current price".

**Candle (Binance calls it a kline).** Prices move constantly, so a day's data would be millions of trades. A candle summarises one fixed slice of time, such as one minute, in five numbers:

- **Open**: the price at the start of the minute
- **High**: the highest price during it
- **Low**: the lowest price during it
- **Close**: the price at the end of it
- **Volume**: how much was traded during it

(OHLCV is the usual abbreviation.) A 1-minute candle is one row summarising 60 seconds. Binance offers other lengths too (5m, 30m, 1d, 1w). A candle is **closed** once its minute has finished and its numbers can no longer change; Binance marks this with `x = true`. The current, unfinished minute is still moving.

**WebSocket and stream.** A normal web request is "ask, get an answer, done". A *WebSocket* is a connection that stays open so the server can push updates as they happen. A *stream* is one named feed on that connection, such as `btcusdt@kline_1m`, which means "send me BTCUSDT's 1-minute candle updates".

**Ticker.** A summary of a symbol over the last 24 hours (last price, high, low, volume). Binance's own "24h weighted average price" lives here, which is why a "24h average" could be defined several ways.

## Simple Moving Average (SMA)

Prices jump around, so a single number is noisy. A *moving average* smooths that noise by averaging the last few values.

**Plain example.** A stock closes at 10, 12, 11, 13, 14 on five days. The 3-day SMA on the last day is the average of the last three closes: (11 + 13 + 14) / 3 = **12.67**. The next day the window slides forward by one. The oldest value drops out and the new one comes in, which is why it is called "moving".

In the assessment's endpoint (`SimpleMovingAverage?n=&p=&s=`):

- **`p`** is how long each data point is: 1m, 5m, 30m, 1d or 1w. Each point is one candle of that length, represented by its closing price.
- **`n`** is how many of those points to average. `n=10, p=5m` means "the average of the last ten 5-minute closing prices", about 50 minutes of history.
- **`s`** is the optional date and time to calculate as of. If omitted, it means "now".

Traders read it like this: price above its SMA suggests an upward trend, and below suggests a downward one. A short window reacts fast but is noisy; a long window is smooth but slow.

**Why it matters for the design.**

- We store 1-minute candles only. A 5-minute point is built from five of them, a 1-day point from 1,440, and a 1-week point from 10,080.
- A *simple* average treats every point equally. An *exponential* average weights recent points more, but the assessment does not ask for that.
- Open questions are tracked on the wayfinder map: what to do when fewer than `n` points exist, whether `s` ends or starts the window, and how to treat gaps.

## The 24-hour average

The same idea without the sliding: the plain average of every closing price in the last 24 hours.
