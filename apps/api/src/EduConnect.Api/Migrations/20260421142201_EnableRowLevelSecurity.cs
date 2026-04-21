using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EduConnect.Api.Migrations
{
    /// <inheritdoc />
    public partial class EnableRowLevelSecurity : Migration
    {
        // Every row-owning table in the schema has a school_id column after
        // this migration runs. refresh_tokens previously tracked tenancy via
        // its users FK join; that column is added and backfilled here so RLS
        // policies can read school_id directly like every other table.
        private static readonly string[] TenantedTablesWithSchoolIdColumn =
        {
            "users",
            "classes",
            "students",
            "teacher_class_assignments",
            "parent_student_links",
            "attendance_records",
            "homework",
            "notices",
            "notice_target_classes",
            "subjects",
            "notifications",
            "attachments",
            "leave_applications",
            "refresh_tokens",
            "auth_reset_tokens",
            "user_push_subscriptions",
            "exams",
            "exam_subjects",
            "exam_results",
        };

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1. refresh_tokens gains its own school_id column, backfilled
            // from the user FK before we tighten NOT NULL.
            migrationBuilder.Sql("ALTER TABLE refresh_tokens ADD COLUMN school_id uuid NULL;");
            migrationBuilder.Sql(@"
                UPDATE refresh_tokens rt
                SET school_id = u.school_id
                FROM users u
                WHERE u.id = rt.user_id AND rt.school_id IS NULL;
            ");
            migrationBuilder.Sql("ALTER TABLE refresh_tokens ALTER COLUMN school_id SET NOT NULL;");
            migrationBuilder.Sql(@"
                ALTER TABLE refresh_tokens
                ADD CONSTRAINT fk_refresh_tokens_schools_school_id
                FOREIGN KEY (school_id) REFERENCES schools(id) ON DELETE CASCADE;
            ");
            migrationBuilder.CreateIndex(
                name: "ix_refresh_tokens_school_id",
                table: "refresh_tokens",
                column: "school_id");

            // 2. Tenant-lookup helper used by every policy. Returns NULL when
            // the app.current_school_id GUC is unset or empty so anonymous
            // paths (login, refresh, seeding) behave identically to the EF
            // global query filter's "!IsAuthenticated" bypass.
            migrationBuilder.Sql(@"
                CREATE OR REPLACE FUNCTION current_app_school_id() RETURNS uuid
                LANGUAGE SQL STABLE AS $$
                  SELECT NULLIF(current_setting('app.current_school_id', true), '')::uuid;
                $$;
            ");

            // 3. schools is the tenancy root. The policy compares id, not
            // school_id. ENABLE + FORCE so the table owner is subject to RLS
            // without needing a dedicated app_runtime role (Phase 4 deploy
            // notes describe that follow-up).
            migrationBuilder.Sql("ALTER TABLE schools ENABLE ROW LEVEL SECURITY;");
            migrationBuilder.Sql("ALTER TABLE schools FORCE ROW LEVEL SECURITY;");
            migrationBuilder.Sql(@"
                CREATE POLICY tenant_isolation ON schools
                USING (current_app_school_id() IS NULL OR id = current_app_school_id())
                WITH CHECK (current_app_school_id() IS NULL OR id = current_app_school_id());
            ");

            // 4. Uniform policy for every table with a school_id column.
            foreach (var table in TenantedTablesWithSchoolIdColumn)
            {
                migrationBuilder.Sql($"ALTER TABLE {table} ENABLE ROW LEVEL SECURITY;");
                migrationBuilder.Sql($"ALTER TABLE {table} FORCE ROW LEVEL SECURITY;");
                migrationBuilder.Sql($@"
                    CREATE POLICY tenant_isolation ON {table}
                    USING (current_app_school_id() IS NULL OR school_id = current_app_school_id())
                    WITH CHECK (current_app_school_id() IS NULL OR school_id = current_app_school_id());
                ");
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            foreach (var table in TenantedTablesWithSchoolIdColumn)
            {
                migrationBuilder.Sql($"DROP POLICY IF EXISTS tenant_isolation ON {table};");
                migrationBuilder.Sql($"ALTER TABLE {table} NO FORCE ROW LEVEL SECURITY;");
                migrationBuilder.Sql($"ALTER TABLE {table} DISABLE ROW LEVEL SECURITY;");
            }

            migrationBuilder.Sql("DROP POLICY IF EXISTS tenant_isolation ON schools;");
            migrationBuilder.Sql("ALTER TABLE schools NO FORCE ROW LEVEL SECURITY;");
            migrationBuilder.Sql("ALTER TABLE schools DISABLE ROW LEVEL SECURITY;");

            migrationBuilder.Sql("DROP FUNCTION IF EXISTS current_app_school_id();");

            migrationBuilder.DropIndex(
                name: "ix_refresh_tokens_school_id",
                table: "refresh_tokens");
            migrationBuilder.Sql("ALTER TABLE refresh_tokens DROP CONSTRAINT IF EXISTS fk_refresh_tokens_schools_school_id;");
            migrationBuilder.DropColumn(
                name: "school_id",
                table: "refresh_tokens");
        }
    }
}
