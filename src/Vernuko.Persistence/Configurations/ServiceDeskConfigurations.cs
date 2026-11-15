using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vernuko.Customers;
using Vernuko.ServiceDesk;

namespace Vernuko.Persistence.Configurations;

internal sealed class ServiceTicketConfiguration : IEntityTypeConfiguration<ServiceTicket>
{
    public void Configure(EntityTypeBuilder<ServiceTicket> b)
    {
        b.ToTable("ServiceTickets");
        b.ConfigureEntity();
        b.Property(e => e.Number).HasMaxLength(30).IsRequired();
        b.HasIndex(e => e.Number).IsUnique();
        b.Property(e => e.Brand).HasMaxLength(60).IsRequired();
        b.Property(e => e.Model).HasMaxLength(80);
        b.Property(e => e.SerialNumber).HasMaxLength(60);
        b.HasIndex(e => e.SerialNumber);
        b.Property(e => e.Complaint).HasMaxLength(1000).IsRequired();
        b.Property(e => e.Diagnosis).HasMaxLength(2000);
        b.Property(e => e.WorkDone).HasMaxLength(2000);
        b.Property(e => e.UnrepairableReason).HasMaxLength(500);
        b.HasIndex(e => new { e.Status, e.DueDate });
        b.HasIndex(e => e.CustomerId);
        b.Property<string?>(PersistenceNames.DocumentKey).HasMaxLength(120);
        b.HasIndex(PersistenceNames.DocumentKey).IsUnique();
        b.HasOne<Customer>().WithMany().HasForeignKey(e => e.CustomerId).OnDelete(DeleteBehavior.Restrict);
        b.HasMany(e => e.Lines).WithOne().HasForeignKey(l => l.TicketId).OnDelete(DeleteBehavior.Restrict);
        b.Navigation(e => e.Lines).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

internal sealed class ServiceTicketLineConfiguration : IEntityTypeConfiguration<ServiceTicketLine>
{
    public void Configure(EntityTypeBuilder<ServiceTicketLine> b)
    {
        b.ToTable("ServiceTicketLines");
        b.HasKey(e => e.Id);
        b.Property(e => e.Id).ValueGeneratedNever();
        b.Property(e => e.Description).HasMaxLength(200).IsRequired();
        b.HasIndex(e => new { e.TicketId, e.LineNo }).IsUnique();
        b.Ignore(e => e.TotalMinor);
    }
}

internal sealed class ServiceTicketEventConfiguration : IEntityTypeConfiguration<ServiceTicketEvent>
{
    public void Configure(EntityTypeBuilder<ServiceTicketEvent> b)
    {
        b.ToTable("ServiceTicketEvents");
        b.HasKey(e => e.Id);
        b.Property(e => e.Id).ValueGeneratedNever();
        b.HasIndex(e => new { e.TicketId, e.AtUtc });
        b.HasOne<ServiceTicket>().WithMany().HasForeignKey(e => e.TicketId).OnDelete(DeleteBehavior.Restrict);
    }
}
