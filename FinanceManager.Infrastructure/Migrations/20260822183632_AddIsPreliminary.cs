using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FinanceManager.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddIsPreliminary : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsPreliminary",
                table: "StatementDrafts",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsPreliminary",
                table: "Postings",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsPreliminary",
                table: "StatementDrafts");

            migrationBuilder.DropColumn(
                name: "IsPreliminary",
                table: "Postings");
        }
    }
}
