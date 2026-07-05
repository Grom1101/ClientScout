using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ClientScout.Infrastructure.Persistence.Migrations
{
    public partial class AddCampaignScheduleWindow : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                ALTER TABLE "OutreachCampaigns"
                ADD COLUMN IF NOT EXISTS "ScheduleMode" text NOT NULL DEFAULT 'allday',
                ADD COLUMN IF NOT EXISTS "ScheduleStartTime" text NULL,
                ADD COLUMN IF NOT EXISTS "ScheduleEndTime" text NULL,
                ADD COLUMN IF NOT EXISTS "TimezoneOffsetMinutes" integer NOT NULL DEFAULT 0;
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                ALTER TABLE "OutreachCampaigns"
                DROP COLUMN IF EXISTS "TimezoneOffsetMinutes",
                DROP COLUMN IF EXISTS "ScheduleEndTime",
                DROP COLUMN IF EXISTS "ScheduleStartTime",
                DROP COLUMN IF EXISTS "ScheduleMode";
                """);
        }
    }
}
