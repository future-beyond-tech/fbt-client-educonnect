using EduConnect.Api.Common.Auth;
using EduConnect.Api.Common.Exceptions;
using EduConnect.Api.Features.Exams.CreateExam;
using EduConnect.Api.Features.Exams.FinalizeExamResults;
using EduConnect.Api.Features.Exams.PublishExamSchedule;
using EduConnect.Api.Features.Exams.UploadExamResultsCsv;
using EduConnect.Api.Features.Exams.UpsertExamResults;
using EduConnect.Api.Infrastructure.Database;
using EduConnect.Api.Infrastructure.Database.Entities;
using EduConnect.Api.Infrastructure.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace EduConnect.Api.Tests;

public class ExamFlowTests
{
    [Fact]
    public async Task CreateExam_ByNonClassTeacher_IsForbidden()
    {
        var schoolId = Guid.NewGuid();
        var classId = Guid.NewGuid();
        var teacherId = Guid.NewGuid();

        var options = CreateOptions();
        await SeedAsync(options, schoolId,
            classes: [CreateClass(classId, schoolId)],
            users: [CreateUser(teacherId, schoolId, "Teacher")],
            // Assigned but NOT class teacher
            assignments: [CreateAssignment(schoolId, teacherId, classId, isClassTeacher: false)]);

        var currentUser = CreateCurrentUser(schoolId, "Teacher", teacherId);
        await using var context = new AppDbContext(options, currentUser);
        var handler = new CreateExamCommandHandler(
            context, currentUser, Mock.Of<ILogger<CreateExamCommandHandler>>());

        var act = () => handler.Handle(
            new CreateExamCommand(
                classId,
                "Term 1 Exam",
                "2026-27",
                [new CreateExamSubjectInput(
                    "Math",
                    new DateOnly(2026, 6, 1),
                    new TimeOnly(9, 0),
                    new TimeOnly(11, 0),
                    100m,
                    "Room 1")]),
            CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenException>()
            .WithMessage("*class teacher*");
    }

    [Fact]
    public async Task CreateExam_ByClassTeacher_PersistsDraft()
    {
        var schoolId = Guid.NewGuid();
        var classId = Guid.NewGuid();
        var teacherId = Guid.NewGuid();

        var options = CreateOptions();
        await SeedAsync(options, schoolId,
            classes: [CreateClass(classId, schoolId)],
            users: [CreateUser(teacherId, schoolId, "Teacher")],
            assignments: [CreateAssignment(schoolId, teacherId, classId, isClassTeacher: true)]);

        var currentUser = CreateCurrentUser(schoolId, "Teacher", teacherId);
        await using var context = new AppDbContext(options, currentUser);
        var handler = new CreateExamCommandHandler(
            context, currentUser, Mock.Of<ILogger<CreateExamCommandHandler>>());

        var response = await handler.Handle(
            new CreateExamCommand(
                classId,
                "Term 1 Exam",
                "2026-27",
                [
                    new CreateExamSubjectInput(
                        "Math",
                        new DateOnly(2026, 6, 1),
                        new TimeOnly(9, 0), new TimeOnly(11, 0),
                        100m, "Room 1"),
                    new CreateExamSubjectInput(
                        "English",
                        new DateOnly(2026, 6, 2),
                        new TimeOnly(9, 0), new TimeOnly(11, 0),
                        80m, null),
                ]),
            CancellationToken.None);

        var exam = await context.Exams.Include(e => e.Subjects)
            .SingleAsync(e => e.Id == response.ExamId);

        exam.IsSchedulePublished.Should().BeFalse();
        exam.IsResultsFinalized.Should().BeFalse();
        exam.Subjects.Should().HaveCount(2);
    }

    [Fact]
    public async Task PublishExamSchedule_NotifiesAllParentsOfActiveStudentsInClass()
    {
        var schoolId = Guid.NewGuid();
        var classId = Guid.NewGuid();
        var teacherId = Guid.NewGuid();
        var parentAId = Guid.NewGuid();
        var parentBId = Guid.NewGuid();
        var studentAId = Guid.NewGuid();
        var studentBId = Guid.NewGuid();
        var examId = Guid.NewGuid();

        var options = CreateOptions();
        await SeedAsync(options, schoolId,
            classes: [CreateClass(classId, schoolId)],
            users:
            [
                CreateUser(teacherId, schoolId, "Teacher"),
                CreateUser(parentAId, schoolId, "Parent"),
                CreateUser(parentBId, schoolId, "Parent")
            ],
            students:
            [
                CreateStudent(studentAId, schoolId, classId, "R1", "Student A"),
                CreateStudent(studentBId, schoolId, classId, "R2", "Student B"),
            ],
            assignments: [CreateAssignment(schoolId, teacherId, classId, isClassTeacher: true)],
            parentLinks:
            [
                CreateParentLink(schoolId, parentAId, studentAId),
                CreateParentLink(schoolId, parentBId, studentBId),
            ],
            exams:
            [
                new ExamEntity
                {
                    Id = examId,
                    SchoolId = schoolId,
                    ClassId = classId,
                    Name = "Term 1",
                    AcademicYear = "2026-27",
                    CreatedById = teacherId,
                    IsSchedulePublished = false,
                    IsResultsFinalized = false,
                    IsDeleted = false,
                    CreatedAt = DateTimeOffset.UtcNow,
                    UpdatedAt = DateTimeOffset.UtcNow,
                }
            ],
            examSubjects:
            [
                new ExamSubjectEntity
                {
                    Id = Guid.NewGuid(),
                    SchoolId = schoolId,
                    ExamId = examId,
                    Subject = "Math",
                    ExamDate = new DateOnly(2026, 6, 1),
                    StartTime = new TimeOnly(9, 0),
                    EndTime = new TimeOnly(11, 0),
                    MaxMarks = 100m,
                    CreatedAt = DateTimeOffset.UtcNow,
                    UpdatedAt = DateTimeOffset.UtcNow,
                }
            ]);

        var currentUser = CreateCurrentUser(schoolId, "Teacher", teacherId);
        await using var context = new AppDbContext(options, currentUser);

        IReadOnlyList<Guid>? notified = null;
        string? notifiedType = null;
        var notificationService = new Mock<INotificationService>();
        notificationService
            .Setup(s => s.SendBatchAsync(
                schoolId,
                It.IsAny<IReadOnlyList<Guid>>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string?>(),
                examId,
                "exam_schedule",
                It.IsAny<CancellationToken>()))
            .Callback<Guid, IReadOnlyList<Guid>, string, string, string?, Guid?, string?, CancellationToken>(
                (_, ids, type, _, _, _, _, _) =>
                {
                    notified = ids;
                    notifiedType = type;
                })
            .Returns(Task.CompletedTask);

        var handler = new PublishExamScheduleCommandHandler(
            context,
            currentUser,
            notificationService.Object,
            Mock.Of<IEmailService>(),
            Mock.Of<IConfiguration>(),
            Mock.Of<ILogger<PublishExamScheduleCommandHandler>>());

        var response = await handler.Handle(
            new PublishExamScheduleCommand(examId), CancellationToken.None);

        response.NotifiedParentCount.Should().Be(2);
        notified.Should().NotBeNull();
        notified!.Should().BeEquivalentTo(new[] { parentAId, parentBId });
        notifiedType.Should().Be("exam_schedule");

        var reloaded = await context.Exams.SingleAsync(e => e.Id == examId);
        reloaded.IsSchedulePublished.Should().BeTrue();
        reloaded.SchedulePublishedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task UpsertExamResults_SkipsRowsExceedingMaxMarks()
    {
        var schoolId = Guid.NewGuid();
        var classId = Guid.NewGuid();
        var teacherId = Guid.NewGuid();
        var studentId = Guid.NewGuid();
        var examId = Guid.NewGuid();
        var subjectId = Guid.NewGuid();

        var options = CreateOptions();
        await SeedAsync(options, schoolId,
            classes: [CreateClass(classId, schoolId)],
            users: [CreateUser(teacherId, schoolId, "Teacher")],
            students: [CreateStudent(studentId, schoolId, classId, "R1", "Kid")],
            assignments: [CreateAssignment(schoolId, teacherId, classId, isClassTeacher: true)],
            exams:
            [
                new ExamEntity
                {
                    Id = examId, SchoolId = schoolId, ClassId = classId,
                    Name = "Exam", AcademicYear = "2026-27",
                    CreatedById = teacherId,
                    IsSchedulePublished = true,
                    SchedulePublishedAt = DateTimeOffset.UtcNow.AddDays(-1),
                    CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow,
                }
            ],
            examSubjects:
            [
                new ExamSubjectEntity
                {
                    Id = subjectId, SchoolId = schoolId, ExamId = examId,
                    Subject = "Math",
                    ExamDate = new DateOnly(2026, 6, 1),
                    StartTime = new TimeOnly(9, 0), EndTime = new TimeOnly(11, 0),
                    MaxMarks = 50m,
                    CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow,
                }
            ]);

        var currentUser = CreateCurrentUser(schoolId, "Teacher", teacherId);
        await using var context = new AppDbContext(options, currentUser);
        var handler = new UpsertExamResultsCommandHandler(
            context, currentUser, Mock.Of<ILogger<UpsertExamResultsCommandHandler>>());

        var response = await handler.Handle(
            new UpsertExamResultsCommand(examId, new[]
            {
                new ExamResultRowInput(studentId, subjectId, 75m, null, null, false),
            }),
            CancellationToken.None);

        response.InsertedCount.Should().Be(0);
        response.SkippedCount.Should().Be(1);
        response.Warnings.Should().ContainSingle(w => w.Contains("exceed max"));

        var stored = await context.ExamResults.AnyAsync();
        stored.Should().BeFalse();
    }

    [Fact]
    public async Task FinalizeExamResults_RejectsIncompleteGrid()
    {
        var schoolId = Guid.NewGuid();
        var classId = Guid.NewGuid();
        var teacherId = Guid.NewGuid();
        var studentAId = Guid.NewGuid();
        var studentBId = Guid.NewGuid();
        var examId = Guid.NewGuid();
        var subjectId = Guid.NewGuid();

        var options = CreateOptions();
        await SeedAsync(options, schoolId,
            classes: [CreateClass(classId, schoolId)],
            users: [CreateUser(teacherId, schoolId, "Teacher")],
            students:
            [
                CreateStudent(studentAId, schoolId, classId, "R1", "A"),
                CreateStudent(studentBId, schoolId, classId, "R2", "B"),
            ],
            assignments: [CreateAssignment(schoolId, teacherId, classId, isClassTeacher: true)],
            exams:
            [
                new ExamEntity
                {
                    Id = examId, SchoolId = schoolId, ClassId = classId,
                    Name = "T1", AcademicYear = "2026-27",
                    CreatedById = teacherId,
                    IsSchedulePublished = true,
                    SchedulePublishedAt = DateTimeOffset.UtcNow,
                    CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow,
                }
            ],
            examSubjects:
            [
                new ExamSubjectEntity
                {
                    Id = subjectId, SchoolId = schoolId, ExamId = examId,
                    Subject = "Math",
                    ExamDate = new DateOnly(2026, 6, 1),
                    StartTime = new TimeOnly(9, 0), EndTime = new TimeOnly(11, 0),
                    MaxMarks = 100m,
                    CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow,
                }
            ],
            examResults:
            [
                // Only student A has a mark — B is missing
                new ExamResultEntity
                {
                    Id = Guid.NewGuid(),
                    SchoolId = schoolId,
                    ExamId = examId,
                    ExamSubjectId = subjectId,
                    StudentId = studentAId,
                    MarksObtained = 80m,
                    IsAbsent = false,
                    RecordedById = teacherId,
                    CreatedAt = DateTimeOffset.UtcNow,
                    UpdatedAt = DateTimeOffset.UtcNow,
                }
            ]);

        var currentUser = CreateCurrentUser(schoolId, "Teacher", teacherId);
        await using var context = new AppDbContext(options, currentUser);

        var handler = new FinalizeExamResultsCommandHandler(
            context, currentUser,
            Mock.Of<INotificationService>(),
            Mock.Of<IEmailService>(),
            Mock.Of<IConfiguration>(),
            Mock.Of<ILogger<FinalizeExamResultsCommandHandler>>());

        var act = () => handler.Handle(
            new FinalizeExamResultsCommand(examId), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*incomplete*");

        var exam = await context.Exams.SingleAsync(e => e.Id == examId);
        exam.IsResultsFinalized.Should().BeFalse();
    }

    [Fact]
    public void ExamResultsCsvParser_ParsesValidFile()
    {
        var csv = "roll_number,subject,marks_obtained,grade,is_absent,remarks\n" +
                  "R1,Math,85,A,,Great work\n" +
                  "R2,Math,,,true,\n" +
                  "R3,English,72,B-,false,";

        using var ms = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(csv));
        var result = ExamResultsCsvParser.Parse(ms);

        result.Errors.Should().BeEmpty();
        result.Rows.Should().HaveCount(3);

        result.Rows[0].RollNumber.Should().Be("R1");
        result.Rows[0].MarksObtained.Should().Be(85m);
        result.Rows[0].IsAbsent.Should().BeFalse();

        result.Rows[1].RollNumber.Should().Be("R2");
        result.Rows[1].IsAbsent.Should().BeTrue();
        result.Rows[1].MarksObtained.Should().BeNull();

        result.Rows[2].Grade.Should().Be("B-");
    }

    [Fact]
    public void ExamResultsCsvParser_FlagsMarksForAbsentRow()
    {
        var csv = "roll_number,subject,marks_obtained,is_absent\n" +
                  "R1,Math,40,true";

        using var ms = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(csv));
        var result = ExamResultsCsvParser.Parse(ms);

        result.Errors.Should().ContainSingle(e => e.Contains("absent"));
        result.Rows.Should().BeEmpty();
    }

    // ---- seed helpers ---------------------------------------------------

    private static DbContextOptions<AppDbContext> CreateOptions()
    {
        return new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"Exam_{Guid.NewGuid()}")
            .Options;
    }

