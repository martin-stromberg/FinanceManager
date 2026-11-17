using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FinanceManager.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAiActionRequests : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AiActionRequests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    OwnerUserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    McpAccessId = table.Column<Guid>(type: "TEXT", nullable: false),
                    McpAccessName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    ToolName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    ActionKey = table.Column<string>(type: "TEXT", maxLength: 60, nullable: false),
                    CatalogVersion = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                    ParameterSnapshot = table.Column<string>(type: "TEXT", nullable: false),
                    ReasonText = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    Status = table.Column<short>(type: "INTEGER", nullable: false),
                    RequestedUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ExpiresUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    DecidedUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    DecidedByUserId = table.Column<Guid>(type: "TEXT", nullable: true),
                    DecisionNote = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    ExecutedUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ExecutionResult = table.Column<string>(type: "TEXT", nullable: true),
                    FailureReason = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    CorrelationId = table.Column<Guid>(type: "TEXT", nullable: false),
                    IdempotencyKey = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    ClaimToken = table.Column<Guid>(type: "TEXT", nullable: true),
                    ClaimedUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    AttemptCount = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 0),
                    CreatedUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ModifiedUtc = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AiActionRequests", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AiActionRequests_CorrelationId",
                table: "AiActionRequests",
                column: "CorrelationId");

            migrationBuilder.CreateIndex(
                name: "IX_AiActionRequests_McpAccessId",
                table: "AiActionRequests",
                column: "McpAccessId");

            migrationBuilder.CreateIndex(
                name: "IX_AiActionRequests_OwnerUserId_IdempotencyKey",
                table: "AiActionRequests",
                columns: new[] { "OwnerUserId", "IdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AiActionRequests_OwnerUserId_Status_RequestedUtc",
                table: "AiActionRequests",
                columns: new[] { "OwnerUserId", "Status", "RequestedUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_AiActionRequests_Status_ExpiresUtc",
                table: "AiActionRequests",
                columns: new[] { "Status", "ExpiresUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AiActionRequests");
        }
    }
}
