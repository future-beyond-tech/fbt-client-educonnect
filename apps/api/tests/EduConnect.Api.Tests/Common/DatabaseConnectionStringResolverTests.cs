using EduConnect.Api.Infrastructure.Database;
using FluentAssertions;
using Npgsql;
using Xunit;

namespace EduConnect.Api.Tests.Common;

public class DatabaseConnectionStringResolverTests
{
    [Fact]
    public void Resolve_ConvertsPostgresUrlIntoNpgsqlConnectionString()
    {
        var connectionString = DatabaseConnectionStringResolver.Resolve(
            "postgresql://educonnect:educonnect_dev@localhost:5433/educonnect?sslmode=Prefer");

        var builder = new NpgsqlConnectionStringBuilder(connectionString);

        builder.Host.Should().Be("localhost");
        builder.Port.Should().Be(5433);
        builder.Database.Should().Be("educonnect");
        builder.Username.Should().Be("educonnect");
        builder.Password.Should().Be("educonnect_dev");
        builder.SslMode.Should().Be(SslMode.Prefer);
    }

    [Fact]
    public void Resolve_LeavesNpgsqlConnectionStringUntouched()
    {
        const string connectionString = "Host=db;Port=5432;Database=educonnect;Username=educonnect;Password=educonnect_dev";

        DatabaseConnectionStringResolver.Resolve(connectionString)
            .Should().Be(connectionString);
    }
}
