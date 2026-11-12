using System.Globalization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Vernuko.LicenseServer;

public sealed record ActivateRequest(string? LicenseKey, string? DeviceHash, string? MachineLabel);

public sealed record ActivationRequest(string? ActivationId, string? DeviceHash);

public sealed record TrialRequest(string? DeviceHash);

public sealed record LicenseResponse(string License, string ActivationId);

public sealed record ErrorResponse(string Code, string Message);

public sealed record TrialResponse(DateTime StartedAtUtc, int TrialDays);

public sealed record CreateLicenseRequest(string CustomerName, string? CustomerEmail, string? Edition, int? MaxDevices, DateTime PaidUntilUtc, IReadOnlyList<string>? Features, string? Note);

public sealed record ExtendRequest(DateTime PaidUntilUtc);

public sealed record CreatedLicense(string Id, string Key);

public sealed record LicenseSummary(string Id, string CustomerName, string? CustomerEmail, string Edition, int MaxDevices, DateTime PaidUntilUtc, string Status, string? Provider, int ActiveDevices);

public sealed record ActivationSummary(string Id, string? MachineLabel, DateTime CreatedAtUtc, DateTime LastSeenUtc, DateTime? DeactivatedAtUtc);

public sealed record LicenseDetails(LicenseSummary License, IReadOnlyList<ActivationSummary> Activations);

/// <summary>
/// The license server: one small ASP.NET Core process with SQLite, meant to run behind a TLS reverse proxy.
/// Public endpoints are rate limited per IP; admin endpoints need the admin key (only its hash is configured).
/// </summary>
public static class LicenseServerApp
{
    public const string AdminKeyHeader = "X-Admin-Key";
    public const string ActivationHeader = "X-Vernuko-Activation";
    public const string DeviceHeader = "X-Vernuko-Device";

