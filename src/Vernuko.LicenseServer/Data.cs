using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Vernuko.LicenseServer;

public enum ServerLicenseStatus
{
    Active,

    /// <summary>Renewal payment failed; the customer keeps working during the payment tolerance.</summary>
    PastDue,

    /// <summary>Paused by the customer or support; ends at the pause time.</summary>
    Suspended,

    Cancelled,
}

/// <summary>
/// One sold license (subscription or manual sale). The license KEY is never stored: only its SHA-256 hash for
/// lookup. The key itself can be derived again from the id with the server's key secret (for re-sending it).
/// No appointment/customer data of the business ever reaches this server.
/// </summary>
public sealed class ServerLicense
{
    public string Id { get; set; } = string.Empty;

    public string KeyHash { get; set; } = string.Empty;

    public string CustomerName { get; set; } = string.Empty;

    public string? CustomerEmail { get; set; }

    public string Edition { get; set; } = "Standart";

    /// <summary>Feature flags, comma separated (empty = core product).</summary>
    public string Features { get; set; } = string.Empty;

    public int MaxDevices { get; set; } = 1;

    /// <summary>End of the paid period (without the payment tolerance).</summary>
    public DateTime PaidUntilUtc { get; set; }

    public ServerLicenseStatus Status { get; set; }

    public string? Provider { get; set; }

    public string? ProviderSubscriptionId { get; set; }

    public string? ProviderCustomerId { get; set; }

    /// <summary>occurred_at of the last applied provider event; older events arriving late are ignored.</summary>
    public DateTime? ProviderEventAtUtc { get; set; }

    public string? Note { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public List<Activation> Activations { get; set; } = [];
}

/// <summary>A computer the license is activated on (identified by a salted hash, never raw hardware ids).</summary>
public sealed class Activation
{
    public string Id { get; set; } = string.Empty;

    public string LicenseId { get; set; } = string.Empty;

    public string DeviceHash { get; set; } = string.Empty;

    public string? MachineLabel { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime LastSeenUtc { get; set; }

    public DateTime? DeactivatedAtUtc { get; set; }

    public bool IsActive => DeactivatedAtUtc is null;
}

/// <summary>Server-side trial start per device, so reinstalling the app does not restart the 30-day trial.</summary>
public sealed class TrialRecord
{
    public string DeviceHash { get; set; } = string.Empty;

    public DateTime StartedAtUtc { get; set; }
}

/// <summary>Every received payment event once (provider + event id): duplicates are acknowledged and skipped.</summary>
public sealed class WebhookEvent
{
    public string Provider { get; set; } = string.Empty;

    public string EventId { get; set; } = string.Empty;

    public string EventType { get; set; } = string.Empty;

    public DateTime OccurredAtUtc { get; set; }

    public DateTime ReceivedAtUtc { get; set; }

    public string Outcome { get; set; } = string.Empty;

    public string? LicenseId { get; set; }
}

public sealed class ServerAuditEntry
{
    public long Id { get; set; }

    public DateTime AtUtc { get; set; }

    public string Action { get; set; } = string.Empty;

    public string? LicenseId { get; set; }

    public string Detail { get; set; } = string.Empty;
}

public sealed class LicenseServerDb(DbContextOptions<LicenseServerDb> options) : DbContext(options)
{
    public DbSet<ServerLicense> Licenses => Set<ServerLicense>();

    public DbSet<Activation> Activations => Set<Activation>();

    public DbSet<TrialRecord> Trials => Set<TrialRecord>();

    public DbSet<WebhookEvent> WebhookEvents => Set<WebhookEvent>();

    public DbSet<ServerAuditEntry> Audit => Set<ServerAuditEntry>();

    /// <summary>SQLite has no time zone: every DateTime is stored as UTC and read back marked as UTC.</summary>
    private static readonly ValueConverter<DateTime, DateTime> Utc = new(v => v.Kind == DateTimeKind.Utc ? v : v.ToUniversalTime(), v => DateTime.SpecifyKind(v, DateTimeKind.Utc));

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        configurationBuilder.Properties<DateTime>().HaveConversion<UtcConverter>();
    }

    private sealed class UtcConverter() : ValueConverter<DateTime, DateTime>(Utc.ConvertToProviderExpression, Utc.ConvertFromProviderExpression);

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ServerLicense>(b =>
        {
            b.HasKey(x => x.Id);
            b.Property(x => x.Id).HasMaxLength(40);
            b.Property(x => x.KeyHash).HasMaxLength(64).IsRequired();
            b.HasIndex(x => x.KeyHash).IsUnique();
            b.Property(x => x.CustomerName).HasMaxLength(200);
            b.Property(x => x.CustomerEmail).HasMaxLength(200);
            b.Property(x => x.Edition).HasMaxLength(60);
            b.Property(x => x.Features).HasMaxLength(500);
            b.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
            b.Property(x => x.Provider).HasMaxLength(20);
            b.Property(x => x.ProviderSubscriptionId).HasMaxLength(80);
            b.Property(x => x.ProviderCustomerId).HasMaxLength(80);
            b.HasIndex(x => new { x.Provider, x.ProviderSubscriptionId }).IsUnique();
            b.Property(x => x.Note).HasMaxLength(1000);
            b.HasMany(x => x.Activations).WithOne().HasForeignKey(a => a.LicenseId).OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<Activation>(b =>
        {
            b.HasKey(x => x.Id);
            b.Property(x => x.Id).HasMaxLength(40);
            b.Property(x => x.LicenseId).HasMaxLength(40);
            b.Property(x => x.DeviceHash).HasMaxLength(128).IsRequired();
            b.Property(x => x.MachineLabel).HasMaxLength(100);
            b.HasIndex(x => new { x.LicenseId, x.DeviceHash });
            b.Ignore(x => x.IsActive);
        });
        modelBuilder.Entity<TrialRecord>(b =>
        {
            b.HasKey(x => x.DeviceHash);
            b.Property(x => x.DeviceHash).HasMaxLength(128);
        });
        modelBuilder.Entity<WebhookEvent>(b =>
        {
            b.HasKey(x => new { x.Provider, x.EventId });
            b.Property(x => x.Provider).HasMaxLength(20);
            b.Property(x => x.EventId).HasMaxLength(80);
            b.Property(x => x.EventType).HasMaxLength(80);
            b.Property(x => x.Outcome).HasMaxLength(200);
            b.Property(x => x.LicenseId).HasMaxLength(40);
        });
        modelBuilder.Entity<ServerAuditEntry>(b =>
        {
            b.HasKey(x => x.Id);
            b.Property(x => x.Action).HasMaxLength(60);
            b.Property(x => x.LicenseId).HasMaxLength(40);
            b.Property(x => x.Detail).HasMaxLength(1000);
        });
    }
}
