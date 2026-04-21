using EduConnect.Api.Infrastructure.Database.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EduConnect.Api.Infrastructure.Database.Configurations;

public class ExamSubjectConfiguration : IEntityTypeConfiguration<ExamSubjectEntity>
{
    public void Configure(EntityTypeBuilder<ExamSubjectEntity> builder)
    {
        builder.ToTable("exam_subjects", tableBuilder =>
        {
            tableBuilder.HasCheckConstraint(
                "chk_exam_subjects_time_order",
                "end_time > start_time");
            tableBuilder.HasCheckConstraint(
                "chk_exam_subjects_max_marks_positive",
                "max_marks > 0");
        });

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.Subject).IsRequired().HasMaxLength(100);
        builder.Property(x => x.Room).HasMaxLength(64);
        builder.Property(x => x.MaxMarks).HasColumnType("numeric(6,2)");
        builder.Property(x => x.CreatedAt).HasDefaultValueSql("NOW()");
        builder.Property(x => x.UpdatedAt).HasDefaultValueSql("NOW()");

        builder.HasIndex(x => x.SchoolId);
        builder.HasIndex(x => x.ExamId);
        // One subject may only appear once per exam — prevents duplicate
        // "Math" rows being saved when a teacher accidentally submits twice.
        builder.HasIndex(x => new { x.ExamId, x.Subject }).IsUnique();

        builder.HasOne(x => x.School)
            .WithMany()
            .HasForeignKey(x => x.SchoolId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Exam)
            .WithMany(e => e.Subjects)
            .HasForeignKey(x => x.ExamId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
