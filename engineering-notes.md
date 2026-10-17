# Engineering notes

A living document for the VSG technical assessment. It records the decisions made and how AI was used, so a reviewer can follow the reasoning. Planning is tracked on the GitHub issue map "Wayfinder: VSG assessment implementation spec".

## Decisions so far

| Area | Decision | Why |
| --- | --- | --- |
| Platform | .NET 10 (LTS), C# 14 | Current LTS and already installed |
| Architecture | Clean architecture (Domain / Application / Infrastructure / Api), single ASP.NET Core host running ingestion as a `BackgroundService` plus the API | Simplest deployable unit for the time budget. Splitting ingestion into its own worker is a documented future improvement (independent scaling, isolated failures, no duplicate ingesters when the API scales out) |
| Database | SQL Server (Docker Compose locally, Azure SQL in the proposal), EF Core | Author's experience; first-party EF Core provider; natural fit for the Azure deployment proposal. Cost: large Docker image, amd64-only |
| Collected data | Closed 1-minute klines (`<symbol>@kline_1m`) on one combined Binance stream | All SMA periods (5m, 30m, 1d, 1w) aggregate from 1m candles; order-book and ticker streams are not needed |
| Price | Close of a closed 1m Candle | One definition serves both endpoints |
| 24h average | Simple mean of closed 1m closes over a rolling 24h, partial windows allowed, `404` when there is no data | Plain reading of the assessment; Binance's weighted average and a volume-weighted mean are documented alternatives |
| Precision | `decimal` end to end, results rounded to 8 dp | Binance sends prices as strings; avoids floating-point error |
| SMA window | `s` is the end Reference Instant (omitted = now): the `n` Buckets ending with the one containing `s`. Bucket value = last Candle's close; the in-progress Bucket is included; a Candle counts only if closed by `s` | Trailing SMA is the standard definition and a start that defaults to now would have no data. Departs from the PDF's word "starting", stated in the README |
| SMA alignment and gaps | Everything UTC, weeks start Monday 00:00 (observed on Binance, undocumented). Missing Buckets are not filled; average what exists, `isPartial` when fewer than `n`, `404` when none | Same "average what exists" rule as the 24h average; forward-fill would present uncollected data as real. Forward-fill is a README trade-off |
| SMA API contract | One object (`symbol`, `period`, `requested`, `from`, `to`, `average`, `dataPoints`, `isPartial`, `latestBucketInProgress`); `n` 1..1000 required, `p` one of five values (case-sensitive) required, `s` optional; bad input is `400` ProblemDetails | The PDF says "returns the SMA", singular; strict validation, no silent defaults |
| Symbol management | Admin API `GET` / `PUT /{symbol}` / `DELETE /{symbol}` under `/api/admin/symbols`. A DB table is the source of truth, seeded once from config. Add is validated against Binance `exchangeInfo` (`422`, `503` if Binance REST is down). Changes apply live via an ingestion reconcile loop (desired set in DB vs subscribed set), with an async 24h backfill on add. Removal stops collection but keeps history; reads then return `404`. Idempotent (`PUT` 201/200, `DELETE` 204). Cap of 20 symbols | A typo must not create a symbol that never gets data; reconciling makes failed subscribe frames self-healing and restarts trivial. Purge/retention is a future improvement |
| Admin auth | Static `X-Api-Key` header on `/api/admin/*` only; key from config/env (Key Vault in the Azure proposal) | The PDF asks for no auth, but open admin endpoints would be the first thing a reviewer flags. Entra ID/JWT with roles is the documented upgrade |
| Deployment | Written Azure proposal plus working `docker compose up`; no real cloud deploy | Time budget (about 10-12 hours) |

Open decisions (symbol management, ingestion pipeline, data model, API surface, console client, solution structure) are tracked as tickets on the map. Vocabulary is in [GLOSSARY.md](GLOSSARY.md); plain-language background is in [docs/primer-market-data.md](docs/primer-market-data.md); sourced research is in [docs/research/](docs/research/).

## AI usage

