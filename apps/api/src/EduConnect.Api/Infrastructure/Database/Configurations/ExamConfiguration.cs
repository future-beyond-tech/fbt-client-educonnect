using EduConnect.Api.Infrastructure.Database.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EduConnect.Api.Infrastructure.Database.Configurations;

public class ExamConfiguration : IEntityTypeConfiguration<ExamEntity>
{
    public void Configure(EntityTypeBuilder<ExamEntity> builder)
    {
        builder.ToTable("exams");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.Name).IsRequired().HasMaxLength(128);
        builder.Property(x => x.AcademicYear).IsRequired().HasMaxLength(16);
        builder.Property(x => x.IsSchedulePublished).IsRequired().HasDefaultValue(false);
        builder.Property(x => x.IsResultsFinalized).IsRequired().HasDefaultValue(false);
        builder.Property(x => x.IsDeleted).IsRequired().HasDefaultValue(false);
        builder.Property(x => x.CreatedAt).HasDefaultValueSql("NOW()");
        builder.Property(x => x.UpdatedAt).HasDefaultValueSql("NOW()");

        builder.HasIndex(x => x.SchoolId);
        builder.HasIndex(x => x.ClassId);
        builder.HasIndex(x => new { x.SchoolId, x.ClassId, x.IsDeleted })
            .HasFilter("is_deleted = false");

        builder.HasOne(x => x.School)
            .WithMany()
            .HasForeignKey(x => x.SchoolId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Class)
            .WithMany()
            .HasForeignKey(x => x.ClassId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.CreatedBy)
            .WithMany()
            .HasForeignKey(x => x.CreatedById)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
