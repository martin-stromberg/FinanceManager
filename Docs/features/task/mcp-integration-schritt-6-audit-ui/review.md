# Review – EP-06 Audit-Ansicht & Nutzungsprotokoll

## Umsetzungsumfang vs. Lieferumfang
- **F-0601** `IMcpAuditQueryService`/`McpAuditQueryService` + `McpAuditController`
  (`/api/mcp-audit`): Liste+Filter (Zeitraum, Kategorie, `mcpAccessId`, `correlationId`,
  `toolName`, `result`), `take` ≤ 200, Detail inkl. Changes, `GET verify`,
  `GET export?format=csv|json` als Datei (JSON vollständig/CSV-Kurzfassung), Export
  schreibt `AuditExported`-Ereignis (AK: „Export selbst Audit-Ereignis"). Nur GET —
  kein Mutationspfad. `ApiErrorDto`/`Origin API_McpAudit`; ungültiges Format →
  `Err_Invalid_format` (resx `API_McpAudit_*`).
- **F-0602** `/card/mcp-audit`: `McpAuditViewModel` + `McpAuditPanel.razor` — Tages-
  gruppierung, Kategorie-Badges, lokalisierte Klartextzeilen (`McpAuditText`, resx-
  Templates `McpAudit_Line_*`), Filterleiste inkl. „nur Verweigertes", Expand-Drilling
  (Payload-Rohdaten + Vorher/Nachher-Tabelle + CorrelationId + Entry-Hash),
  „Kette prüfen" (intakt/Bruchstelle + Lückenliste), Export-Buttons mit Filterstand,
  Deep-Link `?accessId`/`correlationId`, Leer-/Disabled-Zustände, Einstieg via
  Zugangsliste-Ribbon + Nutzungs-Section.
- **F-0603** `GET /api/mcp-audit/usage/{accessId}`: Aufrufzahlen, Fehlquote, Top-5-Tools,
  letzte Aufrufe — aggregiert aus `McpAuditEntries`; Nutzung-Section der Zugangskarte
  ersetzt den Platzhalter; freundlicher Leerzustand.

## Konformitätsprüfung
- **Owner-Isolation** serverseitig in `ApplyFilter`/`GetAsync`/`GetAccessUsageAsync`;
  Controller reicht nur `_current.UserId` — fachkonform (architekturkonzept §8).
- **Append-only**: Query-Service hat keinen Update-/Delete-Pfad; einzige Schreibung
  ist das `AuditExported`-Ereignis über den bestehenden Writer.
- **Detailtiefe**: Payload/Deltas kommen bereits abgestuft+redacted aus dem Writer —
  die Lese-API reicht sie unverändert weiter; CSV trägt keine Nutzlast-Spalte.
- **Konventionen**: `IApiClient`-only, `Err_Mcp_*`/`API_McpAudit_*`-Muster, XML-Docs,
  resx de/en/neutral, `IConfirmationService` nicht verwendet (nur Reads — regelkonform).
- **Tests**: 1688/1688 grün, davon 46 neue `McpAudit`-Tests.

## Bewusste Abweichungen / Entscheidungen
- `POST /verify` des Backlog-Ansatzes → `GET verify` umgesetzt (Aufgabenstellung;
  rein lesend, REST-konform).
- `AuditExported` als `SecurityEvent` kategorisiert (Export ist sicherheitsrelevante
  Datenablage; keine der 5 Kategorien passt exakt — dokumentiert im Service).
- Admin-`security-overview` (ui-konzept §6.3) **nicht** Teil von F-0601–F-0603 —
  bleibt EP-11/Nachschritt (Admin-Sicht ist separat konzipiert).
- CSV nutzt Semikolon + BOM (Kurzfassung für Tabellenwerkzeuge); JSON ist der
  vollständige Export inkl. Deltas/Kettenfelder.

## Bekannte Grenzen / offene Punkte
- Drilling-Links auf `AiActionRequest`/Ergebnis-Entitäten sind vorbereitet
  (`RelatedRequestId` angezeigt), echte Navigation folgt mit EP-09/EP-10
  (Genehmigungscenter existiert noch nicht).
- „Wer/wann/was"-Ergebnis-Referenzen (Posting-Ids → Listenroute) hängen von
  `PayloadJson`-Struktur ab — generische Feld-Delta-Darstellung statt typ-spezifischer
  Links; ausreichend für F-0602-AK (Details + CorrelationId).
- Verify meldet aktuell „Bruchstelle" bei abgeschnittenem Kettenkopf (Retention EP-11)
  — dokumentiertes Verhalten des Writers, UI zeigt FailureReason.
- Export-Cap 5000 Zeilen; bei Bedarf Chunked-Export nachrüstbar (Konstante im Service).

## Korrekturen Abnahme 1
- [x] **F-0602 (Teilfunktion): Verlinkung im Drilling.** Neuer statischer Helper
  `McpAuditLinks` (`FinanceManager.Web/ViewModels/Mcp/McpAuditLinks.cs`): mappt
  Payload-Argumentfelder (`draftId` → `/card/statement-drafts/{id}`, `entryId` →
  `/card/statement-drafts/entries/{id}`, `accountId`, `contactId`, `postingId`,
  `savingsPlanId`, `securityId`, `mcpAccessId`) und `Changes.EntityType`
  (`Posting`, `Contact`, `Account`, `StatementDraft(Entry)`, `Security`,
  `SavingsPlan`, `McpAccess`, Kategorien, Budget) auf die heute existierenden
  Card-Routen; Parsing des Payload-Snapshots via `JsonDocument` (rekursiv,
  dedupliziert, Cap 20, `JsonException`-sicher). `McpAuditPanel.razor`: Zugangs-
  zeile verlinkt auf `/card/mcp-accesses/{id}`, neue Detail-Zeile
  „Verknüpfte Datensätze"/„Linked records" (`McpAudit_Detail_References`,
  resx de/en/neutral) listet Payload-Referenzen als `<a href>`, und
  `Changes.EntityId` wird bei bekannter Route klickbar (sonst `<code>`).
  `RelatedRequestId` bleibt bewusst Text + Kommentar — die
  `AiActionRequest`-/Genehmigungscenter-Route existiert erst ab EP-09; ebenso
  bleiben `importId`/`requestId` unverlinkt (keine Import-Card-Route).
  Tests: `McpAuditLinksTests` (Link-Generierung für bekannte Ziele,
  Text-Fallback für unbekannte, Robustheit bei malformed/non-Guid-Payloads).