    private static CurrentUserService CreateCurrentUser(Guid schoolId, string role, Guid? userId = null)
    {
        return new CurrentUserService
        {
            UserId = userId ?? Guid.NewGuid(),
            SchoolId = schoolId,
            Role = role,
            Name = $"{role} User"
        };
    }

    private static ClassEntity CreateClass(Guid id, Guid schoolId) => new()
    {
        Id = id,
        SchoolId = schoolId,
        Name = "5",
        Section = "A",
        AcademicYear = "2026-27",
        CreatedAt = DateTimeOffset.UtcNow,
        UpdatedAt = DateTimeOffset.UtcNow,
    };

    private static UserEntity CreateUser(Guid id, Guid schoolId, string role) => new()
    {
        Id = id,
        SchoolId = schoolId,
        Phone = $"089{id.ToString()[..8]}",
        Email = $"{id:N}@test.com",
        Name = $"{role} {id.ToString()[..4]}",
        Role = role,
        PasswordHash = role == "Teacher" || role == "Admin" ? "hashed-password" : null,
        PinHash = role == "Parent" ? "hashed-pin" : null,
        IsActive = true,
        CreatedAt = DateTimeOffset.UtcNow,
        UpdatedAt = DateTimeOffset.UtcNow,
    };

    private static StudentEntity CreateStudent(Guid id, Guid schoolId, Guid classId, string rollNumber, string name) => new()
    {
        Id = id,
        SchoolId = schoolId,
        ClassId = classId,
        RollNumber = rollNumber,
        Name = name,
        IsActive = true,
        CreatedAt = DateTimeOffset.UtcNow,
        UpdatedAt = DateTimeOffset.UtcNow,
    };

