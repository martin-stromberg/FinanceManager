using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FinanceManager.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPostingReversalFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ReversalForPostingId",
                table: "Postings",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ReversedAtUtc",
                table: "Postings",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ReversedByPostingId",
                table: "Postings",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ReversedByUserId",
                table: "Postings",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ReversalForPostingId",
                table: "Postings");

            migrationBuilder.DropColumn(
                name: "ReversedAtUtc",
                table: "Postings");

            migrationBuilder.DropColumn(
                name: "ReversedByPostingId",
                table: "Postings");

            migrationBuilder.DropColumn(
                name: "ReversedByUserId",
                table: "Postings");
        }
    }
}
