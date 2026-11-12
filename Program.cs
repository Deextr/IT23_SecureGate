using System.Security.Claims;
using Microsoft.AspNetCore.DataProtection;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Options;
using SecureGate1.Data;
using SecureGate1.Models;
using SecureGate1.Security;
using SecureGate1.Services;

var builder = WebApplication.CreateBuilder(args);

var policy = builder.Configuration.GetSection(SecurityPolicyOptions.SectionName).Get<SecurityPolicyOptions>() ?? new SecurityPolicyOptions();
var policyErrors = SecurityPolicyOptions.Validate(policy);
if (policyErrors.Count > 0)
    throw new InvalidOperationException("SecurityPolicy configuration is invalid. " + string.Join(" ", policyErrors));

byte[] recoveryKey;
try
{
    recoveryKey = Convert.FromBase64String(builder.Configuration["RecoveryCodes:HmacKey"] ?? "");
}
catch (FormatException)
{
    recoveryKey = [];
}

if (recoveryKey.Length < 32)
{
    throw new InvalidOperationException(
        "Set RecoveryCodes:HmacKey to a base64-encoded key of at least 32 bytes. Example: dotnet user-secrets set \"RecoveryCodes:HmacKey\" \"<value>\"");
}

var keyPath = builder.Configuration["DataProtection:KeyPath"];
if (string.IsNullOrWhiteSpace(keyPath))
{
    keyPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SecureGate", "keys");
}

Directory.CreateDirectory(keyPath);
var dataProtection = builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(keyPath))
    .SetApplicationName("SecureGate");
if (OperatingSystem.IsWindows())
    dataProtection.ProtectKeysWithDpapi();

builder.WebHost.ConfigureKestrel(kestrel => kestrel.Limits.MaxRequestBodySize = policy.MaxRequestBodyBytes);
builder.Services.Configure<FormOptions>(options =>
{
    options.ValueLengthLimit = 16 * 1024;
    options.MultipartBodyLengthLimit = policy.MaxRequestBodyBytes;
});

builder.Services.AddOptions<SecurityPolicyOptions>()
    .Bind(builder.Configuration.GetSection(SecurityPolicyOptions.SectionName))
    .Validate(value => SecurityPolicyOptions.Validate(value).Count == 0, "SecurityPolicy configuration is outside the approved ranges.")
    .ValidateOnStart();

builder.Services.AddSingleton(new RecoveryCodeKey(recoveryKey));
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddHttpContextAccessor();

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("ConnectionStrings:DefaultConnection is missing.");

builder.Services.AddDbContextFactory<ApplicationDbContext>(options =>
    options.UseSqlServer(connectionString));
builder.Services.AddScoped(sp => sp.GetRequiredService<IDbContextFactory<ApplicationDbContext>>().CreateDbContext());

builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
    {
        options.Password.RequiredLength = policy.PasswordMinLength;
        options.Password.RequiredUniqueChars = 1;
        options.Password.RequireDigit = false;
        options.Password.RequireLowercase = false;
        options.Password.RequireUppercase = false;
        options.Password.RequireNonAlphanumeric = false;
        options.Lockout.AllowedForNewUsers = true;
        options.Lockout.MaxFailedAccessAttempts = 100;
        options.User.RequireUniqueEmail = true;
        options.User.AllowedUserNameCharacters = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789._-@";
        options.SignIn.RequireConfirmedAccount = false;
        options.SignIn.RequireConfirmedEmail = false;
    })
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddDefaultTokenProviders();

builder.Services.Configure<SecurityStampValidatorOptions>(options =>
{
    options.ValidationInterval = TimeSpan.Zero;
    options.OnRefreshingPrincipal = context =>
    {
        if (context.CurrentPrincipal is null || context.NewPrincipal?.Identity is not ClaimsIdentity identity)
            return Task.CompletedTask;

        foreach (var type in new[] { AppClaims.Amr, AppClaims.AuthTime, AppClaims.MfaTime })
        {
            var claim = context.CurrentPrincipal.FindFirst(type);
            if (claim is not null && !identity.HasClaim(c => c.Type == type))
                identity.AddClaim(claim);
        }

        return Task.CompletedTask;
    };
});

var authenticated = new AuthorizationPolicyBuilder()
    .RequireAuthenticatedUser()
    .RequireClaim(AppClaims.Amr, AppClaims.Mfa)
    .Build();

