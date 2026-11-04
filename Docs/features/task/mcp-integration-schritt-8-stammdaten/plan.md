# Umsetzungsplan – EP-08 Stammdaten-Pflege (Issue #486)

## Ansatz
Dünne Adapter über die `I*Service`-Contracts mit `ownerUserId` — kein eigener
Persistenzpfad, kein DbContext/Repo in Handlern. Acht neue Descriptor+Handler-Paare
im etablierten Muster (`FinanceManager.Web/Mcp/Tools/Fm*ManageTool.cs`), eine geteilte
Guard-Schicht (`McpMasterDataGuards`) und Wiederverwendung von `McpWriteAudit`
(StageDataChange/DiscardStaged) für die transaktionale Audit-Kopplung.

## Schritte

1. **`McpMasterDataGuards`** (neu, `Web/Mcp/`): owner-scoped Referenzauflösung
   (Kategorien aller vier Familien, BudgetPurpose/BudgetCategory, Attachment-Ziel
   via Resolver-Decke), Plausibilitäten (Name/IBAN/URL/base64-Upload, Decimal/
   DateOnly-Parsing), `RequireRecordWritableAsync` (Record-Filter auf die
   Write-Scope-Rows), `ApprovalRequiredAsync` (Löschung-Scope prüfen →
   `Err_Mcp_ScopeDenied`, sonst strukturierte `Err_Mcp_ApprovalRequired`-Antwort
   mit Wirkungsbeschreibung + Parameter-Snapshot), Audit-Delta-Helfer.
2. **T22 `FmContactsManageTool`**: create/update (Merge-Semantik) / addAlias /
   removeAlias direkt; merge+delete → approval-required. Self-Invarianten vorab.
3. **T23 `FmAccountsManageTool`**: create/update / addLinkedIban/removeLinkedIban
   direkt; delete → approval-required. Record-Filter-Dimension AccountType.
4. **T24 `FmSavingsPlansManageTool`**: create/update direkt; archive →
   approval-required (Archivierung = einziger Lösch-Modus; DeleteAsync nicht
   exponiert); ArchivedUtc blockt Updates.
5. **T25 `FmSecuritiesManageTool`**: create/update direkt (Identifier, Currency-
   Plausibilität); archive → approval-required; !IsActive blockt Updates.
6. **T26 `FmBudgetsManageTool`**: entity purpose/rule/override/category ×
   create/update direkt (Source-Auflösung, Regel-Plausibilität, BudgetPeriodKey);
   alle deletes → approval-required je Datentyp (Budgets für purpose+override,
   BudgetRules, BudgetCategories).
7. **T27 `FmCategoriesManageTool`**: categoryType × create/update direkt; delete →
   approval-required je Kategorietyp; eigene Kategorie-Id als Record-Klassifikation.
8. **T28 `FmAttachmentsManageTool`**: upload (base64 ≤512 KiB, sha256 im Audit),
   createUrl (http(s)), createReference (owner-scoped Master), update/setCategory,
   reassign mit Ziel-Entitäts-Decke; delete → approval-required.
9. **T29 `FmReportsConfigurationManageTool`**: reportFavorite/homeKpi ×
   create/update direkt (Filter-DTO, KPI-Kindvalidierung, Merge über DTOs);
   deletes → approval-required (Reports / KpiConfigurations).
10. **DI**: 8 Descriptoren (Singleton) + 8 Handler (Scoped) in `ProgramExtensions`.
11. **Tests** `McpMasterDataWriteToolsTests`: Katalogvertrag (8 Tools, Klasse,
    Scope-Tupel, Matrix-Validität), direkte Pfade inkl. Audit-Kopplung und
    untrusted_data, Fremd-Referenz-/Plausibilitäts-Denials vor Wirkung,
    Record-Scope-Denial, alle approval-required-Modi mit Mock-Verify
    `Times.Never` auf den destruktiven Services.
12. **Lifecycle-Artefakte** + Commit/Push.

## Entscheidungen
- **Deskriptor-Scopes = Katalog-Union ohne Löschung-Tupel**: die im Katalog §3.4
  aufgeführten Erstellung/Änderung/Vervollständigung/Lesen-Tupel werden deklariert
  (Dispatcher-AND); Löschung-Tupel bewusst nicht — sie würden die direkten
  Teiloperationen unnötig sperren und werden im Handler vor der
  approval-required-Antwort geprüft.
- **Approval-Antwort strukturiert statt ToolError**: `McpToolException` trägt den
  Code nicht maschinenlesbar in `structuredContent`; die Antwort
  `{requiresApproval, code:"Err_Mcp_ApprovalRequired", operation, effect,
  parameters, message}` ist für EP-09 direkt anschlussfähig (Queue statt Antwort).
- **Update = Merge über aktuellen Stand** (T18-Muster): nicht gesendete Felder
  bleiben erhalten; explizites `null` löscht nur nullable Felder.
- **SavingsPlan/Security**: `DeleteAsync` wird nicht exponiert (Katalog: Archiv ist
  das einzige Lösch-Äquivalent — Kante aus Schritt 1).
- **Attachments**: Upload-Deckel 512 KiB (JSON-RPC-Envelope = 1 MiB); Ziel-Kinds
  auf Stammdaten beschränkt; Reassign prüft beide Enden (`ReassignAsync` selbst
  validiert die Ziel-Entität nicht).
- **Budgets/Reports**: keine Record-Filter-Dimension — gefilterte Write-Rows
  verwehren konservativ (`RequireRecordWritableAsync` mit unklassifiziertem
  Record).
