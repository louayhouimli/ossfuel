using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OSSFuel.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddApiKeysAndAIGateway : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "AIUsageId",
                table: "BalanceTransactions",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ProjectApiKeys",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    KeyPrefix = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    KeyHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastUsedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    RevokedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectApiKeys", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProjectApiKeys_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProjectApiKeys_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AIUsages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    ApiKeyId = table.Column<Guid>(type: "uuid", nullable: false),
                    Model = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Provider = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    PromptTokens = table.Column<int>(type: "integer", nullable: false),
                    CompletionTokens = table.Column<int>(type: "integer", nullable: false),
                    TotalTokens = table.Column<int>(type: "integer", nullable: false),
                    CostMinor = table.Column<long>(type: "bigint", nullable: false),
                    Status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AIUsages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AIUsages_ProjectApiKeys_ApiKeyId",
                        column: x => x.ApiKeyId,
                        principalTable: "ProjectApiKeys",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AIUsages_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BalanceTransactions_AIUsageId",
                table: "BalanceTransactions",
                column: "AIUsageId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AIUsages_ApiKeyId_CreatedAt",
                table: "AIUsages",
                columns: new[] { "ApiKeyId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AIUsages_ProjectId_CreatedAt",
                table: "AIUsages",
                columns: new[] { "ProjectId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectApiKeys_CreatedByUserId",
                table: "ProjectApiKeys",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectApiKeys_KeyHash",
                table: "ProjectApiKeys",
                column: "KeyHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProjectApiKeys_ProjectId_CreatedAt",
                table: "ProjectApiKeys",
                columns: new[] { "ProjectId", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AIUsages");

            migrationBuilder.DropTable(
                name: "ProjectApiKeys");

            migrationBuilder.DropIndex(
                name: "IX_BalanceTransactions_AIUsageId",
                table: "BalanceTransactions");

            migrationBuilder.DropColumn(
                name: "AIUsageId",
                table: "BalanceTransactions");
        }
    }
}
