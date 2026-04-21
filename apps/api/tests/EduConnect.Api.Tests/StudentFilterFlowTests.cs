using EduConnect.Api.Common.Auth;
using EduConnect.Api.Common.Exceptions;
using EduConnect.Api.Features.Students.GetStudentsByClass;
using EduConnect.Api.Infrastructure.Database;
using EduConnect.Api.Infrastructure.Database.Entities;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace EduConnect.Api.Tests;

/// <summary>
/// Covers the composable student filter bar backend contract: multi-class,
/// status, and sort extensions to <see cref="GetStudentsByClassQuery"/>.
/// The existing single-class + search flow is still exercised by
/// <see cref="AdminOnboardingFlowTests"/> — the test here focuses on the
/// new params and tenant/role gates.
/// </summary>
public class StudentFilterFlowTests
{
    [Fact]
    public async Task ListStudents_WithoutFilters_ReturnsAllInSchool_InClassSectionRollOrder()
    {
        var schoolId = Guid.NewGuid();
        var classA = Guid.NewGuid();
        var classB = Guid.NewGuid();
        var options = CreateOptions();

        await SeedAsync(options, schoolId,
            classes:
            [
                Class(classA, schoolId, "6", "A"),
                Class(classB, schoolId, "6", "B")
            ],
            students:
            [
                Student(schoolId, classB, "6B-002", "Ben", isActive: true),
                Student(schoolId, classA, "6A-001", "Asha", isActive: true),
                Student(schoolId, classA, "6A-002", "Arjun", isActive: true)
            ]);

        var result = await ExecuteAsync(options, CreateAdmin(schoolId), new GetStudentsByClassQuery());

        result.Items.Select(i => i.RollNumber).Should().ContainInOrder("6A-001", "6A-002", "6B-002");
    }

    [Fact]
    public async Task ListStudents_WithClassIdsFilter_AppliesOrSemanticsAcrossSelectedClasses()
    {
        var schoolId = Guid.NewGuid();
        var classA = Guid.NewGuid();
        var classB = Guid.NewGuid();
        var classC = Guid.NewGuid();
        var options = CreateOptions();

        await SeedAsync(options, schoolId,
            classes:
            [
                Class(classA, schoolId, "6", "A"),
                Class(classB, schoolId, "6", "B"),
                Class(classC, schoolId, "6", "C")
            ],
            students:
            [
                Student(schoolId, classA, "A-1", "Asha"),
                Student(schoolId, classB, "B-1", "Ben"),
                Student(schoolId, classC, "C-1", "Cora")
            ]);

        var result = await ExecuteAsync(options, CreateAdmin(schoolId),
            new GetStudentsByClassQuery(ClassIds: $"{classA},{classC}"));

        result.Items.Select(i => i.Name).Should().BeEquivalentTo(["Asha", "Cora"]);
    }

    [Fact]
    public async Task ListStudents_OldSingleClassIdParam_StillWorksForBackwardsCompat()
    {
        var schoolId = Guid.NewGuid();
        var classA = Guid.NewGuid();
        var classB = Guid.NewGuid();
        var options = CreateOptions();

        await SeedAsync(options, schoolId,
            classes: [Class(classA, schoolId, "6", "A"), Class(classB, schoolId, "6", "B")],
            students:
            [
                Student(schoolId, classA, "A-1", "Asha"),
                Student(schoolId, classB, "B-1", "Ben")
            ]);

        var result = await ExecuteAsync(options, CreateAdmin(schoolId),
            new GetStudentsByClassQuery(ClassId: classA));

        result.Items.Should().ContainSingle().Which.Name.Should().Be("Asha");
    }

    [Fact]
    public async Task ListStudents_StatusInactiveFilter_ReturnsOnlyDeactivatedStudents()
    {
        var schoolId = Guid.NewGuid();
        var classA = Guid.NewGuid();
        var options = CreateOptions();

        await SeedAsync(options, schoolId,
            classes: [Class(classA, schoolId, "6", "A")],
            students:
            [
                Student(schoolId, classA, "A-1", "Active One", isActive: true),
                Student(schoolId, classA, "A-2", "Inactive Two", isActive: false)
            ]);

        var result = await ExecuteAsync(options, CreateAdmin(schoolId),
            new GetStudentsByClassQuery(Status: "inactive"));

        result.Items.Should().ContainSingle().Which.Name.Should().Be("Inactive Two");
    }

