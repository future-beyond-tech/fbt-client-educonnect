using EduConnect.Api.Infrastructure.Database.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EduConnect.Api.Infrastructure.Database.Configurations;

public class UserPushSubscriptionConfiguration : IEntityTypeConfiguration<UserPushSubscriptionEntity>
{
    public void Configure(EntityTypeBuilder<UserPushSubscriptionEntity> builder)
    {
        builder.ToTable("user_push_subscriptions");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.Endpoint).IsRequired().HasMaxLength(2048);
        builder.Property(x => x.P256dh).IsRequired().HasMaxLength(256);
        builder.Property(x => x.Auth).IsRequired().HasMaxLength(64);
        builder.Property(x => x.UserAgent).HasMaxLength(512);
        builder.Property(x => x.CreatedAt).HasDefaultValueSql("NOW()");

        // Endpoint is globally unique — the same browser profile on the same
        // device always gets the same endpoint, so we want upsert semantics.
        builder.HasIndex(x => x.Endpoint)
            .IsUnique()
            .HasDatabaseName("ix_user_push_subscriptions_endpoint");

        builder.HasIndex(x => x.UserId)
            .HasDatabaseName("ix_user_push_subscriptions_user_id");

        builder.HasIndex(x => x.SchoolId)
            .HasDatabaseName("ix_user_push_subscriptions_school_id");

        builder.HasOne(x => x.School)
            .WithMany()
            .HasForeignKey(x => x.SchoolId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.User)
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
