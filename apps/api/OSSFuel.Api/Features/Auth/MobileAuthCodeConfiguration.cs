using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace OSSFuel.Api.Features.Auth;

public class MobileAuthCodeConfiguration : IEntityTypeConfiguration<MobileAuthCode>
{
    public void Configure(EntityTypeBuilder<MobileAuthCode> builder)
    {
        builder.HasKey(code => code.Id);

        builder.Property(code => code.CodeHash).HasMaxLength(64).IsRequired();

        builder
            .HasOne(code => code.User)
            .WithMany()
            .HasForeignKey(code => code.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(code => code.CodeHash).IsUnique();
        builder.HasIndex(code => code.ExpiresAt);
    }
}
