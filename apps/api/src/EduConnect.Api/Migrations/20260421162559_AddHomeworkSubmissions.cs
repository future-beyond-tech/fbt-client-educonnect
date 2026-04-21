using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EduConnect.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddHomeworkSubmissions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "homework_submissions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    school_id = table.Column<Guid>(type: "uuid", nullable: false),
                    homework_id = table.Column<Guid>(type: "uuid", nullable: false),
                    student_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    body_text = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    submitted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    grade = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    feedback = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    graded_by_id = table.Column<Guid>(type: "uuid", nullable: true),
                    graded_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_homework_submissions", x => x.id);
                    table.CheckConstraint("chk_homework_submission_status", "status IN ('Submitted', 'Late', 'Graded', 'Returned')");
                    table.ForeignKey(
                        name: "fk_homework_submissions_homework_homework_id",
                        column: x => x.homework_id,
                        principalTable: "homework",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_homework_submissions_schools_school_id",
                        column: x => x.school_id,
                        principalTable: "schools",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_homework_submissions_students_student_id",
                        column: x => x.student_id,
                        principalTable: "students",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_homework_submissions_users_graded_by_id",
                        column: x => x.graded_by_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "ix_homework_submissions_graded_by_id",
                table: "homework_submissions",
                column: "graded_by_id");

            migrationBuilder.CreateIndex(
                name: "ix_homework_submissions_homework_id",
                table: "homework_submissions",
                column: "homework_id");

            migrationBuilder.CreateIndex(
                name: "ix_homework_submissions_school_id",
                table: "homework_submissions",
                column: "school_id");

            migrationBuilder.CreateIndex(
                name: "ix_homework_submissions_student_id",
                table: "homework_submissions",
                column: "student_id");

            migrationBuilder.CreateIndex(
                name: "ux_homework_submissions_homework_student",
                table: "homework_submissions",
                columns: new[] { "homework_id", "student_id" },
                unique: true);

            // RLS (Phase 4 pattern). New tenanted table gets isolation from
            // day one — migration itself runs with no tenant set so the
            // NULL-bypass branch keeps DDL unaffected.
            migrationBuilder.Sql("ALTER TABLE homework_submissions ENABLE ROW LEVEL SECURITY;");
            migrationBuilder.Sql("ALTER TABLE homework_submissions FORCE ROW LEVEL SECURITY;");
            migrationBuilder.Sql(@"
                CREATE POLICY tenant_isolation ON homework_submissions
                USING (current_app_school_id() IS NULL OR school_id = current_app_school_id())
                WITH CHECK (current_app_school_id() IS NULL OR school_id = current_app_school_id());
            ");

            // Widen attachments.entity_type to allow attaching files to a
            // submission. Existing 'homework' / 'notice' rows are unaffected.
            migrationBuilder.Sql(@"
                ALTER TABLE attachments DROP CONSTRAINT IF EXISTS chk_attachment_entity_type;
                ALTER TABLE attachments ADD CONSTRAINT chk_attachment_entity_type
                  CHECK (entity_type IS NULL OR entity_type IN ('homework', 'notice', 'homework_submission'));
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Restore attachments check to the pre-phase-6 set before dropping
            // the new table.
            migrationBuilder.Sql(@"
                ALTER TABLE attachments DROP CONSTRAINT IF EXISTS chk_attachment_entity_type;
                ALTER TABLE attachments ADD CONSTRAINT chk_attachment_entity_type
                  CHECK (entity_type IS NULL OR entity_type IN ('homework', 'notice'));
            ");

            migrationBuilder.Sql("DROP POLICY IF EXISTS tenant_isolation ON homework_submissions;");
            migrationBuilder.Sql("ALTER TABLE homework_submissions NO FORCE ROW LEVEL SECURITY;");
            migrationBuilder.Sql("ALTER TABLE homework_submissions DISABLE ROW LEVEL SECURITY;");

            migrationBuilder.DropTable(
                name: "homework_submissions");
        }
    }
}