builder.Services.AddAuthorization(options =>
{
    options.DefaultPolicy = authenticated;
    options.AddPolicy(Policies.AdministratorOnly, p => p.RequireRole(Roles.Superadmin, Roles.Administrator).RequireClaim(AppClaims.Amr, AppClaims.Mfa));
    options.AddPolicy(Policies.MonitoringAccess, p => p.RequireRole(Roles.Superadmin, Roles.Administrator, Roles.SecurityOfficer).RequireClaim(AppClaims.Amr, AppClaims.Mfa));
});

builder.Services.ConfigureApplicationCookie(options =>
{
    options.Cookie.Name = "SecureGate.Auth";
    options.Cookie.HttpOnly = true;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    options.Cookie.SameSite = SameSiteMode.Strict;
    options.ExpireTimeSpan = TimeSpan.FromMinutes(policy.SessionIdleMinutes);
    options.SlidingExpiration = true;
    options.LoginPath = "/Account/Login";
    options.AccessDeniedPath = "/Account/AccessDenied";
    options.Events.OnRedirectToAccessDenied = async context =>
    {
        var audit = context.HttpContext.RequestServices.GetRequiredService<AuditService>();
        var userId = context.HttpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);
        await audit.WriteAsync(new AuditWrite
        {
            EventType = EventTypes.AccessDenied,
            Outcome = Outcomes.Denied,
            Severity = Severities.High,
            ActorUserId = userId,
            TargetUserId = userId,
            Details = "Path=" + (context.Request.Path.Value ?? "/")
        });
        context.Response.Redirect(context.RedirectUri);
    };
    options.Events.OnValidatePrincipal = ValidateSessionAsync;
});

builder.Services.AddAuthentication()
    .AddCookie(AuthSchemes.PreAuth, options =>
    {
        options.Cookie.Name = "SecureGate.PreAuth";
        options.Cookie.HttpOnly = true;
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
        options.Cookie.SameSite = SameSiteMode.Strict;
        options.ExpireTimeSpan = TimeSpan.FromMinutes(policy.PreAuthMinutes);
        options.SlidingExpiration = false;
    });

builder.Services.AddAntiforgery(options =>
{
    options.Cookie.Name = "SecureGate.Antiforgery";
    options.Cookie.HttpOnly = true;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    options.Cookie.SameSite = SameSiteMode.Strict;
});

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.OnRejected = async (context, token) =>
    {
        var audit = context.HttpContext.RequestServices.GetRequiredService<AuditService>();
        await audit.WriteAsync(new AuditWrite
        {
            EventType = EventTypes.IpRateLimited,
            Outcome = Outcomes.Denied,
            Severity = Severities.Medium,
            Details = "Authentication endpoint rate limit reached."
        }, token);
        context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        context.HttpContext.Response.ContentType = "text/html; charset=utf-8";
        await context.HttpContext.Response.WriteAsync(
            """
            <!DOCTYPE html>
            <html lang="en"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width, initial-scale=1"><title>Too many attempts</title><link rel="stylesheet" href="/css/site.css"></head>
            <body class="auth-body"><main class="auth-card"><h1>Too many attempts</h1><p>SecureGate is pausing authentication requests from this network for a few minutes. Wait, then try again.</p><p><a href="/Account/Login">Return to sign in</a></p></main></body></html>
            """,
            token);
    };
    options.AddPolicy("auth-endpoints", httpContext =>
    {
        var current = httpContext.RequestServices.GetRequiredService<IOptions<SecurityPolicyOptions>>().Value;
        return RateLimitPartition.GetSlidingWindowLimiter(
            httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new SlidingWindowRateLimiterOptions
            {
                PermitLimit = current.IpPermitLimit,
                Window = TimeSpan.FromMinutes(current.IpWindowMinutes),
                SegmentsPerWindow = 5,
                QueueLimit = 0
            });
    });
});

builder.Services.AddScoped<AuditService>();
builder.Services.AddScoped<AlertService>();
builder.Services.AddScoped<SettingsService>();
builder.Services.AddScoped<FailureWindow>();
builder.Services.AddScoped<TotpService>();
builder.Services.AddScoped<RecoveryCodeService>();
builder.Services.AddScoped<MfaService>();
builder.Services.AddScoped<AuthenticationFlowService>();
builder.Services.AddScoped<SignInCompletionService>();
builder.Services.AddScoped<StepUpGuard>();
builder.Services.AddScoped<UserAdministrationService>();
builder.Services.AddScoped<MfaResetService>();
builder.Services.AddScoped<MonitoringService>();
builder.Services.AddScoped<ProfilePhotoService>();

