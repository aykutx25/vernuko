using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vernuko.Appointments;
using Vernuko.BusinessProfiles;
using Vernuko.Core;
using Vernuko.Customers;
using Vernuko.Packages;
using Vernuko.Payments;
using Vernuko.Security;

namespace Vernuko.Persistence.Configurations;

internal static class EntityDefaults
{
    public static void ConfigureEntity<T>(this EntityTypeBuilder<T> b)
        where T : Entity
    {
        b.HasKey(e => e.Id);
        b.Property(e => e.Id).ValueGeneratedNever();
        b.Property(e => e.Version).IsConcurrencyToken();
    }
}

internal sealed class BusinessSettingsConfiguration : IEntityTypeConfiguration<BusinessSettings>
{
    public void Configure(EntityTypeBuilder<BusinessSettings> b)
    {
        b.ToTable("BusinessSettings");
        b.ConfigureEntity();
        b.Property(e => e.BusinessName).HasMaxLength(150).IsRequired();
        b.Property(e => e.TimeZoneId).HasMaxLength(64).IsRequired();
        b.Property(e => e.Currency).HasMaxLength(3).IsRequired();
        b.Property(e => e.CountryCode).HasMaxLength(2).IsRequired();
        b.Property(e => e.BusinessHours)
            .HasConversion(new WeeklyScheduleConverter(), new WeeklyScheduleComparer())
            .HasMaxLength(2000)
            .IsRequired();
        b.Ignore(e => e.Profile);
    }
}

internal sealed class BranchConfiguration : IEntityTypeConfiguration<Branch>
{
    public void Configure(EntityTypeBuilder<Branch> b)
    {
        b.ToTable("Branches");
        b.ConfigureEntity();
        b.Property(e => e.Name).HasMaxLength(150).IsRequired();
    }
}

internal sealed class UserAccountConfiguration : IEntityTypeConfiguration<UserAccount>
{
    public void Configure(EntityTypeBuilder<UserAccount> b)
    {
        b.ToTable("Users");
        b.ConfigureEntity();
        b.Property(e => e.Username).HasMaxLength(60).IsRequired().UseCollation("NOCASE");
        b.HasIndex(e => e.Username).IsUnique();
        b.Property(e => e.DisplayName).HasMaxLength(100).IsRequired();
        b.Property(e => e.PasswordHash).HasMaxLength(200).IsRequired();
        b.Property(e => e.GrantedPermissions).HasMaxLength(2000);
        b.Property(e => e.DeniedPermissions).HasMaxLength(2000);
    }
}

internal sealed class CustomerConfiguration : IEntityTypeConfiguration<Customer>
{
    public void Configure(EntityTypeBuilder<Customer> b)
    {
        b.ToTable("Customers");
        b.ConfigureEntity();
        b.HasIndex(e => e.Number).IsUnique();
        b.Property(e => e.FirstName).HasMaxLength(Customer.MaxNameLength).IsRequired();
        b.Property(e => e.LastName).HasMaxLength(Customer.MaxNameLength).IsRequired();
        b.Property(e => e.Phone).HasMaxLength(20);
        b.HasIndex(e => e.Phone);
        b.Property(e => e.Email).HasMaxLength(254);
        b.Property(e => e.Address).HasMaxLength(500);
        b.Property(e => e.Notes).HasMaxLength(Customer.MaxNotesLength);
        b.Property(e => e.NationalId).HasMaxLength(11);
        b.Property(e => e.CompanyName).HasMaxLength(200);
        b.Property(e => e.TaxOffice).HasMaxLength(100);
        b.Property(e => e.TaxNumber).HasMaxLength(11);
        b.Property(e => e.MarketingConsentSource).HasMaxLength(100);
        b.Property(e => e.SearchKey).HasMaxLength(400).IsRequired();
        b.HasIndex(e => e.SearchKey);
        b.HasIndex(e => e.State);
        b.Ignore(e => e.FullName);
        b.Ignore(e => e.DisplayPhone);
    }
}

internal sealed class ServiceConfiguration : IEntityTypeConfiguration<ServiceDefinition>
{
    public void Configure(EntityTypeBuilder<ServiceDefinition> b)
    {
        b.ToTable("Services");
        b.ConfigureEntity();
        b.Property(e => e.Name).HasMaxLength(120).IsRequired();
        b.Property(e => e.Category).HasMaxLength(80);
        b.Property(e => e.Color).HasMaxLength(16);
        b.HasIndex(e => e.Name);
    }
}