AI assistance: Claude Code (Claude Sonnet 5.5) in the desktop app, using the `research`, `wayfinder`, `grilling` and `domain-modeling` skills from Matt Pocock's skills collection (plugin `mattpocock-skills`, v1.3.1), published through [AI Hero](https://www.aihero.dev) ([skills newsletter](https://www.aihero.dev/s/skills-newsletter), [source repository](https://github.com/mattpocock/skills)). These skills shaped the workflow: research delegated to background agents against primary sources, a shared decision map on GitHub Issues, and decisions made through numbered questions with recommended answers. The AI was used for reading and research, structuring decisions, and drafting documents. Decisions were put to the author as numbered questions with a recommended answer; the author accepted, changed or rejected each. Nothing below has been compiled into the final build yet.

### Prompts and outcomes

| # | Prompt (author's words, lightly trimmed) | What the AI did | What the author kept, changed or rejected |
| --- | --- | --- | --- |
| 1 | `/research` with no question | Asked for the question | Supplied the question in the next prompt |
| 2 | "look at Technical Assessment at VSG.pdf and research Binance Web Socket API to find the needed data to collect" | Background agent extracted the PDF text and researched Binance's official docs, plus a live 10-second connection to the public stream. Wrote `docs/research/binance-websocket-market-data.md` | Adopted its recommendation to collect only closed 1m klines. Several items remain flagged unverified (for example live subscribe/unsubscribe) |
| 3 | "Simple Moving Average algorithm, also mentioned in the same PDF" (`/research`) | Background agent researched the SMA definition and Binance interval behaviour, tested the week boundary live. Wrote `docs/research/simple-moving-average.md` | Open: its reading of `s` as the end of the window is a recommendation, not yet decided |
| 4 | "take a look at the Technical Assessment pdf. I want us to complete this assessment successfully and make a good impression ... Senior .NET Developer position" (`/wayfinder`) | Grilled on the destination, then charted a map of decision tickets on GitHub Issues | Chose plan-only, GitHub Issues as tracker, scope of about 10-12 hours with extras documented as future improvements |
| 5 | "are there any downsides to [SQL Server] in comparison to Postgre?" | Compared them honestly (image size, amd64-only, licensing) | Rejected the AI's initial PostgreSQL recommendation; chose SQL Server |
| 6 | "single host for now and add separating it as a future improvement, highlighting the benefits" | Recorded the decision | Author's own addition to the AI's recommendation |
| 7 | "first, give me an introduction to the domain terms used in binance API and the simple moving average algorithm, to a person with minimal finance knowledge" | Wrote a plain-language explanation | Kept; added as `docs/primer-market-data.md` |
| 8 | Answered round 1 of "Define the price and 24h average semantics" (Q1-Q7: yes) | Recorded the decisions on the ticket, created `GLOSSARY.md` | Accepted all seven recommendations |
| 9 | "lets include what we have by now about AI prompts as well, in engineering-notes.md" | Created this file | - |
| 10 | Answered two rounds of "Define the Simple Moving Average contract" (Q1-Q4, then Q5-Q9) | Claimed the ticket, grilled in numbered rounds, recorded the resolution, closed the ticket, updated the map and `GLOSSARY.md` (Bucket, Simple Moving Average, Reference Instant, In-progress Bucket) | Accepted all nine recommendations. Asked what forward-fill means before accepting Q5 (no fill) |
| 11 | "also interested what our options are for healing the data if we have such gaps" | Listed backfill options (REST klines on startup, on reconnect, on symbol add; periodic reconciliation) | Kept out of the SMA contract; recorded as pointers on the ingestion (#6) and symbol management (#5) tickets |
| 12 | Answered "Design symbol management" (Q1-Q9: yes). Asked whether Q4 meant the Add endpoint waits for market data, and what "reconcile loop" means | Claimed the ticket, grilled nine questions, explained that the add returns after the DB write and the reconcile loop does the subscribing, recorded the resolution, closed the ticket, left pointers on #6, #7 and #8, updated the map and `GLOSSARY.md` (Removed Symbol) | Accepted all nine recommendations, including the API key on admin routes. The AI first posted two pointer comments on the wrong tickets (misnumbered) and deleted and reposted them |

### Corrections and caveats worth knowing

- While testing GitHub access, the AI accidentally created a throwaway test issue on the repository. It was noticed straight away and deleted; nothing else was affected.
- Research agents flagged parts of their findings as unverified (Binance connection limits, some XML serializer details, hosted-service behaviour in `WebApplicationFactory`, the Testcontainers SQL Server defaults). These are to be checked by a spike before being relied on.
- The AI's reading of the assessment PDF came from decoding its text streams; some punctuation was garbled, and the author should re-check any wording that matters against the original.

This table is updated as the work continues.
