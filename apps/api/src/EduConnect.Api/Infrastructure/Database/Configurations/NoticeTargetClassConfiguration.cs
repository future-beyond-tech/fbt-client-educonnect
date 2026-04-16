using EduConnect.Api.Infrastructure.Database.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EduConnect.Api.Infrastructure.Database.Configurations;

public class NoticeTargetClassConfiguration : IEntityTypeConfiguration<NoticeTargetClassEntity>
{
    public void Configure(EntityTypeBuilder<NoticeTargetClassEntity> builder)
    {
        builder.ToTable("notice_target_classes");

        builder.HasKey(x => new { x.NoticeId, x.ClassId });

        builder.Property(x => x.CreatedAt).HasDefaultValueSql("NOW()");

        builder.HasIndex(x => x.SchoolId);
        builder.HasIndex(x => x.ClassId);
        builder.HasIndex(x => new { x.SchoolId, x.ClassId });

        builder.HasOne(x => x.Notice)
            .WithMany(x => x.TargetClasses)
            .HasForeignKey(x => x.NoticeId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.TargetClass)
            .WithMany(x => x.NoticeTargetClasses)
            .HasForeignKey(x => x.ClassId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.School)
            .WithMany(x => x.NoticeTargetClasses)
            .HasForeignKey(x => x.SchoolId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
