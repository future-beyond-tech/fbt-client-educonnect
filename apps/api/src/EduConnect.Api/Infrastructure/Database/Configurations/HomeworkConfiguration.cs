using EduConnect.Api.Infrastructure.Database.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EduConnect.Api.Infrastructure.Database.Configurations;

public class HomeworkConfiguration : IEntityTypeConfiguration<HomeworkEntity>
{
    public void Configure(EntityTypeBuilder<HomeworkEntity> builder)
    {
        builder.ToTable("homework", tableBuilder =>
        {
            tableBuilder.HasCheckConstraint(
                "chk_homework_status",
                "status IN ('Draft', 'PendingApproval', 'Published', 'Rejected')");
        });

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.Subject).IsRequired().HasMaxLength(100);
        builder.Property(x => x.Title).IsRequired().HasMaxLength(256);
        builder.Property(x => x.Description).IsRequired().HasMaxLength(2000);
        builder.Property(x => x.Status).IsRequired().HasMaxLength(20).HasDefaultValue("Draft");
        builder.Property(x => x.RejectedReason).HasMaxLength(500);
        builder.Property(x => x.PublishedAt).HasDefaultValueSql("NOW()");
        builder.Property(x => x.IsEditable).IsRequired().HasDefaultValue(true);
        builder.Property(x => x.IsDeleted).IsRequired().HasDefaultValue(false);
        builder.Property(x => x.CreatedAt).HasDefaultValueSql("NOW()");
        builder.Property(x => x.UpdatedAt).HasDefaultValueSql("NOW()");

        builder.HasIndex(x => x.SchoolId);
        builder.HasIndex(x => x.ClassId);
        builder.HasIndex(x => x.AssignedById);
        builder.HasIndex(x => x.ApprovedById);
        builder.HasIndex(x => x.Status);
        builder.HasIndex(x => x.DueDate);
        builder.HasIndex(x => new { x.ClassId, x.IsDeleted })
            .HasFilter("is_deleted = false");

        builder.HasOne(x => x.School)
            .WithMany(x => x.Homeworks)
            .HasForeignKey(x => x.SchoolId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Class)
            .WithMany(x => x.Homeworks)
            .HasForeignKey(x => x.ClassId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.AssignedBy)
            .WithMany()
            .HasForeignKey(x => x.AssignedById)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.ApprovedBy)
            .WithMany()
            .HasForeignKey(x => x.ApprovedById)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(x => x.RejectedBy)
            .WithMany()
            .HasForeignKey(x => x.RejectedById)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
