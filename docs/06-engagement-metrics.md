# 06 — Engagement Metrics

All indicators come from the `public_metrics` object that the X API v2 returns for each public post.
No metric is estimated, scraped or inferred.

## 1. Raw indicators (per post)

| Indicator | X API field | Shown as |
|-----------|-------------|----------|
| Likes | `like_count` | Likes |
| Reposts | `retweet_count` | Reposts |
| Replies | `reply_count` | Replies |
| Quotes | `quote_count` | Quotes |
| Views | `impression_count` | Views (may be unavailable) |

## 2. Formulas

**Interactions (per post)**

```
Interactions = Likes + Reposts + Replies + Quotes
```

**Engagement Rate (per post)**

```
Engagement Rate (%) = Interactions ÷ Views × 100
```

- If `Views` is missing or `0`, the rate is **N/A** — never estimated, never shown as 0 %.

**Topic Engagement Rate (weighted)** — the headline KPI

```
Topic Engagement Rate (%) = Σ Interactions of posts that have views
                            ÷ Σ Views of those posts × 100
```

- Weighted by views so a single low-view post with 3 likes cannot distort the topic figure.
- Posts without views are counted in *Posts* and *Interactions* totals but excluded from the rate's numerator and denominator.
- The dashboard shows a coverage note, e.g. *"Rate based on 182 of 190 posts with view data."*

**Engagement Level Distribution** — how many posts are High / Medium / Low / N/A. Complements the
weighted rate by showing whether engagement is broad-based or driven by a few viral posts.

## 3. Engagement level

| Level | Engagement Rate | Badge |
|-------|-----------------|-------|
| High | > 3 % | High |
| Medium | 1 % – 3 % | Medium |
| Low | < 1 % | Low |
| Unknown | Views not available | N/A |

Thresholds are **assumptions based on common social-media benchmarks**, not an X standard. They are configurable
per environment (`Engagement:Thresholds`) so the client can calibrate them.

## 4. Engagement type filter

The filter "engagement type" lets analysts focus on one interaction kind:
*All · Likes · Reposts · Replies · Quotes*. KPIs, charts and post ranking switch to that measure.

## 5. Worked example

| Post | Likes | Reposts | Replies | Quotes | Views | Interactions | Rate | Level |
|------|------:|--------:|--------:|-------:|------:|-------------:|-----:|-------|
| A | 120 | 30 | 10 | 5 | 6,000 | 165 | 2.75 % | Medium |
| B | 8 | 1 | 0 | 0 | 2,000 | 9 | 0.45 % | Low |
| C | 40 | 15 | 6 | 2 | — | 63 | N/A | N/A |

- Total interactions: 165 + 9 + 63 = **237**
- Topic rate: (165 + 9) ÷ (6,000 + 2,000) × 100 = **2.18 %** → *Medium* (coverage: 2 of 3 posts)

## 6. Trends (period over period)

Every headline indicator is compared with the **previous period of the same length**, for example the last 7 days against the 7 days before.

```
Change (%)  = (current − previous) ÷ previous × 100
Rate change = current rate − previous rate   (percentage points)
```

- With no previous value the change is shown as **"new"**, never as an infinite percentage.
- **Data coverage rule.** A topic's data is complete from *topic creation − search window* (X Recent Search: 7 days). When the previous period starts before that point it would be compared with data never collected, so trends are reported as "new" instead of an inflated figure.

## 7. Spikes

A period is flagged as a **spike** when its post count is above *mean + 2 standard deviations* of the displayed series and at least 3 posts (at least 5 for alerts). Spikes are marked on the activity chart. A spike in the last 24 hours raises an alert.

## 8. Insights

| Insight | Definition |
|---------|-----------|
| Top hashtags | Hashtags extracted from post text, ranked by the share of posts containing them |
| Top accounts | Authors ranked by interactions on their posts in the period. The rate is view-weighted per account |
| Rule performance | For each inclusion rule, the share of the topic's posts it matches |
| Activity heatmap | Posts by weekday and hour, in the viewer's local time |
| Languages | Share of posts by the language code X reports |
| Highlights | Plain-language statements generated from the above |

Insights cover at most 20,000 posts per request (newest first). The response says when that cap applies.

## 9. Point-in-time nature

Metrics are snapshots taken at `CapturedUtc`. The dashboard always shows when the values were captured.
Refresh cadence by post age is defined in [03 §6](03-flowcharts.md#6-tiered-metric-refresh).
