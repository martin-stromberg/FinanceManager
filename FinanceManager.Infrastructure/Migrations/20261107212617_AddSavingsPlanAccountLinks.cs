using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FinanceManager.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSavingsPlanAccountLinks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SavingsPlanAccountLinks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    SavingsPlanId = table.Column<Guid>(type: "TEXT", nullable: false),
                    AccountId = table.Column<Guid>(type: "TEXT", nullable: false),
                    OwnerUserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ModifiedUtc = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SavingsPlanAccountLinks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SavingsPlanAccountLinks_Accounts_AccountId",
                        column: x => x.AccountId,
                        principalTable: "Accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SavingsPlanAccountLinks_SavingsPlans_SavingsPlanId",
                        column: x => x.SavingsPlanId,
                        principalTable: "SavingsPlans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SavingsPlanAccountLinks_AccountId",
                table: "SavingsPlanAccountLinks",
                column: "AccountId");

            migrationBuilder.CreateIndex(
                name: "IX_SavingsPlanAccountLinks_OwnerUserId",
                table: "SavingsPlanAccountLinks",
                column: "OwnerUserId");

            migrationBuilder.CreateIndex(
                name: "IX_SavingsPlanAccountLinks_SavingsPlanId_AccountId",
                table: "SavingsPlanAccountLinks",
                columns: new[] { "SavingsPlanId", "AccountId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SavingsPlanAccountLinks");
        }
    }
}
