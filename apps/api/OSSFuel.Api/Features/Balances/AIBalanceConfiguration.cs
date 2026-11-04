using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OSSFuel.Api.Features.Projects;

namespace OSSFuel.Api.Features.Balances;

public class AIBalanceConfiguration : IEntityTypeConfiguration<AIBalance>
{
    public void Configure(EntityTypeBuilder<AIBalance> builder)
    {
        builder.HasKey(balance => balance.Id);

        builder.HasIndex(balance => balance.ProjectId).IsUnique();

        builder.Property(balance => balance.Currency).HasMaxLength(3).IsRequired();
        builder.Property(balance => balance.RowVersion).IsRowVersion();

        builder
            .HasOne<Project>()
            .WithOne()
            .HasForeignKey<AIBalance>(balance => balance.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
