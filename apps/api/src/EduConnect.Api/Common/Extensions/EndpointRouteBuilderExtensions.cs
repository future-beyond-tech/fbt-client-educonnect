using EduConnect.Api.Features.Attendance.GetAttendance;
using EduConnect.Api.Features.Attendance.MarkAbsence;
using EduConnect.Api.Features.Attendance.AdminOverride;
using EduConnect.Api.Features.Attendance.ApplyLeave;
using EduConnect.Api.Features.Attendance.ApproveLeave;
using EduConnect.Api.Features.Attendance.CancelLeaveApplication;
using EduConnect.Api.Features.Attendance.GetAttendanceTakeContext;
using EduConnect.Api.Features.Attendance.GetLeaveApplications;
using EduConnect.Api.Features.Attendance.RejectLeave;
using EduConnect.Api.Features.Attendance.SubmitAttendanceTake;
using EduConnect.Api.Features.Attendance.UpdateLeaveApplication;
using EduConnect.Api.Features.Auth.Login;
using EduConnect.Api.Features.Auth.LoginParent;
using EduConnect.Api.Features.Auth.SetPin;
using EduConnect.Api.Features.Auth.RefreshToken;
using EduConnect.Api.Features.Auth.Logout;
using EduConnect.Api.Features.Auth.ForgotPassword;
using EduConnect.Api.Features.Auth.ResetPassword;
using EduConnect.Api.Features.Auth.ForgotPin;
using EduConnect.Api.Features.Auth.ResetPin;
using EduConnect.Api.Features.Auth.ChangePassword;
using EduConnect.Api.Features.Auth.ChangePin;
using EduConnect.Api.Features.Homework.CreateHomework;
using EduConnect.Api.Features.Homework.GetHomework;
using EduConnect.Api.Features.Homework.UpdateHomework;
using EduConnect.Api.Features.Homework.SubmitHomeworkForApproval;
using EduConnect.Api.Features.Homework.ApproveHomework;
using EduConnect.Api.Features.Homework.RejectHomework;
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
using EduConnect.Api.Features.Parents.CreateParent;
using EduConnect.Api.Features.Classes.GetClassesBySchool;
using EduConnect.Api.Features.Classes.GetClassAssignments;
using EduConnect.Api.Features.Classes.CreateClass;
using EduConnect.Api.Features.Classes.UpdateClass;
using EduConnect.Api.Features.Teachers.GetTeachersBySchool;
using EduConnect.Api.Features.Teachers.GetTeacherProfile;
using EduConnect.Api.Features.Teachers.GetClassesForTeacher;
using EduConnect.Api.Features.Teachers.CreateTeacher;
using EduConnect.Api.Features.Teachers.AssignClassToTeacher;
using EduConnect.Api.Features.Teachers.PromoteClassTeacher;
using EduConnect.Api.Features.Teachers.RemoveClassFromTeacher;
using EduConnect.Api.Features.Subjects.GetSubjectsBySchool;
using EduConnect.Api.Features.Subjects.CreateSubject;
using EduConnect.Api.Features.Notifications.GetNotificationsForUser;
using EduConnect.Api.Features.Notifications.GetUnreadCount;
using EduConnect.Api.Features.Notifications.MarkNotificationRead;
using EduConnect.Api.Features.Notifications.MarkAllNotificationsRead;
using EduConnect.Api.Features.Attachments.RequestUploadUrl;
using EduConnect.Api.Features.Attachments.RequestUploadUrlV2;
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
        app.MapParentEndpoints();
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
        group.MapPost("/change-password", ChangePasswordEndpoint.Handle).WithName("ChangePassword").RequireAuthorization();
        group.MapPost("/change-pin", ChangePinEndpoint.Handle).WithName("ChangePin").RequireAuthorization();
    }

    private static void MapAttendanceEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/attendance").WithTags("Attendance").RequireAuthorization();

        group.MapPost("/", MarkAbsenceEndpoint.Handle).WithName("MarkAbsence");
        group.MapGet("/", GetAttendanceEndpoint.Handle).WithName("GetAttendance");
        group.MapPut("/{recordId}/override", AdminOverrideEndpoint.Handle).WithName("AdminOverride");
        group.MapPost("/leave", ApplyLeaveEndpoint.Handle).WithName("ApplyLeave");
        group.MapGet("/leave", GetLeaveApplicationsEndpoint.Handle).WithName("GetLeaveApplications");
        group.MapPut("/leave/{id}", UpdateLeaveApplicationEndpoint.Handle).WithName("UpdateLeaveApplication");
        group.MapDelete("/leave/{id}", CancelLeaveApplicationEndpoint.Handle).WithName("CancelLeaveApplication");
        group.MapPut("/leave/{id}/approve", ApproveLeaveEndpoint.Handle).WithName("ApproveLeave");
        group.MapPut("/leave/{id}/reject", RejectLeaveEndpoint.Handle).WithName("RejectLeave");
        group.MapGet("/take", GetAttendanceTakeContextEndpoint.Handle).WithName("GetAttendanceTakeContext");
        group.MapPost("/take", SubmitAttendanceTakeEndpoint.Handle).WithName("SubmitAttendanceTake");
    }

    private static void MapHomeworkEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/homework").WithTags("Homework").RequireAuthorization();

        group.MapPost("/", CreateHomeworkEndpoint.Handle).WithName("CreateHomework");
        group.MapGet("/", GetHomeworkEndpoint.Handle).WithName("GetHomework");
        group.MapPut("/{id}", UpdateHomeworkEndpoint.Handle).WithName("UpdateHomework");
        group.MapPut("/{id}/submit", SubmitHomeworkForApprovalEndpoint.Handle).WithName("SubmitHomeworkForApproval");
        group.MapPut("/{id}/approve", ApproveHomeworkEndpoint.Handle).WithName("ApproveHomework");
        group.MapPut("/{id}/reject", RejectHomeworkEndpoint.Handle).WithName("RejectHomework");
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

    private static void MapParentEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/parents").WithTags("Parents").RequireAuthorization();

        group.MapPost("/", CreateParentEndpoint.Handle).WithName("CreateParent");
    }

    private static void MapClassEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/classes").WithTags("Classes").RequireAuthorization();

        group.MapGet("/", GetClassesBySchoolEndpoint.Handle).WithName("GetClassesBySchool");
        group.MapGet("/{id}/assignments", GetClassAssignmentsEndpoint.Handle).WithName("GetClassAssignments");
        group.MapPost("/", CreateClassEndpoint.Handle).WithName("CreateClass");
        group.MapPut("/{id}", UpdateClassEndpoint.Handle).WithName("UpdateClass");
    }

    private static void MapTeacherEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/teachers").WithTags("Teachers").RequireAuthorization();

        group.MapGet("/", GetTeachersBySchoolEndpoint.Handle).WithName("GetTeachersBySchool");
        group.MapGet("/my-classes", GetClassesForTeacherEndpoint.Handle).WithName("GetClassesForTeacher");
        group.MapGet("/{id}", GetTeacherProfileEndpoint.Handle).WithName("GetTeacherProfile");
        group.MapPost("/", CreateTeacherEndpoint.Handle).WithName("CreateTeacher");
        group.MapPost("/{id}/assignments", AssignClassToTeacherEndpoint.Handle).WithName("AssignClassToTeacher");
        group.MapPut("/{id}/assignments/{assignmentId}/class-teacher", PromoteClassTeacherEndpoint.Handle).WithName("PromoteClassTeacher");
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
        group.MapPost("/request-upload-url-v2", RequestUploadUrlV2Endpoint.Handle).WithName("RequestUploadUrlV2");
        group.MapPost("/attach", AttachFileToEntityEndpoint.Handle).WithName("AttachFileToEntity");
        group.MapGet("/", GetAttachmentsForEntityEndpoint.Handle).WithName("GetAttachmentsForEntity");
        group.MapDelete("/{id}", DeleteAttachmentEndpoint.Handle).WithName("DeleteAttachment");
    }
}
