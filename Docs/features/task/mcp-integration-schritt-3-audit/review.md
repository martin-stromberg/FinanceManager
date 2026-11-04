# Review – EP-03 Audit-Grundlage & Revisionssicherheit

## Status: umgesetzt, build- und testgrün

## Umfang vs. Lieferumfang
| Anforderung | Status | Nachweis |
|---|---|---|
| Persistenter `McpAuditWriter` statt `NullMcpAuditWriter` | ✅ | `Infrastructure/Mcp/McpAuditWriter.cs`, DI `ServiceCollectionExtensions.cs:177` |
| Schreibende Wirkung ohne Beleg schlägt fehl (F-0301) | ✅ | `StageAsync`-Kopplung in `McpAccessService` (eine SaveChanges-Runde); Dispatcher-Guard `Err_Mcp_AuditFailed` für Schreibklassen |
| Lesender Aufruf trotz Audit-Fehler erfolgreich | ✅ | Dispatcher `AuditAsync` (catch + Warnlog, Rückgabe bool); Auth-Handler-Deny-Pfad try/catch; Tests |
| Feld-Deltas als eigene Zeilen | ✅ | `McpAuditEntryChange`-Rows im selben Save (Test `WriteAsync_PersistsFieldDeltas_AsChildRows`) |
| 5 Kategorien an Callsites | ✅ | `ToolCall` (Dispatcher), `AccessEvent` (McpAccessService), `SecurityEvent` (AuthHandler); `DataChange`/`ApprovalEvent` vom Writer unterstützt, Callsites folgen mit Write-Tools/Approval (EP-06/EP-09) |
| Abgestufte Detailtiefe | ✅ | Parameter-Snapshot ohne Ergebnis-Nutzlast; Secret-Redaction (`fm_mcp_*`/`pbkdf2|`/`Bearer`); Payload-Cap 4000 (valid-JSON-Envelope); Delta-Cap 500 + Kürzungsvermerk; kein Binärinhalt-Pfad |
| Hash-Kette (F-0302) | ✅ | `SequenceNo` ab 1 je Kette, `PrevHash`/`EntryHash` SHA-256 über kanonisiertes JSON (v=1); Prozess-Semaphor + Unique-Index `(OwnerUserId,SequenceNo)` + gefilterter Unique-Index `SequenceNo WHERE OwnerUserId IS NULL` (Migration `AddMcpAuditGlobalChainIndex`) |
| „Integrität prüfen" erkennt Bruchstelle/Lücke | ✅ | `IMcpAuditWriter.VerifyAsync` → `McpAuditChainVerificationResult`; Tests: Gap, Content-Tamper, PrevHash-Fake, Ketten-Scope, Reload-Stabilität |
| Append-only | ✅ | Kein Update-/Delete-Pfad; einzige Bestands-Löschung bleibt `ClearUserDataAsync` (Konto-Löschung) + künftiger Retention-Executor (EP-11) |
| CorrelationId durchgängig | ✅ | Dispatcher erzeugt je Call → `McpToolCallContext` (Handler) + `ToolCall`-Record |
| DI + Signatur der Callsites | ✅ | `McpAuditWriter` scoped; `NullMcpAuditWriter` bleibt als No-op für Tests/Referenz |
| Lifecycle-Artefakte | ✅ | requirement/plan/review/test-results/todo |

## Entscheidungen / Abweichungen
- **Schnittstelle erweitert statt Zweit-API:** `StageAsync` + `VerifyAsync` auf
  `IMcpAuditWriter` (Task-Vorgabe „der Writer bietet eine WriteAsync-API … oder
  dokumentierter zweistufiger Pfad" → zweistufig gewählt, weil alle jetzigen
  Schreib-Callsites scoped arbeiten). `StageAsync` gibt den getrackten Eintrag zurück
  (Retry-Detach); `NullMcpAuditWriter` liefert `null`.
- **Gefilterter Unique-Index + Migration:** schließt das im Datenmodell benannte
  NULL-Duplikat-Risiko der globalen Kette auf DB-Ebene (zusätzlich zum
  Prozess-Semaphor). Additive Migration, kein Schema-Bruch.
- **`Id` nicht im EntryHash:** `McpAuditEntry.Id` wird im Konstruktor vergeben
  (Identität, kein Inhalt) — Hash deckt das semantische Grundgerüst + Kettenglieder
  ab; Positions-/Verkettungsmanipulation bleibt erkennbar.
- **`SetFiltersAsync` lädt den Zugriff vor dem Save** (für den Namens-Snapshot);
  fehlender Zugriff ⇒ `false` statt Wirkung ohne Beleg.
- **Dispatcher schreibt Roh-Argumente als `PayloadJson`** (Parameter-Snapshot;
  Writer kappt/redacted). Ergebnis-Nutzlast wird nie gespeichert — Test belegt
  `"items"` nicht im Payload.
- **WriteAsync-Kontrakt dokumentiert:** committet alle getrackten Änderungen des
  geteilten Contexts — gekoppelte Pfade müssen `StageAsync` nutzen (XML-Doc +
  plan.md).

## Risiken / offene Punkte für die Abnahme
- `DataChange`-/`ApprovalEvent`-Callsites entstehen erst mit den Write-Tools und dem
  Approval-Flow (EP-06/EP-09) — der Writer trägt beide Kategorien bereits; der
  `StageAsync`-Kopplungspfad ist dafür vorgesehen (in Handler-Wirktransaktion).
- `VerifyAsync` erwartet Kettenbeginn bei `SequenceNo` 1; Retention-gekürzte Ketten
  (EP-11) brauchen in der künftigen Audit-Ansicht eine „ab diesem Eintrag"-
  Kennzeichnung (im Code dokumentiert).
- `ClearUserDataAsync` (Konto-Löschung) entfernt Audit-Einträge des Users — bewusste
  Bestands-Abgrenzung, kein Retention-Pfad; bricht dort die Kette (Gap) — fachlich
  akzeptabel, da der gesamte Benutzerbestand gelöscht wird.
- `Mcp:Enabled` bleibt `false` (opt-in).

## Korrekturen Abnahme 1
- [x] **A-01: Vorher/Nachher-Scope-Delta bei Scope-/Filter-/Konfigurationsänderungen**
  (audit-konzept.md §3.3). `McpAccessService` erzeugt jetzt
  `McpAuditFieldChange`-Zeilen für `ScopesChanged` (Delta je `DataType:Right`-Tupel:
  hinzugefügt mit Nachher-Bild, entfernt mit Vorher-Bild, geänderte Filter-Sets als
  alt→neu; Kompaktformat `DataType:Right[Dim=csv;…]`), `FiltersChanged` (eine Zeile je
  geänderter Filterdimension, `EntityId` = Scope-Id) und `AccessUpdated` (geänderte
  Konfigurationsfelder alt→neu plus Scope-Delta bei Stufenwechsel weg von
  `Individuell`). Credential-Material (`TokenHash`, `AccessKeyHint`) ist nie Teil des
  Snapshots; die Writer-Redaction bleibt als Safety-Net dahinter. Tests:
  `SetScopes_WritesScopeDelta_AddedChangedRemoved`,
  `SetFilters_WritesFilterDelta_PerDimension`,
  `Update_WritesFieldDelta_IncludingScopeRemovalOnLevelSwitch`,
  `AccessEvents_DeltaContainsNoSecrets` (`McpAccessServiceTests`).
