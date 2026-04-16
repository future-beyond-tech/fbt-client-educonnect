using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EduConnect.Api.Migrations
{
    /// <inheritdoc />
    public partial class ExpandNotificationConstraintsForLeave : Migration
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
                sql: "entity_type IS NULL OR entity_type IN ('notice', 'homework', 'attendance', 'leave_application')");

            migrationBuilder.AddCheckConstraint(
                name: "chk_notification_type",
                table: "notifications",
                sql: "type IN ('notice_published', 'homework_assigned', 'absence_marked', 'leave_applied')");
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
                sql: "entity_type IS NULL OR entity_type IN ('notice', 'homework', 'attendance')");

            migrationBuilder.AddCheckConstraint(
                name: "chk_notification_type",
                table: "notifications",
                sql: "type IN ('notice_published', 'homework_assigned', 'absence_marked')");
        }
    }
}
