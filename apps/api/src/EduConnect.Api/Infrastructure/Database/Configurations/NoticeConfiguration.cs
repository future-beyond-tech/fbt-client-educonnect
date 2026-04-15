using EduConnect.Api.Infrastructure.Database.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EduConnect.Api.Infrastructure.Database.Configurations;

public class NoticeConfiguration : IEntityTypeConfiguration<NoticeEntity>
{
    public void Configure(EntityTypeBuilder<NoticeEntity> builder)
    {
        builder.ToTable("notices", tableBuilder =>
        {
            tableBuilder.HasCheckConstraint(
                "chk_notices_target_audience",
                "target_audience IN ('All', 'Class', 'Section')");
        });

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.Title).IsRequired().HasMaxLength(256);
        builder.Property(x => x.Body).IsRequired().HasMaxLength(5000);
        builder.Property(x => x.TargetAudience).IsRequired().HasMaxLength(50);
        builder.Property(x => x.IsPublished).IsRequired().HasDefaultValue(false);
        builder.Property(x => x.IsDeleted).IsRequired().HasDefaultValue(false);
        builder.Property(x => x.CreatedAt).HasDefaultValueSql("NOW()");
        builder.Property(x => x.UpdatedAt).HasDefaultValueSql("NOW()");

        builder.HasIndex(x => x.SchoolId);
        builder.HasIndex(x => x.PublishedById);
        builder.HasIndex(x => x.TargetClassId);
        builder.HasIndex(x => new { x.SchoolId, x.IsPublished, x.IsDeleted })
            .HasFilter("is_published = true AND is_deleted = false");

        builder.HasOne(x => x.School)
            .WithMany(x => x.Notices)
            .HasForeignKey(x => x.SchoolId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.PublishedBy)
            .WithMany()
            .HasForeignKey(x => x.PublishedById)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.TargetClass)
            .WithMany(x => x.Notices)
            .HasForeignKey(x => x.TargetClassId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
