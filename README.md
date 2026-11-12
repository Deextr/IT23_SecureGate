# SecureGate

SecureGate is the DavaoTech employee sign-in portal. An administrator creates each account. The employee sets a password, enrolls an authenticator, and only then receives a session. There is no public registration page.

## What you need

- Windows with the .NET 10 SDK (`dotnet --version` should report 10.x)
- SQL Server 2022 Express instance `.\SQLEXPRESS`
- Database `DB_SecureGate` (already created on this machine; it may be empty before the first migration)
- Windows Authentication. This instance does not use SQL logins. The app connects as the Windows account that starts it.

The development connection string is in `appsettings.Development.json`:

`Server=.\SQLEXPRESS;Database=DB_SecureGate;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=True`

## Secrets

Do not put the recovery-code key or the bootstrap password in source control. They belong in user secrets for this project (`securegate-dev-7f3a9c2e4b18`).

From this folder:

```powershell
dotnet user-secrets set "RecoveryCodes:HmacKey" "<base64 of at least 32 random bytes>"
dotnet user-secrets set "Bootstrap:AdminEmail" "admin@davaotech.local"
dotnet user-secrets set "Bootstrap:AdminTemporaryPassword" "<12 to 128 characters>"
```

Generate the recovery key in PowerShell:

```powershell
$bytes = New-Object byte[] 32
[System.Security.Cryptography.RandomNumberGenerator]::Create().GetBytes($bytes)
[Convert]::ToBase64String($bytes)
```

The app refuses to start if `RecoveryCodes:HmacKey` is missing or shorter than 32 bytes. The bootstrap values are used only when the database has no Administrator. They do not reset an administrator that already exists.

## Database

Apply the EF Core migration before the first run, and again after a new migration is added:

```powershell
dotnet tool install --global dotnet-ef --version 10.0.4
dotnet ef database update --project SecureGate1.csproj
```

`dotnet ef` 10.0.2 also works; it may warn that the design package is 10.0.4. The migration is not applied automatically at startup.

## Run

Cookies are marked Secure. `dotnet run` and `dotnet watch` use the HTTPS profile by default:

```powershell
dotnet run
dotnet watch
```

Profile photos are stored under `AppData/uploads` (not `wwwroot`) so uploads do not restart `dotnet watch` or break Hot Reload.

- Site: `https://localhost:7101`
- `http://localhost:5238` redirects to that HTTPS address. Sign-in cookies are not stored on plain HTTP.
- If the browser warns about the certificate, trust the local development certificate once: `dotnet dev-certs https --trust`

## Tests

```powershell
dotnet test tests/SecureGate.Tests/SecureGate.Tests.csproj
```

Tests create and delete their own databases named `DB_SecureGate_Tests_{guid}`. They do not write to `DB_SecureGate`.

## Deployment notes

`Database/CreateLeastPrivilegeAppUser.sql` creates a SQL login `securegate_app` that can read and write tables but cannot change the schema. Do not run it against this Express instance until Mixed Mode is enabled. Local development stays on Windows Authentication, and that Windows login is a sysadmin on this machine.

`Database/BackupAndRestore.sql` backs up to `C:\SqlBackups\DB_SecureGate.bak`. Create that folder first. Restore is commented out. Hosting on MonsterASP is not configured: there is no hosting account in this project.
