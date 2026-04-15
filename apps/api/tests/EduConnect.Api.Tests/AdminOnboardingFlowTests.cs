using EduConnect.Api.Common.Auth;
using EduConnect.Api.Common.Exceptions;
using EduConnect.Api.Features.Classes.CreateClass;
using EduConnect.Api.Features.Classes.UpdateClass;
using EduConnect.Api.Features.Homework.ApproveHomework;
using EduConnect.Api.Features.Homework.RejectHomework;
using EduConnect.Api.Features.Parents.CreateParent;
using EduConnect.Api.Features.Teachers.AssignClassToTeacher;
using EduConnect.Api.Features.Teachers.CreateTeacher;
using EduConnect.Api.Features.Teachers.PromoteClassTeacher;
using EduConnect.Api.Features.Students.EnrollStudent;
using EduConnect.Api.Infrastructure.Database;
using EduConnect.Api.Infrastructure.Database.Entities;
using EduConnect.Api.Infrastructure.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace EduConnect.Api.Tests;

public class AdminOnboardingFlowTests
{
    [Fact]
    public async Task CreateTeacher_CreatesTeacherWithHashedPassword()
    {
        var schoolId = Guid.NewGuid();
        var currentUser = CreateCurrentUser(schoolId, "Admin");
        var options = CreateOptions();

        await SeedAsync(options, schoolId);

        await using var context = new AppDbContext(options, currentUser);
        var passwordHasher = new PasswordHasher();
        var handler = new CreateTeacherCommandHandler(
            context,
            currentUser,
            passwordHasher,
            Mock.Of<ILogger<CreateTeacherCommandHandler>>());

        var response = await handler.Handle(
            new CreateTeacherCommand("Asha Menon", "9876543210", "Asha@School.com", "EduConnect@2026"),
            CancellationToken.None);

        var teacher = await context.Users.FirstAsync(u => u.Id == response.TeacherId);
        teacher.Role.Should().Be("Teacher");
        teacher.Email.Should().Be("asha@school.com");
        teacher.PasswordHash.Should().NotBe("EduConnect@2026");
        passwordHasher.VerifyPassword("EduConnect@2026", teacher.PasswordHash!).Should().BeTrue();
    }

    [Fact]
    public async Task CreateParent_CreatesParentWithHashedPin()
    {
        var schoolId = Guid.NewGuid();
        var currentUser = CreateCurrentUser(schoolId, "Admin");
        var options = CreateOptions();

        await SeedAsync(options, schoolId);

        await using var context = new AppDbContext(options, currentUser);
        var pinService = new PinService();
        var handler = new CreateParentCommandHandler(
            context,
            currentUser,
            pinService,
            Mock.Of<ILogger<CreateParentCommandHandler>>());

        var response = await handler.Handle(
            new CreateParentCommand("Meera Das", "9123456789", "Meera@Home.com", "1234"),
            CancellationToken.None);

        var parent = await context.Users.FirstAsync(u => u.Id == response.ParentId);
        parent.Role.Should().Be("Parent");
        parent.Email.Should().Be("meera@home.com");
        parent.PinHash.Should().NotBe("1234");
        pinService.VerifyPin("1234", parent.PinHash!).Should().BeTrue();
    }

