using EduConnect.Api.Common.Auth;
using EduConnect.Api.Infrastructure.Database.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace EduConnect.Api.Infrastructure.Database;

public class AppDbContext : DbContext
{
    private readonly CurrentUserService _currentUserService;

    public AppDbContext(DbContextOptions<AppDbContext> options)
        : this(options, new CurrentUserService())
    {
    }

    public AppDbContext(DbContextOptions<AppDbContext> options, CurrentUserService currentUserService)
        : base(options)
    {
        _currentUserService = currentUserService;
    }

    public DbSet<SchoolEntity> Schools { get; set; }
    public DbSet<UserEntity> Users { get; set; }
    public DbSet<ClassEntity> Classes { get; set; }
    public DbSet<StudentEntity> Students { get; set; }
    public DbSet<TeacherClassAssignmentEntity> TeacherClassAssignments { get; set; }
    public DbSet<ParentStudentLinkEntity> ParentStudentLinks { get; set; }
    public DbSet<AttendanceRecordEntity> AttendanceRecords { get; set; }
    public DbSet<HomeworkEntity> Homeworks { get; set; }
    public DbSet<NoticeEntity> Notices { get; set; }
    public DbSet<SubjectEntity> Subjects { get; set; }
    public DbSet<NotificationEntity> Notifications { get; set; }
    public DbSet<AttachmentEntity> Attachments { get; set; }
    public DbSet<RefreshTokenEntity> RefreshTokens { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

        modelBuilder.Entity<SchoolEntity>()
            .HasQueryFilter(entity => !_currentUserService.IsAuthenticated || entity.Id == _currentUserService.SchoolId);
        modelBuilder.Entity<UserEntity>()
            .HasQueryFilter(entity => !_currentUserService.IsAuthenticated || entity.SchoolId == _currentUserService.SchoolId);
        modelBuilder.Entity<ClassEntity>()
            .HasQueryFilter(entity => !_currentUserService.IsAuthenticated || entity.SchoolId == _currentUserService.SchoolId);
        modelBuilder.Entity<StudentEntity>()
            .HasQueryFilter(entity => !_currentUserService.IsAuthenticated || entity.SchoolId == _currentUserService.SchoolId);
        modelBuilder.Entity<TeacherClassAssignmentEntity>()
            .HasQueryFilter(entity => !_currentUserService.IsAuthenticated || entity.SchoolId == _currentUserService.SchoolId);
        modelBuilder.Entity<ParentStudentLinkEntity>()
            .HasQueryFilter(entity => !_currentUserService.IsAuthenticated || entity.SchoolId == _currentUserService.SchoolId);
        modelBuilder.Entity<AttendanceRecordEntity>()
            .HasQueryFilter(entity => !_currentUserService.IsAuthenticated || entity.SchoolId == _currentUserService.SchoolId);
        modelBuilder.Entity<HomeworkEntity>()
            .HasQueryFilter(entity => !_currentUserService.IsAuthenticated || entity.SchoolId == _currentUserService.SchoolId);
        modelBuilder.Entity<NoticeEntity>()
            .HasQueryFilter(entity => !_currentUserService.IsAuthenticated || entity.SchoolId == _currentUserService.SchoolId);
        modelBuilder.Entity<SubjectEntity>()
            .HasQueryFilter(entity => !_currentUserService.IsAuthenticated || entity.SchoolId == _currentUserService.SchoolId);
        modelBuilder.Entity<NotificationEntity>()
            .HasQueryFilter(entity => !_currentUserService.IsAuthenticated || entity.SchoolId == _currentUserService.SchoolId);
        modelBuilder.Entity<AttachmentEntity>()
            .HasQueryFilter(entity => !_currentUserService.IsAuthenticated || entity.SchoolId == _currentUserService.SchoolId);
        modelBuilder.Entity<RefreshTokenEntity>()
            .HasQueryFilter(entity => !_currentUserService.IsAuthenticated ||
                                      (entity.User != null && entity.User.SchoolId == _currentUserService.SchoolId));

        ApplySnakeCaseNamingConventions(modelBuilder);
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var entries = ChangeTracker
            .Entries()
            .Where(e => e.Entity is not RefreshTokenEntity &&
                        e.State == EntityState.Modified);

        foreach (var entry in entries)
        {
            entry.Property("UpdatedAt").CurrentValue = DateTimeOffset.UtcNow;
        }

        return await base.SaveChangesAsync(cancellationToken);
    }

    private static void ApplySnakeCaseNamingConventions(ModelBuilder modelBuilder)
    {
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (entityType.GetTableName() is { } tableName)
            {
                entityType.SetTableName(ToSnakeCase(tableName));
            }

            foreach (var property in entityType.GetProperties())
            {
                property.SetColumnName(ToSnakeCase(property.Name));
            }

            foreach (var key in entityType.GetKeys())
            {
                key.SetName(ToSnakeCase(key.GetName() ?? string.Empty));
            }

            foreach (var foreignKey in entityType.GetForeignKeys())
            {
                foreignKey.SetConstraintName(ToSnakeCase(foreignKey.GetConstraintName() ?? string.Empty));
            }

            foreach (var index in entityType.GetIndexes())
            {
                index.SetDatabaseName(ToSnakeCase(index.GetDatabaseName() ?? string.Empty));
            }
        }
    }

    private static string ToSnakeCase(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        var builder = new System.Text.StringBuilder(value.Length + 8);

        for (var i = 0; i < value.Length; i++)
        {
            var current = value[i];
            var hasPrevious = i > 0;
            var hasNext = i + 1 < value.Length;

            if (char.IsUpper(current))
            {
                var previous = hasPrevious ? value[i - 1] : '\0';
                var next = hasNext ? value[i + 1] : '\0';

                if (hasPrevious &&
                    (char.IsLower(previous) ||
                     char.IsDigit(previous) ||
                     (char.IsUpper(previous) && hasNext && char.IsLower(next))))
                {
                    builder.Append('_');
                }

                builder.Append(char.ToLowerInvariant(current));
            }
            else
            {
                builder.Append(current);
            }
        }

        return builder.ToString();
    }
}
