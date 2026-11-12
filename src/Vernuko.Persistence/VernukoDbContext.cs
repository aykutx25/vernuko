using Microsoft.EntityFrameworkCore;
using Vernuko.Appointments;
using Vernuko.BusinessProfiles;
using Vernuko.Core;
using Vernuko.Customers;
using Vernuko.Inventory;
using Vernuko.Packages;
using Vernuko.Payments;
using Vernuko.Security;

namespace Vernuko.Persistence;

public sealed class VernukoDbContext : DbContext
{
    private readonly TimeProvider _time;

    public VernukoDbContext(DbContextOptions<VernukoDbContext> options, TimeProvider time)
        : base(options)
    {
        _time = time;
    }

    public DbSet<BusinessSettings> BusinessSettings => Set<BusinessSettings>();

    public DbSet<Branch> Branches => Set<Branch>();

    public DbSet<UserAccount> Users => Set<UserAccount>();

    public DbSet<Customer> Customers => Set<Customer>();

    public DbSet<ServiceDefinition> Services => Set<ServiceDefinition>();

    public DbSet<StaffMember> Staff => Set<StaffMember>();

    public DbSet<StaffService> StaffServices => Set<StaffService>();

    public DbSet<Resource> Resources => Set<Resource>();

    public DbSet<ScheduleException> ScheduleExceptions => Set<ScheduleException>();

    public DbSet<Appointment> Appointments => Set<Appointment>();

    public DbSet<AppointmentBooking> AppointmentBookings => Set<AppointmentBooking>();

    public DbSet<AppointmentStatusChange> AppointmentStatusChanges => Set<AppointmentStatusChange>();

    public DbSet<PackageTemplate> PackageTemplates => Set<PackageTemplate>();

    public DbSet<CustomerPackage> CustomerPackages => Set<CustomerPackage>();

    public DbSet<SessionLedgerEntry> SessionLedger => Set<SessionLedgerEntry>();

    public DbSet<CustomerTransaction> Transactions => Set<CustomerTransaction>();

    public DbSet<AuditEntry> AuditEntries => Set<AuditEntry>();

    public DbSet<AppSetting> AppSettings => Set<AppSetting>();

    public DbSet<CommissionRule> CommissionRules => Set<CommissionRule>();

    public DbSet<CommissionEntry> CommissionEntries => Set<CommissionEntry>();

    public DbSet<Product> Products => Set<Product>();

    public DbSet<StockMovement> StockMovements => Set<StockMovement>();

    public DbSet<ServiceMaterial> ServiceMaterials => Set<ServiceMaterial>();

    public DbSet<WaitlistEntry> Waitlist => Set<WaitlistEntry>();

    public DbSet<ConsentTemplate> ConsentTemplates => Set<ConsentTemplate>();

    public DbSet<CustomerConsent> CustomerConsents => Set<CustomerConsent>();

    public DbSet<CustomerFile> CustomerFiles => Set<CustomerFile>();

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        Stamp();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        Stamp();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        configurationBuilder.Properties<DateTime>().HaveConversion<UtcDateTimeConverter>();
        configurationBuilder.Properties<Enum>().HaveConversion<string>().HaveMaxLength(40);
        configurationBuilder.Properties<string>().HaveMaxLength(500);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(VernukoDbContext).Assembly);
    }

    /// <summary>Sets audit timestamps and bumps the optimistic concurrency version.</summary>
    private void Stamp()
    {
        var now = _time.GetUtcNow().UtcDateTime;
        foreach (var entry in ChangeTracker.Entries<Entity>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    entry.Entity.CreatedAtUtc = now;
                    entry.Entity.UpdatedAtUtc = now;
                    entry.Entity.Version = 1;
                    break;
                case EntityState.Modified:
                    entry.Entity.UpdatedAtUtc = now;
                    entry.Property(e => e.Version).CurrentValue = entry.Property(e => e.Version).OriginalValue + 1;
                    break;
            }
        }
    }
}
