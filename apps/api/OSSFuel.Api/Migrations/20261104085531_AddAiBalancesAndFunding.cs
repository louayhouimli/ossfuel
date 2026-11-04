using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OSSFuel.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddAiBalancesAndFunding : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AIBalances",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    AmountMinor = table.Column<long>(type: "bigint", nullable: false),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AIBalances", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AIBalances_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Fundings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    SponsorUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    AmountMinor = table.Column<long>(type: "bigint", nullable: false),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Provider = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ProviderCheckoutSessionId = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    ProviderCheckoutUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ProviderPaymentIntentId = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    FailureReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Fundings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Fundings_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Fundings_Users_SponsorUserId",
                        column: x => x.SponsorUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PaymentEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Provider = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ProviderEventId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ProviderObjectId = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    Outcome = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Detail = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ReceivedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaymentEvents", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "BalanceTransactions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AIBalanceId = table.Column<Guid>(type: "uuid", nullable: false),
                    FundingId = table.Column<Guid>(type: "uuid", nullable: true),
                    AmountMinor = table.Column<long>(type: "bigint", nullable: false),
                    Type = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Description = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BalanceTransactions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BalanceTransactions_AIBalances_AIBalanceId",
                        column: x => x.AIBalanceId,
                        principalTable: "AIBalances",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AIBalances_ProjectId",
                table: "AIBalances",
                column: "ProjectId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BalanceTransactions_AIBalanceId_CreatedAt",
                table: "BalanceTransactions",
                columns: new[] { "AIBalanceId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_BalanceTransactions_FundingId",
                table: "BalanceTransactions",
                column: "FundingId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Fundings_ProjectId",
                table: "Fundings",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_Fundings_ProviderCheckoutSessionId",
                table: "Fundings",
                column: "ProviderCheckoutSessionId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Fundings_SponsorUserId_CreatedAt",
                table: "Fundings",
                columns: new[] { "SponsorUserId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Fundings_SponsorUserId_ProjectId",
                table: "Fundings",
                columns: new[] { "SponsorUserId", "ProjectId" },
                unique: true,
                filter: "\"Status\" = 'pending'");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentEvents_ProviderEventId",
                table: "PaymentEvents",
                column: "ProviderEventId",
                unique: true);

            // Projects imported before balances existed still need a row, or their
            // page has nothing to read. New imports open the balance themselves.
            migrationBuilder.Sql(
                """
                INSERT INTO "AIBalances" ("Id", "ProjectId", "AmountMinor", "Currency", "CreatedAt", "UpdatedAt")
                SELECT gen_random_uuid(), "Id", 0, 'usd', now(), now() FROM "Projects";
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BalanceTransactions");

            migrationBuilder.DropTable(
                name: "Fundings");

            migrationBuilder.DropTable(
                name: "PaymentEvents");

            migrationBuilder.DropTable(
                name: "AIBalances");
        }
    }
}
