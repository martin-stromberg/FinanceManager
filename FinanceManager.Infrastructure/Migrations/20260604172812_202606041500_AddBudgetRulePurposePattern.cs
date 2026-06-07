using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FinanceManager.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class _202606041500_AddBudgetRulePurposePattern : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PurposePattern",
                table: "BudgetRules",
                type: "TEXT",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "PurposePatternIsRegex",
                table: "BudgetRules",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PurposePattern",
                table: "BudgetRules");

            migrationBuilder.DropColumn(
                name: "PurposePatternIsRegex",
                table: "BudgetRules");
        }
    }
}
