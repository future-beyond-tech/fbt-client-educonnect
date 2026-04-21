using EduConnect.Api.Infrastructure.Database.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EduConnect.Api.Infrastructure.Database.Configurations;

public class AttachmentConfiguration : IEntityTypeConfiguration<AttachmentEntity>
{
    public void Configure(EntityTypeBuilder<AttachmentEntity> builder)
    {
        builder.ToTable("attachments", tableBuilder =>
        {
            tableBuilder.HasCheckConstraint(
                "chk_attachment_entity_type",
                "entity_type IS NULL OR entity_type IN ('homework', 'notice')");
            tableBuilder.HasCheckConstraint(
                "chk_attachment_content_type",
                "content_type IN ('image/jpeg', 'image/png', 'image/webp', 'application/pdf', 'application/msword', 'application/vnd.openxmlformats-officedocument.wordprocessingml.document')");
            tableBuilder.HasCheckConstraint(
                "chk_attachment_size",
                "size_bytes > 0 AND size_bytes <= 10485760");
            tableBuilder.HasCheckConstraint(
                "chk_attachment_status",
                "status IN ('Pending', 'Available', 'Infected', 'ScanFailed')");
        });

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.EntityType).HasMaxLength(30);
        builder.Property(x => x.StorageKey).IsRequired().HasMaxLength(500);
        builder.Property(x => x.FileName).IsRequired().HasMaxLength(255);
        builder.Property(x => x.ContentType).IsRequired().HasMaxLength(100);
        builder.Property(x => x.SizeBytes).IsRequired();
        builder.Property(x => x.UploadedAt).HasDefaultValueSql("NOW()");

        builder.Property(x => x.Status)
            .IsRequired()
            .HasMaxLength(16)
            .HasDefaultValue("Pending");
        builder.Property(x => x.ScannedAt);
        builder.Property(x => x.ThreatName).HasMaxLength(256);

        builder.HasIndex(x => x.Status)
            .HasDatabaseName("ix_attachments_status")
            .HasFilter("status <> 'Available'");

        builder.HasIndex(x => new { x.EntityId, x.EntityType })
            .HasDatabaseName("ix_attachments_entity");

        builder.HasIndex(x => x.SchoolId);
        builder.HasIndex(x => x.UploadedById);
        builder.HasIndex(x => x.UploadedAt)
            .HasFilter("entity_id IS NULL");

        builder.HasOne(x => x.School)
            .WithMany()
            .HasForeignKey(x => x.SchoolId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.UploadedBy)
            .WithMany()
            .HasForeignKey(x => x.UploadedById)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
