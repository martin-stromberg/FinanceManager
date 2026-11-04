# Testergebnisse – EP-03 Audit-Grundlage & Revisionssicherheit

## Build
```
dotnet build FinanceManager.sln  →  erfolgreich, 0 Fehler
```
(XML-Dokumentation vollständig — CS1591 ist Warning-as-Error; keine Stubs.)

## Migration
```
dotnet ef migrations add AddMcpAuditGlobalChainIndex
  -p FinanceManager.Infrastructure -s FinanceManager.Web  →  erfolgreich
  Up: CREATE UNIQUE INDEX IX_McpAuditEntries_SequenceNo ON McpAuditEntries(SequenceNo)
      WHERE [OwnerUserId] IS NULL
```

## Unit-Tests (`FinanceManager.Tests`)
```
dotnet test FinanceManager.Tests              →  1636 erfolgreich, 0 fehlgeschlagen (41 s)
dotnet test … --filter "FullyQualifiedName~McpAudit"  →  25 erfolgreich (2,9 s)
```

## Integrations-Tests (`FinanceManager.Tests.Integration`)
```
dotnet test FinanceManager.Tests.Integration  →  155 erfolgreich, 0 fehlgeschlagen (3 min 27 s)
```
(/mcp-Pipeline schreibt jetzt reale `McpAuditEntries` über den persistenten Writer —
kein Test-Bruch.)

## Abgedeckte Kernfälle

### `Mcp/McpAuditWriterTests.cs` (SQLite :memory:, 18 Tests)
- **Kategorien:** alle 5 `McpAuditCategory`-Werte persistiert; volles Grundgerüst
  (EventType/Result/Owner/AccessName-Snapshot/Tool/Katalogversion/Context/CorrelationId/
  SourceIp/Duration/AffectedCount), `Utc` serverseitig, `EntryHash` 64-stellig lowercase.
- **Kette:** `SequenceNo` 1..n monoton je Besitzer; Besitzer-Ketten und globale
  (NULL-)Kette unabhängig (jeweils Start bei 1); `PrevHash` = `EntryHash` des Vorgängers;
  parallele Writes über zwei Contexts → eindeutige Sequenzen.
- **Bündel:** Feld-Deltas als eigene `McpAuditEntryChanges`-Zeilen (EntityType/EntityId/
  Field/Old/New).
- **Detailtiefe:** `fm_mcp_*`-Token im Payload und `Bearer …` in Delta-Werten →
  `[redacted]`; 5000-Zeichen-Payload → valid-JSON-Envelope `truncated:true` +
  `originalChars` + 4000-Zeichen-`head`; Delta-Wert >500 → `…[truncated]`;
  ungültige EventType/ResultStatus → `ArgumentException`, nichts persistiert.
- **Staging:** `StageAsync` schreibt nichts — erst der Save des Aufrufers committet
  Eintrag + gekoppelte Fachwirkung (McpAccess) gemeinsam.
- **Verify:** intakte Kette (inkl. Deltas) verifiziert **nach Reload** aus frischem
  Context (kanonische Serialisierung ist round-trip-stabil); gelöschter Mitteleintrag →
  `SequenceGaps=[2..2]` + Bruchstelle 3 (dangling PrevHash); manipulierter Payload →
  Bruchstelle 2 (Hash-Recompute); gefälschter `PrevHash` → Bruchstelle 2; Verify ist
  ketten-scoped (global vs. Besitzer); leere Kette → intakt.

### `Mcp/McpAuditPipelineTests.cs` (EF InMemory + Moq, 7 Tests)
- **Lesender Aufruf trotz Audit-Fehler erfolgreich:** Dispatcher + werfender Writer →
  `Success` (AK F-0301).
- **Schreibklasse ohne Beleg schlägt fehl:** `Schreibvorbereitend`-Deskriptor +
  werfender Writer → `DefinedError Err_Mcp_AuditFailed`.
- **ToolCall-Record e2e:** echter `McpAuditWriter` → Category `ToolCall`, Result
  `Success`, ToolName, `katalog-v1`, AccessName-Snapshot, SourceIp, CorrelationId ≠ null,
  `PayloadJson` enthält Aufrufparameter, **nicht** die Ergebnis-Nutzlast.
- **Scope-Denied bei Audit-Ausfall:** definiert `Err_Mcp_ScopeDenied`, kein Crash.
- **`McpAccessService`-Kopplung:** `CreateAsync` mit echtem Writer → `AccessCreated`-
  Record (AccessEvent, `Ui`-Kontext, Access-Verknüpfung) atomar mit dem Zugang;
  werfender `StageAsync`-Stub → `CreateAsync` schlägt, **kein** Zugang persistiert
  (Wirkung ohne Beleg schlägt fehl).
