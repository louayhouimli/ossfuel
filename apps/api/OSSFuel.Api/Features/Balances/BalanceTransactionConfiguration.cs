using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace OSSFuel.Api.Features.Balances;

public class BalanceTransactionConfiguration : IEntityTypeConfiguration<BalanceTransaction>
{
    public void Configure(EntityTypeBuilder<BalanceTransaction> builder)
    {
        builder.HasKey(transaction => transaction.Id);

        builder.Property(transaction => transaction.Type).HasMaxLength(40).IsRequired();
        builder.Property(transaction => transaction.Status).HasMaxLength(40).IsRequired();
        builder.Property(transaction => transaction.Description).HasMaxLength(200);

        builder
            .HasOne<AIBalance>()
            .WithMany(balance => balance.Transactions)
            .HasForeignKey(transaction => transaction.AIBalanceId)
            .OnDelete(DeleteBehavior.Cascade);

        // One funding can only ever produce one credit, even if two requests race.
        builder.HasIndex(transaction => transaction.FundingId).IsUnique();
        builder.HasIndex(transaction => new { transaction.AIBalanceId, transaction.CreatedAt });
    }
}
