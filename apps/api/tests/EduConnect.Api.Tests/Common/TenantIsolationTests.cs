using EduConnect.Api.Common.Auth;
using EduConnect.Api.Infrastructure.Database;
using EduConnect.Api.Infrastructure.Database.Entities;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace EduConnect.Api.Tests.Common;

public class TenantIsolationTests
{
    [Fact]
    public async Task AuthenticatedUser_OnlySeesRecordsForTheirSchool()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: $"TenantIsolationTest_{Guid.NewGuid()}")
            .Options;

        using (var seedContext = new AppDbContext(options))
        {
            var schoolA = new SchoolEntity
            {
                Id = Guid.NewGuid(),
                Name = "School A",
                Code = "SCHOOL-A",
                Address = "123 Main St",
                ContactPhone = "01234567890",
                ContactEmail = "school-a@example.com",
                IsActive = true,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            };

            var schoolB = new SchoolEntity
            {
                Id = Guid.NewGuid(),
                Name = "School B",
                Code = "SCHOOL-B",
                Address = "456 Oak Ave",
                ContactPhone = "09876543210",
                ContactEmail = "school-b@example.com",
                IsActive = true,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            };

            seedContext.Schools.AddRange(schoolA, schoolB);

            var userFromSchoolA = new UserEntity
            {
                Id = Guid.NewGuid(),
                SchoolId = schoolA.Id,
                Phone = "05555555555",
                Name = "User A",
                Role = "Parent",
                PasswordHash = "hash",
                IsActive = true,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            };

            var userFromSchoolB = new UserEntity
            {
                Id = Guid.NewGuid(),
                SchoolId = schoolB.Id,
                Phone = "06666666666",
                Name = "User B",
                Role = "Parent",
                PasswordHash = "hash",
                IsActive = true,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            };

            seedContext.Users.AddRange(userFromSchoolA, userFromSchoolB);

            var classInSchoolA = new ClassEntity
            {
                Id = Guid.NewGuid(),
                SchoolId = schoolA.Id,
                Name = "Grade 1",
                Section = "A",
                AcademicYear = "2024-2025",
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            };

            var classInSchoolB = new ClassEntity
            {
                Id = Guid.NewGuid(),
                SchoolId = schoolB.Id,
                Name = "Grade 1",
                Section = "B",
                AcademicYear = "2024-2025",
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            };

            seedContext.Classes.AddRange(classInSchoolA, classInSchoolB);

            await seedContext.SaveChangesAsync();
        }

        var currentUser = new CurrentUserService
        {
            UserId = Guid.NewGuid(),
            Role = "Parent",
            Name = "Test User"
        };

        using (var lookupContext = new AppDbContext(options))
        {
            currentUser.SchoolId = lookupContext.Schools.First(s => s.Code == "SCHOOL-A").Id;
        }

        using (var filteredContext = new AppDbContext(options, currentUser))
        {
            var visibleClasses = await filteredContext.Classes.ToListAsync();
            var hiddenSchoolClass = await filteredContext.Classes
                .IgnoreQueryFilters()
                .FirstAsync(c => c.Section == "B");

            visibleClasses.Should().HaveCount(1);
            visibleClasses[0].Section.Should().Be("A");
            visibleClasses[0].SchoolId.Should().Be(currentUser.SchoolId);
            visibleClasses.Should().NotContain(c => c.Id == hiddenSchoolClass.Id);
        }
    }
}
