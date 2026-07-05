using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ClientScout.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAccountAuth : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            var legacyAccountId = new Guid("00000000-0000-0000-0000-000000000001");

            migrationBuilder.DropForeignKey(
                name: "FK_Profiles_Users_UserId",
                table: "Profiles");

            migrationBuilder.DropIndex(
                name: "IX_Profiles_UserId",
                table: "Profiles");

            migrationBuilder.DropColumn(
                name: "Subscription",
                table: "Users");

            migrationBuilder.CreateTable(
                name: "Accounts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Email = table.Column<string>(type: "text", nullable: false),
                    PasswordHash = table.Column<string>(type: "text", nullable: false),
                    TelegramUserId = table.Column<long>(type: "bigint", nullable: true),
                    ActiveProfileId = table.Column<Guid>(type: "uuid", nullable: true),
                    Subscription = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Accounts", x => x.Id);
                });

            migrationBuilder.InsertData(
                table: "Accounts",
                columns: new[] { "Id", "Email", "PasswordHash", "TelegramUserId", "ActiveProfileId", "Subscription", "CreatedAt" },
                values: new object[]
                {
                    legacyAccountId,
                    "legacy@clientscout.local",
                    "legacy-disabled",
                    null,
                    null,
                    "free",
                    new DateTimeOffset(2026, 6, 26, 0, 0, 0, TimeSpan.Zero)
                });

            migrationBuilder.AddColumn<Guid>(
                name: "AccountId",
                table: "Profiles",
                type: "uuid",
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE "Profiles"
                SET "AccountId" = '00000000-0000-0000-0000-000000000001'
                WHERE "AccountId" IS NULL;
                """);

            migrationBuilder.AlterColumn<Guid>(
                name: "AccountId",
                table: "Profiles",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.DropColumn(
                name: "UserId",
                table: "Profiles");

            migrationBuilder.CreateIndex(
                name: "IX_Profiles_AccountId",
                table: "Profiles",
                column: "AccountId");

            migrationBuilder.CreateIndex(
                name: "IX_Accounts_Email",
                table: "Accounts",
                column: "Email",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Profiles_Accounts_AccountId",
                table: "Profiles",
                column: "AccountId",
                principalTable: "Accounts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Profiles_Accounts_AccountId",
                table: "Profiles");

            migrationBuilder.DropTable(
                name: "Accounts");

            migrationBuilder.DropIndex(
                name: "IX_Profiles_AccountId",
                table: "Profiles");

            migrationBuilder.DropColumn(
                name: "AccountId",
                table: "Profiles");

            migrationBuilder.AddColumn<string>(
                name: "Subscription",
                table: "Users",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<long>(
                name: "UserId",
                table: "Profiles",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.CreateIndex(
                name: "IX_Profiles_UserId",
                table: "Profiles",
                column: "UserId");

            migrationBuilder.AddForeignKey(
                name: "FK_Profiles_Users_UserId",
                table: "Profiles",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
