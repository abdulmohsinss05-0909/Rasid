# 05 — Data Model

```mermaid
erDiagram
    TOPIC ||--o{ TOPIC_RULE : "defined by"
    TOPIC ||--o{ TOPIC_POST : "matches"
    POST  ||--o{ TOPIC_POST : "matched by"
    POST  ||--o{ METRIC_SNAPSHOT : "measured by"
    AUTHOR ||--o{ POST : "writes"
    TOPIC ||--o{ TOPIC_METRICS_HOURLY : "aggregated into"
    TOPIC ||--o{ REFRESH_LOG : "refreshed in"
    APP_USER ||--o{ AUDIT_LOG : "performs"

    TOPIC {
        guid Id PK
        string Name UK
        string Description
        string Priority "Low | Normal | High"
        bool IsActive
        string GeneratedQuery
        string LastSeenPostId "since_id cursor"
        datetime LastRefreshAttemptUtc
        datetime LastRefreshRequestedUtc "manual refresh cooldown"
        datetime LastSuccessfulRefreshUtc
        string LastRefreshStatus
        datetime CreatedUtc
        string CreatedBy
        rowversion RowVersion "optimistic concurrency"
    }
    TOPIC_RULE {
        guid Id PK
        guid TopicId FK
        string Type "Keyword | Hashtag | Account"
        string Value
        bool IsExclusion
    }
    POST {
        string Id PK "X post id"
        string AuthorId FK
        string Text
        string Lang
        datetime CreatedAtUtc
        string Url
        string Source "XApi | Sample"
        bigint Likes "current metrics (denormalised)"
        bigint Reposts
        bigint Replies
        bigint Quotes
        bigint Views "nullable"
        datetime MetricsCapturedUtc
        datetime FirstSeenUtc
        datetime LastValidatedUtc
        datetime ContentExpiresUtc
    }
    TOPIC_POST {
        guid TopicId PK, FK
        string PostId PK, FK
        string MatchedRules
    }
    AUTHOR {
        string Id PK "X user id"
        string Username
        string DisplayName
        string ProfileImageUrl
        bool Verified
    }
    METRIC_SNAPSHOT {
        bigint Id PK
        string PostId FK
        datetime CapturedUtc
        int Likes
        int Reposts
        int Replies
        int Quotes
        bigint Views "nullable"
    }
    TOPIC_METRICS_HOURLY {
        guid TopicId PK, FK
        datetime HourUtc PK
        int Posts
        bigint Likes
        bigint Reposts
        bigint Replies
        bigint Quotes
        bigint Views
        bigint ViewedPosts
        bigint ViewedInteractions
        bigint HighPosts
        bigint MediumPosts
        bigint LowPosts
        bigint UnknownPosts
    }
    REFRESH_LOG {
        bigint Id PK
        guid TopicId FK
        datetime StartedUtc
        datetime FinishedUtc
        string Status "Success | NoResults | Deferred | RateLimited | AuthFailed | ConnectionFailed"
        int PostsFetched
        int ApiCalls
        datetime RateLimitResetUtc
        string ErrorCode
    }
    AUDIT_LOG {
        bigint Id PK
        string UserId
        string Action
        string EntityType
        string EntityId
        datetime OccurredUtc
        string CorrelationId
    }
```

## Design notes

- **Many-to-many Topic ↔ Post.** One post may match several topics; it is stored once (no duplicate content, single deletion point).
- **Metric snapshots are append-only.** They keep the history; the post row holds the current values (denormalised) so lists and rankings need no join.
- **`TOPIC_METRICS_HOURLY`** is the read model for dashboards (CQRS-lite). Daily views are rolled up from it. `ViewedPosts` / `ViewedInteractions` cover only posts that have a view count, so the engagement rate is never inflated by posts without views (see [06](06-engagement-metrics.md)). Level counts (`HighPosts` …) power the level distribution without touching raw posts.
- **Schemas.** `rasid` (application), `auth` (identity), `hangfire` (jobs) — permissions can be granted per schema.
- **Minimal author data.** Only what X's display requirements need. No follower graphs, no profiling.
- **Retention.** `ContentExpiresUtc` drives purging of post text and author details; aggregates (which contain no personal content) are kept longer.
- **Concurrency.** `rowversion` on `TOPIC` prevents lost updates when two analysts edit the same topic.
- **Indexes.** `POST(CreatedAtUtc)`, `TOPIC_POST(TopicId, PostId)`, `METRIC_SNAPSHOT(PostId, CapturedUtc DESC)`, clustered key on `TOPIC_METRICS_HOURLY(TopicId, HourUtc)`. Columnstore on snapshots when volume justifies it.
