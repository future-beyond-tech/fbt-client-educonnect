using EduConnect.Api.Infrastructure.Database.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EduConnect.Api.Infrastructure.Database.Configurations;

public class ExamResultConfiguration : IEntityTypeConfiguration<ExamResultEntity>
{
    public void Configure(EntityTypeBuilder<ExamResultEntity> builder)
    {
        builder.ToTable("exam_results", tableBuilder =>
        {
            // Absent students must not carry a raw mark; a non-absent result
            // must have either marks OR a grade (enforced DB-side so any
            // backdoor SQL cannot bypass the validator).
            tableBuilder.HasCheckConstraint(
                "chk_exam_results_absent_marks",
                "(is_absent = true AND marks_obtained IS NULL) OR is_absent = false");
            tableBuilder.HasCheckConstraint(
                "chk_exam_results_has_score",
                "is_absent = true OR marks_obtained IS NOT NULL OR grade IS NOT NULL");
        });

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.MarksObtained).HasColumnType("numeric(6,2)");
        builder.Property(x => x.Grade).HasMaxLength(8);
        builder.Property(x => x.Remarks).HasMaxLength(500);
        builder.Property(x => x.IsAbsent).IsRequired().HasDefaultValue(false);
        builder.Property(x => x.CreatedAt).HasDefaultValueSql("NOW()");
        builder.Property(x => x.UpdatedAt).HasDefaultValueSql("NOW()");

        builder.HasIndex(x => x.SchoolId);
        builder.HasIndex(x => x.ExamId);
        builder.HasIndex(x => x.StudentId);
        // One row per (student, exam subject) — upserts target this key.
        builder.HasIndex(x => new { x.ExamSubjectId, x.StudentId }).IsUnique();

        builder.HasOne(x => x.School)
            .WithMany()
            .HasForeignKey(x => x.SchoolId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Exam)
            .WithMany(e => e.Results)
            .HasForeignKey(x => x.ExamId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.ExamSubject)
            .WithMany(es => es.Results)
            .HasForeignKey(x => x.ExamSubjectId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Student)
            .WithMany()
            .HasForeignKey(x => x.StudentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.RecordedBy)
            .WithMany()
            .HasForeignKey(x => x.RecordedById)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
