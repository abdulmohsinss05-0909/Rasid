# Rasid (رصد) — X Topic Monitoring

Rasid monitors public posts on X for defined topics and shows engagement indicators (posts, likes, reposts,
replies, quotes, views, engagement rate and level). It uses **only the official X API v2**. Without X
credentials it runs on a clearly labelled **Sample** data source.

Architecture, decisions, flowcharts, engagement method and the compliance statement are in **[docs/](docs/README.md)**.
New machine: start with **[docs/11 — Setup Guide](docs/11-setup-guide.md)**.

Project owner: **Abdul Mohsin** · Prepared for **The Global Advisors**.

## Repository layout

```
src/backend/
  Rasid.Domain           entities and business rules (no dependencies)
  Rasid.Application      use cases, ports, validation
  Rasid.Infrastructure   SQL Server (EF Core), X connector, Sample connector, Hangfire, identity
  Rasid.Api              REST API (/api/v1)
  Rasid.Worker           background jobs (Hangfire server)
  config/                appsettings*.json and endpoints.{Environment}.json (all base URLs)
src/frontend/rasid-web   Angular application
tests/                   unit and integration tests
deploy/docker/           Dockerfiles, nginx, docker-compose.yml, .env.example
deploy/sql/              SQL scripts: database, schema, least-privilege logins
docs/                    solution documentation
```

## Prerequisites

| Tool | Version |
|------|---------|
| .NET SDK | 10.0 (pinned in `global.json`) |
| SQL Server | 2019+ (local instance, or the Docker container) |
| Node.js | 22 LTS (frontend) |
| IDE | Visual Studio 2026, or VS Code with C# Dev Kit. Visual Studio 2022 cannot load .NET 10 projects |

## Quick start with Docker (whole stack)

```powershell
cd deploy/docker
copy .env.example .env      # then fill in the values (see comments in the file)
docker compose up --build
```

Open **http://localhost:8080** and sign in with `ADMIN_EMAIL` / `ADMIN_PASSWORD` from `.env`.
The sample topic *UAE Humanitarian Aid* starts collecting within about a minute.

## Switching to live X data (no code change)

Set `X_BEARER_TOKEN` in `deploy/docker/.env` (or `XApi:BearerToken` in user-secrets), and optionally the
monthly budget `Monitoring__Quota__MonthlyPostReadBudget`, then restart. Within a minute every screen shows
**Live · X API v2** and the sample data is removed automatically. Details:
[docs/07 — Switching to live X data](docs/07-x-api-and-compliance.md#switching-to-live-x-data-configuration-only).

## Run locally (backend)

1. **Restore tools:** `dotnet tool restore`
2. **Set the local secrets.** They are stored in your user profile, outside the repository:
   ```powershell
   dotnet user-secrets set "Jwt:SigningKey" "<random string, 32+ characters>" --project src/backend/Rasid.Api
   dotnet user-secrets set "Seed:AdminEmail" "admin@rasid.local" --project src/backend/Rasid.Api
   dotnet user-secrets set "Seed:AdminPassword" "<12+ chars, upper, lower, digit, symbol>" --project src/backend/Rasid.Api
   # Optional: live X data instead of Sample data
   dotnet user-secrets set "XApi:BearerToken" "<X API bearer token>" --project src/backend/Rasid.Api
   ```
   The API and the Worker share these secrets.
3. **Database:** Development uses `Server=localhost` with Windows authentication (see `src/backend/config/appsettings.Development.json`).
   On start-up the API applies migrations and seeds roles, the admin user and the sample topic *UAE Humanitarian Aid*.
4. **Start both processes** in two terminals:
   ```powershell
   dotnet run --project src/backend/Rasid.Api      # http://localhost:5080  (Swagger: /swagger, Scalar: /scalar)
   dotnet run --project src/backend/Rasid.Worker   # http://localhost:5090  (jobs dashboard: /jobs, local only)
   ```
   The Worker collects the first data within about a minute.

## Run locally (frontend)

```powershell
cd src/frontend/rasid-web
npm ci
npm start                    # http://localhost:4200, proxies /api to http://localhost:5080
```

Builds: `npm run build:prod`, `npm run build:uat`. Each environment's API base URL is in `src/environments/`.

## API documentation

Development and UAT expose **Swagger UI** at `/swagger` and **Scalar** at `/scalar`. Production does not expose them.
1. Call `POST /api/v1/auth/login` and copy the `accessToken` from the response.
2. Click **Authorize** and paste the token.

## Tests

```powershell
dotnet test                                   # domain unit tests + integration tests (Docker required: SQL Server container)
cd src/frontend/rasid-web; npm test           # Angular unit tests (Vitest)
```

## Environments

| Environment | `ASPNETCORE_ENVIRONMENT` | Data source | Base URLs |
|-------------|--------------------------|-------------|-----------|
| Development | `Development` | Sample, or X if a token is set | `config/endpoints.Development.json` |
| UAT | `UAT` | X or Sample (set explicitly) | `config/endpoints.UAT.json` |
| Production | `Production` | X only. Sample data is disabled | `config/endpoints.Production.json` |

Secrets never go in configuration files. Use user-secrets locally, and environment variables or a vault elsewhere.

## Health

- `GET /health/live`: the process is running
- `GET /health/ready`: database reachable and data source configured. It reports *Degraded* in Sample mode.
