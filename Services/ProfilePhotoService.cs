namespace SecureGate1.Services;

public class ProfilePhotoService
{
    public const long MaxBytes = 5 * 1024 * 1024;

    private static readonly HashSet<string> AllowedContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg",
        "image/png",
        "image/webp",
        "image/gif",
    };

    private readonly IWebHostEnvironment _environment;

    public ProfilePhotoService(IWebHostEnvironment environment)
    {
        _environment = environment;
    }

    public string? PublicUrl(string? relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
            return null;

        var normalized = "/" + relativePath.Replace('\\', '/').TrimStart('/');
        var absolute = Path.Combine(
            _environment.ContentRootPath,
            relativePath.Replace('/', Path.DirectorySeparatorChar).TrimStart(Path.DirectorySeparatorChar));
        if (!File.Exists(absolute))
            return normalized;

        return normalized + "?v=" + new FileInfo(absolute).LastWriteTimeUtc.Ticks.ToString(System.Globalization.CultureInfo.InvariantCulture);
    }

    public async Task<(string? Path, string? Error)> SaveAsync(string userId, IFormFile file, CancellationToken cancellationToken)
    {
        if (file.Length == 0)
            return (null, null);

        if (file.Length > MaxBytes)
            return (null, "Profile photo must be 5 MB or smaller.");

        if (!AllowedContentTypes.Contains(file.ContentType))
            return (null, "Use a JPEG, PNG, WebP, or GIF image.");

        var extension = file.ContentType.ToLowerInvariant() switch
        {
            "image/jpeg" => ".jpg",
            "image/png" => ".png",
            "image/webp" => ".webp",
            "image/gif" => ".gif",
            _ => null
        };
        if (extension is null)
            return (null, "Use a JPEG, PNG, WebP, or GIF image.");

        var relativeDir = Path.Combine("uploads", "profiles");
        var absoluteDir = ProfileUploadDirectory;
        Directory.CreateDirectory(absoluteDir);

        foreach (var existing in Directory.EnumerateFiles(absoluteDir, $"{userId}.*"))
            File.Delete(existing);

        var fileName = $"{userId}{extension}";
        var absolutePath = Path.Combine(absoluteDir, fileName);
        await using (var stream = File.Create(absolutePath))
            await file.CopyToAsync(stream, cancellationToken);

        return (Path.Combine(relativeDir, fileName).Replace('\\', '/'), null);
    }

    private string ProfileUploadDirectory =>
        Path.Combine(_environment.ContentRootPath, "AppData", "uploads", "profiles");

    public void Delete(string userId)
    {
        var absoluteDir = ProfileUploadDirectory;
        if (!Directory.Exists(absoluteDir))
            return;
        foreach (var existing in Directory.EnumerateFiles(absoluteDir, $"{userId}.*"))
            File.Delete(existing);
    }
}
