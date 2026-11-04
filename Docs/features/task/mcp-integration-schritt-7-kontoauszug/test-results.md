# Testergebnisse – EP-07 Kontoauszugs-Assistent (Issue #485)

## Befehle

```
dotnet build FinanceManager.Web/FinanceManager.Web.csproj   → 0 Fehler (nur vorhandene Warnungen)
dotnet build FinanceManager.Tests/FinanceManager.Tests.csproj → 0 Fehler
dotnet test  FinanceManager.Tests/FinanceManager.Tests.csproj --no-build
```

## Ergebnis

| Suite | Ergebnis |
|---|---|
| `FinanceManager.Tests` gesamt | **1744/1744 bestanden**, 0 fehlgeschlagen, 0 übersprungen (38 s) |
| davon `Tests.Mcp` | 172/172 bestanden |
| davon neu `McpStatementWriteToolsTests` | 22/22 bestanden |

## Abgedeckte Fälle (neu)

**Katalog**: Namen/Sicherheitsklasse (schreibvorbereitend), Scope-Tupel exakt nach
Katalog, F3-Matrix-Validität, JSON-Schema parsebar.

**T16 `fm_statement_draft_import`**
- Upload: Bytes+Dateiname 1:1 in `CreateDraftAsync`; `recognized`, draftCount,
  `untrusted_data.originalFileName`; Audit-Payload enthält sha256+Metadaten, niemals
  den base64-Inhalt.
- Upload unbekanntes Format → `recognized=false`, drafts=[].
- Ungültiges base64 + >20 MB → `Err_Mcp_InvalidArguments`, kein Service-Aufruf.
- `empty` → `CreateEmptyDraftAsync`; `preliminary` mit fremdem `accountId` →
  `Err_Mcp_InvalidArguments` ohne Wirkung.
- `massImport` → Orchestrator mit `ConfirmExecution=true`, untrusted_data je Datei.

**T17 `fm_statement_draft_complete`**
- Nur Lücken: gesetztes ContactId unangetastet (kein SaveEntryAll-Aufruf),
  fehlendes gefüllt; Ausgabe `skipped: alreadySet` / `filled`.
- `dryRun`: Plan gemeldet, **kein** Schreibaufruf, **kein** Audit-Staging.
- Fremde ContactId → `Err_Mcp_InvalidArguments`, atomar vor jeder Wirkung.
- securityId ohne securityTransactionType → `Err_Mcp_InvalidArguments`
  (Zuordnungsvollständigkeit).
- Record-gefilterter Draft → `Err_Mcp_ScopeDenied`; fremder Draft → `result=null`.
- `DataChange`-Staging mit Feld-Delta (ContactId: null→id).

**T18 `fm_statement_draft_edit`**
- updateEntry: Merge (ungeforderte Felder bleiben), Audit-Delta Amount alt→neu.
- Nicht-korrigierbares Feld (contactId im Patch) → `Err_Mcp_InvalidArguments`.
- AlreadyBooked-Entry → `Err_Mcp_InvalidState`; amount=0 → `Err_Mcp_InvalidArguments`.
- addEntry ohne Create-Scope → `Err_Mcp_ScopeDenied`, kein Service-Aufruf.
- addEntry/deleteEntry/resetDuplicate über die Service-Pfade; Committed-Draft →
  `Err_Mcp_InvalidState`.
