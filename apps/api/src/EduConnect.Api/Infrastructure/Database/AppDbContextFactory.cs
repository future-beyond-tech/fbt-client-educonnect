using EduConnect.Api.Common.Auth;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace EduConnect.Api.Infrastructure.Database;

public class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    private const string FallbackConnectionString =
        "Host=localhost;Port=5432;Database=educonnect;Username=postgres;Password=postgres";

    public AppDbContext CreateDbContext(string[] args)
    {
        var databaseUrl = Environment.GetEnvironmentVariable("DATABASE_URL");
        var connectionString = string.IsNullOrWhiteSpace(databaseUrl)
            ? FallbackConnectionString
            : DatabaseConnectionStringResolver.Resolve(databaseUrl);

        var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
        optionsBuilder.UseNpgsql(connectionString);

        return new AppDbContext(optionsBuilder.Options, new CurrentUserService());
    }
}
