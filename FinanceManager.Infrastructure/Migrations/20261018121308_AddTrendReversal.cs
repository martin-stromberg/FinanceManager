using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FinanceManager.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTrendReversal : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<short>(
                name: "TrendReversalNotifiedStatus",
                table: "Securities",
                type: "INTEGER",
                nullable: false,
                defaultValue: (short)1);

            migrationBuilder.AddColumn<bool>(
                name: "TrendReversalNotificationsEnabled",
                table: "AspNetUsers",
                type: "INTEGER",
                nullable: false,
                defaultValue: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TrendReversalNotifiedStatus",
                table: "Securities");

            migrationBuilder.DropColumn(
                name: "TrendReversalNotificationsEnabled",
                table: "AspNetUsers");
        }
    }
}
