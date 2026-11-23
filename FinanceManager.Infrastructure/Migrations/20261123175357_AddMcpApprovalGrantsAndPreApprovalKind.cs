using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FinanceManager.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddMcpApprovalGrantsAndPreApprovalKind : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<short>(
                name: "PreApprovalKind",
                table: "AiActionRequests",
                type: "INTEGER",
                nullable: false,
                defaultValue: (short)0);

            migrationBuilder.CreateTable(
                name: "McpAccessApprovalGrants",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    McpAccessId = table.Column<Guid>(type: "TEXT", nullable: false),
                    OwnerUserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    DataType = table.Column<short>(type: "INTEGER", nullable: false),
                    Right = table.Column<short>(type: "INTEGER", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ModifiedUtc = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_McpAccessApprovalGrants", x => x.Id);
                    table.ForeignKey(
                        name: "FK_McpAccessApprovalGrants_McpAccesses_McpAccessId",
                        column: x => x.McpAccessId,
                        principalTable: "McpAccesses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_McpAccessApprovalGrants_McpAccessId_DataType_Right",
                table: "McpAccessApprovalGrants",
                columns: new[] { "McpAccessId", "DataType", "Right" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_McpAccessApprovalGrants_OwnerUserId",
                table: "McpAccessApprovalGrants",
                column: "OwnerUserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "McpAccessApprovalGrants");

            migrationBuilder.DropColumn(
                name: "PreApprovalKind",
                table: "AiActionRequests");
        }
    }
}
