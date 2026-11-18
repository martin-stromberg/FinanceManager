using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FinanceManager.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddProjectionSuppressionPostingIdentity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ReportSuppressedExpectations_OwnerUserId_ReportFavoriteId_Kind_EntityId_PriorYearDate",
                table: "ReportSuppressedExpectations");

            migrationBuilder.AddColumn<decimal>(
                name: "Amount",
                table: "ReportSuppressedExpectations",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "PostingId",
                table: "ReportSuppressedExpectations",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ReportSuppressedExpectations_OwnerUserId_ReportFavoriteId_Kind_EntityId_PriorYearDate",
                table: "ReportSuppressedExpectations",
                columns: new[] { "OwnerUserId", "ReportFavoriteId", "Kind", "EntityId", "PriorYearDate" },
                unique: true,
                filter: "[PostingId] IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ReportSuppressedExpectations_OwnerUserId_ReportFavoriteId_Kind_EntityId_PriorYearDate_PostingId",
                table: "ReportSuppressedExpectations",
                columns: new[] { "OwnerUserId", "ReportFavoriteId", "Kind", "EntityId", "PriorYearDate", "PostingId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ReportSuppressedExpectations_OwnerUserId_ReportFavoriteId_Kind_EntityId_PriorYearDate",
                table: "ReportSuppressedExpectations");

            migrationBuilder.DropIndex(
                name: "IX_ReportSuppressedExpectations_OwnerUserId_ReportFavoriteId_Kind_EntityId_PriorYearDate_PostingId",
                table: "ReportSuppressedExpectations");

            migrationBuilder.DropColumn(
                name: "Amount",
                table: "ReportSuppressedExpectations");

            migrationBuilder.DropColumn(
                name: "PostingId",
                table: "ReportSuppressedExpectations");

            migrationBuilder.CreateIndex(
                name: "IX_ReportSuppressedExpectations_OwnerUserId_ReportFavoriteId_Kind_EntityId_PriorYearDate",
                table: "ReportSuppressedExpectations",
                columns: new[] { "OwnerUserId", "ReportFavoriteId", "Kind", "EntityId", "PriorYearDate" },
                unique: true);
        }
    }
}
