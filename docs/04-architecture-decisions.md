# 04 — Architecture Decision Records (ADR)

Each record states the context, the decision, the alternatives considered and the trade-off accepted.

| ADR | Decision | Status |
|-----|----------|--------|
| [001](#adr-001--backend-platform-net-10-lts) | Backend on .NET 10 LTS | Accepted |
| [002](#adr-002--frontend-angular) | Frontend in Angular | Accepted |
| [003](#adr-003--database-microsoft-sql-server) | Microsoft SQL Server | Accepted |
| [004](#adr-004--clean-architecture-with-a-connector-model) | Clean Architecture + connector model | Accepted |
| [005](#adr-005--cqrs-lite-not-full-cqrs) | CQRS-lite (not full CQRS / event sourcing) | Accepted |
| [006](#adr-006--background-processing-with-hangfire-in-a-dedicated-worker) | Hangfire in a dedicated Worker | Accepted |
| [007](#adr-007--quota-aware-polling-instead-of-streaming) | Quota-aware polling (streaming-ready) | Accepted |
| [008](#adr-008--environment-specific-endpoint-files) | Environment-specific endpoint files | Accepted |
| [009](#adr-009--containerisation-with-docker) | Docker for every runtime component | Accepted |
| [010](#adr-010--no-commercially-licensed-mediator--mapper-libraries) | No MediatR / AutoMapper | Accepted |

---

## ADR-001 — Backend platform: .NET 10 LTS

**Context.** Production system expected to live for several years.
**Decision.** ASP.NET Core on **.NET 10 (Long-Term Support, supported to Nov 2028)**.
**Alternatives.** .NET 9 — Standard-Term Support, end of support Nov 2026; rejected for a production launch.
**Consequences.** Three years of security patches; access to built-in OpenAPI, HybridCache, rate limiting and resilience libraries.

## ADR-002 — Frontend: Angular

**Context.** Dashboard-heavy business application with forms, validation, role-based navigation; the engineering team is .NET-centric.

| Criterion | Angular | React | Blazor Server |
|-----------|---------|-------|---------------|
| Structure for long-lived enterprise apps | Opinionated: DI, routing, forms, HTTP built in | Assemble-your-own | Built in |
| Familiarity for .NET engineers | High (TypeScript, DI, services) | Medium | High |
| Horizontal scalability | Static files on CDN | Static files on CDN | **Stateful SignalR circuit per user** — needs sticky sessions |
| Resilience to network blips | Stateless API calls | Stateless | UI freezes when the circuit drops |

**Decision.** Angular with standalone components, signals and Angular Material; mobile-first responsive layout.
**Consequences.** Stateless front end deployable to any static host/CDN; consistent, strongly typed codebase on both tiers. The API is reusable by future mobile apps or BI tools.

## ADR-003 — Database: Microsoft SQL Server

**Context.** The client places **security and governance** above licence cost. The data is relational (Topic → Rules → Posts → Metric snapshots) and the workload is analytical (group-by, time buckets, filters).

**Decision.** Microsoft SQL Server (2022 locally / in containers; Azure SQL or SQL Server in UAE regions for hosted environments).

**Why SQL Server for a security-first client**

| Capability | Benefit to Rasid |
|-----------|------------------|
| Transparent Data Encryption (TDE) | Data files and backups encrypted at rest |
| Always Encrypted | Column-level encryption where keys never reach the DB engine (available for sensitive columns) |
| TLS-enforced connections (`Encrypt=True`) | Data encrypted in transit |
| Row-Level Security | Ready for multi-department / multi-tenant isolation |
| Dynamic Data Masking | Limit exposure of sensitive columns to lower-privileged roles |
| SQL Server Audit / Azure SQL Auditing | Tamper-evident record of data access |
| Microsoft Entra authentication + Managed Identity (Azure) | Password-less connections in hosted environments |
| Mature compliance certifications on Azure (ISO 27001, SOC 1/2/3, etc.) | Easier client security review |
| **First-party Hangfire storage** | Background job storage officially supported |
| Columnstore indexes | Fast analytics over large metric tables as data grows |

**Alternatives.** PostgreSQL — licence-free and portable, but the client prioritises the SQL Server security/governance ecosystem and its existing standards. Firestore / NoSQL — poor fit for relational, aggregate-heavy analytics and for data-residency control.
**Consequences.** Licence cost in production (accepted by client). EF Core keeps the data-access layer provider-agnostic.

## ADR-004 — Clean Architecture with a connector model

**Decision.** Four production projects (Domain, Application, Infrastructure, hosts) with inward-only dependencies. Data sources implement the `IPostSource` port.
**Why.** Business rules (engagement calculation, query construction, quota policy) are framework-free and unit-testable. X is an adapter; new platforms are additive.
**Trade-off.** More projects than a single-project app — justified by the stated plan to grow into a full product.

## ADR-005 — CQRS-lite, not full CQRS

**Context.** Writes are bursty and machine-driven (Worker ← X API). Reads are frequent and human-driven (dashboards with filters). Live `GROUP BY` over growing post tables would degrade dashboard latency.
**Decision.** Separate command and query handlers in code; the Worker maintains **pre-aggregated hourly/daily tables**; dashboards read aggregates. **One database.**
**Rejected.** Full CQRS with separate read store and event sourcing — operational complexity without benefit at this scale.
**Consequences.** Dashboard latency stays flat as data grows; read and write workloads scale independently.

## ADR-006 — Background processing with Hangfire in a dedicated Worker

| Criterion | `BackgroundService` | **Hangfire** | Azure Service Bus |
|-----------|--------------------|--------------|-------------------|
| Jobs survive restarts | No | Yes (persisted in SQL Server) | Yes |
| Automatic retries | Hand-written | Built in | Built in |
| Cron scheduling | Hand-written | Built in | Needs separate scheduler |
| No duplicate execution across replicas | Hand-written | Distributed locks | Built in |
| Operations dashboard | No | Built in (Admin only) | Portal |
| Extra infrastructure | None | **None — uses SQL Server** | Additional paid service |

**Decision.** Hangfire with SQL Server storage, hosted in `Rasid.Worker`, separate from the API.
**Why.** Production requires durable, retryable, non-duplicated jobs; Hangfire provides this without new infrastructure. Duplicate jobs would directly waste paid X API quota.
**Future.** If multiple platforms and very high volumes arrive, a message broker can be introduced behind the same job interfaces.

## ADR-007 — Quota-aware polling instead of streaming

**Context.** X Filtered Stream (real-time) is available only on higher API tiers.
**Decision.** Scheduled polling of Recent Search with `since_id`, tiered metric refresh and a Quota Manager that reads `x-rate-limit-*` headers and enforces a monthly budget.
**Consequences.** Freshness of minutes rather than seconds; predictable cost. A streaming connector can be added without changing the core.

## ADR-008 — Environment-specific endpoint files

**Decision.** All base URLs live in one dedicated file per environment — `endpoints.Development.json`, `endpoints.UAT.json`, `endpoints.Production.json` (backend) and `environment.*.ts` (frontend). Secrets never appear in these files.
**Why.** Single place to change URLs per environment; reviewers can diff environments; no URL is hard-coded in code. See [09](09-environments-and-configuration.md).

## ADR-009 — Containerisation with Docker

**Decision.** Separate images for `rasid-web` (Nginx + Angular), `rasid-api`, `rasid-worker`; SQL Server container for local and UAT. Multi-stage builds, non-root users, health checks.
**Why.** Identical artefacts promoted across environments; one-command local start-up; host-agnostic deployment.

## ADR-010 — No commercially licensed mediator / mapper libraries

**Context.** MediatR and AutoMapper moved to commercial licensing in 2025.
**Decision.** A small in-house command/query dispatcher and explicit mapping methods.
**Why.** Removes licensing risk for the client; explicit mapping is easier to read and review for security.
