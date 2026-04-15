using EduConnect.Api.Infrastructure.Database.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EduConnect.Api.Infrastructure.Database.Configurations;

public class ParentStudentLinkConfiguration : IEntityTypeConfiguration<ParentStudentLinkEntity>
{
    public void Configure(EntityTypeBuilder<ParentStudentLinkEntity> builder)
    {
        builder.ToTable("parent_student_links", tableBuilder =>
        {
            tableBuilder.HasCheckConstraint(
                "chk_parent_student_links_relationship",
                "relationship IN ('parent', 'guardian', 'grandparent', 'sibling', 'other')");
        });

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.Relationship).IsRequired().HasMaxLength(30).HasDefaultValue("parent");
        builder.Property(x => x.CreatedAt).HasDefaultValueSql("NOW()");

        builder.HasIndex(x => x.SchoolId);
        builder.HasIndex(x => x.ParentId);
        builder.HasIndex(x => x.StudentId);
        builder.HasIndex(x => new { x.ParentId, x.StudentId }).IsUnique();

        builder.HasOne(x => x.School)
            .WithMany(x => x.ParentStudentLinks)
            .HasForeignKey(x => x.SchoolId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Parent)
            .WithMany()
            .HasForeignKey(x => x.ParentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Student)
            .WithMany(x => x.ParentLinks)
            .HasForeignKey(x => x.StudentId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
