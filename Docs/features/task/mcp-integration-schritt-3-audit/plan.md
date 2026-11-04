# Umsetzungsplan: Audit-Grundlage & Revisionssicherheit (EP-03, F-0301/F-0302)

## Ziel
Der persistente, hash-verkettete `McpAuditWriter` ersetzt den `NullMcpAuditWriter`: jeder
MCP-Vorgang ist vom ersten Aufruf an revisionssicher protokolliert — transaktional an
schreibende Wirkung gekoppelt, nicht-blockierend bei lesenden Aufrufen, mit abgestufter
Detailtiefe und verifizierbarer Hash-Kette.

## Umsetzung (abgeschlossen)

### 1. Schnittstelle (`FinanceManager.Application/Mcp/`)
- `IMcpAuditWriter` um zwei Member erweitert:
  - `StageAsync(request, ct)` → staged das Eintragsbündel im geteilten
    `AppDbContext`-Change-Tracker **ohne** zu speichern; der Aufrufer committet Wirkung +
    Beleg atomar in einer `SaveChangesAsync`. Rückgabe: getrackte `McpAuditEntry`
    (für Detach bei Save-Retry) bzw. `null` (No-op-Writer).
  - `VerifyAsync(ownerUserId, ct)` → `McpAuditChainVerificationResult` (IsIntact,
    EntriesChecked, FirstBrokenSequenceNo, FailureReason, SequenceGaps).
- `McpAuditChainVerification.cs` (neu): Result-Record + `McpAuditChainGap`
  (FirstMissing/LastMissingSequenceNo).

### 2. `McpAuditWriter` (`Infrastructure/Mcp/`, neu)
- **Zwei Schreibmodi** (architekturkonzept §7.1):
  `WriteAsync` = Build + sofortiger Save (lesende/unkritische Pfade);
  `StageAsync` = Build + Track ohne Save (transaktionale Kopplung an Fachwirkung).
- **Kettenanker:** Kettenkopf (`MAX(SequenceNo)`, `EntryHash`) unter prozessweitem
  `SemaphoreSlim` (SQLite serialisiert Schreiber ohnehin; schützt zusätzlich die
  NULL-Besitzer-Kette). `SequenceNo` ab 1 je Kette; `PrevHash`/`EntryHash` über
  kanonisiertes JSON (Utf8JsonWriter, feste Feldreihenfolge, `Utc.Ticks`,
  lowercase-GUIDs, Deltas kanonisch sortiert nach EntityType/EntityId/Field/Old/New;
  Format-Version `v=1`, SHA-256 lowercase-hex). Der Hash deckt den **semantischen
  Eintragsinhalt** ab — die Entity-`Id` (im Konstruktor vergeben) ist Identität, nicht
  Inhalt.
- **Detailtiefe/Sicherheitsnetz:** `RedactSecrets` (`fm_mcp_*`, `pbkdf2|*`,
  `Bearer *` → `[redacted]`); `PayloadJson`-Kappung 4000 Zeichen als valid-JSON-Envelope
  `{"head":…,"truncated":true,"originalChars":n}`; Delta-Werte 500 Zeichen +
  `…[truncated]`; Sanitisierung vor dem Hashen ⇒ gespeicherter Inhalt und `EntryHash`
  stimmen immer überein. `EventType`/`ResultStatus` hart validiert (Pflicht, ≤100/≤50).
- **`VerifyAsync`:** lädt Kette (inkl. `Changes`, `AsNoTracking`), prüft monotone
  Lückenlosigkeit ab 1 (Gaps als Bereiche), `PrevHash`-Verkettung (Kopf muss
  `PrevHash == null` haben) und Hash-Rekomputation; meldet erste Bruchstelle + Gaps.
- **Append-only:** kein Update-/Delete-Pfad.

### 3. Kette auf DB-Ebene gehärtet
- `AppDbContext`: zusätzlich `HasIndex(SequenceNo).IsUnique().HasFilter("[OwnerUserId] IS NULL")`
  — schließt das NULL-Duplikat-Risiko der globalen Kette, das der zusammengesetzte
  Unique-Index `(OwnerUserId, SequenceNo)` unter SQLite nicht abdeckt.
