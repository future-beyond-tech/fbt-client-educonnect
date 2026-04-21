using EduConnect.Api.Common.Auth;
using EduConnect.Api.Common.Exceptions;
using EduConnect.Api.Features.Teachers.GetTeacherFilterMetadata;
using EduConnect.Api.Features.Teachers.GetTeachersBySchool;
using EduConnect.Api.Infrastructure.Database;
using EduConnect.Api.Infrastructure.Database.Entities;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace EduConnect.Api.Tests;

/// <summary>
/// Covers the composable staff filter bar backend: search + subjects + class-load + sort,
/// each in isolation, and one combined case. Also covers the filter-metadata endpoint.
/// Uses EF Core InMemory; correlated subqueries are evaluated client-side, but the LINQ
/// shape matches what Postgres translates in production.
/// </summary>
public class TeacherFilterFlowTests
{
    [Fact]
    public async Task ListTeachers_WithNoFilters_ReturnsAllStaffInNameAscByDefault()
    {
        var schoolId = Guid.NewGuid();
        var currentUser = CreateAdmin(schoolId);
        var options = CreateOptions();

        await SeedAsync(options, schoolId, staff:
        [
            Teacher(schoolId, "Zara Khan"),
            Teacher(schoolId, "Asha Menon"),
            Admin(schoolId, "Mika Patel")
        ]);

        var result = await ExecuteListAsync(options, currentUser, new GetTeachersBySchoolQuery());

        result.Items.Should().HaveCount(3);
        result.Items.Select(i => i.Name).Should().ContainInOrder("Asha Menon", "Mika Patel", "Zara Khan");
    }

    [Fact]
    public async Task ListTeachers_FilteredBySubject_ReturnsOnlyTeachersAssignedToThatSubject()
    {
        var schoolId = Guid.NewGuid();
        var currentUser = CreateAdmin(schoolId);
        var classId = Guid.NewGuid();
        var tMath = Teacher(schoolId, "Math Teacher");
        var tScience = Teacher(schoolId, "Science Teacher");

        var options = CreateOptions();
        await SeedAsync(options, schoolId,
            staff: [tMath, tScience],
            classes: [new ClassEntity { Id = classId, SchoolId = schoolId, Name = "7", Section = "A", AcademicYear = "2026-27" }],
            assignments:
            [
                Assignment(schoolId, tMath.Id, classId, "Mathematics"),
                Assignment(schoolId, tScience.Id, classId, "Science")
            ]);

        var result = await ExecuteListAsync(options, currentUser,
            new GetTeachersBySchoolQuery(Subjects: "Mathematics"));

        result.Items.Should().ContainSingle().Which.Name.Should().Be("Math Teacher");
    }

    [Fact]
    public async Task ListTeachers_SubjectsFilter_UsesOrSemanticsWithinMultiSelect()
    {
        var schoolId = Guid.NewGuid();
        var currentUser = CreateAdmin(schoolId);
        var classId = Guid.NewGuid();
        var tMath = Teacher(schoolId, "Math Teacher");
        var tScience = Teacher(schoolId, "Science Teacher");
        var tEnglish = Teacher(schoolId, "English Teacher");

        var options = CreateOptions();
        await SeedAsync(options, schoolId,
            staff: [tMath, tScience, tEnglish],
            classes: [new ClassEntity { Id = classId, SchoolId = schoolId, Name = "7", Section = "A", AcademicYear = "2026-27" }],
            assignments:
            [
                Assignment(schoolId, tMath.Id, classId, "Mathematics"),
                Assignment(schoolId, tScience.Id, classId, "Science"),
                Assignment(schoolId, tEnglish.Id, classId, "English")
            ]);

        var result = await ExecuteListAsync(options, currentUser,
            new GetTeachersBySchoolQuery(Subjects: "Mathematics, Science"));

        result.Items.Select(i => i.Name).Should().BeEquivalentTo(["Math Teacher", "Science Teacher"]);
    }

