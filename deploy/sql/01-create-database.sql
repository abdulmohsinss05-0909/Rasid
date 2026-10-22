/*
  Rasid — step 1 of 3: create the database.
  Run as a sysadmin (or a login with CREATE DATABASE). Safe to run more than once.

    sqlcmd -S <server> -E -C -i 01-create-database.sql -v DatabaseName="Rasid"

  -E uses Windows authentication; use -U <login> -P <password> for SQL authentication.
*/
:on error exit

IF DB_ID(N'$(DatabaseName)') IS NULL
BEGIN
    PRINT N'Creating database [$(DatabaseName)]';
    EXEC (N'CREATE DATABASE [$(DatabaseName)]');
END
ELSE
    PRINT N'Database [$(DatabaseName)] already exists';
GO

-- Read-committed snapshot: dashboard reads do not block the worker's writes (and vice versa).
IF EXISTS (SELECT 1 FROM sys.databases WHERE name = N'$(DatabaseName)' AND is_read_committed_snapshot_on = 0)
    EXEC (N'ALTER DATABASE [$(DatabaseName)] SET READ_COMMITTED_SNAPSHOT ON WITH ROLLBACK IMMEDIATE');
GO
