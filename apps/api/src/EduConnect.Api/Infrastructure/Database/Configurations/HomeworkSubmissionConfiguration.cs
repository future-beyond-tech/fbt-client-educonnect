using EduConnect.Api.Infrastructure.Database.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EduConnect.Api.Infrastructure.Database.Configurations;

public class HomeworkSubmissionConfiguration : IEntityTypeConfiguration<HomeworkSubmissionEntity>
{
    public void Configure(EntityTypeBuilder<HomeworkSubmissionEntity> builder)
    {
        builder.ToTable("homework_submissions", tableBuilder =>
        {
            tableBuilder.HasCheckConstraint(
                "chk_homework_submission_status",
                "status IN ('Submitted', 'Late', 'Graded', 'Returned')");
        });

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.Status).IsRequired().HasMaxLength(16);
        builder.Property(x => x.BodyText).HasMaxLength(4000);
        builder.Property(x => x.Grade).HasMaxLength(32);
        builder.Property(x => x.Feedback).HasMaxLength(2000);
        builder.Property(x => x.SubmittedAt).HasDefaultValueSql("NOW()");
        builder.Property(x => x.CreatedAt).HasDefaultValueSql("NOW()");
        builder.Property(x => x.UpdatedAt).HasDefaultValueSql("NOW()");

        // One active submission per (homework, student).
        builder.HasIndex(x => new { x.HomeworkId, x.StudentId })
            .IsUnique()
            .HasDatabaseName("ux_homework_submissions_homework_student");

        builder.HasIndex(x => x.SchoolId);
        builder.HasIndex(x => x.HomeworkId);
        builder.HasIndex(x => x.StudentId);

        builder.HasOne(x => x.School)
            .WithMany()
            .HasForeignKey(x => x.SchoolId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Homework)
            .WithMany()
            .HasForeignKey(x => x.HomeworkId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Student)
            .WithMany()
            .HasForeignKey(x => x.StudentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.GradedBy)
            .WithMany()
            .HasForeignKey(x => x.GradedById)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
