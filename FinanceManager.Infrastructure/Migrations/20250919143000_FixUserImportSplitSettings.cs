using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FinanceManager.Infrastructure.Migrations
{
    /// <summary>
    /// Safety migration to (re)apply missing user import split settings columns if they were not created previously.
    /// Uses raw SQL with IF NOT EXISTS to avoid duplicate column errors in SQLite.
    /// </summary>
    public partial class FixUserImportSplitSettings : Migration
    {
        /// <summary>
        /// Applies the migration changes. Adds missing import split settings columns to the <c>Users</c> table
        /// if they do not already exist and initializes the monthly threshold where it is null.
        /// </summary>
        /// <param name="migrationBuilder">The <see cref="MigrationBuilder"/> used to build operations.</param>
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // SQLite: ADD COLUMN IF NOT EXISTS is supported on modern SQLite versions shipped with .NET.
            migrationBuilder.Sql("ALTER TABLE Users ADD COLUMN IF NOT EXISTS ImportSplitMode INTEGER NOT NULL DEFAULT 2;");
            migrationBuilder.Sql("ALTER TABLE Users ADD COLUMN IF NOT EXISTS ImportMaxEntriesPerDraft INTEGER NOT NULL DEFAULT 250;");
            migrationBuilder.Sql("ALTER TABLE Users ADD COLUMN IF NOT EXISTS ImportMonthlySplitThreshold INTEGER NULL;");

            // Initialize threshold to max where still NULL
            migrationBuilder.Sql("UPDATE Users SET ImportMonthlySplitThreshold = ImportMaxEntriesPerDraft WHERE ImportMonthlySplitThreshold IS NULL;");
        }

        /// <summary>
        /// Reverts the migration. This migration intentionally does not remove added columns because dropping columns
        /// in SQLite requires table rebuild and could cause data loss. The method is left as a no-op to avoid
        /// unintended destructive operations.
        /// </summary>
        /// <param name="migrationBuilder">The <see cref="MigrationBuilder"/> used to build operations.</param>
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // SQLite cannot drop columns easily without table rebuild; leaving columns in place.
            // Intentionally no-op to avoid data loss / complexity.
        }
    }
}
