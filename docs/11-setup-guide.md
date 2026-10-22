# 11 — Setup Guide

How to set Rasid up on a new machine, put it under source control, and prepare a database by hand.
Project owner: **Abdul Mohsin**.
Pick **one** of the two set-up options below; Option A is the quickest.

| | Option A: Docker | Option B: local SQL Server |
|---|---|---|
| Installs needed | Docker Desktop | .NET 10 SDK, Node.js 22, SQL Server 2019+ |
| Database | SQL Server container, created automatically | Your instance; created by the app or by the SQL scripts |
| SQL scripts | Not needed | Optional (needed for Production-style set-ups) |
| App URL | http://localhost:8080 | http://localhost:4200 |
| Use it for | Demos, UAT-style testing | Development and debugging |

## 1. What is (and is not) in the package

Included: source code, tests, documentation, Docker files, SQL scripts, `.env.example`.

Deliberately **not** included, and never to be committed:

| Item | Why | How to get it |
|------|-----|---------------|
| `deploy/docker/.env` | Holds real passwords and keys | Copy `.env.example` and fill it in (§2) |
| `dotnet user-secrets` | Stored in the user profile, outside the repository | Set them yourself (§3) |
| `bin/`, `obj/`, `node_modules/`, `dist/`, `.angular/` | Build output, recreated by the build | `dotnet build`, `npm ci` |

Secrets are never shared through the repository or chat. Each person generates their own.

## 2. Option A: Docker (whole stack, recommended)

Prerequisite: Docker Desktop running.

```powershell
cd deploy/docker
copy .env.example .env
```

Fill in `.env`:

| Key | Value |
|-----|-------|
| `RASID_ENVIRONMENT` | `Development` (or `UAT` to test the UAT configuration) |
| `RASID_WEB_CONFIGURATION` | `production` (use `uat` together with `RASID_ENVIRONMENT=UAT`) |
| `SQL_SA_PASSWORD` | 8+ characters with upper, lower, digit and symbol |
| `JWT_SIGNING_KEY` | 32+ random characters |
| `ADMIN_EMAIL` | Sign-in email of the first administrator |
| `ADMIN_PASSWORD` | 12+ characters with upper, lower, digit and symbol |
| `X_BEARER_TOKEN` | Leave empty for Sample data; set it for live X data |

To generate random values in PowerShell:

```powershell
$b = New-Object byte[] 48; [Security.Cryptography.RandomNumberGenerator]::Create().GetBytes($b); [Convert]::ToBase64String($b)
```

Start:

```powershell
docker compose up -d --build
```

The first build takes a few minutes. Then:

- App: http://localhost:8080 (sign in with `ADMIN_EMAIL` / `ADMIN_PASSWORD`)
- Swagger: http://localhost:8080/swagger, Scalar: http://localhost:8080/scalar

**Nothing else is needed for the database.** On start-up the API creates the database and tables, seeds the
roles, the administrator and the sample topic, and the Worker creates the Hangfire tables. Sample data appears within
about a minute.

| Task | Command |
|------|---------|
| Status | `docker compose ps` |
| Logs | `docker compose logs -f api` (or `worker`, `web`, `sql`) |
| Stop, keep data | `docker compose stop` |
| Start again | `docker compose up -d` |
| After code changes | `docker compose up -d --build` |
| Reset all data | `docker compose down -v` |

## 3. Option B: local SQL Server

Prerequisites: .NET 10 SDK (`winget install Microsoft.DotNet.SDK.10`), Node.js 22 LTS, SQL Server 2019+.
Visual Studio 2022 cannot open .NET 10 projects; use Visual Studio 2026 or VS Code with C# Dev Kit.

1. **Connection string.** `src/backend/config/appsettings.Development.json` uses `Server=localhost` with Windows
   authentication. For a named instance or SQL authentication, override it in user-secrets instead of editing the
   file:
   ```powershell
   dotnet user-secrets set "ConnectionStrings:Rasid" "Server=.\SQLEXPRESS;Database=Rasid;Trusted_Connection=True;Encrypt=True;TrustServerCertificate=True" --project src/backend/Rasid.Api
   ```
2. **Secrets** (shared by the API and the Worker):
   ```powershell
   dotnet tool restore
   dotnet user-secrets set "Jwt:SigningKey" "<32+ random characters>" --project src/backend/Rasid.Api
   dotnet user-secrets set "Seed:AdminEmail" "admin@rasid.local" --project src/backend/Rasid.Api
   dotnet user-secrets set "Seed:AdminPassword" "<12+ chars, upper, lower, digit, symbol>" --project src/backend/Rasid.Api
   ```
3. **Database.** In Development the API applies the migrations on start-up, so the database is created
   automatically. To prepare it by hand instead, use the scripts in §4.
