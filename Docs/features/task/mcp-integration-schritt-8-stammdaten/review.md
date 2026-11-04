# Review – EP-08 Stammdaten-Pflege (Issue #486)

## Umgesetzte Lieferung

### Neue Dateien
| Datei | Inhalt |
|---|---|
| `FinanceManager.Web/Mcp/McpMasterDataGuards.cs` | Geteilte Guard-Schicht: owner-scoped Referenzauflösung (vier Kategoriefamilien, BudgetPurpose/-Category, Attachment-Ziel-Decke), Plausibilitäten (Name/IBAN/http(s)-URL/base64 ≤512 KiB, Decimal/DateOnly), `RequireRecordWritableAsync` (Record-Filter auf Write-Rows), `ApprovalRequiredAsync` (Löschung-Scope → Denial, sonst strukturierte `Err_Mcp_ApprovalRequired`-Antwort mit Wirkungsbeschreibung + Parameter-Snapshot), Audit-Delta-Helfer |
| `FinanceManager.Web/Mcp/Tools/FmContactsManageTool.cs` | T22: create/update/addAlias/removeAlias direkt; merge+delete approval-required |
| `FinanceManager.Web/Mcp/Tools/FmAccountsManageTool.cs` | T23: create/update/addLinkedIban/removeLinkedIban direkt; delete approval-required |
| `FinanceManager.Web/Mcp/Tools/FmSavingsPlansManageTool.cs` | T24: create/update direkt; archive approval-required |
| `FinanceManager.Web/Mcp/Tools/FmSecuritiesManageTool.cs` | T25: create/update direkt; archive approval-required |
| `FinanceManager.Web/Mcp/Tools/FmBudgetsManageTool.cs` | T26: purpose/rule/override/category × create/update direkt; 4 deletes approval-required |
| `FinanceManager.Web/Mcp/Tools/FmCategoriesManageTool.cs` | T27: 4 Kategorietypen × create/update direkt; delete approval-required |
| `FinanceManager.Web/Mcp/Tools/FmAttachmentsManageTool.cs` | T28: upload/createUrl/createReference/update/setCategory/reassign direkt; delete approval-required |
| `FinanceManager.Web/Mcp/Tools/FmReportsConfigurationManageTool.cs` | T29: reportFavorite/homeKpi × create/update direkt; deletes approval-required |
| `FinanceManager.Tests/Mcp/McpMasterDataWriteToolsTests.cs` | 39 Tests (siehe test-results.md) |

### Geänderte Dateien
- `FinanceManager.Web/ProgramExtensions.cs` — 8 Descriptor- (Singleton) +
  8 Handler- (Scoped) Registrierungen.

## Spezifikations-Abdeckung
- **F-0801**: `fm_contacts_manage` (Create/Update/Alias, kein Merge/Löschen-Wirken)
  + `fm_categories_manage` (vier Kategorietypen, ohne Löschen); `(Owner,Name)`-/
  Service-Unique-Fehler via `Err_Mcp_InvalidArguments`.
- **F-0802**: `fm_accounts_manage` (ohne Löschen), `fm_savings_plans_manage`,
  `fm_securities_manage` (jeweils ohne Archiv-Wirken); Unter-IBAN-Pflege;
  Feld-Deltas im `DataChange`-Audit.
- **F-0803**: `fm_budgets_manage`, `fm_attachments_manage` (Upload mit
  sha256/Metadaten im Audit, Metadaten-/Kategorie-Korrektur, Reassign mit
  Ziel-Entitäts-Decke beider Enden), `fm_reports_configuration_manage`.
- **Sicherheitsklassen**: alle acht Tools `Schreibvorbereitend`; die
  genehmigungspflichtigen Teiloperationen sind innerhalb der Handler realisiert
  (Löschung-Scope-Prüfung → `Err_Mcp_ScopeDenied`, sonst strukturierte
  `Err_Mcp_ApprovalRequired`-Antwort, **keine** Service-Wirkung) — Deskriptoren
  tragen die Katalog-Union der Nicht-Löschung-Tupel, EP-09 hängt nur noch die
  Queue an `ApprovalRequiredAsync` an.
- **Audit-Kopplung**: jede direkte Wirkung staged `DataChange` + Feld-Deltas vor
  dem Service-Aufruf (atomarer Commit in geteiltem `AppDbContext`); `DiscardStaged`
  bei Fehlschlag; Upload-Payload = Metadaten + sha256, niemals Inhalt.
- **untrusted_data** auf Name/Description/Pattern/FileName/URL/Title/
  PurposePattern/ReportFavoriteName/ContractNumber/Region/Sektor/AlphaVantageCode.
- **404-Semantik**: fremde/fehlende Ziel-Ids liefern `{<entity>: null}` statt
  Fehler, wo die Operation auf einen Bestand zielt (update/alias/delete-Vorbereitung).
- **Kein Buchungspfad, kein AiActionRequest, kein DbContext/Repo in Handlern.**

## Offene Punkte / Folgeschritte
- EP-09: `AiActionRequest`-Queue an die approval-required-Antwort anschließen
  (Parameter-Snapshot liegt bereits in `parameters` vor; `RelatedRequestId`-Feld
  des Audit-Contracts vorgesehen).
- Attachment-Upload ist auf ≤512 KiB begrenzt (Envelope-Deckel); größere Dateien
  bleiben dem REST-Upload vorbehalten — im Descriptor dokumentiert.
- Ledger-Attachment-Kinds (Posting/StatementEntry/StatementImport/
  StatementDraftEntry) sind bewusst nicht via MCP pflegbar — sie fließen über die
  Buchungspipeline.
