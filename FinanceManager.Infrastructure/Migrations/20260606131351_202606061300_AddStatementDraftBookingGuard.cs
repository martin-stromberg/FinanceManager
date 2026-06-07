using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FinanceManager.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class _202606061300_AddStatementDraftBookingGuard : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "StatementDraftBookingGuards",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    OwnerUserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    DraftId = table.Column<Guid>(type: "TEXT", nullable: false),
                    LockToken = table.Column<Guid>(type: "TEXT", nullable: false),
                    AcquiredUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ExpiresUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StatementDraftBookingGuards", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StatementDraftBookingGuards_StatementDrafts_DraftId",
                        column: x => x.DraftId,
                        principalTable: "StatementDrafts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_StatementDraftBookingGuards_DraftId",
                table: "StatementDraftBookingGuards",
                column: "DraftId");

            migrationBuilder.CreateIndex(
                name: "IX_StatementDraftBookingGuards_ExpiresUtc",
                table: "StatementDraftBookingGuards",
                column: "ExpiresUtc");

            migrationBuilder.CreateIndex(
                name: "IX_StatementDraftBookingGuards_OwnerUserId_DraftId",
                table: "StatementDraftBookingGuards",
                columns: new[] { "OwnerUserId", "DraftId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "StatementDraftBookingGuards");
        }
    }
}
