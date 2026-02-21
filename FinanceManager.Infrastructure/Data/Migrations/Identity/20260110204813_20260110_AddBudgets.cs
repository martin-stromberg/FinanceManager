using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FinanceManager.Infrastructure.Data.Migrations.Identity
{
    /// <inheritdoc />
    public partial class _20260110_AddBudgets : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BudgetOverrides",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    OwnerUserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    BudgetPurposeId = table.Column<Guid>(type: "TEXT", nullable: false),
                    PeriodYear = table.Column<int>(type: "INTEGER", nullable: false),
                    PeriodMonth = table.Column<int>(type: "INTEGER", nullable: false),
                    Amount = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ModifiedUtc = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BudgetOverrides", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "BudgetPurposes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    OwnerUserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 150, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    SourceType = table.Column<short>(type: "INTEGER", nullable: false),
                    SourceId = table.Column<Guid>(type: "TEXT", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ModifiedUtc = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BudgetPurposes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "BudgetRules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    OwnerUserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    BudgetPurposeId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Amount = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    Interval = table.Column<short>(type: "INTEGER", nullable: false),
                    CustomIntervalMonths = table.Column<int>(type: "INTEGER", nullable: true),
                    StartDate = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    EndDate = table.Column<DateOnly>(type: "TEXT", nullable: true),
                    CreatedUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ModifiedUtc = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BudgetRules", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BudgetOverrides_OwnerUserId_BudgetPurposeId_PeriodYear_PeriodMonth",
                table: "BudgetOverrides",
                columns: new[] { "OwnerUserId", "BudgetPurposeId", "PeriodYear", "PeriodMonth" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BudgetPurposes_OwnerUserId_Name",
                table: "BudgetPurposes",
                columns: new[] { "OwnerUserId", "Name" });

            migrationBuilder.CreateIndex(
                name: "IX_BudgetPurposes_OwnerUserId_SourceType_SourceId",
                table: "BudgetPurposes",
                columns: new[] { "OwnerUserId", "SourceType", "SourceId" });

            migrationBuilder.CreateIndex(
                name: "IX_BudgetRules_BudgetPurposeId_StartDate",
                table: "BudgetRules",
                columns: new[] { "BudgetPurposeId", "StartDate" });

            migrationBuilder.CreateIndex(
                name: "IX_BudgetRules_OwnerUserId_BudgetPurposeId",
                table: "BudgetRules",
                columns: new[] { "OwnerUserId", "BudgetPurposeId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BudgetOverrides");

            migrationBuilder.DropTable(
                name: "BudgetPurposes");

            migrationBuilder.DropTable(
                name: "BudgetRules");
        }
    }
}