internal sealed class StaffConfiguration : IEntityTypeConfiguration<StaffMember>
{
    public void Configure(EntityTypeBuilder<StaffMember> b)
    {
        b.ToTable("Staff");
        b.ConfigureEntity();
        b.Property(e => e.DisplayName).HasMaxLength(100).IsRequired();
        b.Property(e => e.Title).HasMaxLength(80);
        b.Property(e => e.Phone).HasMaxLength(20);
        b.Property(e => e.Color).HasMaxLength(16);
        b.Property(e => e.WorkingHours)
            .HasConversion(new NullableWeeklyScheduleConverter(), new NullableWeeklyScheduleComparer())
            .HasMaxLength(2000);
        b.HasOne<Branch>().WithMany().HasForeignKey(e => e.BranchId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class StaffServiceConfiguration : IEntityTypeConfiguration<StaffService>
{
    public void Configure(EntityTypeBuilder<StaffService> b)
    {
        b.ToTable("StaffServices");
        b.HasKey(e => new { e.StaffMemberId, e.ServiceId });
        b.HasOne<StaffMember>().WithMany().HasForeignKey(e => e.StaffMemberId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne<ServiceDefinition>().WithMany().HasForeignKey(e => e.ServiceId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class ResourceConfiguration : IEntityTypeConfiguration<Resource>
{
    public void Configure(EntityTypeBuilder<Resource> b)
    {
        b.ToTable("Resources");
        b.ConfigureEntity();
        b.Property(e => e.Name).HasMaxLength(100).IsRequired();
        b.Property(e => e.Notes).HasMaxLength(500);
        b.HasOne<Branch>().WithMany().HasForeignKey(e => e.BranchId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class ScheduleExceptionConfiguration : IEntityTypeConfiguration<ScheduleException>
{
    public void Configure(EntityTypeBuilder<ScheduleException> b)
    {
        b.ToTable("ScheduleExceptions");
        b.ConfigureEntity();
        b.Property(e => e.Note).HasMaxLength(200);
        b.HasIndex(e => new { e.StartUtc, e.EndUtc });
        b.Ignore(e => e.Range);
    }
}

internal sealed class AppointmentConfiguration : IEntityTypeConfiguration<Appointment>
{
    public void Configure(EntityTypeBuilder<Appointment> b)
    {
        b.ToTable("Appointments");
        b.ConfigureEntity();
        b.Property(e => e.Notes).HasMaxLength(Appointment.MaxNotesLength);
        b.Property(e => e.MeetingUrl).HasMaxLength(Appointment.MaxMeetingUrlLength);
        b.Property(e => e.BodyArea).HasMaxLength(80);
        b.Property(e => e.CancellationReason).HasMaxLength(300);
        b.HasIndex(e => new { e.BranchId, e.StartUtc });
        b.HasIndex(e => new { e.CustomerId, e.StartUtc });
        b.HasIndex(e => new { e.StaffMemberId, e.StartUtc });
        b.HasIndex(e => e.CustomerPackageId);
        b.HasIndex(e => e.SeriesId);
        b.HasOne<Branch>().WithMany().HasForeignKey(e => e.BranchId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Customer>().WithMany().HasForeignKey(e => e.CustomerId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<ServiceDefinition>().WithMany().HasForeignKey(e => e.ServiceId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<StaffMember>().WithMany().HasForeignKey(e => e.StaffMemberId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<CustomerPackage>().WithMany().HasForeignKey(e => e.CustomerPackageId).OnDelete(DeleteBehavior.Restrict);
        b.HasMany(e => e.Bookings).WithOne().HasForeignKey(x => x.AppointmentId).OnDelete(DeleteBehavior.Cascade);
        b.HasMany(e => e.History).WithOne().HasForeignKey(x => x.AppointmentId).OnDelete(DeleteBehavior.Cascade);
        b.Navigation(e => e.Bookings).UsePropertyAccessMode(PropertyAccessMode.Field);
        b.Navigation(e => e.History).UsePropertyAccessMode(PropertyAccessMode.Field);
        b.Ignore(e => e.ServiceRange);
    }
}

internal sealed class AppointmentBookingConfiguration : IEntityTypeConfiguration<AppointmentBooking>
{
    public void Configure(EntityTypeBuilder<AppointmentBooking> b)
    {
        b.ToTable("AppointmentBookings");
        b.HasKey(e => e.Id);
        b.Property(e => e.Id).ValueGeneratedNever();
        b.HasIndex(e => new { e.ResourceId, e.BlockStartUtc, e.BlockEndUtc });
        // One row per resource and start: a waiting part splits the staff member into two rows of the same appointment.
        b.HasIndex(e => new { e.AppointmentId, e.ResourceId, e.BlockStartUtc }).IsUnique();
        b.Ignore(e => e.Block);
    }
}

internal sealed class AppointmentStatusChangeConfiguration : IEntityTypeConfiguration<AppointmentStatusChange>
{
    public void Configure(EntityTypeBuilder<AppointmentStatusChange> b)
    {
        b.ToTable("AppointmentStatusChanges");
        b.HasKey(e => e.Id);
        b.Property(e => e.Id).ValueGeneratedNever();
        b.Property(e => e.Reason).HasMaxLength(300);
        b.HasIndex(e => new { e.AppointmentId, e.AtUtc });
    }
}

internal sealed class PackageTemplateConfiguration : IEntityTypeConfiguration<PackageTemplate>
{
    public void Configure(EntityTypeBuilder<PackageTemplate> b)
    {
        b.ToTable("PackageTemplates");
        b.ConfigureEntity();
        b.Property(e => e.Name).HasMaxLength(120).IsRequired();
        b.HasOne<ServiceDefinition>().WithMany().HasForeignKey(e => e.ServiceId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class CustomerPackageConfiguration : IEntityTypeConfiguration<CustomerPackage>
{
    public void Configure(EntityTypeBuilder<CustomerPackage> b)
    {
        b.ToTable("CustomerPackages");
        b.ConfigureEntity();

        // Idempotency of the sale itself: a free package has no charge row to carry the key (double click = 2 packages).
        b.Property<string?>(PersistenceNames.SaleKey).HasMaxLength(120);
        b.HasIndex(PersistenceNames.SaleKey).IsUnique();
        b.Property(e => e.Name).HasMaxLength(120).IsRequired();
        b.Property(e => e.BodyArea).HasMaxLength(80);
        b.Property(e => e.Notes).HasMaxLength(1000);
        b.HasIndex(e => new { e.CustomerId, e.Status });
        b.HasOne<Customer>().WithMany().HasForeignKey(e => e.CustomerId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<ServiceDefinition>().WithMany().HasForeignKey(e => e.ServiceId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<PackageTemplate>().WithMany().HasForeignKey(e => e.TemplateId).OnDelete(DeleteBehavior.SetNull);
        b.HasMany(e => e.Entries).WithOne().HasForeignKey(x => x.CustomerPackageId).OnDelete(DeleteBehavior.Restrict);
        b.Navigation(e => e.Entries).UsePropertyAccessMode(PropertyAccessMode.Field);
        b.Ignore(e => e.RemainingSessions);
        b.Ignore(e => e.UsedSessions);
    }
}

internal sealed class SessionLedgerConfiguration : IEntityTypeConfiguration<SessionLedgerEntry>
{
    public void Configure(EntityTypeBuilder<SessionLedgerEntry> b)
    {
        b.ToTable("SessionLedger");
        b.HasKey(e => e.Id);
        b.Property(e => e.Id).ValueGeneratedNever();
        b.Property(e => e.Reason).HasMaxLength(300);
        b.HasIndex(e => new { e.CustomerPackageId, e.AtUtc });
        b.HasIndex(e => e.AppointmentId);
    }
}

internal sealed class CustomerTransactionConfiguration : IEntityTypeConfiguration<CustomerTransaction>
{
    public void Configure(EntityTypeBuilder<CustomerTransaction> b)
    {
        b.ToTable("CustomerTransactions");
        b.HasKey(e => e.Id);
        b.Property(e => e.Id).ValueGeneratedNever();
        b.Property(e => e.Currency).HasMaxLength(3).IsRequired();
        b.Property(e => e.Description).HasMaxLength(300);
        b.Property(e => e.IdempotencyKey).HasMaxLength(100).IsRequired();
        b.HasIndex(e => e.IdempotencyKey).IsUnique();
        b.HasIndex(e => new { e.CustomerId, e.OccurredAtUtc });
        b.HasIndex(e => e.OccurredAtUtc);
        b.HasIndex(e => e.AppointmentId);
        b.HasIndex(e => e.ReversesTransactionId).IsUnique();
        b.HasOne<Customer>().WithMany().HasForeignKey(e => e.CustomerId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class AuditEntryConfiguration : IEntityTypeConfiguration<AuditEntry>
{
    public void Configure(EntityTypeBuilder<AuditEntry> b)
    {
        b.ToTable("AuditEntries");
        b.HasKey(e => e.Id);
        b.Property(e => e.Id).ValueGeneratedNever();
        b.HasIndex(e => e.Sequence).IsUnique();
        b.HasIndex(e => e.OccurredAtUtc);
        b.HasIndex(e => new { e.EntityType, e.EntityId });
        b.Property(e => e.UserName).HasMaxLength(60);
        b.Property(e => e.Action).HasMaxLength(60).IsRequired();
        b.Property(e => e.EntityType).HasMaxLength(60).IsRequired();
        b.Property(e => e.Summary).HasMaxLength(1000).IsRequired();
        b.Property(e => e.PreviousHash).HasMaxLength(64).IsRequired();
        b.Property(e => e.Hash).HasMaxLength(64).IsRequired();
    }
}

internal sealed class AppSettingConfiguration : IEntityTypeConfiguration<AppSetting>
{
    public void Configure(EntityTypeBuilder<AppSetting> b)
    {
        b.ToTable("AppSettings");
        b.HasKey(e => e.Key);
        b.Property(e => e.Key).HasMaxLength(100);
        b.Property(e => e.Value).HasMaxLength(20000).IsRequired();
    }
}