    [Fact]
    public async Task ListTeachers_ClassLoadUnassigned_ReturnsOnlyStaffWithZeroClasses()
    {
        var schoolId = Guid.NewGuid();
        var currentUser = CreateAdmin(schoolId);
        var classId = Guid.NewGuid();
        var assigned = Teacher(schoolId, "Assigned One");
        var unassigned = Teacher(schoolId, "Unassigned Two");
        var admin = Admin(schoolId, "Admin Three");

        var options = CreateOptions();
        await SeedAsync(options, schoolId,
            staff: [assigned, unassigned, admin],
            classes: [new ClassEntity { Id = classId, SchoolId = schoolId, Name = "7", Section = "A", AcademicYear = "2026-27" }],
            assignments: [Assignment(schoolId, assigned.Id, classId, "Mathematics")]);

        var result = await ExecuteListAsync(options, currentUser,
            new GetTeachersBySchoolQuery(ClassLoad: "unassigned"));

        result.Items.Select(i => i.Name).Should().BeEquivalentTo(["Admin Three", "Unassigned Two"]);
    }

    [Fact]
    public async Task ListTeachers_ClassLoadHeavy_ReturnsStaffWithThreeOrMoreDistinctClasses()
    {
        var schoolId = Guid.NewGuid();
        var currentUser = CreateAdmin(schoolId);
        var classA = Guid.NewGuid();
        var classB = Guid.NewGuid();
        var classC = Guid.NewGuid();
        var heavy = Teacher(schoolId, "Heavy Load");
        var light = Teacher(schoolId, "Light Load");

        var options = CreateOptions();
        await SeedAsync(options, schoolId,
            staff: [heavy, light],
            classes:
            [
                new ClassEntity { Id = classA, SchoolId = schoolId, Name = "6", Section = "A", AcademicYear = "2026-27" },
                new ClassEntity { Id = classB, SchoolId = schoolId, Name = "6", Section = "B", AcademicYear = "2026-27" },
                new ClassEntity { Id = classC, SchoolId = schoolId, Name = "6", Section = "C", AcademicYear = "2026-27" }
            ],
            assignments:
            [
                Assignment(schoolId, heavy.Id, classA, "Mathematics"),
                Assignment(schoolId, heavy.Id, classB, "Mathematics"),
                Assignment(schoolId, heavy.Id, classC, "Mathematics"),
                // Duplicate subject on same class should not inflate the distinct class count.
                Assignment(schoolId, heavy.Id, classA, "Science"),
                Assignment(schoolId, light.Id, classA, "English"),
                Assignment(schoolId, light.Id, classB, "English")
            ]);

        var result = await ExecuteListAsync(options, currentUser,
            new GetTeachersBySchoolQuery(ClassLoad: "heavy"));

        result.Items.Should().ContainSingle().Which.Name.Should().Be("Heavy Load");
    }

    [Fact]
    public async Task ListTeachers_SortByClassesDesc_OrdersByDistinctClassCountDescending()
    {
        var schoolId = Guid.NewGuid();
        var currentUser = CreateAdmin(schoolId);
        var classA = Guid.NewGuid();
        var classB = Guid.NewGuid();
        var one = Teacher(schoolId, "Aaron One");
        var two = Teacher(schoolId, "Bella Two");

        var options = CreateOptions();
        await SeedAsync(options, schoolId,
            staff: [one, two],
            classes:
            [
                new ClassEntity { Id = classA, SchoolId = schoolId, Name = "6", Section = "A", AcademicYear = "2026-27" },
                new ClassEntity { Id = classB, SchoolId = schoolId, Name = "6", Section = "B", AcademicYear = "2026-27" }
            ],
            assignments:
            [
                Assignment(schoolId, one.Id, classA, "Mathematics"),
                Assignment(schoolId, two.Id, classA, "English"),
                Assignment(schoolId, two.Id, classB, "English")
            ]);

        var result = await ExecuteListAsync(options, currentUser,
            new GetTeachersBySchoolQuery(SortBy: "classesDesc"));

        result.Items.Select(i => i.Name).Should().ContainInOrder("Bella Two", "Aaron One");
    }

    [Fact]
    public async Task ListTeachers_SortByCreatedDesc_ReturnsMostRecentlyAddedFirst()
    {
        var schoolId = Guid.NewGuid();
        var currentUser = CreateAdmin(schoolId);

        var oldUser = Teacher(schoolId, "Old Staff");
        oldUser.CreatedAt = DateTimeOffset.UtcNow.AddDays(-30);
        var newUser = Teacher(schoolId, "New Staff");
        newUser.CreatedAt = DateTimeOffset.UtcNow;

        var options = CreateOptions();
        await SeedAsync(options, schoolId, staff: [oldUser, newUser]);

        var result = await ExecuteListAsync(options, currentUser,
            new GetTeachersBySchoolQuery(SortBy: "createdDesc"));

        result.Items.Select(i => i.Name).Should().ContainInOrder("New Staff", "Old Staff");
    }

