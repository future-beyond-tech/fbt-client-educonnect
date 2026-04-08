using EduConnect.Api.Features.Attendance.GetAttendance;
using EduConnect.Api.Features.Attendance.MarkAbsence;
using EduConnect.Api.Features.Attendance.AdminOverride;
using EduConnect.Api.Features.Auth.Login;
using EduConnect.Api.Features.Auth.LoginParent;
using EduConnect.Api.Features.Auth.SetPin;
using EduConnect.Api.Features.Auth.RefreshToken;
using EduConnect.Api.Features.Auth.Logout;
using EduConnect.Api.Features.Auth.ForgotPassword;
using EduConnect.Api.Features.Auth.ResetPassword;
using EduConnect.Api.Features.Auth.ForgotPin;
using EduConnect.Api.Features.Auth.ResetPin;
using EduConnect.Api.Features.Homework.CreateHomework;
using EduConnect.Api.Features.Homework.GetHomework;
using EduConnect.Api.Features.Homework.UpdateHomework;
using EduConnect.Api.Features.Notices.CreateNotice;
using EduConnect.Api.Features.Notices.PublishNotice;
using EduConnect.Api.Features.Notices.GetNotices;
using EduConnect.Api.Features.Students.GetStudentsByClass;
using EduConnect.Api.Features.Students.GetStudentById;
using EduConnect.Api.Features.Students.GetStudentsForParent;
using EduConnect.Api.Features.Students.SearchParentsByPhone;
using EduConnect.Api.Features.Students.EnrollStudent;
using EduConnect.Api.Features.Students.UpdateStudent;
using EduConnect.Api.Features.Students.DeactivateStudent;
using EduConnect.Api.Features.Students.LinkParentToStudent;
using EduConnect.Api.Features.Students.UnlinkParentFromStudent;
using EduConnect.Api.Features.Classes.GetClassesBySchool;
using EduConnect.Api.Features.Teachers.GetTeachersBySchool;
using EduConnect.Api.Features.Teachers.GetTeacherProfile;
using EduConnect.Api.Features.Teachers.GetClassesForTeacher;
using EduConnect.Api.Features.Teachers.AssignClassToTeacher;
using EduConnect.Api.Features.Teachers.RemoveClassFromTeacher;
using EduConnect.Api.Features.Subjects.GetSubjectsBySchool;
using EduConnect.Api.Features.Subjects.CreateSubject;
using EduConnect.Api.Features.Notifications.GetNotificationsForUser;
using EduConnect.Api.Features.Notifications.GetUnreadCount;
using EduConnect.Api.Features.Notifications.MarkNotificationRead;
using EduConnect.Api.Features.Notifications.MarkAllNotificationsRead;
using EduConnect.Api.Features.Attachments.RequestUploadUrl;
using EduConnect.Api.Features.Attachments.AttachFileToEntity;
using EduConnect.Api.Features.Attachments.DeleteAttachment;
using EduConnect.Api.Features.Attachments.GetAttachmentsForEntity;

namespace EduConnect.Api.Common.Extensions;

public static class EndpointRouteBuilderExtensions
{
    public static void MapAllEndpoints(this WebApplication app)
    {
        app.MapAuthEndpoints();
        app.MapAttendanceEndpoints();
        app.MapHomeworkEndpoints();
        app.MapNoticeEndpoints();
        app.MapStudentEndpoints();
        app.MapClassEndpoints();
        app.MapTeacherEndpoints();
        app.MapSubjectEndpoints();
        app.MapNotificationEndpoints();
        app.MapAttachmentEndpoints();
    }

    private static void MapAuthEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/auth").WithTags("Auth");

