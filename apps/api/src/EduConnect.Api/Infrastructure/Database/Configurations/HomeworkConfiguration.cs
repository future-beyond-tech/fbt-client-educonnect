using EduConnect.Api.Infrastructure.Database.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EduConnect.Api.Infrastructure.Database.Configurations;

public class HomeworkConfiguration : IEntityTypeConfiguration<HomeworkEntity>
{
    public void Configure(EntityTypeBuilder<HomeworkEntity> builder)
    {
        builder.ToTable("homework");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.Subject).IsRequired().HasMaxLength(100);
        builder.Property(x => x.Title).IsRequired().HasMaxLength(256);
        builder.Property(x => x.Description).IsRequired().HasMaxLength(2000);
        builder.Property(x => x.IsEditable).IsRequired();
        builder.Property(x => x.IsDeleted).IsRequired();

        builder.HasIndex(x => x.SchoolId);
        builder.HasIndex(x => x.ClassId);
        builder.HasIndex(x => x.AssignedById);
        builder.HasIndex(x => x.DueDate);

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
    }
}
