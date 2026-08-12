using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FinanceManager.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddRegionAndSectorToSecurity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Region",
                table: "Securities",
                type: "TEXT",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Sector",
                table: "Securities",
                type: "TEXT",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CacheValidUntilUtc",
                table: "ReportCacheEntries",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "PortfolioKpiConfigurations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    OwnerUserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ActiveTileIds = table.Column<string>(type: "TEXT", nullable: false),
                    TileOrder = table.Column<string>(type: "TEXT", nullable: false),
                    UpdatedUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ModifiedUtc = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PortfolioKpiConfigurations", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PortfolioKpiConfigurations_OwnerUserId",
                table: "PortfolioKpiConfigurations",
                column: "OwnerUserId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PortfolioKpiConfigurations");

            migrationBuilder.DropColumn(
                name: "Region",
                table: "Securities");

            migrationBuilder.DropColumn(
                name: "Sector",
                table: "Securities");

            migrationBuilder.DropColumn(
                name: "CacheValidUntilUtc",
                table: "ReportCacheEntries");
        }
    }
}