4. **Run** in three terminals:
   ```powershell
   dotnet run --project src/backend/Rasid.Api       # http://localhost:5080/swagger
   dotnet run --project src/backend/Rasid.Worker    # http://localhost:5090/jobs
   cd src/frontend/rasid-web; npm ci; npm start     # http://localhost:4200
   ```

## 4. SQL scripts (manual or Production-style database)

`deploy/sql/` holds three scripts, run in order with `sqlcmd` (or SSMS in SQLCMD mode). All three are idempotent,
so running them again is safe.

| Script | What it does | Run as |
|--------|--------------|--------|
| `01-create-database.sql` | Creates the database and turns on read-committed snapshot | sysadmin |
| `02-schema.sql` | All tables, keys and indexes (schemas `rasid`, `auth`), from the EF Core migrations | sysadmin or `db_owner` |
| `03-logins-and-permissions.sql` | Least-privilege logins `rasid_api` and `rasid_worker` (see [08 §3](08-security.md#3-database-security-sql-server)) | sysadmin |

```powershell
cd deploy/sql
sqlcmd -S localhost -E -C -b -i 01-create-database.sql -v DatabaseName="Rasid"
sqlcmd -S localhost -E -C -b -d Rasid -i 02-schema.sql
sqlcmd -S localhost -E -C -b -i 03-logins-and-permissions.sql -v DatabaseName="Rasid" ApiPassword="<strong>" WorkerPassword="<strong>"
```

`-E` is Windows authentication; use `-U <login> -P <password>` for SQL authentication. Script 3 needs SQL
authentication (mixed mode) enabled on the instance, and the passwords must not contain a single quote.

Then point each process at its own login, and turn off start-up migrations (the schema is now managed by script 2):

```
API:     ConnectionStrings__Rasid = Server=<server>;Database=Rasid;User Id=rasid_api;Password=<...>;Encrypt=True
Worker:  ConnectionStrings__Rasid = Server=<server>;Database=Rasid;User Id=rasid_worker;Password=<...>;Encrypt=True
Both:    Seed__ApplyMigrations = false
```

The roles, the administrator (`Seed__AdminEmail`, `Seed__AdminPassword`) and the Hangfire tables are still created
by the API and the Worker on their first start. Production already has `Seed:ApplyMigrations` set to `false`.

**After adding a migration**, regenerate script 2 with `powershell -File deploy/sql/generate-schema.ps1`.

`02-schema.sql` prints three warnings about 900-byte index keys on the Identity tables (`UserLogins`, `UserRoles`,
`UserTokens`). They are standard for ASP.NET Core Identity on SQL Server and harmless: the real key values are short.

Verified on 21 Oct 2026 against SQL Server 2022: the scripts ran twice on an empty database without errors, and the
API and the Worker ran with the `rasid_api` and `rasid_worker` logins (administrator seeded, sample data collected).

## 5. Putting the project under Git

The repository contains a `.gitignore` that already excludes secrets and build output.

```powershell
git init
git add .
git status                      # check: no .env, no bin/obj, no node_modules
git check-ignore -v deploy/docker/.env    # must print the .gitignore rule
git commit -m "Initial import of Rasid"
git remote add origin <repository URL>
git push -u origin main
```

Before every push, make sure `git status` shows no `.env` file and no file containing a password or token.
`.env.example` **is** committed: it lists the keys without values.

## 6. Checks after set-up

| Check | Expected |
|-------|----------|
| `GET /health/live` | `Healthy` |
| `GET /health/ready` | `Degraded` with Sample data (no X token), `Healthy` with live X data |
| Sign-in | Lands on the Overview page |
| Topic dashboard | KPIs, charts and posts within about a minute; banner shows *SAMPLE DATA* |
| `dotnet test` | 107 tests pass (Docker must be running: the integration tests start a SQL Server container) |
| `npm test` in `src/frontend/rasid-web` | 20 tests pass |

## 7. Troubleshooting

| Symptom | Cause and fix |
|---------|---------------|
| API exits with *must use HTTPS in the 'UAT' environment* | UAT and Production require HTTPS URLs in `endpoints.{Env}.json`. Only `localhost` HTTP is accepted in UAT |
| Build fails: file is locked by `Rasid.Api` / `Rasid.Worker` | Stop the running API and Worker before building |
| `CREATE INDEX failed ... QUOTED_IDENTIFIER` | Use `02-schema.sql` as generated (it sets the option), or pass `-I` to `sqlcmd` |
| Login fails after `docker compose down -v` | The administrator is recreated from `.env`; check `ADMIN_EMAIL` / `ADMIN_PASSWORD` |
| Port 8080 in use | Stop the other program, or change the `ports` entry of `web` in `docker-compose.yml` |
| Visual Studio cannot load the projects | Visual Studio 2022 does not support .NET 10; use Visual Studio 2026 or VS Code |
