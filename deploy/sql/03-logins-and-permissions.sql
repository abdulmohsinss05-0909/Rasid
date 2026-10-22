/*
  Rasid — step 3 of 3: least-privilege logins (docs/08-security.md §3).
  Run as a sysadmin after 02-schema.sql. Safe to run more than once (existing passwords are reset).
  Passwords are passed on the command line, never stored in this file:

    sqlcmd -S <server> -E -C -i 03-logins-and-permissions.sql ^
      -v DatabaseName="Rasid" ApiPassword="<strong password>" WorkerPassword="<strong password>"

  Requires SQL Server authentication (mixed mode) on the instance. Passwords must meet the Windows password policy
  and must not contain a single quote (').

    rasid_api     the API: read/write on the application (rasid) and identity (auth) schemas; enqueues jobs (hangfire)
    rasid_worker  the Worker: read/write on rasid; owns the Hangfire tables (creates and upgrades them on start-up)

  Neither login can change the rasid or auth schema. Schema changes are applied with 02-schema.sql by a DBA or the
  deployment pipeline.
*/
:on error exit

USE [master];
GO

IF SUSER_ID(N'rasid_api') IS NULL
    EXEC (N'CREATE LOGIN [rasid_api] WITH PASSWORD = N''$(ApiPassword)'', CHECK_POLICY = ON, DEFAULT_DATABASE = [$(DatabaseName)]');
ELSE
    EXEC (N'ALTER LOGIN [rasid_api] WITH PASSWORD = N''$(ApiPassword)''');

IF SUSER_ID(N'rasid_worker') IS NULL
    EXEC (N'CREATE LOGIN [rasid_worker] WITH PASSWORD = N''$(WorkerPassword)'', CHECK_POLICY = ON, DEFAULT_DATABASE = [$(DatabaseName)]');
ELSE
    EXEC (N'ALTER LOGIN [rasid_worker] WITH PASSWORD = N''$(WorkerPassword)''');
GO

USE [$(DatabaseName)];
GO

IF SCHEMA_ID(N'hangfire') IS NULL
    EXEC (N'CREATE SCHEMA [hangfire] AUTHORIZATION [dbo]');
GO

IF DATABASE_PRINCIPAL_ID(N'rasid_api') IS NULL
    CREATE USER [rasid_api] FOR LOGIN [rasid_api] WITH DEFAULT_SCHEMA = [rasid];
IF DATABASE_PRINCIPAL_ID(N'rasid_worker') IS NULL
    CREATE USER [rasid_worker] FOR LOGIN [rasid_worker] WITH DEFAULT_SCHEMA = [rasid];
GO

-- API
GRANT SELECT, INSERT, UPDATE, DELETE ON SCHEMA::[rasid]    TO [rasid_api];
GRANT SELECT, INSERT, UPDATE, DELETE ON SCHEMA::[auth]     TO [rasid_api];
GRANT SELECT, INSERT, UPDATE, DELETE ON SCHEMA::[hangfire] TO [rasid_api];
GO

-- Worker
GRANT SELECT, INSERT, UPDATE, DELETE ON SCHEMA::[rasid]    TO [rasid_worker];
GRANT SELECT, INSERT, UPDATE, DELETE ON SCHEMA::[hangfire] TO [rasid_worker];
GRANT ALTER, REFERENCES                ON SCHEMA::[hangfire] TO [rasid_worker];
GRANT CREATE TABLE TO [rasid_worker];
GO

PRINT N'Logins rasid_api and rasid_worker are ready on [$(DatabaseName)]';
GO
