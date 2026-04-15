using System.Reflection;
using EduConnect.Api.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;

namespace EduConnect.Api.Infrastructure.Database;

public static class DatabaseSeeder
{
    public static async Task SeedProductionDataAsync(AppDbContext context, ILogger logger)
    {
        // 1. Ensure at least one School exists
        if (!await context.Schools.AnyAsync())
        {
            logger.LogInformation("Seeding default production school...");
            var defaultSchool = new Entities.SchoolEntity
            {
                Id = Guid.Parse("a1b2c3d4-0001-4000-8000-000000000001"),
                Name = "Default School",
                Code = "DEFAULT-001",
                Address = "Update Address",
                ContactPhone = "0000000000",
                ContactEmail = "admin@example.com"
            };
            context.Schools.Add(defaultSchool);
            await context.SaveChangesAsync();
        }

        // 2. Ensure at least one Admin user exists
        var schoolId = await context.Schools.Select(s => s.Id).FirstOrDefaultAsync();
        if (schoolId != Guid.Empty && !await context.Users.AnyAsync(u => u.Role == "Admin"))
        {
            logger.LogInformation("Seeding default production admin user...");
            var defaultAdmin = new Entities.UserEntity
            {
                Id = Guid.Parse("b1b2c3d4-0001-4000-8000-000000000001"),
                SchoolId = schoolId,
                Phone = "9000000001",
                Name = "System Administrator",
                Role = "Admin",
                PasswordHash = "$2a$12$LJ3m4ys7CQbMgOYFm5UMAO5eRvNPZ5vHQxdGBqH1x1zZt1TdGfJSa" // Password: EduConnect@2026
            };
            context.Users.Add(defaultAdmin);
            await context.SaveChangesAsync();
        }
    }
    public static async Task SeedDevelopmentDataAsync(AppDbContext context, ILogger logger)
    {
        var exePath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? "";
        var seedDir = Path.Combine(exePath, "Migrations", "seed");

        if (!Directory.Exists(seedDir))
        {
            logger.LogInformation("No seed directory found at {SeedDir}", seedDir);
            return;
        }

        var sqlFiles = Directory.GetFiles(seedDir, "*.sql").OrderBy(f => f).ToList();
        
        foreach (var file in sqlFiles)
        {
            logger.LogInformation("Executing seed file: {FileName}", Path.GetFileName(file));
            
            var sql = await File.ReadAllTextAsync(file);
            try
            {
                await context.Database.ExecuteSqlRawAsync(sql);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to apply seed script: {FileName}", Path.GetFileName(file));
                throw;
            }
        }
        
        logger.LogInformation("Database seeded successfully.");
    }
}