builder.Services.AddRazorPages(options =>
{
    options.Conventions.AuthorizeFolder("/");
    options.Conventions.AuthorizeFolder("/Admin", Policies.AdministratorOnly);
    options.Conventions.AuthorizeFolder("/Monitoring", Policies.MonitoringAccess);
});

var app = builder.Build();

app.UseExceptionHandler("/Error");
if (!app.Environment.IsDevelopment())
    app.UseHsts();

app.Use(async (context, next) =>
{
    if (!context.Request.IsHttps)
    {
        var host = context.Request.Host.Host;
        if (string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(host, "127.0.0.1", StringComparison.OrdinalIgnoreCase))
        {
            var httpsUrl = "https://" + host + ":7101" + context.Request.PathBase + context.Request.Path + context.Request.QueryString;
            context.Response.Redirect(httpsUrl, permanent: false);
            return;
        }
    }

    await next();
});
app.UseHttpsRedirection();
app.UseMiddleware<SecurityHeadersMiddleware>();
app.UseStatusCodePagesWithReExecute("/StatusCode/{0}");
app.UseRouting();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

var profileUploadsRoot = Path.Combine(app.Environment.ContentRootPath, "AppData", "uploads");
Directory.CreateDirectory(profileUploadsRoot);
MigrateLegacyProfileUploads(app.Environment);
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(profileUploadsRoot),
    RequestPath = "/uploads",
    OnPrepareResponse = context =>
    {
        context.Context.Response.Headers.CacheControl = "private, max-age=300";
    }
});

app.MapStaticAssets();
app.MapRazorPages().WithStaticAssets();

try
{
    await using var scope = app.Services.CreateAsyncScope();
    await DatabaseSeeder.SeedAsync(scope.ServiceProvider);
}
catch (SqlException exception)
{
    throw new InvalidOperationException(
        "Could not prepare DB_SecureGate. Confirm SQL Server (SQLEXPRESS) is running and apply migrations with: dotnet ef database update",
        exception);
}

app.Run();

static void MigrateLegacyProfileUploads(IWebHostEnvironment environment)
{
    var legacyDir = Path.Combine(environment.WebRootPath, "uploads", "profiles");
    var targetDir = Path.Combine(environment.ContentRootPath, "AppData", "uploads", "profiles");
    if (!Directory.Exists(legacyDir))
        return;

    Directory.CreateDirectory(targetDir);
    foreach (var legacyFile in Directory.EnumerateFiles(legacyDir))
    {
        var dest = Path.Combine(targetDir, Path.GetFileName(legacyFile));
        if (File.Exists(dest))
            continue;
        File.Copy(legacyFile, dest);
    }
}

static async Task ValidateSessionAsync(CookieValidatePrincipalContext context)
{
    var principal = context.Principal;
    var userId = principal?.FindFirstValue(ClaimTypes.NameIdentifier);
    var authTimeValue = principal?.FindFirst(AppClaims.AuthTime)?.Value;
    var sessionPolicy = context.HttpContext.RequestServices.GetRequiredService<IOptions<SecurityPolicyOptions>>().Value;
    if (long.TryParse(authTimeValue, out var unix) &&
        SessionLifetime.IsAbsoluteExpired(DateTimeOffset.FromUnixTimeSeconds(unix), TimeProvider.System.GetUtcNow(), sessionPolicy.SessionAbsoluteHours))
    {
        context.RejectPrincipal();
        await context.HttpContext.SignOutAsync(IdentityConstants.ApplicationScheme);
        if (userId is not null)
        {
            var audit = context.HttpContext.RequestServices.GetRequiredService<AuditService>();
            await audit.WriteAsync(new AuditWrite
            {
                EventType = EventTypes.SessionAbsoluteExpired,
                Outcome = Outcomes.Denied,
                ActorUserId = userId,
                TargetUserId = userId,
                Details = "Absolute session lifetime elapsed."
            });
        }

        return;
    }

    await SecurityStampValidator.ValidatePrincipalAsync(context);
    if (context.Principal is null && userId is not null)
    {
        var audit = context.HttpContext.RequestServices.GetRequiredService<AuditService>();
        await audit.WriteAsync(new AuditWrite
        {
            EventType = EventTypes.SessionRejected,
            Outcome = Outcomes.Denied,
            Severity = Severities.High,
            ActorUserId = userId,
            TargetUserId = userId,
            Details = "Session was rejected after a security-stamp check."
        });
    }
}
