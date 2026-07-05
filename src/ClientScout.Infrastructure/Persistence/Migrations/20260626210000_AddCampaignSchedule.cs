using System;
using ClientScout.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ClientScout.Infrastructure.Persistence.Migrations
{
    [Microsoft.EntityFrameworkCore.Infrastructure.DbContext(typeof(AppDbContext))]
    [Migration("20260626210000_AddCampaignSchedule")]
    public partial class AddCampaignSchedule : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "NextRunAt",
                table: "OutreachCampaigns",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PeriodicityMinutes",
                table: "OutreachCampaigns",
                type: "integer",
                nullable: false,
                defaultValue: 30);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "NextRunAt",
                table: "OutreachCampaigns");

            migrationBuilder.DropColumn(
                name: "PeriodicityMinutes",
                table: "OutreachCampaigns");
        }
    }
}
