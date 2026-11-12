/*
    Least-privilege SQL login for SecureGate.

    DB_SecureGate on this machine uses Windows Authentication only, and the local
    development app connects as the Windows account that starts it. Do not switch
    the instance to Mixed Mode just to run locally.

    Use this script on a host that allows SQL logins, such as the MonsterASP database,
    after you replace the password. The login is not a SQL Server administrator.
    It can read and write application tables. It cannot change the schema.
    The audit trigger still blocks UPDATE and DELETE on AuthenticationAuditLogs.
*/
USE master;
GO
IF DB_ID(N'DB_SecureGate') IS NULL
BEGIN
    RAISERROR('Create or restore DB_SecureGate before creating the application user.', 16, 1);
    RETURN;
END
GO
IF SUSER_ID(N'securegate_app') IS NULL
BEGIN
    CREATE LOGIN securegate_app WITH PASSWORD = N'Replace-this-password-before-running', CHECK_POLICY = ON;
END
GO
USE DB_SecureGate;
GO
IF USER_ID(N'securegate_app') IS NULL
BEGIN
    CREATE USER securegate_app FOR LOGIN securegate_app;
END
GO
GRANT SELECT, INSERT, UPDATE, DELETE ON SCHEMA::dbo TO securegate_app;
GO