        group.MapPost("/login", LoginEndpoint.Handle).WithName("Login").AllowAnonymous();
        group.MapPost("/login-parent", LoginParentEndpoint.Handle).WithName("LoginParent").AllowAnonymous();
        group.MapPut("/set-pin", SetPinEndpoint.Handle).WithName("SetPin").RequireAuthorization();
        group.MapPost("/refresh", RefreshTokenEndpoint.Handle).WithName("RefreshToken").AllowAnonymous();
        group.MapPost("/logout", LogoutEndpoint.Handle).WithName("Logout").RequireAuthorization();
        group.MapPost("/forgot-password", ForgotPasswordEndpoint.Handle).WithName("ForgotPassword").AllowAnonymous();
        group.MapPost("/reset-password", ResetPasswordEndpoint.Handle).WithName("ResetPassword").AllowAnonymous();
        group.MapPost("/forgot-pin", ForgotPinEndpoint.Handle).WithName("ForgotPin").AllowAnonymous();
        group.MapPost("/reset-pin", ResetPinEndpoint.Handle).WithName("ResetPin").AllowAnonymous();
    }

    private static void MapAttendanceEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/attendance").WithTags("Attendance").RequireAuthorization();

        group.MapPost("/", MarkAbsenceEndpoint.Handle).WithName("MarkAbsence");
        group.MapGet("/", GetAttendanceEndpoint.Handle).WithName("GetAttendance");
        group.MapPut("/{recordId}/override", AdminOverrideEndpoint.Handle).WithName("AdminOverride");
    }

    private static void MapHomeworkEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/homework").WithTags("Homework").RequireAuthorization();

        group.MapPost("/", CreateHomeworkEndpoint.Handle).WithName("CreateHomework");
        group.MapGet("/", GetHomeworkEndpoint.Handle).WithName("GetHomework");
        group.MapPut("/{id}", UpdateHomeworkEndpoint.Handle).WithName("UpdateHomework");
    }

    private static void MapNoticeEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/notices").WithTags("Notices").RequireAuthorization();

        group.MapPost("/", CreateNoticeEndpoint.Handle).WithName("CreateNotice");
        group.MapGet("/", GetNoticesEndpoint.Handle).WithName("GetNotices");
        group.MapPut("/{id}/publish", PublishNoticeEndpoint.Handle).WithName("PublishNotice");
    }

    private static void MapStudentEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/students").WithTags("Students").RequireAuthorization();

        group.MapGet("/", GetStudentsByClassEndpoint.Handle).WithName("GetStudentsByClass");
        group.MapGet("/my-children", GetStudentsForParentEndpoint.Handle).WithName("GetStudentsForParent");
        group.MapGet("/search-parents", SearchParentsByPhoneEndpoint.Handle).WithName("SearchParentsByPhone");
        group.MapGet("/{id}", GetStudentByIdEndpoint.Handle).WithName("GetStudentById");
        group.MapPost("/", EnrollStudentEndpoint.Handle).WithName("EnrollStudent");
        group.MapPut("/{id}", UpdateStudentEndpoint.Handle).WithName("UpdateStudent");
        group.MapPut("/{id}/deactivate", DeactivateStudentEndpoint.Handle).WithName("DeactivateStudent");
        group.MapPost("/{id}/parent-links", LinkParentToStudentEndpoint.Handle).WithName("LinkParentToStudent");
        group.MapDelete("/{id}/parent-links/{linkId}", UnlinkParentFromStudentEndpoint.Handle).WithName("UnlinkParentFromStudent");
    }

    private static void MapClassEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/classes").WithTags("Classes").RequireAuthorization();

        group.MapGet("/", GetClassesBySchoolEndpoint.Handle).WithName("GetClassesBySchool");
    }

    private static void MapTeacherEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/teachers").WithTags("Teachers").RequireAuthorization();

        group.MapGet("/", GetTeachersBySchoolEndpoint.Handle).WithName("GetTeachersBySchool");
        group.MapGet("/my-classes", GetClassesForTeacherEndpoint.Handle).WithName("GetClassesForTeacher");
        group.MapGet("/{id}", GetTeacherProfileEndpoint.Handle).WithName("GetTeacherProfile");
        group.MapPost("/{id}/assignments", AssignClassToTeacherEndpoint.Handle).WithName("AssignClassToTeacher");
        group.MapDelete("/{id}/assignments/{assignmentId}", RemoveClassFromTeacherEndpoint.Handle).WithName("RemoveClassFromTeacher");
    }

    private static void MapSubjectEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/subjects").WithTags("Subjects").RequireAuthorization();

        group.MapGet("/", GetSubjectsBySchoolEndpoint.Handle).WithName("GetSubjectsBySchool");
        group.MapPost("/", CreateSubjectEndpoint.Handle).WithName("CreateSubject");
    }

    private static void MapNotificationEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/notifications").WithTags("Notifications").RequireAuthorization();

        group.MapGet("/", GetNotificationsForUserEndpoint.Handle).WithName("GetNotificationsForUser");
        group.MapGet("/unread-count", GetUnreadCountEndpoint.Handle).WithName("GetUnreadCount");
        group.MapPut("/{id}/read", MarkNotificationReadEndpoint.Handle).WithName("MarkNotificationRead");
        group.MapPut("/read-all", MarkAllNotificationsReadEndpoint.Handle).WithName("MarkAllNotificationsRead");
    }

    private static void MapAttachmentEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/attachments").WithTags("Attachments").RequireAuthorization();

        group.MapPost("/request-upload-url", RequestUploadUrlEndpoint.Handle).WithName("RequestUploadUrl");
        group.MapPost("/attach", AttachFileToEntityEndpoint.Handle).WithName("AttachFileToEntity");
        group.MapGet("/", GetAttachmentsForEntityEndpoint.Handle).WithName("GetAttachmentsForEntity");
        group.MapDelete("/{id}", DeleteAttachmentEndpoint.Handle).WithName("DeleteAttachment");
    }
}
