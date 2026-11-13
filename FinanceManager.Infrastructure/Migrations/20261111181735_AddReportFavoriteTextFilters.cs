using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FinanceManager.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddReportFavoriteTextFilters : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BankTextFiltersJson",
                table: "ReportFavorites",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ContactTextFiltersJson",
                table: "ReportFavorites",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SavingsPlanTextFiltersJson",
                table: "ReportFavorites",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SecurityTextFiltersJson",
                table: "ReportFavorites",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BankTextFiltersJson",
                table: "ReportFavorites");

            migrationBuilder.DropColumn(
                name: "ContactTextFiltersJson",
                table: "ReportFavorites");

            migrationBuilder.DropColumn(
                name: "SavingsPlanTextFiltersJson",
                table: "ReportFavorites");

            migrationBuilder.DropColumn(
                name: "SecurityTextFiltersJson",
                table: "ReportFavorites");
        }
    }
}
