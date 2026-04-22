using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EduConnect.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddAttachmentXminConcurrencyToken : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // xmin is a Postgres system column on every relation, so there
            // is no DDL to apply here. EF's auto-generated AddColumn would
            // fail at runtime ("column 'xmin' already exists"). The
            // migration is intentionally empty; it exists only to keep
            // the model snapshot in sync with AttachmentEntity.Xmin being
            // declared as a row-version. The actual wiring lives in
            // AttachmentConfiguration.cs via .IsRowVersion().
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // No-op for the same reason — there's nothing to drop.
        }
    }
}
