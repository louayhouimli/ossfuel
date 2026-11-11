using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OSSFuel.Api.Features.Projects;
using OSSFuel.Api.Features.Users;

namespace OSSFuel.Api.Features.ApiKeys;

public class ProjectApiKeyConfiguration : IEntityTypeConfiguration<ProjectApiKey>
{
    public void Configure(EntityTypeBuilder<ProjectApiKey> builder)
    {
        builder.HasKey(apiKey => apiKey.Id);

        builder.Property(apiKey => apiKey.Name).HasMaxLength(100).IsRequired();
        builder.Property(apiKey => apiKey.KeyPrefix).HasMaxLength(24).IsRequired();
        builder.Property(apiKey => apiKey.KeyHash).HasMaxLength(64).IsRequired();

        builder
            .HasOne<Project>()
            .WithMany()
            .HasForeignKey(apiKey => apiKey.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);

        builder
            .HasOne<User>()
            .WithMany()
            .HasForeignKey(apiKey => apiKey.CreatedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(apiKey => apiKey.KeyHash).IsUnique();
        builder.HasIndex(apiKey => new { apiKey.ProjectId, apiKey.CreatedAt });
    }
}
