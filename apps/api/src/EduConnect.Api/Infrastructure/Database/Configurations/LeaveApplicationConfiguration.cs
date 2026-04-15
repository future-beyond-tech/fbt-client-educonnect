using EduConnect.Api.Infrastructure.Database.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EduConnect.Api.Infrastructure.Database.Configurations;

public class LeaveApplicationConfiguration : IEntityTypeConfiguration<LeaveApplicationEntity>
{
    public void Configure(EntityTypeBuilder<LeaveApplicationEntity> builder)
    {
        builder.ToTable("leave_applications", tableBuilder =>
        {
            tableBuilder.HasCheckConstraint(
                "chk_leave_applications_status",
                "status IN ('Pending', 'Approved', 'Rejected')");
            tableBuilder.HasCheckConstraint(
                "chk_leave_applications_dates",
                "end_date >= start_date");
        });

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();

        builder.Property(x => x.Reason).IsRequired();
        builder.Property(x => x.Status).IsRequired().HasMaxLength(20).HasDefaultValue("Pending");
        builder.Property(x => x.IsDeleted).HasDefaultValue(false);
        builder.Property(x => x.CreatedAt).HasDefaultValueSql("NOW()");
        builder.Property(x => x.UpdatedAt).HasDefaultValueSql("NOW()");

        // Indexes
        builder.HasIndex(x => x.SchoolId);
        builder.HasIndex(x => x.StudentId);
        builder.HasIndex(x => x.ParentId);
        builder.HasIndex(x => new { x.SchoolId, x.Status })
            .HasFilter("is_deleted = false");

        // Foreign keys
        builder.HasOne(x => x.School)
            .WithMany()
            .HasForeignKey(x => x.SchoolId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Student)
            .WithMany()
            .HasForeignKey(x => x.StudentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Parent)
            .WithMany()
            .HasForeignKey(x => x.ParentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.ReviewedBy)
            .WithMany()
            .HasForeignKey(x => x.ReviewedById)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
