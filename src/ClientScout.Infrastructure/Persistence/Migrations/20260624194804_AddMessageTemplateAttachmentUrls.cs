using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ClientScout.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddMessageTemplateAttachmentUrls : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                ALTER TABLE "MessageTemplates"
                ADD COLUMN IF NOT EXISTS "AttachmentUrls" text[] NOT NULL DEFAULT ARRAY[]::text[];
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                ALTER TABLE "MessageTemplates"
                DROP COLUMN IF EXISTS "AttachmentUrls";
                """);
        }
    }
}
