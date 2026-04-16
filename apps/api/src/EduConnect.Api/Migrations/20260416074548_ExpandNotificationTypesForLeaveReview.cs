using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EduConnect.Api.Migrations
{
    /// <inheritdoc />
    public partial class ExpandNotificationTypesForLeaveReview : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "chk_notification_type",
                table: "notifications");

            migrationBuilder.AddCheckConstraint(
                name: "chk_notification_type",
                table: "notifications",
                sql: "type IN ('notice_published', 'homework_assigned', 'absence_marked', 'leave_applied', 'leave_approved', 'leave_rejected')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "chk_notification_type",
                table: "notifications");

            migrationBuilder.AddCheckConstraint(
                name: "chk_notification_type",
                table: "notifications",
                sql: "type IN ('notice_published', 'homework_assigned', 'absence_marked', 'leave_applied')");
        }
    }
}
