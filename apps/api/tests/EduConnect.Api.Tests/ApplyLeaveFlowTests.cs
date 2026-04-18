using EduConnect.Api.Common.Auth;
using EduConnect.Api.Common.Exceptions;
using EduConnect.Api.Features.Attendance.ApplyLeave;
using EduConnect.Api.Infrastructure.Database;
using EduConnect.Api.Infrastructure.Database.Entities;
using EduConnect.Api.Infrastructure.Services;
using FluentAssertions;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace EduConnect.Api.Tests;

public class ApplyLeaveFlowTests
{
    // ─── Single child (backward compat) ─────────────────────────────────────

    [Fact]
    public async Task ApplyLeave_LegacySingleStudentId_CreatesOneApplication()
    {
        var ctx = await BuildContextAsync(childCount: 1);

        var handler = ctx.CreateHandler();
        var response = await handler.Handle(
            new ApplyLeaveCommand(
                StudentIds: [],
                StartDate: Today,
                EndDate: Today,
                Reason: "Fever",
                StudentId: ctx.StudentIds[0]),
            CancellationToken.None);

        response.CreatedCount.Should().Be(1);
        response.LeaveApplicationIds.Should().HaveCount(1);
        response.LeaveApplicationId.Should().Be(response.LeaveApplicationIds[0]);
        response.Status.Should().Be("Pending");

        await using var verify = new AppDbContext(ctx.Options);
        var apps = await verify.LeaveApplications.ToListAsync();
        apps.Should().HaveCount(1);
        apps[0].StudentId.Should().Be(ctx.StudentIds[0]);
        apps[0].Status.Should().Be("Pending");
        apps[0].Reason.Should().Be("Fever");
    }

    [Fact]
    public async Task ApplyLeave_SingleStudentInArray_CreatesOneApplication()
    {
        var ctx = await BuildContextAsync(childCount: 1);

        var handler = ctx.CreateHandler();
        var response = await handler.Handle(
            new ApplyLeaveCommand(
                StudentIds: [ctx.StudentIds[0]],
                StartDate: Today,
                EndDate: Today,
                Reason: "Routine checkup"),
            CancellationToken.None);

        response.CreatedCount.Should().Be(1);
        response.Message.Should().Be("Leave application submitted successfully.");
    }

    // ─── Multi-child happy path ─────────────────────────────────────────────

    [Fact]
    public async Task ApplyLeave_MultipleStudents_CreatesOneApplicationPerChild()
    {
        var ctx = await BuildContextAsync(childCount: 3);

        var handler = ctx.CreateHandler();
        var response = await handler.Handle(
            new ApplyLeaveCommand(
                StudentIds: ctx.StudentIds.ToArray(),
                StartDate: Today,
                EndDate: Today.AddDays(1),
                Reason: "Family wedding"),
            CancellationToken.None);

        response.CreatedCount.Should().Be(3);
        response.LeaveApplicationIds.Should().HaveCount(3);
        response.Message.Should().Be("Leave application submitted for 3 children.");

        await using var verify = new AppDbContext(ctx.Options);
        var apps = await verify.LeaveApplications.OrderBy(a => a.StudentId).ToListAsync();
        apps.Should().HaveCount(3);
        apps.Select(a => a.StudentId).Should().BeEquivalentTo(ctx.StudentIds);
        apps.Should().OnlyContain(a => a.Status == "Pending");
        apps.Should().OnlyContain(a => a.Reason == "Family wedding");
        apps.Should().OnlyContain(a => a.StartDate == Today);
        apps.Should().OnlyContain(a => a.EndDate == Today.AddDays(1));
    }

    [Fact]
    public async Task ApplyLeave_TwoOfThreeChildren_CreatesExactlyTwoRows()
    {
        // Parent with 3 kids applies for only 2 of them — must create exactly 2 rows.
        var ctx = await BuildContextAsync(childCount: 3);

        var handler = ctx.CreateHandler();
        var response = await handler.Handle(
            new ApplyLeaveCommand(
                StudentIds: [ctx.StudentIds[0], ctx.StudentIds[2]],
                StartDate: Today,
                EndDate: Today,
                Reason: "Dentist"),
            CancellationToken.None);

        response.CreatedCount.Should().Be(2);

        await using var verify = new AppDbContext(ctx.Options);
        var apps = await verify.LeaveApplications.ToListAsync();
        apps.Should().HaveCount(2);
        apps.Select(a => a.StudentId).Should().BeEquivalentTo(new[]
        {
            ctx.StudentIds[0],
            ctx.StudentIds[2],
        });
        // The middle child should NOT have a leave row.
        apps.Should().NotContain(a => a.StudentId == ctx.StudentIds[1]);
    }

