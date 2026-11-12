/*
    Backup and restore for DB_SecureGate.
    Change the folder if C:\SqlBackups does not exist, and run the backup in SSMS
    or sqlcmd while SQL Server (SQLEXPRESS) is running.
    Restore replaces the database, so warn anyone using it first.
*/
BACKUP DATABASE DB_SecureGate
TO DISK = N'C:\SqlBackups\DB_SecureGate.bak'
WITH INIT, STATS = 10;
GO

-- Restore example. Do not run this until you intend to replace the current database.
-- ALTER DATABASE DB_SecureGate SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
-- RESTORE DATABASE DB_SecureGate
-- FROM DISK = N'C:\SqlBackups\DB_SecureGate.bak'
-- WITH REPLACE, STATS = 10;
-- ALTER DATABASE DB_SecureGate SET MULTI_USER;
