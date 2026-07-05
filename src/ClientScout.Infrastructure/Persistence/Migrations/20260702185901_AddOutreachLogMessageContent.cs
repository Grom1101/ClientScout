using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ClientScout.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddOutreachLogMessageContent : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // MustIncludeSignals, RejectSignals, SearchProfileSummary, SoftSignals
            // were already applied to the DB directly; skip them here.

            migrationBuilder.AddColumn<string>(
                name: "MessageContent",
                table: "OutreachLogs",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MessageContent",
                table: "OutreachLogs");
        }
    }
}
