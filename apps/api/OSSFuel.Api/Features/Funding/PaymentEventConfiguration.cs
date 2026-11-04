using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace OSSFuel.Api.Features.Funding;

public class PaymentEventConfiguration : IEntityTypeConfiguration<PaymentEvent>
{
    public void Configure(EntityTypeBuilder<PaymentEvent> builder)
    {
        builder.HasKey(paymentEvent => paymentEvent.Id);

        builder.Property(paymentEvent => paymentEvent.Provider).HasMaxLength(20).IsRequired();
        builder.Property(paymentEvent => paymentEvent.ProviderEventId).HasMaxLength(200).IsRequired();
        builder.Property(paymentEvent => paymentEvent.Type).HasMaxLength(100).IsRequired();
        builder.Property(paymentEvent => paymentEvent.ProviderObjectId).HasMaxLength(255);
        builder.Property(paymentEvent => paymentEvent.Detail).HasMaxLength(500);
        builder
            .Property(paymentEvent => paymentEvent.Outcome)
            .HasConversion(
                outcome => outcome.ToString().ToLowerInvariant(),
                value => Enum.Parse<PaymentOutcome>(value, true))
            .HasMaxLength(20)
            .IsRequired();

        builder.HasIndex(paymentEvent => paymentEvent.ProviderEventId).IsUnique();
    }
}
