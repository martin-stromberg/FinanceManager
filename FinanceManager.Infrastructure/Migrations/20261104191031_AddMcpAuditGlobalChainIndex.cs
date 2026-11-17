using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FinanceManager.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddMcpAuditGlobalChainIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_McpAuditEntries_SequenceNo",
                table: "McpAuditEntries",
                column: "SequenceNo",
                unique: true,
                filter: "[OwnerUserId] IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_McpAuditEntries_SequenceNo",
                table: "McpAuditEntries");
        }
    }
}
