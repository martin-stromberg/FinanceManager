# Umsetzungsplan – EP-07 Kontoauszugs-Assistent (Issue #485)

## Ansatz
Dünne Adapter über `IStatementDraftService`/`IMassImportOrchestrator` — kein eigener
Persistenz- oder Buchungspfad. Drei neue Descriptor+Handler-Paare im etablierten
Muster (`FinanceManager.Web/Mcp/Tools/Fm*Tool.cs`), eine geteilte Zusatzvalidierung
(F-0704) und ein Audit-Helfer für die transaktionale `DataChange`-Kopplung.

## Schritte

1. **`McpWriteAudit`** (neu, `Web/Mcp/`): `StageDataChangeAsync` (staged
   `DataChange`-Eintrag + Feld-Deltas in den geteilten `AppDbContext`, wird mit dem
   fachlichen `SaveChangesAsync` des Services atomar committet) + `DiscardStaged`
   (detached das Bündel, wenn die Wirkung vor dem Save scheitert — sonst würde der
   Dispatcher-ToolCall-Save ein Wirkungsloses Audit mitschreiben).
2. **`McpStatementDraftGuards`** (neu, `Web/Mcp/`): F-0704-Validierung —
   `RequireContact/SavingsPlan/Security/AccountAsync` (owner-scoped, fremd = nicht
   gefunden → `Err_Mcp_InvalidArguments`; archivierte/inaktive Ziele abgelehnt),
   `ValidateEntryCore` (Betrag, Datumstoleranz, Textlängen, Currency),
   `ValidateSavingsPlanAssignment`/`ValidateSecurityAssignment` (Pipeline-
   Nebenbedingungen 1:1 gespiegelt aus `SaveEntryAllAsync`), Upload-Konstanten.
3. **T16 `FmStatementDraftImportTool`**: Modi upload/empty/preliminary/massImport;
   base64-Dekodierung mit 20-MB-Deckel, `MassImportOptions`-Limits; SHA-256-
   Fingerprint statt Inhalt im Audit-Payload; `untrusted_data` auf allen
   Namens-/Textfeldern.
4. **T17 `FmStatementDraftCompleteTool`**: Parse → Draft-/Entry-Scope → Nur-Lücken-
   Plan (`Gap`-Semantik: fill/unchanged/alreadySet/requiresEdit) → F-0704 auf dem
   effektiven Zielzustand → `dryRun`? → Phase 1 (SetAccount/SetDescription + Audit
   `DraftCompleted`, danach Re-Eval auf klassifiziertem Stand) → Phase 2
   (`SaveEntryAllAsync` mit Effektivwerten, `SetEntrySplitDraftAsync`) + Audit-Deltas.
5. **T18 `FmStatementDraftEditTool`**: `updateEntry` (Merge über Kernfelder,
   Whitelist erzwungen), `addEntry` (zusätzlicher Create-Scope im Handler),
   `deleteEntry`, `resetDuplicate`; `AlreadyBooked`-Entries nur rücksetzbar;
   Audit `DraftEdited` mit Feld-Deltas.
6. **Doc-Fix `IStatementDraftService`**: Parameternamen von `SaveEntryAllAsync`/
   `SetEntrySecurityAsync` an die tatsächliche Implementierungs-Bindung angeglichen
   (Drift: Interface nannte `splitDraftId`/`contactId`/`price`/`fee` wo die
   Implementierung `contactId`/`savingsPlanId`/`feeAmount`/`taxAmount` bindet).
7. **DI**: 3 Descriptoren (Singleton) + 3 Handler (Scoped) in `ProgramExtensions`.
8. **Tests**: `McpStatementWriteToolsTests` — Katalogvertrag, T16-Modi +
   untrusted_data + Audit-Payload, T17 Lücken/dryRun/Atomarität/Referenz-/
   Nebenbedingungs-/Scope-Prüfung, T18 Merge/Delta/Create-Scope/Status-Guards.

## Entscheidungen
- **Katalognamen** statt der abweichenden Aufgabennamen (siehe requirement.md).
- **Zwei-Phasen-Apply** in T17: SetAccount/SetDescription triggern Pipeline-
  Klassifizierung; Eintrags-Lücken werden danach neu bewertet — Klassifizierungs-
  Ergebnisse gelten als „vorhandene Werte" und werden respektiert.
- **Atomicity-Realität**: `SaveEntryAllAsync` ist je Entry atomar; Entry-übergreifende
  Atomarität wird durch vollständige Vor-Validierung (alle Referenzen + Nebenbedingungen
  vor der ersten Wirkung) hergestellt — `ApplyBatchEntryUpdatesAsync` deckt die
  Zuordnungsfelder nicht ab (nur Kernfelder), daher Set*/Save*-Pfad.
- **Record-Denial für Schreibziele** = `Err_Mcp_ScopeDenied` (nicht stiller Ausschluss);
  Fremd-Drafts bleiben unsichtbar (`result: null`, 404-Semantik).
