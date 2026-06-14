using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Platform.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddLoginTokens : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "login_tokens",
                columns: table => new
                {
                    TokenHash = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    Email = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    ExpiresAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    ConsumedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_login_tokens", x => x.TokenHash);
                });

            migrationBuilder.CreateIndex(
                name: "IX_login_tokens_Email",
                table: "login_tokens",
                column: "Email");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "login_tokens");
        }
    }
}
