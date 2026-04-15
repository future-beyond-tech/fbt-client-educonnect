using System.Reflection;
using EduConnect.Api.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;

namespace EduConnect.Api.Infrastructure.Database;

public static class DatabaseSeeder
{
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
