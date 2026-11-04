using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FinanceManager.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddMcpFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "McpAccesses",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    OwnerUserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    AccessKeyHint = table.Column<string>(type: "TEXT", maxLength: 12, nullable: false),
                    TokenHash = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    Level = table.Column<short>(type: "INTEGER", nullable: false),
                    ApprovalMode = table.Column<short>(type: "INTEGER", nullable: false),
                    Status = table.Column<short>(type: "INTEGER", nullable: false, defaultValue: (short)0),
                    ExpiresUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    AllowedIpRanges = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    LastUsedUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    TokenIssuedUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    RevokedUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    RevocationReason = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    CreatedUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ModifiedUtc = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_McpAccesses", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "McpAuditEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Utc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    SequenceNo = table.Column<long>(type: "INTEGER", nullable: false),
                    Category = table.Column<short>(type: "INTEGER", nullable: false),
                    EventType = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    OwnerUserId = table.Column<Guid>(type: "TEXT", nullable: true),
                    McpAccessId = table.Column<Guid>(type: "TEXT", nullable: true),
                    McpAccessName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    ToolName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    CatalogVersion = table.Column<string>(type: "TEXT", maxLength: 40, nullable: true),
                    Context = table.Column<string>(type: "TEXT", maxLength: 40, nullable: true),
                    ResultStatus = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    CorrelationId = table.Column<Guid>(type: "TEXT", nullable: true),
                    SourceIp = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    DurationMs = table.Column<int>(type: "INTEGER", nullable: true),
                    AffectedCount = table.Column<int>(type: "INTEGER", nullable: true),
                    PayloadJson = table.Column<string>(type: "TEXT", nullable: true),
                    RelatedRequestId = table.Column<Guid>(type: "TEXT", nullable: true),
                    PrevHash = table.Column<string>(type: "TEXT", maxLength: 128, nullable: true),
                    EntryHash = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ModifiedUtc = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_McpAuditEntries", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "McpAccessScopes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    McpAccessId = table.Column<Guid>(type: "TEXT", nullable: false),
                    OwnerUserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    DataType = table.Column<short>(type: "INTEGER", nullable: false),
                    Right = table.Column<short>(type: "INTEGER", nullable: false),
                    AccountTypesCsv = table.Column<string>(type: "TEXT", nullable: true),
                    ContactCategoryIdsCsv = table.Column<string>(type: "TEXT", nullable: true),
                    SecurityCategoryIdsCsv = table.Column<string>(type: "TEXT", nullable: true),
                    SavingsPlanCategoryIdsCsv = table.Column<string>(type: "TEXT", nullable: true),
                    AttachmentCategoryIdsCsv = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ModifiedUtc = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_McpAccessScopes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_McpAccessScopes_McpAccesses_McpAccessId",
                        column: x => x.McpAccessId,
                        principalTable: "McpAccesses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "McpAuditEntryChanges",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    EntryId = table.Column<Guid>(type: "TEXT", nullable: false),
                    EntityType = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    EntityId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Field = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    OldValue = table.Column<string>(type: "TEXT", nullable: true),
                    NewValue = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ModifiedUtc = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_McpAuditEntryChanges", x => x.Id);
                    table.ForeignKey(
                        name: "FK_McpAuditEntryChanges_McpAuditEntries_EntryId",
                        column: x => x.EntryId,
                        principalTable: "McpAuditEntries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_McpAccesses_AccessKeyHint",
                table: "McpAccesses",
                column: "AccessKeyHint",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_McpAccesses_ExpiresUtc",
                table: "McpAccesses",
                column: "ExpiresUtc");

            migrationBuilder.CreateIndex(
                name: "IX_McpAccesses_OwnerUserId_Name",
                table: "McpAccesses",
                columns: new[] { "OwnerUserId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_McpAccesses_OwnerUserId_Status",
                table: "McpAccesses",
                columns: new[] { "OwnerUserId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_McpAccessScopes_McpAccessId_DataType_Right",
                table: "McpAccessScopes",
                columns: new[] { "McpAccessId", "DataType", "Right" });

            migrationBuilder.CreateIndex(
                name: "IX_McpAccessScopes_OwnerUserId",
                table: "McpAccessScopes",
                column: "OwnerUserId");

            migrationBuilder.CreateIndex(
                name: "IX_McpAuditEntries_Category_EventType",
                table: "McpAuditEntries",
                columns: new[] { "Category", "EventType" });

            migrationBuilder.CreateIndex(
                name: "IX_McpAuditEntries_CorrelationId",
                table: "McpAuditEntries",
                column: "CorrelationId");

            migrationBuilder.CreateIndex(
                name: "IX_McpAuditEntries_McpAccessId",
                table: "McpAuditEntries",
                column: "McpAccessId");

            migrationBuilder.CreateIndex(
                name: "IX_McpAuditEntries_OwnerUserId_SequenceNo",
                table: "McpAuditEntries",
                columns: new[] { "OwnerUserId", "SequenceNo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_McpAuditEntries_OwnerUserId_Utc",
                table: "McpAuditEntries",
                columns: new[] { "OwnerUserId", "Utc" });

            migrationBuilder.CreateIndex(
                name: "IX_McpAuditEntryChanges_EntityType_EntityId",
                table: "McpAuditEntryChanges",
                columns: new[] { "EntityType", "EntityId" });

            migrationBuilder.CreateIndex(
                name: "IX_McpAuditEntryChanges_EntryId",
                table: "McpAuditEntryChanges",
                column: "EntryId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "McpAccessScopes");

            migrationBuilder.DropTable(
                name: "McpAuditEntryChanges");

            migrationBuilder.DropTable(
                name: "McpAccesses");

            migrationBuilder.DropTable(
                name: "McpAuditEntries");
        }
    }
}
