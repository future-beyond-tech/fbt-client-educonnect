using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EduConnect.Api.Migrations
{
    /// <inheritdoc />
    public partial class ExpandNotificationTypesForAttachments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "chk_notification_entity_type",
                table: "notifications");

            migrationBuilder.DropCheckConstraint(
                name: "chk_notification_type",
                table: "notifications");

            migrationBuilder.AddCheckConstraint(
                name: "chk_notification_entity_type",
                table: "notifications",
                sql: "entity_type IS NULL OR entity_type IN ('notice', 'homework', 'attendance', 'leave_application', 'attachment')");

            migrationBuilder.AddCheckConstraint(
                name: "chk_notification_type",
                table: "notifications",
                sql: "type IN ('notice_published', 'homework_assigned', 'absence_marked', 'leave_applied', 'leave_approved', 'leave_rejected', 'attachment_infected', 'attachment_scan_failed')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "chk_notification_entity_type",
                table: "notifications");

            migrationBuilder.DropCheckConstraint(
                name: "chk_notification_type",
                table: "notifications");

            migrationBuilder.AddCheckConstraint(
                name: "chk_notification_entity_type",
                table: "notifications",
                sql: "entity_type IS NULL OR entity_type IN ('notice', 'homework', 'attendance', 'leave_application')");

            migrationBuilder.AddCheckConstraint(
                name: "chk_notification_type",
                table: "notifications",
                sql: "type IN ('notice_published', 'homework_assigned', 'absence_marked', 'leave_applied', 'leave_approved', 'leave_rejected')");
        }
    }
}
