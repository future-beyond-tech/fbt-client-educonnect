using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EduConnect.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddPasswordUpdatedAtToUsers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "password_updated_at",
                table: "users",
                type: "timestamp with time zone",
                nullable: true);

            // Backfill existing staff passwords with a conservative lower bound so
            // the legacy-rotation check (PasswordPolicy.IsLegacyPassword) flags
            // them on next login. We don't know when they last rotated, only
            // that it wasn't later than creation, so created_at is the safest
            // stamp. NULL is left for users without a password (e.g. PIN-only
            // parents); the check treats NULL as legacy but the login path for
            // Parent role never evaluates this — no user impact.
            migrationBuilder.Sql(@"
                UPDATE users
                SET password_updated_at = created_at
                WHERE password_hash IS NOT NULL
                  AND password_updated_at IS NULL;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "password_updated_at",
                table: "users");
        }
    }
}
