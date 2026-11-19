using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace OSSFuel.Api.Features.Discovery;

public class ProjectChunkConfiguration : IEntityTypeConfiguration<ProjectChunk>
{
    public void Configure(EntityTypeBuilder<ProjectChunk> builder)
    {
        builder.ToTable("project_chunks");

        builder.HasKey(chunk => chunk.Id);

        builder.Property(chunk => chunk.ChunkType).HasMaxLength(50).IsRequired();
        builder.Property(chunk => chunk.Content).IsRequired();
        builder.Property(chunk => chunk.HeaderPath).HasMaxLength(500);

        builder.Property(chunk => chunk.Embedding).HasColumnType("vector(768)");

        builder
            .HasOne(chunk => chunk.Project)
            .WithMany()
            .HasForeignKey(chunk => chunk.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(chunk => chunk.ProjectId);

        builder
            .HasIndex(chunk => chunk.Embedding)
            .HasMethod("hnsw")
            .HasOperators("vector_cosine_ops");
    }
}