    [Fact]
    public async Task ListStudents_SortByNameDesc_OrdersByNameDescendingThenRoll()
    {
        var schoolId = Guid.NewGuid();
        var classA = Guid.NewGuid();
        var options = CreateOptions();

        await SeedAsync(options, schoolId,
            classes: [Class(classA, schoolId, "6", "A")],
            students:
            [
                Student(schoolId, classA, "A-1", "Asha"),
                Student(schoolId, classA, "A-2", "Ben"),
                Student(schoolId, classA, "A-3", "Cora")
            ]);

        var result = await ExecuteAsync(options, CreateAdmin(schoolId),
            new GetStudentsByClassQuery(SortBy: "nameDesc"));

        result.Items.Select(i => i.Name).Should().ContainInOrder("Cora", "Ben", "Asha");
    }

    [Fact]
    public async Task ListStudents_SortByCreatedDesc_ReturnsMostRecentlyEnrolledFirst()
    {
        var schoolId = Guid.NewGuid();
        var classA = Guid.NewGuid();
        var options = CreateOptions();

        var older = Student(schoolId, classA, "A-1", "Older");
        older.CreatedAt = DateTimeOffset.UtcNow.AddDays(-10);
        var newer = Student(schoolId, classA, "A-2", "Newer");
        newer.CreatedAt = DateTimeOffset.UtcNow;

        await SeedAsync(options, schoolId,
            classes: [Class(classA, schoolId, "6", "A")],
            students: [older, newer]);

        var result = await ExecuteAsync(options, CreateAdmin(schoolId),
            new GetStudentsByClassQuery(SortBy: "createdDesc"));

        result.Items.Select(i => i.Name).Should().ContainInOrder("Newer", "Older");
    }

    [Fact]
    public async Task ListStudents_CombinedClassAndStatusAndSort_AppliesAndSemantics()
    {
        var schoolId = Guid.NewGuid();
        var classA = Guid.NewGuid();
        var classB = Guid.NewGuid();
        var options = CreateOptions();

        await SeedAsync(options, schoolId,
            classes: [Class(classA, schoolId, "6", "A"), Class(classB, schoolId, "6", "B")],
            students:
            [
                Student(schoolId, classA, "A-1", "Match One", isActive: true),
                Student(schoolId, classA, "A-2", "Inactive Skip", isActive: false),
                Student(schoolId, classB, "B-1", "Wrong Class", isActive: true),
                Student(schoolId, classA, "A-3", "Match Three", isActive: true)
            ]);

        var result = await ExecuteAsync(options, CreateAdmin(schoolId),
            new GetStudentsByClassQuery(
                ClassIds: classA.ToString(),
                Status: "active",
                SortBy: "nameAsc"));

        result.Items.Select(i => i.Name).Should().ContainInOrder("Match One", "Match Three");
    }

    [Fact]
    public async Task ListStudents_TeacherWithMultiClassFilter_AllowedOnlyForAssignedClasses()
    {
        var schoolId = Guid.NewGuid();
        var classA = Guid.NewGuid();
        var classB = Guid.NewGuid();
        var classC = Guid.NewGuid();
        var teacherId = Guid.NewGuid();

        var options = CreateOptions();
        await SeedAsync(options, schoolId,
            classes:
            [
                Class(classA, schoolId, "6", "A"),
                Class(classB, schoolId, "6", "B"),
                Class(classC, schoolId, "6", "C")
            ],
            users:
            [
                CreateTeacherUser(teacherId, schoolId, "09000000001", "Teacher One", "t1@school.com")
            ],
            assignments:
            [
                new TeacherClassAssignmentEntity
                {
                    Id = Guid.NewGuid(),
                    SchoolId = schoolId,
                    TeacherId = teacherId,
                    ClassId = classA,
                    Subject = "Mathematics",
                    CreatedAt = DateTimeOffset.UtcNow
                },
                new TeacherClassAssignmentEntity
                {
                    Id = Guid.NewGuid(),
                    SchoolId = schoolId,
                    TeacherId = teacherId,
                    ClassId = classB,
                    Subject = "Mathematics",
                    CreatedAt = DateTimeOffset.UtcNow
                }
            ],
            students:
            [
                Student(schoolId, classA, "A-1", "Asha"),
                Student(schoolId, classB, "B-1", "Ben"),
                Student(schoolId, classC, "C-1", "Cora")
            ]);

        var teacherUser = CreateTeacherCurrentUser(schoolId, teacherId);

        // Within-scope classes are fine.
        var allowed = await ExecuteAsync(options, teacherUser,
            new GetStudentsByClassQuery(ClassIds: $"{classA},{classB}"));
        allowed.Items.Select(i => i.Name).Should().BeEquivalentTo(["Asha", "Ben"]);

        // Out-of-scope class is forbidden.
        var act = () => ExecuteAsync(options, teacherUser,
            new GetStudentsByClassQuery(ClassIds: $"{classA},{classC}"));
        await act.Should().ThrowAsync<ForbiddenException>();
    }

