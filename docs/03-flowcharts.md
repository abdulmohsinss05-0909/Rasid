# 03 — Flowcharts

Every important runtime flow, end to end.

1. [User journey (end to end)](#1-user-journey-end-to-end)
2. [Create a monitoring topic](#2-create-a-monitoring-topic)
3. [Scheduled ingestion job](#3-scheduled-ingestion-job)
4. [Calling the X API (resilience & rate limits)](#4-calling-the-x-api-resilience--rate-limits)
5. [Quota Manager decision](#5-quota-manager-decision)
6. [Tiered metric refresh](#6-tiered-metric-refresh)
7. [Dashboard query with filters](#7-dashboard-query-with-filters)
8. [Data availability states shown to the user](#8-data-availability-states-shown-to-the-user)
9. [Retention & deletion compliance job](#9-retention--deletion-compliance-job)
10. [Authentication & authorisation](#10-authentication--authorisation)

---

## 1. User journey (end to end)

```mermaid
flowchart LR
    A([Sign in]) --> B[Topics list]
    B --> C{Topic exists?}
    C -- No --> D[Create topic:<br/>keywords · hashtags · accounts]
    D --> E[Preview generated X query]
    E --> F[Save]
    F --> G[First collection queued]
    C -- Yes --> H[Open topic dashboard]
    G --> H
    H --> I[KPIs · charts · top posts]
    I --> J[Apply filters:<br/>date · keyword · engagement type · level]
    J --> I
    I --> K[Open post on X]
```

## 2. Create a monitoring topic

```mermaid
flowchart TD
    S([Analyst submits topic form]) --> V1{Client-side validation<br/>Angular reactive form}
    V1 -- invalid --> E1[Show inline field errors]
    V1 -- valid --> P[POST /api/v1/topics]
    P --> AU{Authenticated &<br/>role ≥ Analyst?}
    AU -- No --> E2[401 / 403 ProblemDetails]
    AU -- Yes --> V2{Server validation<br/>FluentValidation}
    V2 -- invalid --> E3[400 ProblemDetails<br/>with field errors]
    V2 -- valid --> QB[XQueryBuilder builds query<br/>e.g. #quot;UAE aid#quot; OR #35;UAEAid OR from:account<br/>-is:retweet]
    QB --> QL{Query length within<br/>plan limit?}
    QL -- No --> E4[400: query too long,<br/>suggest splitting topic]
    QL -- Yes --> DUP{Topic name unique?}
    DUP -- No --> E5[409 Conflict]
    DUP -- Yes --> SAVE[(Save Topic + Rules<br/>Audit log entry)]
    SAVE --> ENQ[Enqueue immediate<br/>ingestion job — Hangfire]
    ENQ --> R[201 Created + Location]
    R --> UI[UI navigates to dashboard<br/>shows 'Collecting data…']
```

## 3. Scheduled ingestion job

```mermaid
flowchart TD
    T([Hangfire recurring trigger<br/>every N minutes]) --> L{Acquire distributed lock<br/>for this topic}
    L -- held by another worker --> SKIP([Skip — prevents duplicate API spend])
    L -- acquired --> Q{Quota Manager:<br/>may we call X now?}
    Q -- No --> DEF[Record 'Deferred' in RefreshLog<br/>with next allowed time] --> END([End])
    Q -- Yes --> SRC{Connector configured?}
    SRC -- X credentials present --> XS[XPostSource.SearchRecent<br/>since_id = last seen post]
    SRC -- none --> SS[SamplePostSource<br/>source = SAMPLE]
    XS --> RES{Outcome}
    SS --> RES
    RES -- Success --> M[Map to domain<br/>Post + MetricSnapshot]
    M --> UP[(Upsert posts<br/>append snapshots)]
    UP --> PAG{next_token<br/>and budget left?}
    PAG -- Yes --> XS
    PAG -- No --> AGG[Update hourly / daily aggregates]
    AGG --> LOG[(RefreshLog: Success,<br/>posts fetched, API calls used)]
    LOG --> CI[Invalidate dashboard cache<br/>for topic]
    CI --> END
    RES -- 401/403 --> F1[(RefreshLog: AuthFailed<br/>admin action required)]
    RES -- 429 --> F2[(RefreshLog: RateLimited<br/>reset time stored)]
    RES -- timeout / 5xx after retries --> F3[(RefreshLog: ConnectionFailed)]
    F1 --> END
    F2 --> END
    F3 --> END
```

## 4. Calling the X API (resilience & rate limits)

```mermaid
sequenceDiagram
    autonumber
    participant J as Ingestion Job
    participant S as XPostSource
    participant A as XAuthHandler
    participant R as XRateLimitHandler
    participant P as Resilience pipeline
    participant X as api.x.com/2
    participant Q as Quota Manager

    J->>S: SearchRecentAsync(query, sinceId)
    S->>A: GET /tweets/search/recent
    A->>A: attach Bearer token (from secret store)
    A->>R: forward
    R->>P: forward
    P->>X: HTTPS request (timeout guarded)
    alt 2xx
        X-->>P: posts + users + public_metrics
        P-->>R: response
        R->>Q: record x-rate-limit-remaining / reset
        R-->>S: response
        S-->>J: Result.Success(posts)
    else 429 Too Many Requests
        X-->>P: 429 + x-rate-limit-reset
        P-->>R: (no blind retry — honour reset)
        R->>Q: mark window exhausted until reset
        S-->>J: Result.Failure(RateLimited, resetAt)
    else 5xx / timeout
        P->>X: retry with exponential backoff + jitter
        P-->>S: circuit opens after repeated failures
        S-->>J: Result.Failure(ConnectionFailed)
    end
```

## 5. Quota Manager decision

```mermaid
flowchart TD
    IN([Request to call X for topic T]) --> W{Rate-limit window<br/>exhausted?}
    W -- Yes --> NO1[Deny until x-rate-limit-reset]
    W -- No --> M{Monthly read budget used}
    M -- "< 80 %" --> OK[Allow — normal cadence]
    M -- "80–95 %" --> TH[Allow — throttled cadence<br/>low-priority topics refresh less often<br/>admin alerted]
    M -- "> 95 %" --> CR{Topic priority = High?}
    CR -- Yes --> OK2[Allow minimal discovery only]
    CR -- No --> NO2[Deny — protect remaining budget]
```

## 6. Tiered metric refresh

Engagement on a post decays quickly, so refresh effort follows the post's age.

```mermaid
flowchart LR
    P([Post in cache]) --> A{Post age}
    A -- "< 24 h" --> T1[Refresh metrics<br/>every 15 min]
    A -- "1 – 3 days" --> T2[Every 2 h]
    A -- "3 – 7 days" --> T3[Daily]
    A -- "> retention period" --> T4[Purge content<br/>keep aggregates only]
    T1 & T2 & T3 --> B[Batched lookup<br/>GET /2/tweets?ids= up to 100 per call]
```

## 7. Dashboard query with filters

```mermaid
flowchart TD
    UI([Analyst changes filter]) --> D[Debounce 300 ms]
    D --> REQ["GET /api/v1/topics/{id}/dashboard<br/>?from, to, keyword, type, level"]
    REQ --> C{Cache hit<br/>for this filter key?}
    C -- Yes --> RESP
    C -- No --> AGG[(Query pre-aggregated<br/>hourly / daily tables)]
    AGG --> POSTS[(Top posts — indexed query)]
    POSTS --> META[Attach freshness metadata:<br/>lastUpdatedUtc · source · status · isStale]
    META --> STORE[Store in cache<br/>short TTL]
    STORE --> RESP[200 DashboardDto]
    RESP --> RENDER[KPI tiles · charts · posts list<br/>source badge · 'Last updated']
```

## 8. Data availability states shown to the user

```mermaid
stateDiagram-v2
    [*] --> Collecting: topic just created
    Collecting --> Live: first successful refresh
    Collecting --> NoResults: success, 0 posts
    Live --> Live: refresh OK
    Live --> Stale: last success older than 2× refresh interval
    Live --> RateLimited: 429 received
    Live --> ConnectionFailed: timeouts / 5xx after retries
    Live --> AuthError: 401 / 403
    RateLimited --> Live: window reset, refresh OK
    ConnectionFailed --> Live: refresh OK
    Stale --> Live: refresh OK
    AuthError --> Live: credentials fixed by admin
    NoResults --> Live: posts found

    note right of Live
      Badge "Live — X API v2"
      or "SAMPLE DATA"
    end note
    note right of ConnectionFailed
      Last-known-good data stays visible,
      clearly marked with its timestamp
    end note
```

## 9. Retention & deletion compliance job

```mermaid
flowchart TD
    T([Daily Hangfire job]) --> A[Select cached posts<br/>due for re-validation]
    A --> B[Batched GET /2/tweets?ids=]
    B --> C{Post returned?}
    C -- Yes --> K[Keep · update metrics]
    C -- "No: deleted / protected / suspended" --> D[Delete post content<br/>from Rasid immediately]
    K --> R{Older than<br/>retention period?}
    R -- Yes --> P[Purge post content<br/>keep anonymous aggregates]
    R -- No --> E([Done])
    D --> AUD[(Audit log entry)]
    P --> AUD
    AUD --> E
```

## 10. Authentication & authorisation

```mermaid
sequenceDiagram
    autonumber
    participant B as Browser (Angular)
    participant API as Rasid.Api
    participant IDP as Identity provider<br/>(built-in / Entra ID)

    B->>API: POST /api/v1/auth/login (credentials)
    API->>IDP: validate
    IDP-->>API: user + roles
    API-->>B: short-lived access token (JWT)<br/>+ refresh token (HttpOnly, Secure, SameSite cookie)
    B->>API: GET /api/v1/topics<br/>Authorization: Bearer …
    API->>API: validate signature, expiry, audience<br/>apply policy (Viewer / Analyst / Admin)
    API-->>B: 200 data
    Note over B,API: On 401 the Angular interceptor silently refreshes once,<br/>then redirects to sign-in.
```

| Role | Permissions |
|------|-------------|
| Viewer | View dashboards |
| Analyst | Viewer + create / edit / pause topics, trigger refresh |
| Admin | Analyst + manage users, view system status, Hangfire dashboard, quota settings |
