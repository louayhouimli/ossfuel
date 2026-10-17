using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace OSSFuel.Api.Features.Projects;

public class ProjectConfiguration : IEntityTypeConfiguration<Project>
{
    public void Configure(EntityTypeBuilder<Project> builder)
    {
        builder.HasKey(project => project.Id);

        // GitHub's numeric id is stable across renames, so it is what enforces one project per repo.
        builder.HasIndex(project => project.GitHubRepoId).IsUnique();
        builder.HasIndex(project => project.FullName);
        builder.HasIndex(project => project.OwnerUserId);

        builder.Property(project => project.FullName).HasMaxLength(300).IsRequired();
        builder.Property(project => project.OwnerLogin).HasMaxLength(100).IsRequired();
        builder.Property(project => project.Name).HasMaxLength(200).IsRequired();
        builder.Property(project => project.Description).HasMaxLength(1000);
        builder.Property(project => project.HtmlUrl).HasMaxLength(500).IsRequired();
        builder.Property(project => project.HomepageUrl).HasMaxLength(500);
        builder.Property(project => project.Language).HasMaxLength(100);
        builder.Property(project => project.DefaultBranch).HasMaxLength(200);

        builder
            .HasOne(project => project.Owner)
            .WithMany(user => user.Projects)
            .HasForeignKey(project => project.OwnerUserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
