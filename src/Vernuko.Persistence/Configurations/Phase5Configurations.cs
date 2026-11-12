using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vernuko.Messaging;

namespace Vernuko.Persistence.Configurations;

internal sealed class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> b)
    {
        b.ToTable("OutboxMessages");
        b.ConfigureEntity();
        b.Property(e => e.Recipient).HasMaxLength(20).IsRequired();
        b.Property(e => e.Text).HasMaxLength(1100).IsRequired();
        b.Property(e => e.IdempotencyKey).HasMaxLength(160).IsRequired();
        b.Property(e => e.ProviderKey).HasMaxLength(40);
        b.Property(e => e.ProviderMessageId).HasMaxLength(120);
        b.Property(e => e.LastError).HasMaxLength(500);
        b.HasIndex(e => e.IdempotencyKey).IsUnique();
        b.HasIndex(e => new { e.Status, e.NextAttemptUtc });
        b.HasIndex(e => e.AppointmentId);
        b.HasIndex(e => e.SentAtUtc);
    }
}
