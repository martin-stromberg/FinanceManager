using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FinanceManager.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPostingReferenceCheckConstraint : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddCheckConstraint(
                name: "CK_Postings_HasReference",
                table: "Postings",
                sql: "[AccountId] IS NOT NULL OR [ContactId] IS NOT NULL OR [SavingsPlanId] IS NOT NULL OR [SecurityId] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Postings_HasReference",
                table: "Postings");
        }
    }
}
