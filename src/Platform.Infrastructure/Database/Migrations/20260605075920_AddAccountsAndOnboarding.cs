using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Platform.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddAccountsAndOnboarding : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Environment",
                table: "projects",
                type: "TEXT",
                maxLength: 20,
                nullable: false,
                defaultValue: "Development");

            migrationBuilder.AddColumn<string>(
                name: "GameType",
                table: "projects",
                type: "TEXT",
                maxLength: 40,
                nullable: false,
                defaultValue: "Unspecified");

            migrationBuilder.AddColumn<string>(
                name: "MultiplayerMode",
                table: "projects",
                type: "TEXT",
                maxLength: 40,
                nullable: false,
                defaultValue: "Unspecified");

            migrationBuilder.AddColumn<string>(
                name: "OrganizationId",
                table: "projects",
                type: "TEXT",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PersistenceFeaturesCsv",
                table: "projects",
                type: "TEXT",
                maxLength: 256,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Slug",
                table: "projects",
                type: "TEXT",
                maxLength: 120,
                nullable: false,
                defaultValue: "project");

            migrationBuilder.AddColumn<string>(
                name: "TargetPlatform",
                table: "projects",
                type: "TEXT",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "UpdatedAtUtc",
                table: "projects",
                type: "TEXT",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.CreateTable(
                name: "organizations",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    OwnerUserId = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_organizations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "user_sessions",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    UserId = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    ExpiresAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    LastSeenAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_sessions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "users",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    Email = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    DisplayName = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                    PasswordHash = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    LastLoginAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_users", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_projects_OrganizationId",
                table: "projects",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_organizations_OwnerUserId",
                table: "organizations",
                column: "OwnerUserId");

            migrationBuilder.CreateIndex(
                name: "IX_user_sessions_UserId",
                table: "user_sessions",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_users_Email",
                table: "users",
                column: "Email",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "organizations");

            migrationBuilder.DropTable(
                name: "user_sessions");

            migrationBuilder.DropTable(
                name: "users");

            migrationBuilder.DropIndex(
                name: "IX_projects_OrganizationId",
                table: "projects");

            migrationBuilder.DropColumn(
                name: "Environment",
                table: "projects");

            migrationBuilder.DropColumn(
                name: "GameType",
                table: "projects");

            migrationBuilder.DropColumn(
                name: "MultiplayerMode",
                table: "projects");

            migrationBuilder.DropColumn(
                name: "OrganizationId",
                table: "projects");

            migrationBuilder.DropColumn(
                name: "PersistenceFeaturesCsv",
                table: "projects");

            migrationBuilder.DropColumn(
                name: "Slug",
                table: "projects");

            migrationBuilder.DropColumn(
                name: "TargetPlatform",
                table: "projects");

            migrationBuilder.DropColumn(
                name: "UpdatedAtUtc",
                table: "projects");
        }
    }
}
