using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FinanceManager.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddReturnAnalysisSettingsToUser : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "BenchmarkSecurityId",
                table: "AspNetUsers",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "RiskFreeRate",
                table: "AspNetUsers",
                type: "TEXT",
                precision: 18,
                scale: 6,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<bool>(
                name: "ShowSharpeRatio",
                table: "AspNetUsers",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_Postings_SecurityId_BookingDate",
                table: "Postings",
                columns: new[] { "SecurityId", "BookingDate" });

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUsers_BenchmarkSecurityId",
                table: "AspNetUsers",
                column: "BenchmarkSecurityId");

            migrationBuilder.AddForeignKey(
                name: "FK_AspNetUsers_Securities_BenchmarkSecurityId",
                table: "AspNetUsers",
                column: "BenchmarkSecurityId",
                principalTable: "Securities",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AspNetUsers_Securities_BenchmarkSecurityId",
                table: "AspNetUsers");

            migrationBuilder.DropIndex(
                name: "IX_Postings_SecurityId_BookingDate",
                table: "Postings");

            migrationBuilder.DropIndex(
                name: "IX_AspNetUsers_BenchmarkSecurityId",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "BenchmarkSecurityId",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "RiskFreeRate",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "ShowSharpeRatio",
                table: "AspNetUsers");
        }
    }
}