    private static TeacherClassAssignmentEntity CreateAssignment(
        Guid schoolId, Guid teacherId, Guid classId, bool isClassTeacher) => new()
    {
        Id = Guid.NewGuid(),
        SchoolId = schoolId,
        TeacherId = teacherId,
        ClassId = classId,
        Subject = "Math",
        IsClassTeacher = isClassTeacher,
        CreatedAt = DateTimeOffset.UtcNow,
    };

    private static ParentStudentLinkEntity CreateParentLink(Guid schoolId, Guid parentId, Guid studentId) => new()
    {
        Id = Guid.NewGuid(),
        SchoolId = schoolId,
        ParentId = parentId,
        StudentId = studentId,
        Relationship = "parent",
        CreatedAt = DateTimeOffset.UtcNow,
    };

    private static async Task SeedAsync(
        DbContextOptions<AppDbContext> options,
        Guid schoolId,
        IEnumerable<ClassEntity>? classes = null,
        IEnumerable<UserEntity>? users = null,
        IEnumerable<StudentEntity>? students = null,
        IEnumerable<TeacherClassAssignmentEntity>? assignments = null,
        IEnumerable<ParentStudentLinkEntity>? parentLinks = null,
        IEnumerable<ExamEntity>? exams = null,
        IEnumerable<ExamSubjectEntity>? examSubjects = null,
        IEnumerable<ExamResultEntity>? examResults = null)
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
            UpdatedAt = DateTimeOffset.UtcNow,
        });

        if (classes != null) context.Classes.AddRange(classes);
        if (users != null) context.Users.AddRange(users);
        if (students != null) context.Students.AddRange(students);
        if (assignments != null) context.TeacherClassAssignments.AddRange(assignments);
        if (parentLinks != null) context.ParentStudentLinks.AddRange(parentLinks);
        if (exams != null) context.Exams.AddRange(exams);
        if (examSubjects != null) context.ExamSubjects.AddRange(examSubjects);
        if (examResults != null) context.ExamResults.AddRange(examResults);

        await context.SaveChangesAsync();
    }
}
