using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EduConnect.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddNoticeTargetClasses : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "notice_target_classes",
                columns: table => new
                {
                    notice_id = table.Column<Guid>(type: "uuid", nullable: false),
                    class_id = table.Column<Guid>(type: "uuid", nullable: false),
                    school_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_notice_target_classes", x => new { x.notice_id, x.class_id });
                    table.ForeignKey(
                        name: "fk_notice_target_classes_classes_class_id",
                        column: x => x.class_id,
                        principalTable: "classes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_notice_target_classes_notices_notice_id",
                        column: x => x.notice_id,
                        principalTable: "notices",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_notice_target_classes_schools_school_id",
                        column: x => x.school_id,
                        principalTable: "schools",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_notice_target_classes_class_id",
                table: "notice_target_classes",
                column: "class_id");

            migrationBuilder.CreateIndex(
                name: "ix_notice_target_classes_school_id",
                table: "notice_target_classes",
                column: "school_id");

            migrationBuilder.CreateIndex(
                name: "ix_notice_target_classes_school_id_class_id",
                table: "notice_target_classes",
                columns: new[] { "school_id", "class_id" });

            migrationBuilder.Sql(
                """
                INSERT INTO notice_target_classes (notice_id, class_id, school_id, created_at)
                SELECT id, target_class_id, school_id, NOW()
                FROM notices
                WHERE target_class_id IS NOT NULL;
                """);

            migrationBuilder.DropForeignKey(
                name: "fk_notices_classes_target_class_id",
                table: "notices");

            migrationBuilder.DropIndex(
                name: "ix_notices_target_class_id",
                table: "notices");

            migrationBuilder.DropColumn(
                name: "target_class_id",
                table: "notices");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "notice_target_classes");

            migrationBuilder.AddColumn<Guid>(
                name: "target_class_id",
                table: "notices",
                type: "uuid",
                nullable: true);

            migrationBuilder.Sql(
                """
                UPDATE notices AS n
                SET target_class_id = target.class_id
                FROM (
                    SELECT notice_id, MIN(class_id) AS class_id
                    FROM notice_target_classes
                    GROUP BY notice_id
                ) AS target
                WHERE n.id = target.notice_id;
                """);

            migrationBuilder.CreateIndex(
                name: "ix_notices_target_class_id",
                table: "notices",
                column: "target_class_id");

            migrationBuilder.AddForeignKey(
                name: "fk_notices_classes_target_class_id",
                table: "notices",
                column: "target_class_id",
                principalTable: "classes",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }
    }
}
