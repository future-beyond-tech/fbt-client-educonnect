using EduConnect.Api.Infrastructure.Database.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EduConnect.Api.Infrastructure.Database.Configurations;

public class TeacherClassAssignmentConfiguration : IEntityTypeConfiguration<TeacherClassAssignmentEntity>
{
    public void Configure(EntityTypeBuilder<TeacherClassAssignmentEntity> builder)
    {
        builder.ToTable("teacher_class_assignments");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.Subject).IsRequired().HasMaxLength(100);

        builder.HasIndex(x => x.SchoolId);
        builder.HasIndex(x => x.TeacherId);
        builder.HasIndex(x => x.ClassId);
        builder.HasIndex(x => new { x.SchoolId, x.TeacherId, x.ClassId, x.Subject }).IsUnique();

        builder.HasOne(x => x.School)
            .WithMany(x => x.TeacherClassAssignments)
            .HasForeignKey(x => x.SchoolId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Teacher)
            .WithMany()
            .HasForeignKey(x => x.TeacherId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Class)
            .WithMany(x => x.TeacherClassAssignments)
            .HasForeignKey(x => x.ClassId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Property(x => x.AssignedById).HasColumnName("assigned_by");
        builder.HasOne(x => x.AssignedBy)
            .WithMany()
            .HasForeignKey(x => x.AssignedById)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
