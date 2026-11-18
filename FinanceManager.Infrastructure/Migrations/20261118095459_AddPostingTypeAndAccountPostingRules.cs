using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FinanceManager.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPostingTypeAndAccountPostingRules : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsPostingTypeManual",
                table: "StatementDraftEntries",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "PostingType",
                table: "StatementDraftEntries",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "PostingType",
                table: "Postings",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "AccountPostingRules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    AccountId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Field = table.Column<int>(type: "INTEGER", nullable: false),
                    Pattern = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    PostingType = table.Column<int>(type: "INTEGER", nullable: false),
                    OrderIndex = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ModifiedUtc = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AccountPostingRules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AccountPostingRules_Accounts_AccountId",
                        column: x => x.AccountId,
                        principalTable: "Accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AccountPostingRules_AccountId",
                table: "AccountPostingRules",
                column: "AccountId");

            // Backfill existing rows: derive the posting type from the amount sign.
            // Bank postings only (Kind = 0): negative amount -> Outflow (2), otherwise Inflow (1).
            migrationBuilder.Sql(
                "UPDATE [Postings] SET [PostingType] = CASE WHEN [Amount] < 0 THEN 2 ELSE 1 END WHERE [Kind] = 0;");
            migrationBuilder.Sql(
                "UPDATE [StatementDraftEntries] SET [PostingType] = CASE WHEN [Amount] < 0 THEN 2 ELSE 1 END;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AccountPostingRules");

            migrationBuilder.DropColumn(
                name: "IsPostingTypeManual",
                table: "StatementDraftEntries");

            migrationBuilder.DropColumn(
                name: "PostingType",
                table: "StatementDraftEntries");

            migrationBuilder.DropColumn(
                name: "PostingType",
                table: "Postings");
        }
    }
}
