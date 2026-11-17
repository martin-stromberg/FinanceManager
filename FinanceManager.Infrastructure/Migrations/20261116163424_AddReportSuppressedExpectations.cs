using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FinanceManager.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddReportSuppressedExpectations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ReportSuppressedExpectations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    OwnerUserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ReportFavoriteId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Kind = table.Column<int>(type: "INTEGER", nullable: false),
                    EntityId = table.Column<Guid>(type: "TEXT", nullable: false),
                    PriorYearDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    SuppressedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ModifiedUtc = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReportSuppressedExpectations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReportSuppressedExpectations_ReportFavorites_ReportFavoriteId",
                        column: x => x.ReportFavoriteId,
                        principalTable: "ReportFavorites",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ReportSuppressedExpectations_OwnerUserId_ReportFavoriteId_Kind_EntityId_PriorYearDate",
                table: "ReportSuppressedExpectations",
                columns: new[] { "OwnerUserId", "ReportFavoriteId", "Kind", "EntityId", "PriorYearDate" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ReportSuppressedExpectations_ReportFavoriteId",
                table: "ReportSuppressedExpectations",
                column: "ReportFavoriteId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ReportSuppressedExpectations");
        }
    }
}
