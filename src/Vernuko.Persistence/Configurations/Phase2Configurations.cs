using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vernuko.Appointments;
using Vernuko.Customers;
using Vernuko.Inventory;
using Vernuko.Payments;

namespace Vernuko.Persistence.Configurations;

internal sealed class CommissionRuleConfiguration : IEntityTypeConfiguration<CommissionRule>
{
    public void Configure(EntityTypeBuilder<CommissionRule> b)
    {
        b.ToTable("CommissionRules");
        b.ConfigureEntity();
        b.HasIndex(e => new { e.Basis, e.StaffMemberId, e.ItemId });
        b.Ignore(e => e.Specificity);
    }
}

internal sealed class CommissionEntryConfiguration : IEntityTypeConfiguration<CommissionEntry>
{
    public void Configure(EntityTypeBuilder<CommissionEntry> b)
    {
        b.ToTable("CommissionEntries");
        b.HasKey(e => e.Id);
        b.Property(e => e.Id).ValueGeneratedNever();
        b.Property(e => e.Description).HasMaxLength(300);
        b.Property(e => e.IdempotencyKey).HasMaxLength(120).IsRequired();
        b.HasIndex(e => e.IdempotencyKey).IsUnique();
        b.HasIndex(e => e.ReversesEntryId).IsUnique();
        b.HasIndex(e => new { e.StaffMemberId, e.OccurredAtUtc });
        b.HasIndex(e => e.SourceId);
    }
}

internal sealed class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> b)
    {
        b.ToTable("Products");
        b.ConfigureEntity();
        b.Property(e => e.Name).HasMaxLength(120).IsRequired();
        b.Property(e => e.Sku).HasMaxLength(60);
        b.Property(e => e.Unit).HasMaxLength(12).IsRequired();
        b.HasIndex(e => e.Name);
    }
}

internal sealed class StockMovementConfiguration : IEntityTypeConfiguration<StockMovement>
{
    public void Configure(EntityTypeBuilder<StockMovement> b)
    {
        b.ToTable("StockMovements");
        b.HasKey(e => e.Id);
        b.Property(e => e.Id).ValueGeneratedNever();
        b.Property(e => e.Note).HasMaxLength(300);
        b.HasIndex(e => new { e.ProductId, e.AtUtc });
        b.HasIndex(e => e.AppointmentId);
        b.HasIndex(e => e.ReversesMovementId).IsUnique();
        b.HasOne<Product>().WithMany().HasForeignKey(e => e.ProductId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class ServiceMaterialConfiguration : IEntityTypeConfiguration<ServiceMaterial>
{
    public void Configure(EntityTypeBuilder<ServiceMaterial> b)
    {
        b.ToTable("ServiceMaterials");
        b.HasKey(e => new { e.ServiceId, e.ProductId });
        b.HasOne<ServiceDefinition>().WithMany().HasForeignKey(e => e.ServiceId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne<Product>().WithMany().HasForeignKey(e => e.ProductId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class WaitlistConfiguration : IEntityTypeConfiguration<WaitlistEntry>
{
    public void Configure(EntityTypeBuilder<WaitlistEntry> b)
    {
        b.ToTable("Waitlist");
        b.ConfigureEntity();
        b.Property(e => e.PreferredTime).HasMaxLength(120);
        b.Property(e => e.Notes).HasMaxLength(500);
        b.HasIndex(e => new { e.Status, e.FromDate, e.ToDate });
        b.HasOne<Customer>().WithMany().HasForeignKey(e => e.CustomerId).OnDelete(DeleteBehavior.Restrict);
        b.Ignore(e => e.IsOpen);
    }
}

internal sealed class ConsentTemplateConfiguration : IEntityTypeConfiguration<ConsentTemplate>
{
    public void Configure(EntityTypeBuilder<ConsentTemplate> b)
    {
        b.ToTable("ConsentTemplates");
        b.ConfigureEntity();
        b.Property(e => e.Title).HasMaxLength(150).IsRequired();
        b.Property(e => e.Body).HasMaxLength(ConsentTemplate.MaxBodyLength).IsRequired();
        b.Ignore(e => e.BodySha256);
    }
}

internal sealed class CustomerConsentConfiguration : IEntityTypeConfiguration<CustomerConsent>
{
    public void Configure(EntityTypeBuilder<CustomerConsent> b)
    {
        b.ToTable("CustomerConsents");
        b.ConfigureEntity();
        b.Property(e => e.Title).HasMaxLength(150).IsRequired();
        b.Property(e => e.BodySha256).HasMaxLength(64).IsRequired();
        b.Property(e => e.Note).HasMaxLength(500);
        b.Property(e => e.RevokeReason).HasMaxLength(300);
        b.HasIndex(e => e.CustomerId);
        b.HasOne<Customer>().WithMany().HasForeignKey(e => e.CustomerId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<ConsentTemplate>().WithMany().HasForeignKey(e => e.TemplateId).OnDelete(DeleteBehavior.Restrict);
        b.Ignore(e => e.IsActive);
    }
}

internal sealed class CustomerFileConfiguration : IEntityTypeConfiguration<CustomerFile>
{
    public void Configure(EntityTypeBuilder<CustomerFile> b)
    {
        b.ToTable("CustomerFiles");
        b.ConfigureEntity();
        b.Property(e => e.OriginalName).HasMaxLength(200).IsRequired();
        b.Property(e => e.Sha256).HasMaxLength(64).IsRequired();
        b.Property(e => e.StoredName).HasMaxLength(60).IsRequired();
        b.Property(e => e.Note).HasMaxLength(500);
        b.HasIndex(e => e.StoredName).IsUnique();
        b.HasIndex(e => e.CustomerId);
        b.HasOne<Customer>().WithMany().HasForeignKey(e => e.CustomerId).OnDelete(DeleteBehavior.Restrict);
        b.Ignore(e => e.IsImage);
    }
}
