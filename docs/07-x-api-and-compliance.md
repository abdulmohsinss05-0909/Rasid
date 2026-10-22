# 07 — X API Integration & Compliance

## 1. Integration summary

| Item | Value |
|------|-------|
| API | X API v2 (official) |
| Base URL | `https://api.x.com/2` (configured in `endpoints.{Environment}.json`, never hard-coded) |
| Authentication | OAuth 2.0 App-Only **Bearer token**, held only in the secret store |
| Discovery endpoint | `GET /tweets/search/recent` — posts from the last 7 days matching the topic query |
| Refresh endpoint | `GET /tweets?ids=…` — up to 100 posts per call, for metric refresh and deletion detection |
| Fields requested | `tweet.fields=created_at,public_metrics,author_id,lang,attachments,note_tweet` · `expansions=author_id,attachments.media_keys` · `user.fields=name,username,profile_image_url,verified` · `media.fields=type,url,preview_image_url,alt_text`. All of these are configurable (`XApi` section), not hard-coded |
| Incremental fetch | `since_id` = newest post already stored for the topic |
| Pagination | `next_token`, bounded by the topic's call budget |
| Rate-limit signals | `x-rate-limit-limit`, `x-rate-limit-remaining`, `x-rate-limit-reset` response headers |

### Query construction

Topic rules are translated by the domain `XQueryBuilder`:

| Rule type | Example input | Query fragment |
|-----------|---------------|----------------|
| Keyword | `UAE aid` | `"UAE aid"` |
| Hashtag | `UAEAid` or `#UAEAid` | `#UAEAid` |
| Account | `@account` | `from:account` |
| Exclusion | keyword `giveaway` | `-giveaway` |

Inclusion rules are OR-ed and wrapped in parentheses; defaults `-is:retweet` (avoid double-counting) are appended.
The builder enforces the maximum query length of the client's X plan (configurable) and rejects unsafe characters.

### Verified against the official documentation (October 2026)

| Item | docs.x.com | Rasid |
|------|------------|-------|
| Search endpoint | `GET /2/tweets/search/recent`, `max_results` 10–100, `since_id` / `next_token` | Same |
| Lookup endpoint | `GET /2/tweets?ids=` (1–100 IDs). Missing, deleted or protected posts come back in `errors` | Same. Any ID not returned is treated as unavailable and removed |
| Rate limits | `x-rate-limit-limit` / `-remaining` / `-reset` headers, HTTP 429 | Headers parsed. A 429 waits for the reset and is never retried blindly |
| Field naming | Docs now describe `post.fields`, `repost_count` and `note_post`. The API still accepts `tweet.fields` (changelog, June 2026) | Responses are read with **both** naming styles. The request parameter name is configuration (`XApi:PostFieldsParameter`) |
| Pricing | Pay-per-use: $0.005 per post read. Repeat reads of the same post within 24 h UTC are not charged again | The Quota Manager budgets in posts read, so it maps directly to cost |

### Switching to live X data: configuration only

1. Create an X developer app at developer.x.com and generate an **App-Only Bearer token**.
2. Provide it as a secret:
   - Docker: `X_BEARER_TOKEN=…` in `deploy/docker/.env`, then run `docker compose up -d`.
   - Local: `dotnet user-secrets set "XApi:BearerToken" "<token>" --project src/backend/Rasid.Api`.
   - Hosted: environment variable `XApi__BearerToken` from the vault.
3. Set the monthly budget for the plan, e.g. `Monitoring__Quota__MonthlyPostReadBudget=2000` (about $10).
4. That is all. On the next scheduler cycle (within a minute):
   - the connector switches to X API v2 and every screen shows **Live · X API v2**;
   - all fictitious sample posts, the aggregates built from them and their cursors are removed automatically (audited as `sample.removed`), so live and sample data never mix;
   - each topic starts a fresh incremental cursor. Cursors older than 6 days are never sent, because X rejects a `since_id` outside its 7-day window.

Contract tests (`XConnectorContractTests`) exercise the connector with X-shaped responses in both naming styles, including media, 429 handling and lookup errors.

## 2. Compliance statement

Rasid is designed to comply with the **X Developer Agreement, X Developer Policy, Display Requirements and Terms of Service**. Specifically:

1. **Official API only.** All data is obtained exclusively through the authenticated X API v2. Rasid does not scrape web pages, automate browsers, use unofficial endpoints, rotate accounts or tokens, or circumvent any access control.
2. **Respect for rate limits and quotas.** Rate-limit headers are honoured. A `429` response is never blindly retried; requests wait until the published reset time. A Quota Manager keeps usage within the purchased plan.
3. **Public content only.** Only public posts returned by the API are processed. Protected accounts, direct messages and non-public data are never requested.
4. **Data minimisation.** Only the fields required for the stated purpose are requested and stored. No follower graphs, no off-platform enrichment, no profiling.
5. **Deletion and modification sync.** Cached posts are periodically re-validated through the API. Content that has been deleted, protected or made unavailable on X is removed from Rasid.
6. **Retention limits.** Post content expires after a configurable retention period; only non-personal aggregates are retained longer.
7. **No sensitive inference.** Rasid does not derive or infer sensitive characteristics of individuals (e.g. health, political affiliation, religious beliefs, ethnicity, sexual orientation).
8. **Topic monitoring, not individual surveillance.** Rasid monitors conversations around topics. It is not designed or offered for tracking or profiling individuals. Use cases involving government entities or law-enforcement require prior review under X's policy; the client must confirm the end-user organisation type before go-live.
9. **Display requirements.** Posts are presented with author display name, @username, timestamp and a link to the original post on X; text is not altered.
10. **No redistribution.** Raw X content is not sold, syndicated or exported in bulk. Exports (if enabled) contain post IDs and aggregate metrics only.
11. **Credential security.** The Bearer token is stored in a secret store, never in source code, configuration files, logs or the browser.
12. **Transparency.** Every screen shows the data source (*Live — X API v2* or *SAMPLE DATA*) and the time of the latest update. Sample data uses fictitious accounts and is never presented as real.

## 3. Sample mode

When no X credentials are configured (e.g. a demo without a paid X plan), Rasid uses the **Sample connector**:

- Deterministic, **fictitious** posts and accounts (e.g. `@sample_relief_news`) that behave like a live feed: new posts arrive over time, engagement grows, and some posts are deleted so the deletion sync can be shown. The feed simulates an established topic with 21 days of history so that trends can be demonstrated.
- Some sample posts carry **licensed stock photos** (Unsplash License, credited in `src/frontend/rasid-web/public/sample-media/CREDITS.md`). Each is labelled "Sample image". Photos showing real company branding were rejected.
- A persistent **SAMPLE DATA** badge on every screen and in API responses (`source: "Sample"`).
- The full pipeline (ingestion → aggregation → dashboard) runs unchanged, so the workflow is demonstrable end to end.
- Sample mode is disabled in Production configuration; missing credentials there fail the readiness health check.

## 4. Known limitations imposed by X

- Recent Search covers 7 days; older history accumulates from the day a topic is created.
- Monthly read caps and rate limits depend on the purchased plan and directly bound data freshness and volume.
- `impression_count` availability depends on the API plan and post; when absent, rates show N/A.
- Metric values are point-in-time and can change after capture.

> Pricing, limits and policy wording are set by X and change over time; they must be verified against
> developer.x.com at procurement and before each release.
