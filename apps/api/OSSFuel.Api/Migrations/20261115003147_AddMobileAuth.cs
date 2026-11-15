using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OSSFuel.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddMobileAuth : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MobileAuthCodes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CodeHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MobileAuthCodes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MobileAuthCodes_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MobileAuthCodes_CodeHash",
                table: "MobileAuthCodes",
                column: "CodeHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MobileAuthCodes_ExpiresAt",
                table: "MobileAuthCodes",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_MobileAuthCodes_UserId",
                table: "MobileAuthCodes",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MobileAuthCodes");
        }
    }
}
