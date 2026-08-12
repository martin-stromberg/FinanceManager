using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FinanceManager.Infrastructure.Data.Migrations.Identity
{
    /// <summary>
    /// Safety migration to add missing user import split settings columns to AspNetUsers
    /// for databases that were upgraded with an empty AddUserImportSplitSettings migration.
    /// </summary>
    public partial class FixUserImportSplitSettings : Migration
    {
        /// <summary>
        /// Applies the migration changes. Adds missing import split settings columns to the <c>AspNetUsers</c> table.
        /// </summary>
        /// <param name="migrationBuilder">The <see cref="MigrationBuilder"/> used to build operations.</param>
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Rebuild AspNetUsers with the full current schema, including all later user
            // columns (BenchmarkSecurityId, RiskFreeRate, ShowSharpeRatio, SymbolAttachmentId,
            // MassImportDialogPolicy, KnownContactAutoCreateEnabled) and the import split
            // settings. The INSERT only copies the columns that are guaranteed to exist before
            // this migration, so it works for both fresh and already-partially-upgraded databases.
            migrationBuilder.Sql(
@"
PRAGMA foreign_keys = 0;

BEGIN TRANSACTION;

CREATE TABLE ""ef_temp_AspNetUsers"" (
    ""Id"" TEXT NOT NULL CONSTRAINT ""PK_AspNetUsers"" PRIMARY KEY,
    ""AccessFailedCount"" INTEGER NOT NULL,
    ""Active"" INTEGER NOT NULL,
    ""AlphaVantageApiKey"" TEXT NULL,
    ""BenchmarkSecurityId"" TEXT NULL,
    ""ConcurrencyStamp"" TEXT NULL,
    ""Email"" TEXT NULL,
    ""EmailConfirmed"" INTEGER NOT NULL,
    ""HolidayCountryCode"" TEXT NULL,
    ""HolidayProviderKind"" INTEGER NOT NULL,
    ""HolidaySubdivisionCode"" TEXT NULL,
    ""ImportMaxEntriesPerDraft"" INTEGER NOT NULL DEFAULT 250,
    ""ImportMinEntriesPerDraft"" INTEGER NOT NULL DEFAULT 8,
    ""ImportMonthlySplitThreshold"" INTEGER,
    ""ImportSplitMode"" INTEGER NOT NULL DEFAULT 2,
    ""IsAdmin"" INTEGER NOT NULL,
    ""KnownContactAutoCreateEnabled"" INTEGER NOT NULL DEFAULT 1,
    ""LastLoginUtc"" TEXT NOT NULL,
    ""LockoutEnabled"" INTEGER NOT NULL,
    ""LockoutEnd"" TEXT NULL,
    ""MassImportDialogPolicy"" INTEGER NOT NULL DEFAULT 1,
    ""MonthlyReminderEnabled"" INTEGER NOT NULL,
    ""MonthlyReminderHour"" INTEGER NULL,
    ""MonthlyReminderMinute"" INTEGER NULL,
    ""NormalizedEmail"" TEXT NULL,
    ""NormalizedUserName"" TEXT NULL,
    ""PasswordHash"" TEXT NOT NULL,
    ""PhoneNumber"" TEXT NULL,
    ""PhoneNumberConfirmed"" INTEGER NOT NULL,
    ""PreferredLanguage"" TEXT NULL,
    ""RiskFreeRate"" TEXT NOT NULL DEFAULT '0.0',
    ""SecurityStamp"" TEXT NULL,
    ""ShareAlphaVantageApiKey"" INTEGER NOT NULL DEFAULT 0,
    ""ShowSharpeRatio"" INTEGER NOT NULL DEFAULT 0,
    ""SymbolAttachmentId"" TEXT NULL,
    ""TimeZoneId"" TEXT NULL,
    ""TwoFactorEnabled"" INTEGER NOT NULL,
    ""UserName"" TEXT NOT NULL,
    CONSTRAINT ""FK_AspNetUsers_Securities_BenchmarkSecurityId"" FOREIGN KEY (""BenchmarkSecurityId"") REFERENCES ""Securities"" (""Id"") ON DELETE SET NULL
);

INSERT INTO ""ef_temp_AspNetUsers"" (""Id"", ""AccessFailedCount"", ""Active"", ""AlphaVantageApiKey"", ""ConcurrencyStamp"", ""Email"", ""EmailConfirmed"", ""HolidayCountryCode"", ""HolidayProviderKind"", ""HolidaySubdivisionCode"", ""IsAdmin"", ""LastLoginUtc"", ""LockoutEnabled"", ""LockoutEnd"", ""MonthlyReminderEnabled"", ""MonthlyReminderHour"", ""MonthlyReminderMinute"", ""NormalizedEmail"", ""NormalizedUserName"", ""PasswordHash"", ""PhoneNumber"", ""PhoneNumberConfirmed"", ""PreferredLanguage"", ""SecurityStamp"", ""ShareAlphaVantageApiKey"", ""TimeZoneId"", ""TwoFactorEnabled"", ""UserName"")
SELECT ""Id"", ""AccessFailedCount"", ""Active"", ""AlphaVantageApiKey"", ""ConcurrencyStamp"", ""Email"", ""EmailConfirmed"", ""HolidayCountryCode"", ""HolidayProviderKind"", ""HolidaySubdivisionCode"", ""IsAdmin"", ""LastLoginUtc"", ""LockoutEnabled"", ""LockoutEnd"", ""MonthlyReminderEnabled"", ""MonthlyReminderHour"", ""MonthlyReminderMinute"", ""NormalizedEmail"", ""NormalizedUserName"", ""PasswordHash"", ""PhoneNumber"", ""PhoneNumberConfirmed"", ""PreferredLanguage"", ""SecurityStamp"", ""ShareAlphaVantageApiKey"", ""TimeZoneId"", ""TwoFactorEnabled"", ""UserName""
FROM ""AspNetUsers"";

DROP TABLE ""AspNetUsers"";

ALTER TABLE ""ef_temp_AspNetUsers"" RENAME TO ""AspNetUsers"";

CREATE INDEX ""EmailIndex"" ON ""AspNetUsers"" (""NormalizedEmail"");

CREATE INDEX ""IX_AspNetUsers_BenchmarkSecurityId"" ON ""AspNetUsers"" (""BenchmarkSecurityId"");

CREATE UNIQUE INDEX ""IX_AspNetUsers_UserName"" ON ""AspNetUsers"" (""UserName"");

CREATE UNIQUE INDEX ""UserNameIndex"" ON ""AspNetUsers"" (""NormalizedUserName"");

UPDATE ""AspNetUsers"" SET ""ImportMonthlySplitThreshold"" = ""ImportMaxEntriesPerDraft"" WHERE ""ImportMonthlySplitThreshold"" IS NULL;

COMMIT;

PRAGMA foreign_keys = 1;
",
            suppressTransaction: true);
        }

        /// <summary>
        /// Reverts the migration. Intentionally no-op to avoid dropping existing user data.
        /// </summary>
        /// <param name="migrationBuilder">The <see cref="MigrationBuilder"/> used to build operations.</param>
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Columns are intentionally left in place to preserve data.
        }
    }
}
