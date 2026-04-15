using EduConnect.Api.Infrastructure.Database.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EduConnect.Api.Infrastructure.Database.Configurations;

public class AttendanceRecordConfiguration : IEntityTypeConfiguration<AttendanceRecordEntity>
{
    public void Configure(EntityTypeBuilder<AttendanceRecordEntity> builder)
    {
        builder.ToTable("attendance_records", tableBuilder =>
        {
            tableBuilder.HasCheckConstraint("chk_attendance_status", "status IN ('Absent', 'Late')");
            tableBuilder.HasCheckConstraint(
                "chk_attendance_entered_by_role",
                "entered_by_role IN ('Parent', 'Teacher', 'Admin')");
        });

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.Status).IsRequired().HasMaxLength(50);
        builder.Property(x => x.EnteredByRole).IsRequired().HasMaxLength(50);
        builder.Property(x => x.IsDeleted).HasDefaultValue(false);
        builder.Property(x => x.CreatedAt).HasDefaultValueSql("NOW()");

        builder.HasIndex(x => x.SchoolId);
        builder.HasIndex(x => x.StudentId);
        builder.HasIndex(x => x.EnteredById);
        builder.HasIndex(x => new { x.StudentId, x.Date }).IsUnique()
            .HasFilter("is_deleted = false");
        builder.HasIndex(x => new { x.SchoolId, x.StudentId, x.Date });

        builder.HasOne(x => x.School)
            .WithMany(x => x.AttendanceRecords)
            .HasForeignKey(x => x.SchoolId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Student)
            .WithMany(x => x.AttendanceRecords)
            .HasForeignKey(x => x.StudentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.EnteredBy)
            .WithMany()
            .HasForeignKey(x => x.EnteredById)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
