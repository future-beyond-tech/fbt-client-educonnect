using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EduConnect.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddAttachmentScanStatus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "scanned_at",
                table: "attachments",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "status",
                table: "attachments",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "Pending");

            migrationBuilder.AddColumn<string>(
                name: "threat_name",
                table: "attachments",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_attachments_status",
                table: "attachments",
                column: "status",
                filter: "status <> 'Available'");

            migrationBuilder.AddCheckConstraint(
                name: "chk_attachment_status",
                table: "attachments",
                sql: "status IN ('Pending', 'Available', 'Infected', 'ScanFailed')");

            // Attachments that existed before the scanning pipeline predate
            // any threat we would now detect, but they also haven't been
            // scanned. Treating them as Available keeps live downloads
            // working on deploy; operators can re-queue a rescan by updating
            // status back to Pending and enqueuing the scan job.
            migrationBuilder.Sql(@"
                UPDATE attachments
                SET status = 'Available'
                WHERE status = 'Pending';
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_attachments_status",
                table: "attachments");

            migrationBuilder.DropCheckConstraint(
                name: "chk_attachment_status",
                table: "attachments");

            migrationBuilder.DropColumn(
                name: "scanned_at",
                table: "attachments");

            migrationBuilder.DropColumn(
                name: "status",
                table: "attachments");

            migrationBuilder.DropColumn(
                name: "threat_name",
                table: "attachments");
        }
    }
}
