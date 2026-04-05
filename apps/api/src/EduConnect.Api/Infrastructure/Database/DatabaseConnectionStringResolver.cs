using Microsoft.AspNetCore.WebUtilities;
using Npgsql;

namespace EduConnect.Api.Infrastructure.Database;

public static class DatabaseConnectionStringResolver
{
    public static string Resolve(string? databaseUrl)
    {
        if (string.IsNullOrWhiteSpace(databaseUrl))
        {
            throw new InvalidOperationException("DATABASE_URL must be set.");
        }

        if (!databaseUrl.Contains("://", StringComparison.Ordinal))
        {
            return databaseUrl;
        }

        if (!Uri.TryCreate(databaseUrl, UriKind.Absolute, out var uri) ||
            (!string.Equals(uri.Scheme, "postgres", StringComparison.OrdinalIgnoreCase) &&
             !string.Equals(uri.Scheme, "postgresql", StringComparison.OrdinalIgnoreCase)))
        {
            return databaseUrl;
        }

        var builder = new NpgsqlConnectionStringBuilder
        {
            Host = uri.Host,
            Port = uri.IsDefaultPort || uri.Port <= 0 ? 5432 : uri.Port,
            Database = Uri.UnescapeDataString(uri.AbsolutePath.TrimStart('/'))
        };

        if (!string.IsNullOrWhiteSpace(uri.UserInfo))
        {
            var parts = uri.UserInfo.Split(':', 2);
            builder.Username = Uri.UnescapeDataString(parts[0]);

            if (parts.Length > 1)
            {
                builder.Password = Uri.UnescapeDataString(parts[1]);
            }
        }

        foreach (var (key, values) in QueryHelpers.ParseQuery(uri.Query))
        {
            var value = values.LastOrDefault();
            if (string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            builder[key] = value;
        }

        return builder.ConnectionString;
    }
}