    [Fact]
    public async Task ListTeachers_CombinedFilters_AppliesRoleSubjectAndClassLoadWithAndSemantics()
    {
        // Role filter isn't in the query yet (still derived client-side), but admins are
        // still included in the base query — we rely on subject + class-load combo here.
        var schoolId = Guid.NewGuid();
        var currentUser = CreateAdmin(schoolId);
        var classA = Guid.NewGuid();
        var classB = Guid.NewGuid();
        var classC = Guid.NewGuid();

        var match = Teacher(schoolId, "Perfect Match");
        var wrongSubject = Teacher(schoolId, "Wrong Subject");
        var notHeavy = Teacher(schoolId, "Not Heavy Enough");
        var admin = Admin(schoolId, "Unassigned Admin");

        var options = CreateOptions();
        await SeedAsync(options, schoolId,
            staff: [match, wrongSubject, notHeavy, admin],
            classes:
            [
                new ClassEntity { Id = classA, SchoolId = schoolId, Name = "6", Section = "A", AcademicYear = "2026-27" },
                new ClassEntity { Id = classB, SchoolId = schoolId, Name = "6", Section = "B", AcademicYear = "2026-27" },
                new ClassEntity { Id = classC, SchoolId = schoolId, Name = "6", Section = "C", AcademicYear = "2026-27" }
            ],
            assignments:
            [
                Assignment(schoolId, match.Id, classA, "Mathematics"),
                Assignment(schoolId, match.Id, classB, "Mathematics"),
                Assignment(schoolId, match.Id, classC, "Mathematics"),
                Assignment(schoolId, wrongSubject.Id, classA, "English"),
                Assignment(schoolId, wrongSubject.Id, classB, "English"),
                Assignment(schoolId, wrongSubject.Id, classC, "English"),
                Assignment(schoolId, notHeavy.Id, classA, "Mathematics"),
                Assignment(schoolId, notHeavy.Id, classB, "Mathematics")
            ]);

        var result = await ExecuteListAsync(options, currentUser,
            new GetTeachersBySchoolQuery(Subjects: "Mathematics", ClassLoad: "heavy"));

        result.Items.Should().ContainSingle().Which.Name.Should().Be("Perfect Match");
    }

    [Fact]
    public async Task ListTeachers_OldCallerOnlyPassingSearch_BehavesLikeBefore()
    {
        var schoolId = Guid.NewGuid();
        var currentUser = CreateAdmin(schoolId);
        var options = CreateOptions();

        await SeedAsync(options, schoolId, staff:
        [
            Teacher(schoolId, "Alice"),
            Teacher(schoolId, "Bob")
        ]);

        var result = await ExecuteListAsync(options, currentUser,
            new GetTeachersBySchoolQuery(Search: "ali"));

        result.Items.Should().ContainSingle().Which.Name.Should().Be("Alice");
    }

    [Fact]
    public async Task ListTeachers_NonAdmin_Forbidden()
    {
        var schoolId = Guid.NewGuid();
        var currentUser = CreateUser(schoolId, "Teacher");
        var options = CreateOptions();
        await SeedAsync(options, schoolId);

        var act = () => ExecuteListAsync(options, currentUser, new GetTeachersBySchoolQuery());

        await act.Should().ThrowAsync<ForbiddenException>();
    }

    [Fact]
    public async Task FilterMetadata_ReturnsDistinctSubjectsAssignedInThisTenantOnly()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var currentUser = CreateAdmin(tenantA);
        var classA = Guid.NewGuid();
        var classB = Guid.NewGuid();
        var tA = Teacher(tenantA, "Tenant A Teacher");
        var tB = Teacher(tenantB, "Tenant B Teacher");

        var options = CreateOptions();
        await SeedAsync(options, tenantA,
            staff: [tA],
            classes: [new ClassEntity { Id = classA, SchoolId = tenantA, Name = "7", Section = "A", AcademicYear = "2026-27" }],
            assignments:
            [
                Assignment(tenantA, tA.Id, classA, "Mathematics"),
                Assignment(tenantA, tA.Id, classA, "Science"),
                Assignment(tenantA, tA.Id, classA, "Mathematics")
            ]);
        // Seed a second tenant separately so its subjects must NOT leak into tenant A's metadata.
        await SeedAsync(options, tenantB,
            staff: [tB],
            classes: [new ClassEntity { Id = classB, SchoolId = tenantB, Name = "7", Section = "A", AcademicYear = "2026-27" }],
            assignments: [Assignment(tenantB, tB.Id, classB, "Physics")]);

