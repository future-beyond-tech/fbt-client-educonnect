using EduConnect.Api.Infrastructure.Database.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EduConnect.Api.Infrastructure.Database.Configurations;

public class AuthResetTokenConfiguration : IEntityTypeConfiguration<AuthResetTokenEntity>
{
    public void Configure(EntityTypeBuilder<AuthResetTokenEntity> builder)
    {
        builder.ToTable("auth_reset_tokens", tableBuilder =>
        {
            tableBuilder.HasCheckConstraint(
                "chk_auth_reset_purpose",
                "purpose IN ('Password', 'Pin')");
        });

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.TokenHash).IsRequired().HasMaxLength(128);
        builder.Property(x => x.Purpose).IsRequired().HasMaxLength(20);
        builder.Property(x => x.ExpiresAt).IsRequired();
        builder.Property(x => x.CreatedAt).IsRequired().HasDefaultValueSql("NOW()");

        builder.HasIndex(x => x.TokenHash).IsUnique();
        builder.HasIndex(x => x.UserId);
        builder.HasIndex(x => new { x.UserId, x.Purpose })
            .HasFilter("used_at IS NULL");
        builder.HasIndex(x => x.ExpiresAt);

        builder.HasOne(x => x.User)
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
