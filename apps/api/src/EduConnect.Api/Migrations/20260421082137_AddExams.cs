using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EduConnect.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddExams : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "exams",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    school_id = table.Column<Guid>(type: "uuid", nullable: false),
                    class_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    academic_year = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    created_by_id = table.Column<Guid>(type: "uuid", nullable: false),
                    is_schedule_published = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    schedule_published_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    is_results_finalized = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    results_finalized_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_exams", x => x.id);
                    table.ForeignKey(
                        name: "fk_exams_classes_class_id",
                        column: x => x.class_id,
                        principalTable: "classes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_exams_schools_school_id",
                        column: x => x.school_id,
                        principalTable: "schools",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_exams_users_created_by_id",
                        column: x => x.created_by_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "exam_subjects",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    school_id = table.Column<Guid>(type: "uuid", nullable: false),
                    exam_id = table.Column<Guid>(type: "uuid", nullable: false),
                    subject = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    exam_date = table.Column<DateOnly>(type: "date", nullable: false),
                    start_time = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    end_time = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    max_marks = table.Column<decimal>(type: "numeric(6,2)", nullable: false),
                    room = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_exam_subjects", x => x.id);
                    table.CheckConstraint("chk_exam_subjects_max_marks_positive", "max_marks > 0");
                    table.CheckConstraint("chk_exam_subjects_time_order", "end_time > start_time");
                    table.ForeignKey(
                        name: "fk_exam_subjects_exams_exam_id",
                        column: x => x.exam_id,
                        principalTable: "exams",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_exam_subjects_schools_school_id",
                        column: x => x.school_id,
                        principalTable: "schools",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "exam_results",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    school_id = table.Column<Guid>(type: "uuid", nullable: false),
                    exam_id = table.Column<Guid>(type: "uuid", nullable: false),
                    exam_subject_id = table.Column<Guid>(type: "uuid", nullable: false),
                    student_id = table.Column<Guid>(type: "uuid", nullable: false),
                    marks_obtained = table.Column<decimal>(type: "numeric(6,2)", nullable: true),
                    grade = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: true),
                    remarks = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    is_absent = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    recorded_by_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_exam_results", x => x.id);
                    table.CheckConstraint("chk_exam_results_absent_marks", "(is_absent = true AND marks_obtained IS NULL) OR is_absent = false");
                    table.CheckConstraint("chk_exam_results_has_score", "is_absent = true OR marks_obtained IS NOT NULL OR grade IS NOT NULL");
                    table.ForeignKey(
                        name: "fk_exam_results_exam_subjects_exam_subject_id",
                        column: x => x.exam_subject_id,
                        principalTable: "exam_subjects",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_exam_results_exams_exam_id",
                        column: x => x.exam_id,
                        principalTable: "exams",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_exam_results_schools_school_id",
                        column: x => x.school_id,
                        principalTable: "schools",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_exam_results_students_student_id",
                        column: x => x.student_id,
                        principalTable: "students",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_exam_results_users_recorded_by_id",
                        column: x => x.recorded_by_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_exam_results_exam_id",
                table: "exam_results",
                column: "exam_id");

            migrationBuilder.CreateIndex(
                name: "ix_exam_results_exam_subject_id_student_id",
                table: "exam_results",
                columns: new[] { "exam_subject_id", "student_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_exam_results_recorded_by_id",
                table: "exam_results",
                column: "recorded_by_id");

            migrationBuilder.CreateIndex(
                name: "ix_exam_results_school_id",
                table: "exam_results",
                column: "school_id");

            migrationBuilder.CreateIndex(
                name: "ix_exam_results_student_id",
                table: "exam_results",
                column: "student_id");

            migrationBuilder.CreateIndex(
                name: "ix_exam_subjects_exam_id",
                table: "exam_subjects",
                column: "exam_id");

            migrationBuilder.CreateIndex(
                name: "ix_exam_subjects_exam_id_subject",
                table: "exam_subjects",
                columns: new[] { "exam_id", "subject" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_exam_subjects_school_id",
                table: "exam_subjects",
                column: "school_id");

            migrationBuilder.CreateIndex(
                name: "ix_exams_class_id",
                table: "exams",
                column: "class_id");

            migrationBuilder.CreateIndex(
                name: "ix_exams_created_by_id",
                table: "exams",
                column: "created_by_id");

            migrationBuilder.CreateIndex(
                name: "ix_exams_school_id",
                table: "exams",
                column: "school_id");

            migrationBuilder.CreateIndex(
                name: "ix_exams_school_id_class_id_is_deleted",
                table: "exams",
                columns: new[] { "school_id", "class_id", "is_deleted" },
                filter: "is_deleted = false");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "exam_results");

            migrationBuilder.DropTable(
                name: "exam_subjects");

            migrationBuilder.DropTable(
                name: "exams");
        }
    }
}
