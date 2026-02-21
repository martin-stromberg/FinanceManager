using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FinanceManager.Infrastructure.Data.Migrations.Identity
{
    /// <inheritdoc />
    public partial class _20260114_budget_rule_category_scope : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<Guid>(
                name: "BudgetPurposeId",
                table: "BudgetRules",
                type: "TEXT",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "TEXT");

            migrationBuilder.AddColumn<Guid>(
                name: "BudgetCategoryId",
                table: "BudgetRules",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_BudgetRules_BudgetCategoryId_StartDate",
                table: "BudgetRules",
                columns: new[] { "BudgetCategoryId", "StartDate" });

            migrationBuilder.CreateIndex(
                name: "IX_BudgetRules_OwnerUserId_BudgetCategoryId",
                table: "BudgetRules",
                columns: new[] { "OwnerUserId", "BudgetCategoryId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_BudgetRules_BudgetCategoryId_StartDate",
                table: "BudgetRules");

            migrationBuilder.DropIndex(
                name: "IX_BudgetRules_OwnerUserId_BudgetCategoryId",
                table: "BudgetRules");

            migrationBuilder.DropColumn(
                name: "BudgetCategoryId",
                table: "BudgetRules");

            migrationBuilder.AlterColumn<Guid>(
                name: "BudgetPurposeId",
                table: "BudgetRules",
                type: "TEXT",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "TEXT",
                oldNullable: true);
        }
    }
}
