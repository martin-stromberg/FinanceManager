# Übersetzte Anforderung – EP-03 Audit-Grundlage & Revisionssicherheit (Issue #481)

## Ausgangslage
Auf dem MCP-Fundament (Schritt 1: Entitäten `McpAuditEntry`/`McpAuditEntryChange`, Migration
`AddMcpFoundation`, Schnittstelle `IMcpAuditWriter` mit Callsites in Dispatcher, Auth-Handler
und `McpAccessService`) und dem Read-Only-Toolkatalog (Schritt 2) wird jetzt der
**persistente, hash-verkettete `McpAuditWriter`** als Ersatz für den dokumentierten No-op
`NullMcpAuditWriter` umgesetzt — F-0301 (Audit-Speicher & Schreibpfad) und F-0302
(Hash-Kette & Lückenlosigkeit) aus
[product-backlog.md](../../../planung/mcp-integration/product-backlog.md) EP-03.

**Normative Quellen:** `Docs/planung/mcp-integration/audit-konzept.md` (5 Kategorien,
abgestufte Detailtiefe §4, WORM-/Integritätsaspekte §7, Verknüpfungskette §6.4),
`datenmodell-erweiterungen.md` §6–§8 (Felder, Indizes, Retention-Anknüpfung),
`architekturkonzept.md` §7 (transaktionaler Schreibpfad).

## Funktionale Anforderungen

### FA-1: Persistenter Schreibpfad (F-0301)
- `McpAuditEntry` (+ `McpAuditEntryChange`) werden über `IMcpAuditWriter.WriteAsync` bzw.
  `StageAsync` persistiert; DI bindet `McpAuditWriter` statt `NullMcpAuditWriter`.
- **Schreibende Wirkung ohne Beleg schlägt fehl:** die Audit-Records schreibender
  Verwaltungsaktionen (`McpAccessService`: Create/Update/SetScopes/SetFilters/
  Activate/Deactivate/RotateToken/Revoke) committen atomar mit der Fachwirkung in einer
  `SaveChangesAsync`-Runde desselben scoped `AppDbContext` (Stage-Then-Save). Ein
  Audit-Fehler schlägt die Wirkung mit.
- **Lesender Aufruf trotz Audit-Fehler erfolgreich:** der Dispatcher schreibt den
  `ToolCall`-Record best-effort (catch + Warnlog); ein Ausfall blockiert lesende
  Aufrufe nicht. Für schreibende Klassen (`Schreibvorbereitend`/`Genehmigungspflichtig`)
  führt ein Audit-Fehler dagegen zu `Err_Mcp_AuditFailed` — die ToolCall-Belegpflicht
  ist dort hart (die DataChange-/ApprovalEvent-Kopplung in der Wirktransaktion folgt mit
  den Write-Tools ab EP-06).
- **5 Kategorien** (`ToolCall`, `DataChange`, `AccessEvent`, `ApprovalEvent`,
  `SecurityEvent`) korrekt an den Callsites gemappt.
- **Abgestufte Detailtiefe** (audit-konzept §4.2): ToolCall-Records tragen die
  Aufrufparameter (keine Ergebnis-Nutzlast), `DataChange`-Records Feld-Deltas als eigene
  Zeilen; mechanisches Sicherheitsnetz im Writer: Secret-Redaction
  (`fm_mcp_*`, `pbkdf2|*`, `Bearer *`), `PayloadJson`-Kappung bei 4000 Zeichen
  (valid-JSON-Envelope mit `truncated`/`originalChars`/`head`), Feld-Delta-Werte bei
  500 Zeichen mit Kürzungsvermerk. Token/Secrets/Binärinhalte niemals.

### FA-2: Hash-Kette & Lückenlosigkeit (F-0302)
- `SequenceNo` fortlaufend ab 1 **je Kette** (Kette = `OwnerUserId`; `null` = globale
  Kette), `PrevHash` = `EntryHash` des Vorgängers, `EntryHash` = SHA-256 (lowercase hex)
  über kanonisierten Inhalt (feste JSON-Feldliste/-reihenfolge, `Utc.Ticks`, lowercase
  GUIDs, kanonisch sortierte Deltas; Format-Version `v=1`).
- Atomare Bündel (Eintrag + Deltas in einem Save); serverseitige `DateTime.UtcNow`.
- Prozessweiter Semaphor serialisiert Kettenkopf-Lesen + Insert; Besitzer-Ketten durch
  Unique-Index `(OwnerUserId, SequenceNo)` geschützt, globale Kette zusätzlich durch
  gefilterten Unique-Index `SequenceNo WHERE OwnerUserId IS NULL` (Migration
  `AddMcpAuditGlobalChainIndex`) — schließt das NULL-Duplikat-Risiko auf DB-Ebene.
- `VerifyAsync(ownerUserId)`: prüft Lückenlosigkeit (startet bei 1, streng monoton,
  Gap-Meldung als Bereiche), `PrevHash`-Verkettung und `EntryHash`-Rekomputation;
  meldet `IsIntact`, erste Bruchstelle (`FirstBrokenSequenceNo` + `FailureReason`) und
  `SequenceGaps`.
- Append-only: kein Update-/Delete-Pfad in der Anwendung (Retention-Executor = EP-11).

### FA-3: CorrelationId
- Der Dispatcher erzeugt pro `tools/call` eine `CorrelationId` (bestehend) und propagiert
  sie in `McpToolCallContext` (Handler) sowie in den `ToolCall`-Audit-Record.

## Nicht-Ziele dieses Schritts
- Audit-UI/-Export (EP-08), Retention-Executor (EP-11), `AiActionRequest`-
  Zustandsketten-Protokollierung (EP-06/EP-09), Rate-Limit-Aggregate und Notfallschalter-
  Ereignisse (EP-08+).

## Abnahmekriterien (aus product-backlog)
- Schreibende Wirkung ohne Beleg schlägt fehl; lesender Aufruf trotz Audit-Fehler
  erfolgreich; Feld-Deltas als eigene Zeilen.
- „Integrität prüfen" erkennt Bruchstelle/Lücke; Eintragsnummern monoton je Kette;
  append-only ohne App-Update-Pfad.
- Build + Tests grün; XML-Docs; keine Stubs.
