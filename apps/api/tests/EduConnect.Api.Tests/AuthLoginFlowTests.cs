using EduConnect.Api.Common.Auth;
using EduConnect.Api.Features.Auth.LoginParent;
using EduConnect.Api.Infrastructure.Database;
using EduConnect.Api.Infrastructure.Database.Entities;
using FluentAssertions;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace EduConnect.Api.Tests;

public class AuthLoginFlowTests
{
    [Fact]
    public async Task LoginParent_WithSharedPhoneAcrossSchools_UsesAccountWhosePinMatches()
    {
        var phone = "09123456789";
        var schoolAId = Guid.NewGuid();
        var schoolBId = Guid.NewGuid();
        var parentAId = Guid.NewGuid();
        var parentBId = Guid.NewGuid();
        var options = CreateOptions();
        var pinService = new PinService();

        await SeedAsync(
            options,
            CreateSchool(schoolAId, "School A"),
            CreateSchool(schoolBId, "School B"),
            CreateParent(parentAId, schoolAId, phone, "Parent A", pinService.HashPin("1111")),
            CreateParent(parentBId, schoolBId, phone, "Parent B", pinService.HashPin("2222")));

        var jwtTokenService = CreateJwtTokenService();
        var httpContextAccessor = new HttpContextAccessor { HttpContext = new DefaultHttpContext() };

        await using var context = new AppDbContext(options);
        var handler = new LoginParentCommandHandler(
            context,
            pinService,
            jwtTokenService,
            httpContextAccessor,
            Mock.Of<ILogger<LoginParentCommandHandler>>());

        var response = await handler.Handle(
            new LoginParentCommand(phone, "2222"),
            CancellationToken.None);

        response.AccessToken.Should().NotBeNullOrWhiteSpace();

        var principal = jwtTokenService.ValidateToken(response.AccessToken);
        principal.Should().NotBeNull();
        principal!.FindFirst("userId")!.Value.Should().Be(parentBId.ToString());
        principal.FindFirst("schoolId")!.Value.Should().Be(schoolBId.ToString());
        (principal.FindFirst("role")?.Value ?? principal.FindFirst(ClaimTypes.Role)?.Value)
            .Should()
            .Be("Parent");

        var refreshTokens = await context.RefreshTokens.ToListAsync();
        refreshTokens.Should().ContainSingle(rt => rt.UserId == parentBId);
        refreshTokens.Should().NotContain(rt => rt.UserId == parentAId);
    }

    private static DbContextOptions<AppDbContext> CreateOptions()
    {
        return new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"AuthLogin_{Guid.NewGuid()}")
            .Options;
    }

    private static JwtTokenService CreateJwtTokenService()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["JWT_SECRET"] = "dev-secret-key-minimum-64-characters-long-for-hmac-sha256-signing-requirement",
                ["JWT_ISSUER"] = "educonnect-api",
                ["JWT_AUDIENCE"] = "educonnect-client",
            })
            .Build();

        return new JwtTokenService(configuration, Mock.Of<ILogger<JwtTokenService>>());
    }

    private static SchoolEntity CreateSchool(Guid schoolId, string name)
    {
        return new SchoolEntity
        {
            Id = schoolId,
            Name = name,
            Code = $"SCH-{schoolId.ToString()[..6]}",
            Address = "Test Address",
            ContactPhone = "09999999999",
            ContactEmail = $"{name.Replace(" ", string.Empty).ToLowerInvariant()}@test.com",
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };
    }

    private static UserEntity CreateParent(
        Guid parentId,
        Guid schoolId,
        string phone,
        string name,
        string pinHash)
    {
        return new UserEntity
        {
            Id = parentId,
            SchoolId = schoolId,
            Phone = phone,
            Email = $"{name.Replace(" ", string.Empty).ToLowerInvariant()}@home.com",
            Name = name,
            Role = "Parent",
            PinHash = pinHash,
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };
    }

    private static async Task SeedAsync(
        DbContextOptions<AppDbContext> options,
        SchoolEntity schoolA,
        SchoolEntity schoolB,
        UserEntity parentA,
        UserEntity parentB)
    {
        await using var context = new AppDbContext(options);

        context.Schools.AddRange(schoolA, schoolB);
        context.Users.AddRange(parentA, parentB);

        await context.SaveChangesAsync();
    }
}