    // ─── IDOR / authorization ──────────────────────────────────────────────

    [Fact]
    public async Task ApplyLeave_UnlinkedStudent_ThrowsForbiddenAndCreatesNothing()
    {
        var ctx = await BuildContextAsync(childCount: 1);
        var unrelatedStudentId = Guid.NewGuid();

        var handler = ctx.CreateHandler();

        var act = () => handler.Handle(
            new ApplyLeaveCommand(
                StudentIds: [unrelatedStudentId],
                StartDate: Today,
                EndDate: Today,
                Reason: "Trying"),
            CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenException>();

        await using var verify = new AppDbContext(ctx.Options);
        (await verify.LeaveApplications.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task ApplyLeave_PartialMismatch_FailsAtomicallyWithNoRowsCreated()
    {
        // Parent has 2 linked kids; tries to apply for [own, someone else's].
        // Must throw and create ZERO rows (no partial commit).
        var ctx = await BuildContextAsync(childCount: 2);
        var unrelatedStudentId = Guid.NewGuid();

        var handler = ctx.CreateHandler();

        var act = () => handler.Handle(
            new ApplyLeaveCommand(
                StudentIds: [ctx.StudentIds[0], unrelatedStudentId],
                StartDate: Today,
                EndDate: Today,
                Reason: "Mix"),
            CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenException>();

        await using var verify = new AppDbContext(ctx.Options);
        (await verify.LeaveApplications.CountAsync()).Should().Be(0);
    }

    // ─── Validation ────────────────────────────────────────────────────────

    [Fact]
    public async Task ApplyLeaveValidator_EmptyStudentIds_Fails()
    {
        var validator = new ApplyLeaveCommandValidator();
        var result = await validator.ValidateAsync(new ApplyLeaveCommand(
            StudentIds: [],
            StartDate: Today,
            EndDate: Today,
            Reason: "test"));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e =>
            e.ErrorMessage == "Select at least one child to apply leave for.");
    }

    [Fact]
    public async Task ApplyLeaveValidator_DuplicateStudentIds_Fails()
    {
        var duplicate = Guid.NewGuid();
        var validator = new ApplyLeaveCommandValidator();
        var result = await validator.ValidateAsync(new ApplyLeaveCommand(
            StudentIds: [duplicate, duplicate],
            StartDate: Today,
            EndDate: Today,
            Reason: "test"));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e =>
            e.ErrorMessage == "Duplicate children are not allowed in the same leave request.");
    }

    [Fact]
    public async Task ApplyLeaveValidator_TooManyStudentIds_Fails()
    {
        var ids = Enumerable.Range(0, ApplyLeaveCommandValidator.MaxStudentsPerRequest + 1)
            .Select(_ => Guid.NewGuid())
            .ToArray();

        var validator = new ApplyLeaveCommandValidator();
        var result = await validator.ValidateAsync(new ApplyLeaveCommand(
            StudentIds: ids,
            StartDate: Today,
            EndDate: Today,
            Reason: "test"));

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task ApplyLeaveValidator_LegacySingleStudentIdOnly_Passes()
    {
        var validator = new ApplyLeaveCommandValidator();
        var result = await validator.ValidateAsync(new ApplyLeaveCommand(
            StudentIds: [],
            StartDate: Today,
            EndDate: Today,
            Reason: "test",
            StudentId: Guid.NewGuid()));

        result.IsValid.Should().BeTrue();
    }

    // ─── Notification fan-out ──────────────────────────────────────────────

    [Fact]
    public async Task ApplyLeave_MultipleChildren_SendsOneConsolidatedAdminNotification()
    {
        // Admins should NOT get one notification per child — they should get a
        // single consolidated notification summarizing the batch.
        var ctx = await BuildContextAsync(childCount: 3);

        var handler = ctx.CreateHandler();
        await handler.Handle(
            new ApplyLeaveCommand(
                StudentIds: ctx.StudentIds.ToArray(),
                StartDate: Today,
                EndDate: Today,
                Reason: "Trip"),
            CancellationToken.None);

        // Two SendBatchAsync calls are expected:
        //   1. Admins — ONE call for the whole batch
        //   2. Class teachers — one per class (children share the same class in this seed)
        // Assert admin call once:
        ctx.NotificationService.Verify(
            n => n.SendBatchAsync(
                ctx.SchoolId,
                It.Is<IReadOnlyList<Guid>>(list => list.Contains(ctx.AdminId)),
                "leave_applied",
                It.Is<string>(title => title.Contains("3 children")),
                It.IsAny<string>(),
                It.IsAny<Guid?>(),
                "leave_application",
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // ─── Helpers / fixtures ─────────────────────────────────────────────────

    private static DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow);

    private sealed class TestContext
    {
        public required DbContextOptions<AppDbContext> Options { get; init; }
        public required Guid SchoolId { get; init; }
        public required Guid ParentId { get; init; }
        public required Guid AdminId { get; init; }
        public required Guid ClassId { get; init; }
        public required IReadOnlyList<Guid> StudentIds { get; init; }
        public required CurrentUserService CurrentUser { get; init; }
        public required Mock<INotificationService> NotificationService { get; init; }

        public ApplyLeaveCommandHandler CreateHandler()
        {
            var ctx = new AppDbContext(Options, CurrentUser);
            return new ApplyLeaveCommandHandler(
                ctx,
                CurrentUser,
                NotificationService.Object,
                Mock.Of<ILogger<ApplyLeaveCommandHandler>>());
        }
    }

    private static async Task<TestContext> BuildContextAsync(int childCount)
    {
        var schoolId = Guid.NewGuid();
        var parentId = Guid.NewGuid();
        var adminId = Guid.NewGuid();
        var classId = Guid.NewGuid();
        var studentIds = Enumerable.Range(0, childCount).Select(_ => Guid.NewGuid()).ToList();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"ApplyLeave_{Guid.NewGuid()}")
            .Options;

        await using (var seed = new AppDbContext(options))
        {
            seed.Schools.Add(new SchoolEntity
            {
                Id = schoolId,
                Name = "Test School",
                Code = $"SCH-{schoolId.ToString()[..6]}",
                Address = "Addr",
                ContactPhone = "09999999999",
                ContactEmail = "school@test.com",
                IsActive = true,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow,
            });

            seed.Classes.Add(new ClassEntity
            {
                Id = classId,
                SchoolId = schoolId,
                Name = "5",
                Section = "A",
                AcademicYear = "2026-27",
            });

            seed.Users.AddRange(
                new UserEntity
                {
                    Id = parentId,
                    SchoolId = schoolId,
                    Phone = "9000000001",
                    Email = "parent@test.com",
                    Name = "Parent",
                    Role = "Parent",
                    PinHash = "hashed-pin",
                    IsActive = true,
                    CreatedAt = DateTimeOffset.UtcNow,
                    UpdatedAt = DateTimeOffset.UtcNow,
                },
                new UserEntity
                {
                    Id = adminId,
                    SchoolId = schoolId,
                    Phone = "9000000099",
                    Email = "admin@test.com",
                    Name = "Admin",
                    Role = "Admin",
                    PasswordHash = "hashed",
                    IsActive = true,
                    CreatedAt = DateTimeOffset.UtcNow,
                    UpdatedAt = DateTimeOffset.UtcNow,
                });

            for (var i = 0; i < studentIds.Count; i++)
            {
                seed.Students.Add(new StudentEntity
                {
                    Id = studentIds[i],
                    SchoolId = schoolId,
                    ClassId = classId,
                    RollNumber = $"R{i + 1:000}",
                    Name = $"Child-{i + 1}",
                    IsActive = true,
                    CreatedAt = DateTimeOffset.UtcNow,
                    UpdatedAt = DateTimeOffset.UtcNow,
                });

                seed.ParentStudentLinks.Add(new ParentStudentLinkEntity
                {
                    Id = Guid.NewGuid(),
                    SchoolId = schoolId,
                    ParentId = parentId,
                    StudentId = studentIds[i],
                    Relationship = "parent",
                    CreatedAt = DateTimeOffset.UtcNow,
                });
            }

            await seed.SaveChangesAsync();
        }

        var currentUser = new CurrentUserService
        {
            UserId = parentId,
            SchoolId = schoolId,
            Role = "Parent",
            Name = "Parent",
        };

        var notif = new Mock<INotificationService>();
        notif
            .Setup(n => n.SendBatchAsync(
                It.IsAny<Guid>(),
                It.IsAny<IReadOnlyList<Guid>>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<Guid?>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        return new TestContext
        {
            Options = options,
            SchoolId = schoolId,
            ParentId = parentId,
            AdminId = adminId,
            ClassId = classId,
            StudentIds = studentIds,
            CurrentUser = currentUser,
            NotificationService = notif,
        };
    }
}
