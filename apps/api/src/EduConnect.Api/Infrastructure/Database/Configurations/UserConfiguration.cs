using EduConnect.Api.Infrastructure.Database.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EduConnect.Api.Infrastructure.Database.Configurations;

public class UserConfiguration : IEntityTypeConfiguration<UserEntity>
{
    public void Configure(EntityTypeBuilder<UserEntity> builder)
    {
        builder.ToTable("users");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.Phone).IsRequired().HasMaxLength(20);
        builder.Property(x => x.Name).IsRequired().HasMaxLength(256);
        builder.Property(x => x.Role).IsRequired().HasMaxLength(50);
        builder.Property(x => x.PasswordHash).HasMaxLength(500);
        builder.Property(x => x.PinHash).HasMaxLength(500);
        builder.Property(x => x.IsActive).IsRequired();

        builder.HasIndex(x => new { x.SchoolId, x.Phone }).IsUnique();
        builder.HasIndex(x => x.SchoolId);

        builder.HasOne(x => x.School)
            .WithMany(x => x.Users)
            .HasForeignKey(x => x.SchoolId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
