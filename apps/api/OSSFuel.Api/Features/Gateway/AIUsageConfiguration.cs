using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OSSFuel.Api.Features.ApiKeys;
using OSSFuel.Api.Features.Projects;

namespace OSSFuel.Api.Features.Gateway;

public class AIUsageConfiguration : IEntityTypeConfiguration<AIUsage>
{
    public void Configure(EntityTypeBuilder<AIUsage> builder)
    {
        builder.HasKey(usage => usage.Id);

        builder.Property(usage => usage.Model).HasMaxLength(120).IsRequired();
        builder.Property(usage => usage.Provider).HasMaxLength(40).IsRequired();
        builder.Property(usage => usage.Status).HasMaxLength(40).IsRequired();

        builder
            .HasOne<Project>()
            .WithMany()
            .HasForeignKey(usage => usage.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);

        builder
            .HasOne<ProjectApiKey>()
            .WithMany()
            .HasForeignKey(usage => usage.ApiKeyId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(usage => new { usage.ProjectId, usage.CreatedAt });
        builder.HasIndex(usage => new { usage.ApiKeyId, usage.CreatedAt });
    }
}