    [Fact]
    public async Task ListStudents_TeacherRoleIgnoresInactiveStatus_AlwaysReturnsActiveOnly()
    {
        var schoolId = Guid.NewGuid();
        var classA = Guid.NewGuid();
        var teacherId = Guid.NewGuid();

        var options = CreateOptions();
        await SeedAsync(options, schoolId,
            classes: [Class(classA, schoolId, "6", "A")],
            users: [CreateTeacherUser(teacherId, schoolId, "09000000001", "Teacher", "t@school.com")],
            assignments:
            [
                new TeacherClassAssignmentEntity
                {
                    Id = Guid.NewGuid(),
                    SchoolId = schoolId,
                    TeacherId = teacherId,
                    ClassId = classA,
                    Subject = "Mathematics",
                    CreatedAt = DateTimeOffset.UtcNow
                }
            ],
            students:
            [
                Student(schoolId, classA, "A-1", "Active", isActive: true),
                Student(schoolId, classA, "A-2", "Inactive", isActive: false)
            ]);

        // Even though Status=inactive is requested, teacher-role path clamps to active.
        var result = await ExecuteAsync(options, CreateTeacherCurrentUser(schoolId, teacherId),
            new GetStudentsByClassQuery(Status: "inactive"));

        result.Items.Should().ContainSingle().Which.Name.Should().Be("Active");
    }

    private static async Task<EduConnect.Api.Common.Models.PagedResult<StudentListDto>> ExecuteAsync(
        DbContextOptions<AppDbContext> options,
        CurrentUserService currentUser,
        GetStudentsByClassQuery query)
    {
        await using var context = new AppDbContext(options, currentUser);
        var handler = new GetStudentsByClassQueryHandler(
            context,
            currentUser,
            Mock.Of<ILogger<GetStudentsByClassQueryHandler>>());
        return await handler.Handle(query, CancellationToken.None);
    }

    private static DbContextOptions<AppDbContext> CreateOptions() =>
        new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"StudentFilter_{Guid.NewGuid()}")
            .Options;

    private static CurrentUserService CreateAdmin(Guid schoolId) => new()
    {
        UserId = Guid.NewGuid(),
        SchoolId = schoolId,
        Role = "Admin",
        Name = "Test Admin"
    };

    private static CurrentUserService CreateTeacherCurrentUser(Guid schoolId, Guid teacherId) => new()
    {
        UserId = teacherId,
        SchoolId = schoolId,
        Role = "Teacher",
        Name = "Test Teacher"
    };

    private static UserEntity CreateTeacherUser(Guid id, Guid schoolId, string phone, string name, string email) => new()
    {
        Id = id,
        SchoolId = schoolId,
        Phone = phone,
        Email = email,
        Name = name,
        Role = "Teacher",
        PasswordHash = "hashed",
        IsActive = true,
        CreatedAt = DateTimeOffset.UtcNow,
        UpdatedAt = DateTimeOffset.UtcNow
    };

    private static ClassEntity Class(Guid id, Guid schoolId, string name, string section) => new()
    {
        Id = id,
        SchoolId = schoolId,
        Name = name,
        Section = section,
        AcademicYear = "2026-27"
    };

    private static StudentEntity Student(Guid schoolId, Guid classId, string roll, string name, bool isActive = true) => new()
    {
        Id = Guid.NewGuid(),
        SchoolId = schoolId,
        ClassId = classId,
        Name = name,
        RollNumber = roll,
        IsActive = isActive,
        CreatedAt = DateTimeOffset.UtcNow,
        UpdatedAt = DateTimeOffset.UtcNow
    };

    private static async Task SeedAsync(
        DbContextOptions<AppDbContext> options,
        Guid schoolId,
        IEnumerable<ClassEntity>? classes = null,
        IEnumerable<StudentEntity>? students = null,
        IEnumerable<UserEntity>? users = null,
        IEnumerable<TeacherClassAssignmentEntity>? assignments = null)
    {
        await using var context = new AppDbContext(options);

        context.Schools.Add(new SchoolEntity
        {
            Id = schoolId,
            Name = "Test School",
            Code = $"SCH-{schoolId.ToString()[..6]}",
            Address = "Test Address",
            ContactPhone = "09999999999",
            ContactEmail = "school@test.com",
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        });

        if (classes != null) context.Classes.AddRange(classes);
        if (students != null) context.Students.AddRange(students);
        if (users != null) context.Users.AddRange(users);
        if (assignments != null) context.TeacherClassAssignments.AddRange(assignments);

        await context.SaveChangesAsync();
    }
}
