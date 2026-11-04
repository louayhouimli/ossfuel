using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace OSSFuel.Api.Features.Funding;

public class FundingConfiguration : IEntityTypeConfiguration<Funding>
{
    public void Configure(EntityTypeBuilder<Funding> builder)
    {
        builder.HasKey(funding => funding.Id);

        builder.Property(funding => funding.Currency).HasMaxLength(3).IsRequired();
        builder.Property(funding => funding.Provider).HasMaxLength(20).IsRequired();
        builder.Property(funding => funding.ProviderCheckoutSessionId).HasMaxLength(255);
        builder.Property(funding => funding.ProviderCheckoutUrl).HasMaxLength(500);
        builder.Property(funding => funding.ProviderPaymentIntentId).HasMaxLength(255);
        builder.Property(funding => funding.FailureReason).HasMaxLength(500);
        builder
            .Property(funding => funding.Status)
            .HasConversion(
                status => status.ToString().ToLowerInvariant(),
                value => Enum.Parse<FundingStatus>(value, true))
            .HasMaxLength(20)
            .IsRequired();

        builder
            .HasOne(funding => funding.Project)
            .WithMany()
            .HasForeignKey(funding => funding.ProjectId)
            .OnDelete(DeleteBehavior.Restrict);

        builder
            .HasOne(funding => funding.Sponsor)
            .WithMany()
            .HasForeignKey(funding => funding.SponsorUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(funding => new { funding.SponsorUserId, funding.CreatedAt });
        builder.HasIndex(funding => funding.ProviderCheckoutSessionId).IsUnique();

        // One pending funding per sponsor and project, so a double click is harmless.
        builder
            .HasIndex(funding => new { funding.SponsorUserId, funding.ProjectId })
            .IsUnique()
            .HasFilter("\"Status\" = 'pending'");
    }
}
