# Rasid (رصد) — Solution Documentation

> **Rasid** (Arabic: *observation, monitoring*) is a production-grade platform for monitoring public posts on X
> around defined topics and presenting engagement indicators — built exclusively on X's official API and policies.

Prepared by **Abdul Mohsin** for **The Global Advisors**.

| # | Document | What it answers |
|---|----------|-----------------|
| 01 | [Project Plan](01-project-plan.md) | Scope, phases, deliverables, assumptions, dependencies |
| 02 | [Solution Architecture](02-architecture.md) | Layers, components, deployment, scalability |
| 03 | [Flowcharts](03-flowcharts.md) | Every key runtime flow, step by step |
| 04 | [Architecture Decision Records](04-architecture-decisions.md) | *Why* each technology / pattern was chosen, alternatives, trade-offs |
| 05 | [Data Model](05-data-model.md) | Entities, relationships, retention |
| 06 | [Engagement Metrics](06-engagement-metrics.md) | Exact formulas and edge cases |
| 07 | [X API Integration & Compliance](07-x-api-and-compliance.md) | How we integrate with X and how we comply with its rules |
| 08 | [Security](08-security.md) | Security controls, application → database → infrastructure |
| 09 | [Environments & Configuration](09-environments-and-configuration.md) | Dev / UAT / Production, base-URL files, secrets |
| 10 | [Submission Summary](10-submission-summary.md) | Requirement coverage, limitations, tests, tools and AI disclosure |
| 11 | [Setup Guide](11-setup-guide.md) | Set-up with Docker or local SQL Server, SQL scripts, putting the project under Git |

## Technology at a glance

| Layer | Technology |
|-------|------------|
| Frontend | Angular 22 (standalone components, signals) + Angular Material, mobile-first responsive, light/dark |
| Backend API | ASP.NET Core on .NET 10 (LTS), OpenAPI (Swagger UI / Scalar) |
| Background processing | Hangfire (SQL Server storage) in a dedicated Worker service |
| Database | Microsoft SQL Server |
| Data source | X API v2 (official) — with a clearly labelled Sample connector when credentials are not configured |
| Packaging | Docker / Docker Compose |
| Architecture | Clean Architecture, CQRS-lite, Ports & Adapters (connector model) |
