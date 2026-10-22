# 10 — Submission Summary (Task 2: X Monitoring Prototype)

Submitted by **Abdul Mohsin** to **The Global Advisors**.

This page maps the assessment requirements to what Rasid delivers and where to find it.

## 1. Requirements coverage

| Requirement | Where it is delivered |
|-------------|-----------------------|
| Interface to add a monitoring topic with keywords, hashtags or public accounts | **Topics → New topic**. Chip inputs per rule type, exclusions, priority, and a live preview of the exact X query with a length check |
| One sample topic demonstrating the full workflow | *UAE Humanitarian Aid*, created on first start-up, then collected, aggregated and shown on its dashboard |
| Display public posts for the topic within X's access limits | Dashboard **Posts** list (sortable, paged) and **Top posts**. Collection is quota-aware (see [07](07-x-api-and-compliance.md)) |
| Posts, likes, reposts, replies, views, engagement rate / level | KPI cards, interaction strip, engagement-level chip and distribution ([06](06-engagement-metrics.md)) |
| Explain how the engagement metric is calculated | In the dashboard (*How the numbers are calculated*), on the **Method** page, and in [06](06-engagement-metrics.md) |
| Filter by date range, keyword or engagement type | Filter bar: 24 h / 3 d / 7 d / 30 d / custom range, keyword, engagement type, engagement level |
| Visual summary | Overview of all topics with sparklines; per topic: animated KPI cards with trends against the previous period, engagement gauge, multi-series engagement chart with spike markers, engagement mix, engagement levels |
| Insights beyond the brief | Key highlights in plain language, top hashtags, top accounts, rule performance, activity heatmap (weekday × hour), language mix, and alerts (spikes, collection problems, stale data, API budget) |
| Media | Post images and video previews from X (`attachments.media_keys`), with alt text. Sample posts use licensed stock photos labelled "Sample image" |
| Date/time of the latest update and the data source | Freshness banner on every dashboard: *Live · X API v2* or *SAMPLE DATA*, last update (relative and local time), status |
| Handle unavailable data and connection failures | Defined states: collecting, no results, deferred (quota), rate limited, credentials error, connection failed, not configured, stale. The last good data stays visible ([03 §8](03-flowcharts.md#8-data-availability-states-shown-to-the-user)) |
| No prohibited collection, no bypassing restrictions, labelled sample data | Official X API v2 only. Sample connector with fictitious accounts, labelled on every screen ([07](07-x-api-and-compliance.md)) |

## 2. Technical architecture (brief)

- **Angular 22 SPA.** Mobile-first, light and dark themes, lazy-loaded features. All HTTP calls go through typed services.
- **ASP.NET Core 10 API.** Clean Architecture with CQRS-lite, JWT authentication, role-based policies, ProblemDetails errors, rate limiting, OpenAPI with Swagger UI.
- **Worker.** A Hangfire server in its own process: discovery scheduler, per-topic distributed locks, tiered metric refresh, retention.
- **SQL Server.** Three schemas (`rasid`, `auth`, `hangfire`) and a pre-aggregated hourly read model.
- **X connector.** Typed `HttpClient`, auth handler, rate-limit handler, and a resilience pipeline (timeout, retry, circuit breaker; 429 is never retried blindly).
- **Docker.** Non-root images. `docker compose up` starts the whole stack.

Details: [02 — Architecture](02-architecture.md), [04 — Decisions](04-architecture-decisions.md).

## 3. Data source

- **Live:** X API v2 `GET /2/tweets/search/recent` (discovery) and `GET /2/tweets` (metric refresh and deletion detection), with an App-Only bearer token.
- **Demonstration:** without a token, the **Sample** connector produces deterministic, fictitious posts that behave like a live feed. New posts appear over time, engagement grows, and a few posts are "deleted" so the deletion-sync path can be shown. Every screen marks this data as SAMPLE.

## 4. Engagement method

`Interactions = Likes + Reposts + Replies + Quotes`
`Post rate = Interactions ÷ Views × 100`
`Topic rate = Σ Interactions ÷ Σ Views`, over posts with view counts.
Missing views give **N/A**, never an estimate. Levels: High > 3 %, Medium 1–3 %, Low < 1 % (configurable).

## 5. Known limitations

1. Live data requires an X API plan that includes Recent Search. Its rate limits and monthly read cap bound freshness and volume.
2. Recent Search covers 7 days. History builds up from the day a topic is created.
3. View counts depend on what X returns for each post and plan. When absent, rates are N/A.
4. Metrics are point-in-time snapshots that refresh on a schedule: every 15 min for posts under a day old, every 2 h up to 3 days, then daily.
5. Dashboard responses are cached for up to 60 s. A new collection run changes the cache key, so fresh data is never hidden behind the cache.
6. The sample connector understands plain match terms, not full X query syntax.
7. The UI is in English. Arabic text in posts is displayed correctly (bidirectional). A full Arabic UI is a planned extension.
8. Trends are shown as "new" when the previous period predates the topic's data coverage. With live X data a new topic shows week-over-week trends after its first week. The sample feed simulates 21 days of history to demonstrate them.
9. Insights analyse up to 20,000 posts per request (newest first). The response says when that limit applies.
10. The live X connector is verified with contract tests built from the official documentation. It has not yet been run against a real X account, because that requires a paid X API plan.

## 6. Assumptions

See [01 — Project plan §5](01-project-plan.md#5-assumptions).

## 7. Tests

| Suite | Scope | Count |
|-------|-------|-------|
| `Rasid.Domain.Tests` | Engagement formulas, levels, query builder, rule validation, quota policy, topic rules, source-aware cursors, Snowflake time, spike detection, hashtag extraction, trend maths | 72 |
| `Rasid.Application.Tests` | End-to-end flows against SQL Server in a container: validation, ingestion, incremental cursor, parallel collection of shared posts, rate limiting, budget deferral, metric refresh and deletion sync, dashboard consistency, trends and coverage, insights, overview, alerts, media, sample → live switch, topic deletion. Plus X connector contract tests (both field-naming styles, media, 429, lookup errors) and start-up URL validation per environment | 35 |
| Angular (Vitest) | Auth interceptor (token, refresh and retry, sign-out), theme, filters, rule validation, formatting, trend chip, animated number, sparkline | 20 |

UI verification: automated browser runs (Microsoft Edge) on desktop and phone (390 px), in light and dark themes, with no console errors. A horizontal-overflow check covers all seven pages.

## 8. Tools and disclosure

- **Frameworks and libraries:** .NET 10, ASP.NET Core, Entity Framework Core, ASP.NET Core Identity, Hangfire, FluentValidation, Polly (via Microsoft.Extensions.Http.Resilience), Serilog, Scalar and Swashbuckle Swagger UI, Angular 22, Angular Material, Chart.js and ng2-charts, xUnit, Testcontainers, Vitest. Fonts: Inter, Noto Kufi Arabic, JetBrains Mono, Material Symbols (Google Fonts).
- **Services:** X API v2 (optional, for live data). No other external services.
- **Media:** six stock photographs from Unsplash (Unsplash License) illustrate sample posts only. They are credited in `src/frontend/rasid-web/public/sample-media/CREDITS.md`.
- **AI assistance:** This solution was designed and implemented with the help of an AI coding assistant (Anthropic Claude, via Claude Code). That covered architecture discussion, code generation, documentation drafting and test authoring. Every component was built, run and verified locally, with test suites and UI checks in desktop and mobile layouts.
