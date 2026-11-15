using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vernuko.Commerce;
using Vernuko.Customers;
using Vernuko.Inventory;

namespace Vernuko.Persistence.Configurations;

internal sealed class SupplierConfiguration : IEntityTypeConfiguration<Supplier>
{
    public void Configure(EntityTypeBuilder<Supplier> b)
    {
        b.ToTable("Suppliers");
        b.ConfigureEntity();
        b.Property(e => e.Name).HasMaxLength(200).IsRequired();
        b.Property(e => e.TaxOffice).HasMaxLength(100);
        b.Property(e => e.TaxNumber).HasMaxLength(11);
        b.Property(e => e.Phone).HasMaxLength(40);
        b.Property(e => e.Email).HasMaxLength(200);
        b.Property(e => e.Iban).HasMaxLength(34);
        b.HasIndex(e => e.Name);
    }
}

internal sealed class SupplierTransactionConfiguration : IEntityTypeConfiguration<SupplierTransaction>
{
    public void Configure(EntityTypeBuilder<SupplierTransaction> b)
    {
        b.ToTable("SupplierTransactions");
        b.HasKey(e => e.Id);
        b.Property(e => e.Id).ValueGeneratedNever();
        b.Property(e => e.IdempotencyKey).HasMaxLength(120).IsRequired();
        b.HasIndex(e => e.IdempotencyKey).IsUnique();
        b.HasIndex(e => new { e.SupplierId, e.OccurredAtUtc });
        b.HasIndex(e => e.ReversesId).IsUnique();
        b.HasOne<Supplier>().WithMany().HasForeignKey(e => e.SupplierId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class SaleConfiguration : IEntityTypeConfiguration<Sale>
{
    public void Configure(EntityTypeBuilder<Sale> b)
    {
        b.ToTable("Sales");
        b.ConfigureEntity();
        b.Property(e => e.Number).HasMaxLength(30).IsRequired();
        b.HasIndex(e => e.Number).IsUnique();
        b.HasIndex(e => e.OccurredAtUtc);
        b.HasIndex(e => e.CustomerId);
        b.HasIndex(e => e.ReturnOfSaleId);

        // Idempotency of a sale: the screen sends one key per sale; a double click or a retry finds the first one.
        b.Property<string?>(PersistenceNames.DocumentKey).HasMaxLength(120);
        b.HasIndex(PersistenceNames.DocumentKey).IsUnique();
        b.HasOne<Customer>().WithMany().HasForeignKey(e => e.CustomerId).OnDelete(DeleteBehavior.Restrict);
        b.HasMany(e => e.Lines).WithOne().HasForeignKey(l => l.SaleId).OnDelete(DeleteBehavior.Restrict);
        b.Navigation(e => e.Lines).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

internal sealed class SaleLineConfiguration : IEntityTypeConfiguration<SaleLine>
{
    public void Configure(EntityTypeBuilder<SaleLine> b)
    {
        b.ToTable("SaleLines");
        b.HasKey(e => e.Id);
        b.Property(e => e.Id).ValueGeneratedNever();
        b.Property(e => e.ProductName).HasMaxLength(120).IsRequired();
        b.Property(e => e.Barcode).HasMaxLength(32);
        b.HasIndex(e => new { e.SaleId, e.LineNo }).IsUnique();
        b.HasIndex(e => e.ProductId);
        b.HasIndex(e => e.ReturnOfLineId);
        b.HasOne<Product>().WithMany().HasForeignKey(e => e.ProductId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class PurchaseConfiguration : IEntityTypeConfiguration<Purchase>
{
    public void Configure(EntityTypeBuilder<Purchase> b)
    {
        b.ToTable("Purchases");
        b.ConfigureEntity();
        b.Property(e => e.Number).HasMaxLength(30).IsRequired();
        b.Property(e => e.DocumentNo).HasMaxLength(60);
        b.HasIndex(e => e.Number).IsUnique();
        b.HasIndex(e => new { e.SupplierId, e.OccurredAtUtc });
        b.Property<string?>(PersistenceNames.DocumentKey).HasMaxLength(120);
        b.HasIndex(PersistenceNames.DocumentKey).IsUnique();
        b.HasOne<Supplier>().WithMany().HasForeignKey(e => e.SupplierId).OnDelete(DeleteBehavior.Restrict);
        b.HasMany(e => e.Lines).WithOne().HasForeignKey(l => l.PurchaseId).OnDelete(DeleteBehavior.Restrict);
        b.Navigation(e => e.Lines).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

internal sealed class PurchaseLineConfiguration : IEntityTypeConfiguration<PurchaseLine>
{
    public void Configure(EntityTypeBuilder<PurchaseLine> b)
    {
        b.ToTable("PurchaseLines");
        b.HasKey(e => e.Id);
        b.Property(e => e.Id).ValueGeneratedNever();
        b.Property(e => e.ProductName).HasMaxLength(120).IsRequired();
        b.HasIndex(e => new { e.PurchaseId, e.LineNo }).IsUnique();
        b.HasIndex(e => e.ProductId);
        b.HasOne<Product>().WithMany().HasForeignKey(e => e.ProductId).OnDelete(DeleteBehavior.Restrict);
    }
}
