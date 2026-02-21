using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FinanceManager.Infrastructure.Data.Migrations.Identity
{
    /// <inheritdoc />
    public partial class AddBudgetCategories : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "BudgetCategoryId",
                table: "BudgetPurposes",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "BudgetCategories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    OwnerUserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 150, nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ModifiedUtc = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BudgetCategories", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BudgetPurposes_BudgetCategoryId",
                table: "BudgetPurposes",
                column: "BudgetCategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_BudgetCategories_OwnerUserId_Name",
                table: "BudgetCategories",
                columns: new[] { "OwnerUserId", "Name" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_BudgetPurposes_BudgetCategories_BudgetCategoryId",
                table: "BudgetPurposes",
                column: "BudgetCategoryId",
                principalTable: "BudgetCategories",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_BudgetPurposes_BudgetCategories_BudgetCategoryId",
                table: "BudgetPurposes");

            migrationBuilder.DropTable(
                name: "BudgetCategories");

            migrationBuilder.DropIndex(
                name: "IX_BudgetPurposes_BudgetCategoryId",
                table: "BudgetPurposes");

            migrationBuilder.DropColumn(
                name: "BudgetCategoryId",
                table: "BudgetPurposes");
        }
    }
}