- Migration `20261104191031_AddMcpAuditGlobalChainIndex` (dotnet ef, Infrastruktur/Web).

### 4. Callsites
- **`McpAccessService`:** alle schreibenden Verwaltungsaktionen stagen den
  `AccessEvent`-Record vor dem gemeinsamen `SaveChangesAsync` (Stage-Then-Save):
  Create (inkl. Detach des staged Eintrags im Hint-Kollisions-Retry), Update, SetScopes,
  SetFilters (Zugriff wird nun vor dem Save für den Namens-Snapshot geladen; fehlender
  Zugriff ⇒ `false`, keine Wirkung ohne Beleg), Activate/Deactivate, RotateToken
  (Retry-Detach), Revoke. Alter Best-Effort-`AuditAsync`-Pfad entfernt — Staging-Fehler
  propagieren bewusst (Wirkung ohne Beleg schlägt fehl).
- **`McpToolDispatcher`:** `AuditAsync` liefert Erfolg/Misserfolg zurück und loggt
  `Warning` statt still zu schlucken; `PayloadJson` = Roh-Argumente (Parameter-Snapshot —
  die Ergebnis-Nutzlast lesender Aufrufe wird nie gespeichert). Schlägt der Audit-Record
  nach erfolgreichem Aufruf einer **Schreibklasse** fehl ⇒ `Err_Mcp_AuditFailed`
  (DefinedError); Lesend/Vorschlagend bleiben nicht-blockierend. `ILogger<McpToolDispatcher>`
  als Ctor-Dep ergänzt.
- **`McpAuthHandler`:** Deny-Pfad-`WriteAsync` in try/catch + `LogWarning` — ein
  Audit-Ausfall darf die uniforme 401 nicht in 500 verwandeln.
- **`McpAnomalyTracker`:** unverändert (Benachrichtigungskanal, kein Audit-Pfad).

### 5. DI
- `ServiceCollectionExtensions`: `IMcpAuditWriter` → `McpAuditWriter` (scoped).
  `NullMcpAuditWriter` bleibt für Tests/Referenz (implementiert die erweiterte
  Schnittstelle als No-op: `StageAsync` → `null`, `VerifyAsync` → leer/intakt).

### 6. CorrelationId
- Bestehend: `Guid.NewGuid()` je `tools/call` im Dispatcher → `McpToolCallContext`
  (Handler) + `ToolCall`-Record (`CorrelationId`). Damit durchgängig pro Aufruf;
  `AiActionRequest`-Anknüpfung folgt mit den Write-Tools (EP-06/EP-09).

## Bewusste Entscheidungen / Restrisiken
- **Geteilter `AppDbContext` statt `IDbContextFactory`:** entspricht architekturkonzept
  §7.1 („derselben AppDbContext-Transaktion … im selben Request-Scope") und macht die
  Kopplung ohne explizite Transaktionssteuerung möglich. Folgevertrag (dokumentiert an
  `WriteAsync`): Aufrufer sichern zu, dass im Scope keine fremden Mutationen pending sind,
  oder nutzen `StageAsync` + expliziten Save. Lesepfade mutieren nichts.
- **`Err_Mcp_AuditFailed` für Schreibklassen im Dispatcher:** der ToolCall-Record ist
  der Primärbeleg; die Wirkung selbst ist zu diesem Zeitpunkt bereits durch den Handler
  committed (Write-Tools existieren noch nicht) — für EP-06+ ist der dokumentierte
  Kopplungspfad `StageAsync` innerhalb der Wirktransaktion des Handlers; der
  Dispatcher-Guard bleibt als Tiefenabsicherung.
- **`Id` nicht im Hash:** siehe plan-Kommentar — Inhalts-Hash reicht, Positions-
  (SequenceNo) und Verkettungsverletzungen werden weiter erkannt.
- **Verify-Start bei SequenceNo 1:** passt zur EP-03-Welt ohne Retention; sobald der
  Retention-Executor (EP-11) Kettenköpfe kürzt, wird die Audit-Ansicht den
  „verbleibender Kopf mit PrevHash"-Fall gesondert kennzeichnen müssen (datenmodell §8:
  `RetentionPurged`-Ereignis als neuer Kettenanfang) — in `VerifyAsync` dokumentiert.