    public static WebApplication Build(string[] args, Action<WebApplicationBuilder>? configure = null)
    {
        var builder = WebApplication.CreateBuilder(args);
        configure?.Invoke(builder);
        builder.Services.TryAddSingleton(builder.Configuration.GetSection("LicenseServer").Get<LicenseServerOptions>() ?? new LicenseServerOptions());
        builder.Services.TryAddSingleton(sp => ServerSecrets.Load(sp.GetRequiredService<LicenseServerOptions>()));
        builder.Services.TryAddSingleton(TimeProvider.System);
        builder.Services.AddDbContext<LicenseServerDb>((sp, o) => o.UseSqlite(sp.GetRequiredService<LicenseServerOptions>().ConnectionString));
        builder.Services.AddScoped<LicenseIssuer>();
        builder.Services.AddScoped<PaddleWebhooks>();
        builder.Services.Configure<ForwardedHeadersOptions>(o => o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto);
        builder.Services.AddRateLimiter(o =>
        {
            o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            o.AddPolicy("public", ctx => RateLimitPartition.GetFixedWindowLimiter(
                ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => new FixedWindowRateLimiterOptions { PermitLimit = 30, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
        });

        var app = builder.Build();
        app.UseForwardedHeaders();
        app.UseRateLimiter();

        app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

        var v1 = app.MapGroup("/v1").RequireRateLimiting("public");
        v1.MapPost("/activate", async (ActivateRequest r, LicenseIssuer issuer, CancellationToken ct) =>
            ToHttp(await issuer.ActivateAsync(r.LicenseKey, r.DeviceHash, r.MachineLabel, ct)));
        v1.MapPost("/refresh", async (ActivationRequest r, LicenseIssuer issuer, CancellationToken ct) =>
            ToHttp(await issuer.RefreshAsync(r.ActivationId, r.DeviceHash, ct)));
        v1.MapPost("/deactivate", async (ActivationRequest r, LicenseIssuer issuer, CancellationToken ct) =>
        {
            var result = await issuer.DeactivateAsync(r.ActivationId, r.DeviceHash, ct);
            return result.ErrorCode is null ? Results.Ok(new { message = result.Message }) : ToHttp(result);
        });
        v1.MapPost("/trial", async (TrialRequest r, LicenseIssuer issuer, LicenseServerOptions options, CancellationToken ct) =>
        {
            var (started, error) = await issuer.TrialAsync(r.DeviceHash, ct);
            return error is not null ? ToHttp(error) : Results.Ok(new TrialResponse(started!.Value, options.TrialDays));
        });
        v1.MapPost("/updates/permission", async (ActivationRequest r, LicenseIssuer issuer, CancellationToken ct) =>
            Results.Ok(await issuer.UpdatePermissionAsync(r.ActivationId, r.DeviceHash, ct)));

        // Velopack release feed and packages, only for activated installations with a running subscription.
        app.MapGet("/v1/updates/feed/{file}", async (string file, HttpContext http, LicenseIssuer issuer, LicenseServerOptions options, CancellationToken ct) =>
        {
            var permission = await issuer.UpdatePermissionAsync(http.Request.Headers[ActivationHeader], http.Request.Headers[DeviceHeader], ct);
            if (!permission.Allowed)
            {
                return Results.Json(new ErrorResponse("updates.not_allowed", permission.Message), statusCode: StatusCodes.Status403Forbidden);
            }

            var path = SafeReleaseFile(options.ReleasesPath, file);
            return path is null ? Results.NotFound() : Results.File(path, "application/octet-stream", enableRangeProcessing: true);
        });

        // Paddle needs the raw body for the signature; answer fast with 200 (also for duplicates).
        app.MapPost("/v1/webhooks/paddle", async (HttpContext http, PaddleWebhooks paddle, CancellationToken ct) =>
        {
            using var ms = new MemoryStream();
            await http.Request.Body.CopyToAsync(ms, ct);
            var (status, outcome) = await paddle.HandleAsync(http.Request.Headers["Paddle-Signature"], ms.ToArray(), ct);
            return Results.Json(new { outcome }, statusCode: status);
        });

        MapAdmin(app.MapGroup("/v1/admin").AddEndpointFilter(async (ctx, next) =>
            ctx.HttpContext.RequestServices.GetRequiredService<ServerSecrets>().IsAdmin(ctx.HttpContext.Request.Headers[AdminKeyHeader])
                ? await next(ctx)
                : Results.Json(new ErrorResponse("admin.unauthorized", "Yönetici anahtarı gerekli."), statusCode: StatusCodes.Status401Unauthorized)));
        return app;
    }

    private static void MapAdmin(RouteGroupBuilder admin)
    {
        admin.MapPost("/licenses", async (CreateLicenseRequest r, LicenseServerDb db, ServerSecrets secrets, LicenseIssuer issuer, TimeProvider time, CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(r.CustomerName) || r.MaxDevices is < 1 or > 50)
            {
                return Results.BadRequest(new ErrorResponse("request.invalid", "Müşteri adı ve 1–50 cihaz gerekli."));
            }

            var license = new ServerLicense
            {
                Id = LicenseKeys.NewLicenseId(),
                CustomerName = r.CustomerName.Trim(),
                CustomerEmail = r.CustomerEmail?.Trim(),
                Edition = string.IsNullOrWhiteSpace(r.Edition) ? "Standart" : r.Edition.Trim(),
                MaxDevices = r.MaxDevices ?? 1,
                Features = string.Join(',', r.Features ?? []),
                PaidUntilUtc = DateTime.SpecifyKind(r.PaidUntilUtc, DateTimeKind.Utc),
                Status = ServerLicenseStatus.Active,
                Provider = "manual",
                Note = r.Note,
                CreatedAtUtc = time.GetUtcNow().UtcDateTime,
            };
            var key = LicenseKeys.Derive(secrets.KeySecret, license.Id);
            license.KeyHash = LicenseKeys.Hash(LicenseKeys.Normalize(key)!);
            db.Licenses.Add(license);
            issuer.Audit("license.created", license.Id, $"Elle satış: {license.CustomerName}, {license.Edition}, {license.MaxDevices} cihaz, {license.PaidUntilUtc:yyyy-MM-dd}");
            await db.SaveChangesAsync(ct);
            return Results.Ok(new CreatedLicense(license.Id, key));
        });

        admin.MapGet("/licenses", async (string? q, LicenseServerDb db, CancellationToken ct) =>
        {
            var query = db.Licenses.AsNoTracking().Include(l => l.Activations).AsQueryable();
            if (!string.IsNullOrWhiteSpace(q))
            {
                query = query.Where(l => l.Id == q || l.CustomerName.Contains(q) || (l.CustomerEmail != null && l.CustomerEmail.Contains(q)) || l.ProviderSubscriptionId == q);
            }

            var list = await query.OrderByDescending(l => l.CreatedAtUtc).Take(200).ToListAsync(ct);
            return Results.Ok(list.Select(Summary));
        });

        admin.MapGet("/licenses/{id}", async (string id, LicenseServerDb db, CancellationToken ct) =>
            await db.Licenses.AsNoTracking().Include(l => l.Activations).FirstOrDefaultAsync(l => l.Id == id, ct) is { } l
                ? Results.Ok(new LicenseDetails(Summary(l), l.Activations.OrderBy(a => a.CreatedAtUtc).Select(a => new ActivationSummary(a.Id, a.MachineLabel, a.CreatedAtUtc, a.LastSeenUtc, a.DeactivatedAtUtc)).ToList()))
                : Results.NotFound());

        // Re-send a lost key: derived from the id, never stored.
        admin.MapGet("/licenses/{id}/key", async (string id, LicenseServerDb db, ServerSecrets secrets, LicenseIssuer issuer, CancellationToken ct) =>
        {
            if (!await db.Licenses.AnyAsync(l => l.Id == id, ct))
            {
                return Results.NotFound();
            }

            issuer.Audit("license.key_viewed", id, "Anahtar yeniden gösterildi");
            await db.SaveChangesAsync(ct);
            return Results.Ok(new CreatedLicense(id, LicenseKeys.Derive(secrets.KeySecret, id)));
        });

        admin.MapPost("/licenses/{id}/extend", async (string id, ExtendRequest r, LicenseServerDb db, LicenseIssuer issuer, CancellationToken ct) =>
        {
            var l = await db.Licenses.FirstOrDefaultAsync(x => x.Id == id, ct);
            if (l is null)
            {
                return Results.NotFound();
            }

            l.PaidUntilUtc = DateTime.SpecifyKind(r.PaidUntilUtc, DateTimeKind.Utc);
            l.Status = ServerLicenseStatus.Active;
            issuer.Audit("license.extended", id, $"Yeni dönem sonu {l.PaidUntilUtc:yyyy-MM-dd}");
            await db.SaveChangesAsync(ct);
            return Results.Ok(Summary(l));
        });

        admin.MapPost("/licenses/{id}/cancel", async (string id, LicenseServerDb db, LicenseIssuer issuer, TimeProvider time, CancellationToken ct) =>
        {
            var l = await db.Licenses.FirstOrDefaultAsync(x => x.Id == id, ct);
            if (l is null)
            {
                return Results.NotFound();
            }

            // Cancelling stops renewals; what was paid for stays usable until the end of the paid period.
            l.Status = ServerLicenseStatus.Cancelled;
            issuer.Audit("license.cancelled", id, $"İptal; ödenen dönem sonu {l.PaidUntilUtc:yyyy-MM-dd} korunur");
            await db.SaveChangesAsync(ct);
            return Results.Ok(Summary(l));
        });

        admin.MapPost("/activations/{id}/deactivate", async (string id, LicenseServerDb db, LicenseIssuer issuer, TimeProvider time, CancellationToken ct) =>
        {
            var a = await db.Activations.FirstOrDefaultAsync(x => x.Id == id, ct);
            if (a is null)
            {
                return Results.NotFound();
            }

            a.DeactivatedAtUtc ??= time.GetUtcNow().UtcDateTime;
            issuer.Audit("activation.deactivated", a.LicenseId, $"{a.Id} (destek)");
            await db.SaveChangesAsync(ct);
            return Results.Ok(new { message = "Cihaz kaldırıldı." });
        });
    }

    private static LicenseSummary Summary(ServerLicense l) =>
        new(l.Id, l.CustomerName, l.CustomerEmail, l.Edition, l.MaxDevices, l.PaidUntilUtc, l.Status.ToString(), l.Provider, l.Activations.Count(a => a.IsActive));

    private static IResult ToHttp(IssueResult r) => r.ErrorCode is null && r.SignedLicense is not null
        ? Results.Ok(new LicenseResponse(r.SignedLicense, r.ActivationId!))
        : Results.Json(new ErrorResponse(r.ErrorCode ?? "error", r.Message ?? "Hata"), statusCode: r.HttpStatus);

    /// <summary>Only plain file names inside the releases folder (no paths, no traversal).</summary>
    public static string? SafeReleaseFile(string root, string file)
    {
        if (string.IsNullOrWhiteSpace(root) || string.IsNullOrWhiteSpace(file) || file.Length > 200
            || !file.All(c => char.IsAsciiLetterOrDigit(c) || c is '.' or '-' or '_') || file.StartsWith('.'))
        {
            return null;
        }

        var full = Path.GetFullPath(Path.Combine(root, file));
        var rootFull = Path.GetFullPath(root) + Path.DirectorySeparatorChar;
        return full.StartsWith(rootFull, StringComparison.Ordinal) && File.Exists(full) ? full : null;
    }
}

/// <summary>For <c>dotnet ef migrations</c> only.</summary>
public sealed class DesignTimeFactory : IDesignTimeDbContextFactory<LicenseServerDb>
{
    public LicenseServerDb CreateDbContext(string[] args) =>
        new(new DbContextOptionsBuilder<LicenseServerDb>().UseSqlite("Data Source=design.db").Options);
}

/// <summary><c>generate-key &lt;folder&gt;</c>: creates the signing key, key secret and admin key for a new server.</summary>
public static class KeyTool
{
    public static void Generate(string folder, TextWriter output)
    {
        Directory.CreateDirectory(folder);
        using var key = System.Security.Cryptography.ECDsa.Create(System.Security.Cryptography.ECCurve.NamedCurves.nistP256);
        var signingPath = Path.Combine(folder, "signing-key.pem");
        var secretPath = Path.Combine(folder, "key-secret.txt");
        if (File.Exists(signingPath) || File.Exists(secretPath))
        {
            throw new InvalidOperationException("Anahtar dosyaları zaten var; üzerine yazılmaz.");
        }

        File.WriteAllText(signingPath, key.ExportPkcs8PrivateKeyPem());
        File.WriteAllText(secretPath, Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32)));
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(signingPath, UnixFileMode.UserRead | UnixFileMode.UserWrite);
            File.SetUnixFileMode(secretPath, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }

        var adminKey = Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(24));
        var adminHash = Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(adminKey)));
        output.WriteLine("Signing key:   " + signingPath);
        output.WriteLine("Key secret:    " + secretPath);
        output.WriteLine("Public key (desktop LicensingOptions, SubjectPublicKeyInfo base64):");
        output.WriteLine("  " + Convert.ToBase64String(key.ExportSubjectPublicKeyInfo()));
        output.WriteLine("Admin key (store in a password manager; shown ONCE): " + adminKey);
        output.WriteLine("LicenseServer__AdminKeySha256=" + adminHash);
        output.WriteLine(string.Create(CultureInfo.InvariantCulture, $"Suggested key id: {DateTime.UtcNow:yyyy-MM}"));
    }
}