        await using var context = new AppDbContext(options, currentUser);
        var handler = new GetTeacherFilterMetadataQueryHandler(context, currentUser);

        var result = await handler.Handle(new GetTeacherFilterMetadataQuery(), CancellationToken.None);

        result.Subjects.Should().ContainInOrder("Mathematics", "Science");
        result.Subjects.Should().NotContain("Physics");
    }

    [Fact]
    public async Task FilterMetadata_NonAdmin_Forbidden()
    {
        var schoolId = Guid.NewGuid();
        var currentUser = CreateUser(schoolId, "Teacher");
        var options = CreateOptions();
        await SeedAsync(options, schoolId);

        await using var context = new AppDbContext(options, currentUser);
        var handler = new GetTeacherFilterMetadataQueryHandler(context, currentUser);

        var act = () => handler.Handle(new GetTeacherFilterMetadataQuery(), CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenException>();
    }

    private static async Task<EduConnect.Api.Common.Models.PagedResult<TeacherListDto>> ExecuteListAsync(
        DbContextOptions<AppDbContext> options,
        CurrentUserService currentUser,
        GetTeachersBySchoolQuery query)
    {
        await using var context = new AppDbContext(options, currentUser);
        var handler = new GetTeachersBySchoolQueryHandler(context, currentUser);
        return await handler.Handle(query, CancellationToken.None);
    }

    private static DbContextOptions<AppDbContext> CreateOptions() =>
        new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"TeacherFilter_{Guid.NewGuid()}")
            .Options;

    private static CurrentUserService CreateAdmin(Guid schoolId) => CreateUser(schoolId, "Admin");

    private static CurrentUserService CreateUser(Guid schoolId, string role) => new()
    {
        UserId = Guid.NewGuid(),
        SchoolId = schoolId,
        Role = role,
        Name = $"{role} User"
    };

    private static UserEntity Teacher(Guid schoolId, string name) => User(schoolId, name, "Teacher");

    private static UserEntity Admin(Guid schoolId, string name) => User(schoolId, name, "Admin");

    private static UserEntity User(Guid schoolId, string name, string role) => new()
    {
        Id = Guid.NewGuid(),
        SchoolId = schoolId,
        Phone = $"0{Random.Shared.NextInt64(9_000_000_000L, 9_999_999_999L)}",
        Email = $"{name.Replace(' ', '.').ToLowerInvariant()}@school.com",
        Name = name,
        Role = role,
        PasswordHash = "hashed",
        IsActive = true,
        CreatedAt = DateTimeOffset.UtcNow,
        UpdatedAt = DateTimeOffset.UtcNow
    };

    private static TeacherClassAssignmentEntity Assignment(Guid schoolId, Guid teacherId, Guid classId, string subject) => new()
    {
        Id = Guid.NewGuid(),
        SchoolId = schoolId,
        TeacherId = teacherId,
        ClassId = classId,
        Subject = subject,
        IsClassTeacher = false,
        CreatedAt = DateTimeOffset.UtcNow
    };

    private static async Task SeedAsync(
        DbContextOptions<AppDbContext> options,
        Guid schoolId,
        IEnumerable<UserEntity>? staff = null,
        IEnumerable<ClassEntity>? classes = null,
        IEnumerable<TeacherClassAssignmentEntity>? assignments = null)
    {
        await using var context = new AppDbContext(options);

        if (!await context.Schools.AnyAsync(s => s.Id == schoolId))
        {
            context.Schools.Add(new SchoolEntity
            {
                Id = schoolId,
                Name = $"School {schoolId.ToString()[..6]}",
                Code = $"SCH-{schoolId.ToString()[..6]}",
                Address = "Test Address",
                ContactPhone = "09999999999",
                ContactEmail = "school@test.com",
                IsActive = true,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            });
        }

        if (staff != null) context.Users.AddRange(staff);
        if (classes != null) context.Classes.AddRange(classes);
        if (assignments != null) context.TeacherClassAssignments.AddRange(assignments);

        await context.SaveChangesAsync();
    }
}