    [Fact]
    public async Task EnrollStudent_WithoutParentData_CreatesStudentOnly()
    {
        var schoolId = Guid.NewGuid();
        var classId = Guid.NewGuid();
        var currentUser = CreateCurrentUser(schoolId, "Admin");
        var options = CreateOptions();

        await SeedAsync(
            options,
            schoolId,
            classes:
            [
                new ClassEntity
                {
                    Id = classId,
                    SchoolId = schoolId,
                    Name = "5",
                    Section = "A",
                    AcademicYear = "2026-27"
                }
            ]);

        await using var context = new AppDbContext(options, currentUser);
        var handler = new EnrollStudentCommandHandler(
            context,
            currentUser,
            new PinService(),
            Mock.Of<ILogger<EnrollStudentCommandHandler>>());

        var response = await handler.Handle(
            new EnrollStudentCommand("Arjun Nair", "2026-5A-001", classId, new DateOnly(2015, 5, 11), null),
            CancellationToken.None);

        var student = await context.Students.FirstAsync(s => s.Id == response.StudentId);
        response.Message.Should().Be("Student enrolled successfully.");
        student.Name.Should().Be("Arjun Nair");
        student.RollNumber.Should().Be("2026-5A-001");
        student.ClassId.Should().Be(classId);
        student.DateOfBirth.Should().Be(new DateOnly(2015, 5, 11));
        (await context.Users.CountAsync(u => u.Role == "Parent")).Should().Be(0);
        (await context.ParentStudentLinks.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task EnrollStudent_WithParentData_CreatesStudentParentAndLink()
    {
        var schoolId = Guid.NewGuid();
        var classId = Guid.NewGuid();
        var currentUser = CreateCurrentUser(schoolId, "Admin");
        var options = CreateOptions();
        var pinService = new PinService();

        await SeedAsync(
            options,
            schoolId,
            classes:
            [
                new ClassEntity
                {
                    Id = classId,
                    SchoolId = schoolId,
                    Name = "6",
                    Section = "B",
                    AcademicYear = "2026-27"
                }
            ]);

        await using var context = new AppDbContext(options, currentUser);
        var handler = new EnrollStudentCommandHandler(
            context,
            currentUser,
            pinService,
            Mock.Of<ILogger<EnrollStudentCommandHandler>>());

        var response = await handler.Handle(
            new EnrollStudentCommand(
                "Diya Rao",
                "2026-6B-002",
                classId,
                new DateOnly(2014, 10, 3),
                new EnrollStudentParentRequest(
                    "Lakshmi Rao",
                    "9123456789",
                    "Lakshmi@Home.com",
                    "1234",
                    "guardian")),
            CancellationToken.None);

        var student = await context.Students.FirstAsync(s => s.Id == response.StudentId);
        var parent = await context.Users.FirstAsync(u => u.Role == "Parent");
        var link = await context.ParentStudentLinks.FirstAsync(l => l.StudentId == student.Id);

        student.Name.Should().Be("Diya Rao");
        parent.Name.Should().Be("Lakshmi Rao");
        parent.Email.Should().Be("lakshmi@home.com");
        parent.PinHash.Should().NotBe("1234");
        pinService.VerifyPin("1234", parent.PinHash!).Should().BeTrue();
        link.ParentId.Should().Be(parent.Id);
        link.StudentId.Should().Be(student.Id);
        link.Relationship.Should().Be("guardian");
    }

    [Fact]
    public async Task EnrollStudent_DuplicateRollNumber_DoesNotCreateParentAccount()
    {
        var schoolId = Guid.NewGuid();
        var classId = Guid.NewGuid();
        var currentUser = CreateCurrentUser(schoolId, "Admin");
        var options = CreateOptions();

        await SeedAsync(
            options,
            schoolId,
            classes:
            [
                new ClassEntity
                {
                    Id = classId,
                    SchoolId = schoolId,
                    Name = "7",
                    Section = "A",
                    AcademicYear = "2026-27"
                }
            ],
            students:
            [
                new StudentEntity
                {
                    Id = Guid.NewGuid(),
                    SchoolId = schoolId,
                    ClassId = classId,
                    Name = "Existing Student",
                    RollNumber = "2026-7A-001",
                    IsActive = true,
                    CreatedAt = DateTimeOffset.UtcNow,
                    UpdatedAt = DateTimeOffset.UtcNow
                }
            ]);

        await using var context = new AppDbContext(options, currentUser);
        var handler = new EnrollStudentCommandHandler(
            context,
            currentUser,
            new PinService(),
            Mock.Of<ILogger<EnrollStudentCommandHandler>>());

        var act = () => handler.Handle(
            new EnrollStudentCommand(
                "New Student",
                "2026-7A-001",
                classId,
                null,
                new EnrollStudentParentRequest(
                    "Parent One",
                    "9234567890",
                    "parent.one@test.com",
                    "1234",
                    "parent")),
            CancellationToken.None);

        var exception = await act.Should().ThrowAsync<ValidationException>();
        exception.Which.Errors.Should().ContainKey("RollNumber");

        await using var verificationContext = new AppDbContext(options, currentUser);
        (await verificationContext.Students.CountAsync()).Should().Be(1);
        (await verificationContext.Users.CountAsync(u => u.Role == "Parent")).Should().Be(0);
        (await verificationContext.ParentStudentLinks.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task EnrollStudent_DuplicateParentPhone_DoesNotCreateStudent()
    {
        var schoolId = Guid.NewGuid();
        var classId = Guid.NewGuid();
        var currentUser = CreateCurrentUser(schoolId, "Admin");
        var options = CreateOptions();

        await SeedAsync(
            options,
            schoolId,
            classes:
            [
                new ClassEntity
                {
                    Id = classId,
                    SchoolId = schoolId,
                    Name = "8",
                    Section = "A",
                    AcademicYear = "2026-27"
                }
            ],
            users:
            [
                CreateParentUser(Guid.NewGuid(), schoolId, "9345678901", "Existing Parent", "existing.parent@test.com")
            ]);

        await using var context = new AppDbContext(options, currentUser);
        var handler = new EnrollStudentCommandHandler(
            context,
            currentUser,
            new PinService(),
            Mock.Of<ILogger<EnrollStudentCommandHandler>>());

        var act = () => handler.Handle(
            new EnrollStudentCommand(
                "Student Phone Clash",
                "2026-8A-004",
                classId,
                null,
                new EnrollStudentParentRequest(
                    "Parent Two",
                    "9345678901",
                    "new.parent@test.com",
                    "4567",
                    "parent")),
            CancellationToken.None);

        var exception = await act.Should().ThrowAsync<ValidationException>();
        exception.Which.Errors.Should().ContainKey("Phone");

        await using var verificationContext = new AppDbContext(options, currentUser);
        (await verificationContext.Students.CountAsync()).Should().Be(0);
        (await verificationContext.Users.CountAsync(u => u.Role == "Parent")).Should().Be(1);
        (await verificationContext.ParentStudentLinks.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task EnrollStudent_DuplicateParentEmail_DoesNotCreateStudent()
    {
        var schoolId = Guid.NewGuid();
        var classId = Guid.NewGuid();
        var currentUser = CreateCurrentUser(schoolId, "Admin");
        var options = CreateOptions();

        await SeedAsync(
            options,
            schoolId,
            classes:
            [
                new ClassEntity
                {
                    Id = classId,
                    SchoolId = schoolId,
                    Name = "9",
                    Section = "C",
                    AcademicYear = "2026-27"
                }
            ],
            users:
            [
                CreateParentUser(Guid.NewGuid(), schoolId, "9456789012", "Existing Parent", "existing.parent@test.com")
            ]);

        await using var context = new AppDbContext(options, currentUser);
        var handler = new EnrollStudentCommandHandler(
            context,
            currentUser,
            new PinService(),
            Mock.Of<ILogger<EnrollStudentCommandHandler>>());

        var act = () => handler.Handle(
            new EnrollStudentCommand(
                "Student Email Clash",
                "2026-9C-008",
                classId,
                null,
                new EnrollStudentParentRequest(
                    "Parent Three",
                    "9567890123",
                    "Existing.Parent@Test.com",
                    "4567",
                    "guardian")),
            CancellationToken.None);

        var exception = await act.Should().ThrowAsync<ValidationException>();
        exception.Which.Errors.Should().ContainKey("Email");

        await using var verificationContext = new AppDbContext(options, currentUser);
        (await verificationContext.Students.CountAsync()).Should().Be(0);
        (await verificationContext.Users.CountAsync(u => u.Role == "Parent")).Should().Be(1);
        (await verificationContext.ParentStudentLinks.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task EnrollStudent_NonAdminUser_IsForbidden()
    {
        var schoolId = Guid.NewGuid();
        var classId = Guid.NewGuid();
        var teacherId = Guid.NewGuid();
        var currentUser = CreateCurrentUser(schoolId, "Teacher", teacherId);
        var options = CreateOptions();

        await SeedAsync(
            options,
            schoolId,
            classes:
            [
                new ClassEntity
                {
                    Id = classId,
                    SchoolId = schoolId,
                    Name = "10",
                    Section = "A",
                    AcademicYear = "2026-27"
                }
            ],
            users:
            [
                CreateTeacherUser(teacherId, schoolId, "9000000024", "Teacher User", "teacher@school.com")
            ]);

        await using var context = new AppDbContext(options, currentUser);
        var handler = new EnrollStudentCommandHandler(
            context,
            currentUser,
            new PinService(),
            Mock.Of<ILogger<EnrollStudentCommandHandler>>());

        var act = () => handler.Handle(
            new EnrollStudentCommand(
                "Forbidden Student",
                "2026-10A-001",
                classId,
                null,
                new EnrollStudentParentRequest(
                    "Parent Four",
                    "9678901234",
                    "parent.four@test.com",
                    "1234",
                    "parent")),
            CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenException>()
            .WithMessage("Only admins can enroll students.");

        await using var verificationContext = new AppDbContext(options, currentUser);
        (await verificationContext.Students.CountAsync()).Should().Be(0);
        (await verificationContext.Users.CountAsync(u => u.Role == "Parent")).Should().Be(0);
        (await verificationContext.ParentStudentLinks.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task CreateAndUpdateClass_PersistsManagedClassData()
    {
        var schoolId = Guid.NewGuid();
        var currentUser = CreateCurrentUser(schoolId, "Admin");
        var options = CreateOptions();

        await SeedAsync(options, schoolId);

        await using var context = new AppDbContext(options, currentUser);
        var createHandler = new CreateClassCommandHandler(
            context,
            currentUser,
            Mock.Of<ILogger<CreateClassCommandHandler>>());
        var updateHandler = new UpdateClassCommandHandler(
            context,
            currentUser,
            Mock.Of<ILogger<UpdateClassCommandHandler>>());

        var created = await createHandler.Handle(
            new CreateClassCommand("5", "A", "2026-27"),
            CancellationToken.None);
        await updateHandler.Handle(
            new UpdateClassCommand(created.ClassId, "5", "B", "2026-27"),
            CancellationToken.None);

        var classEntity = await context.Classes.FirstAsync(c => c.Id == created.ClassId);
        classEntity.Name.Should().Be("5");
        classEntity.Section.Should().Be("B");
        classEntity.AcademicYear.Should().Be("2026-27");
    }

    [Fact]
    public async Task AssignClassToTeacher_WithClassTeacherFlag_DemotesExistingClassTeacher()
    {
        var schoolId = Guid.NewGuid();
        var classId = Guid.NewGuid();
        var currentUser = CreateCurrentUser(schoolId, "Admin");
        var options = CreateOptions();
        var teacherOneId = Guid.NewGuid();
        var teacherTwoId = Guid.NewGuid();

        await SeedAsync(
            options,
            schoolId,
            classes:
            [
                new ClassEntity
                {
                    Id = classId,
                    SchoolId = schoolId,
                    Name = "6",
                    Section = "A",
                    AcademicYear = "2026-27"
                }
            ],
            users:
            [
                CreateTeacherUser(teacherOneId, schoolId, "9000000002", "Teacher One", "one@school.com"),
                CreateTeacherUser(teacherTwoId, schoolId, "9000000003", "Teacher Two", "two@school.com")
            ],
            subjects:
            [
                new SubjectEntity { Id = Guid.NewGuid(), SchoolId = schoolId, Name = "Mathematics" },
                new SubjectEntity { Id = Guid.NewGuid(), SchoolId = schoolId, Name = "Science" }
            ],
            assignments:
            [
                new TeacherClassAssignmentEntity
                {
                    Id = Guid.NewGuid(),
                    SchoolId = schoolId,
                    TeacherId = teacherOneId,
                    ClassId = classId,
                    Subject = "Mathematics",
                    IsClassTeacher = true,
                    CreatedAt = DateTimeOffset.UtcNow
                }
            ]);

        await using var context = new AppDbContext(options, currentUser);
        var handler = new AssignClassToTeacherCommandHandler(
            context,
            currentUser,
            Mock.Of<ILogger<AssignClassToTeacherCommandHandler>>());

        await handler.Handle(
            new AssignClassToTeacherCommand(teacherTwoId, classId, "Science", true),
            CancellationToken.None);

        var assignments = await context.TeacherClassAssignments
            .Where(tca => tca.ClassId == classId)
            .OrderBy(tca => tca.Subject)
            .ToListAsync();

        assignments.Should().HaveCount(2);
        assignments.Single(tca => tca.TeacherId == teacherOneId).IsClassTeacher.Should().BeFalse();
        assignments.Single(tca => tca.TeacherId == teacherTwoId).IsClassTeacher.Should().BeTrue();
    }

    [Fact]
    public async Task PromoteClassTeacher_DemotesCurrentClassTeacherAndPromotesRequestedAssignment()
    {
        var schoolId = Guid.NewGuid();
        var classId = Guid.NewGuid();
        var currentUser = CreateCurrentUser(schoolId, "Admin");
        var options = CreateOptions();
        var teacherOneId = Guid.NewGuid();
        var teacherTwoId = Guid.NewGuid();
        var assignmentOneId = Guid.NewGuid();
        var assignmentTwoId = Guid.NewGuid();

        await SeedAsync(
            options,
            schoolId,
            classes:
            [
                new ClassEntity
                {
                    Id = classId,
                    SchoolId = schoolId,
                    Name = "7",
                    Section = "A",
                    AcademicYear = "2026-27"
                }
            ],
            users:
            [
                CreateTeacherUser(teacherOneId, schoolId, "9000000010", "Teacher One", "one@school.com"),
                CreateTeacherUser(teacherTwoId, schoolId, "9000000011", "Teacher Two", "two@school.com")
            ],
            assignments:
            [
                new TeacherClassAssignmentEntity
                {
                    Id = assignmentOneId,
                    SchoolId = schoolId,
                    TeacherId = teacherOneId,
                    ClassId = classId,
                    Subject = "English",
                    IsClassTeacher = true,
                    CreatedAt = DateTimeOffset.UtcNow
                },
                new TeacherClassAssignmentEntity
                {
                    Id = assignmentTwoId,
                    SchoolId = schoolId,
                    TeacherId = teacherTwoId,
                    ClassId = classId,
                    Subject = "Science",
                    IsClassTeacher = false,
                    CreatedAt = DateTimeOffset.UtcNow
                }
            ]);

        await using var context = new AppDbContext(options, currentUser);
        var handler = new PromoteClassTeacherCommandHandler(
            context,
            currentUser,
            Mock.Of<ILogger<PromoteClassTeacherCommandHandler>>());

        await handler.Handle(
            new PromoteClassTeacherCommand(teacherTwoId, assignmentTwoId),
            CancellationToken.None);

        var assignments = await context.TeacherClassAssignments
            .Where(tca => tca.ClassId == classId)
            .ToListAsync();

        assignments.Single(tca => tca.Id == assignmentOneId).IsClassTeacher.Should().BeFalse();
        assignments.Single(tca => tca.Id == assignmentTwoId).IsClassTeacher.Should().BeTrue();
    }

    [Fact]
    public async Task ApproveHomework_ClassTeacherCanApprovePendingHomework()
    {
        var schoolId = Guid.NewGuid();
        var classId = Guid.NewGuid();
        var teacherId = Guid.NewGuid();
        var homeworkId = Guid.NewGuid();
        var currentUser = CreateCurrentUser(schoolId, "Teacher", teacherId);
        var options = CreateOptions();

        await SeedAsync(
            options,
            schoolId,
            classes:
            [
                new ClassEntity
                {
                    Id = classId,
                    SchoolId = schoolId,
                    Name = "8",
                    Section = "A",
                    AcademicYear = "2026-27"
                }
            ],
            users:
            [
                CreateTeacherUser(teacherId, schoolId, "9000000020", "Teacher", "teacher@school.com")
            ],
            assignments:
            [
                new TeacherClassAssignmentEntity
                {
                    Id = Guid.NewGuid(),
                    SchoolId = schoolId,
                    TeacherId = teacherId,
                    ClassId = classId,
                    Subject = "Science",
                    IsClassTeacher = true,
                    CreatedAt = DateTimeOffset.UtcNow
                }
            ],
            homeworks:
            [
                new HomeworkEntity
                {
                    Id = homeworkId,
                    SchoolId = schoolId,
                    ClassId = classId,
                    Subject = "Science",
                    Title = "HW 1",
                    Description = "Practice",
                    AssignedById = teacherId,
                    DueDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(2)),
                    Status = "PendingApproval",
                    SubmittedAt = DateTimeOffset.UtcNow
                }
            ]);

        await using var context = new AppDbContext(options, currentUser);
        var notificationService = new Mock<INotificationService>(MockBehavior.Strict);
        var handler = new ApproveHomeworkCommandHandler(
            context,
            currentUser,
            notificationService.Object,
            Mock.Of<ILogger<ApproveHomeworkCommandHandler>>());

        var response = await handler.Handle(
            new ApproveHomeworkCommand(homeworkId),
            CancellationToken.None);

        var homework = await context.Homeworks.FirstAsync(h => h.Id == homeworkId);
        response.Message.Should().Be("Homework approved and published.");
        homework.Status.Should().Be("Published");
        homework.ApprovedById.Should().Be(teacherId);
        homework.IsEditable.Should().BeFalse();
    }

    [Fact]
    public async Task ApproveHomework_NonClassTeacherCannotApprovePendingHomework()
    {
        var schoolId = Guid.NewGuid();
        var classId = Guid.NewGuid();
        var teacherId = Guid.NewGuid();
        var homeworkId = Guid.NewGuid();
        var currentUser = CreateCurrentUser(schoolId, "Teacher", teacherId);
        var options = CreateOptions();

        await SeedAsync(
            options,
            schoolId,
            classes:
            [
                new ClassEntity
                {
                    Id = classId,
                    SchoolId = schoolId,
                    Name = "8",
                    Section = "B",
                    AcademicYear = "2026-27"
                }
            ],
            users:
            [
                CreateTeacherUser(teacherId, schoolId, "9000000021", "Teacher", "teacher@school.com")
            ],
            assignments:
            [
                new TeacherClassAssignmentEntity
                {
                    Id = Guid.NewGuid(),
                    SchoolId = schoolId,
                    TeacherId = teacherId,
                    ClassId = classId,
                    Subject = "Science",
                    IsClassTeacher = false,
                    CreatedAt = DateTimeOffset.UtcNow
                }
            ],
            homeworks:
            [
                new HomeworkEntity
                {
                    Id = homeworkId,
                    SchoolId = schoolId,
                    ClassId = classId,
                    Subject = "Science",
                    Title = "HW 2",
                    Description = "Practice",
                    AssignedById = teacherId,
                    DueDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(2)),
                    Status = "PendingApproval",
                    SubmittedAt = DateTimeOffset.UtcNow
                }
            ]);

        await using var context = new AppDbContext(options, currentUser);
        var handler = new ApproveHomeworkCommandHandler(
            context,
            currentUser,
            Mock.Of<INotificationService>(),
            Mock.Of<ILogger<ApproveHomeworkCommandHandler>>());

        var act = () => handler.Handle(new ApproveHomeworkCommand(homeworkId), CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenException>()
            .WithMessage("Only the class teacher can approve homework for this class.");
    }

    [Fact]
    public async Task RejectHomework_ClassTeacherCanRejectPendingHomework()
    {
        var schoolId = Guid.NewGuid();
        var classId = Guid.NewGuid();
        var teacherId = Guid.NewGuid();
        var homeworkId = Guid.NewGuid();
        var currentUser = CreateCurrentUser(schoolId, "Teacher", teacherId);
        var options = CreateOptions();

        await SeedAsync(
            options,
            schoolId,
            classes:
            [
                new ClassEntity
                {
                    Id = classId,
                    SchoolId = schoolId,
                    Name = "9",
                    Section = "A",
                    AcademicYear = "2026-27"
                }
            ],
            users:
            [
                CreateTeacherUser(teacherId, schoolId, "9000000022", "Teacher", "teacher@school.com")
            ],
            assignments:
            [
                new TeacherClassAssignmentEntity
                {
                    Id = Guid.NewGuid(),
                    SchoolId = schoolId,
                    TeacherId = teacherId,
                    ClassId = classId,
                    Subject = "Science",
                    IsClassTeacher = true,
                    CreatedAt = DateTimeOffset.UtcNow
                }
            ],
            homeworks:
            [
                new HomeworkEntity
                {
                    Id = homeworkId,
                    SchoolId = schoolId,
                    ClassId = classId,
                    Subject = "Science",
                    Title = "HW 3",
                    Description = "Practice",
                    AssignedById = teacherId,
                    DueDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(2)),
                    Status = "PendingApproval",
                    SubmittedAt = DateTimeOffset.UtcNow
                }
            ]);

        await using var context = new AppDbContext(options, currentUser);
        var handler = new RejectHomeworkCommandHandler(
            context,
            currentUser,
            Mock.Of<ILogger<RejectHomeworkCommandHandler>>());

        var response = await handler.Handle(
            new RejectHomeworkCommand(homeworkId, "Needs corrections"),
            CancellationToken.None);

        var homework = await context.Homeworks.FirstAsync(h => h.Id == homeworkId);
        response.Message.Should().Be("Homework rejected.");
        homework.Status.Should().Be("Rejected");
        homework.RejectedById.Should().Be(teacherId);
        homework.RejectedReason.Should().Be("Needs corrections");
    }

    [Fact]
    public async Task RejectHomework_NonClassTeacherCannotRejectPendingHomework()
    {
        var schoolId = Guid.NewGuid();
        var classId = Guid.NewGuid();
        var teacherId = Guid.NewGuid();
        var homeworkId = Guid.NewGuid();
        var currentUser = CreateCurrentUser(schoolId, "Teacher", teacherId);
        var options = CreateOptions();

        await SeedAsync(
            options,
            schoolId,
            classes:
            [
                new ClassEntity
                {
                    Id = classId,
                    SchoolId = schoolId,
                    Name = "9",
                    Section = "B",
                    AcademicYear = "2026-27"
                }
            ],
            users:
            [
                CreateTeacherUser(teacherId, schoolId, "9000000023", "Teacher", "teacher@school.com")
            ],
            assignments:
            [
                new TeacherClassAssignmentEntity
                {
                    Id = Guid.NewGuid(),
                    SchoolId = schoolId,
                    TeacherId = teacherId,
                    ClassId = classId,
                    Subject = "Science",
                    IsClassTeacher = false,
                    CreatedAt = DateTimeOffset.UtcNow
                }
            ],
            homeworks:
            [
                new HomeworkEntity
                {
                    Id = homeworkId,
                    SchoolId = schoolId,
                    ClassId = classId,
                    Subject = "Science",
                    Title = "HW 4",
                    Description = "Practice",
                    AssignedById = teacherId,
                    DueDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(2)),
                    Status = "PendingApproval",
                    SubmittedAt = DateTimeOffset.UtcNow
                }
            ]);

        await using var context = new AppDbContext(options, currentUser);
        var handler = new RejectHomeworkCommandHandler(
            context,
            currentUser,
            Mock.Of<ILogger<RejectHomeworkCommandHandler>>());

        var act = () => handler.Handle(new RejectHomeworkCommand(homeworkId, "Needs work"), CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenException>()
            .WithMessage("Only the class teacher can reject homework for this class.");
    }

    private static DbContextOptions<AppDbContext> CreateOptions()
    {
        return new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"AdminOnboarding_{Guid.NewGuid()}")
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

    private static UserEntity CreateTeacherUser(Guid id, Guid schoolId, string phone, string name, string email)
    {
        return new UserEntity
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
    }

    private static UserEntity CreateParentUser(Guid id, Guid schoolId, string phone, string name, string email)
    {
        return new UserEntity
        {
            Id = id,
            SchoolId = schoolId,
            Phone = phone,
            Email = email,
            Name = name,
            Role = "Parent",
            PinHash = "hashed-pin",
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
    }

    private static async Task SeedAsync(
        DbContextOptions<AppDbContext> options,
        Guid schoolId,
        IEnumerable<ClassEntity>? classes = null,
        IEnumerable<StudentEntity>? students = null,
        IEnumerable<UserEntity>? users = null,
        IEnumerable<SubjectEntity>? subjects = null,
        IEnumerable<TeacherClassAssignmentEntity>? assignments = null,
        IEnumerable<HomeworkEntity>? homeworks = null)
    {
        await using var context = new AppDbContext(options);

        context.Schools.Add(new SchoolEntity
        {
            Id = schoolId,
            Name = "Test School",
            Code = $"SCH-{schoolId.ToString()[..6]}",
            Address = "Test Address",
            ContactPhone = "9999999999",
            ContactEmail = "school@test.com",
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        });

        if (classes != null)
        {
            context.Classes.AddRange(classes);
        }

        if (students != null)
        {
            context.Students.AddRange(students);
        }

        if (users != null)
        {
            context.Users.AddRange(users);
        }

        if (subjects != null)
        {
            context.Subjects.AddRange(subjects);
        }

        if (assignments != null)
        {
            context.TeacherClassAssignments.AddRange(assignments);
        }

        if (homeworks != null)
        {
            context.Homeworks.AddRange(homeworks);
        }

        await context.SaveChangesAsync();
    }
}
