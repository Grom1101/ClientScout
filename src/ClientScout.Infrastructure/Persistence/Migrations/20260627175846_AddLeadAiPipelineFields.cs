using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ClientScout.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddLeadAiPipelineFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_JobLeads_ProfileId",
                table: "JobLeads");

            migrationBuilder.AddColumn<string>(
                name: "AiCategory",
                table: "JobLeads",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "AiConfidence",
                table: "JobLeads",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AiReason",
                table: "JobLeads",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "AiStatus",
                table: "JobLeads",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "AiSummary",
                table: "JobLeads",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ExpiresAt",
                table: "JobLeads",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "now() + interval '7 days'");

            migrationBuilder.AddColumn<List<string>>(
                name: "MatchedTerms",
                table: "JobLeads",
                type: "text[]",
                nullable: false,
                defaultValueSql: "ARRAY[]::text[]");

            migrationBuilder.AddColumn<int>(
                name: "Score",
                table: "JobLeads",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "SourceName",
                table: "JobLeads",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "SourceType",
                table: "JobLeads",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_JobLeads_ProfileId_FoundAt",
                table: "JobLeads",
                columns: new[] { "ProfileId", "FoundAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_JobLeads_ProfileId_FoundAt",
                table: "JobLeads");

            migrationBuilder.DropColumn(
                name: "AiCategory",
                table: "JobLeads");

            migrationBuilder.DropColumn(
                name: "AiConfidence",
                table: "JobLeads");

            migrationBuilder.DropColumn(
                name: "AiReason",
                table: "JobLeads");

            migrationBuilder.DropColumn(
                name: "AiStatus",
                table: "JobLeads");

            migrationBuilder.DropColumn(
                name: "AiSummary",
                table: "JobLeads");

            migrationBuilder.DropColumn(
                name: "ExpiresAt",
                table: "JobLeads");

            migrationBuilder.DropColumn(
                name: "MatchedTerms",
                table: "JobLeads");

            migrationBuilder.DropColumn(
                name: "Score",
                table: "JobLeads");

            migrationBuilder.DropColumn(
                name: "SourceName",
                table: "JobLeads");

            migrationBuilder.DropColumn(
                name: "SourceType",
                table: "JobLeads");

            migrationBuilder.CreateIndex(
                name: "IX_JobLeads_ProfileId",
                table: "JobLeads",
                column: "ProfileId");
        }
    }
}
