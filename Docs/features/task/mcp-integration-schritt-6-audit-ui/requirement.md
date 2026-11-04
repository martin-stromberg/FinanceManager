# Übersetzte Anforderung – EP-06 Audit-Ansicht & Nutzungsprotokoll (Issue #484)

## Ausgangslage
Auf der Audit-Grundlage (Schritt 3: `McpAuditWriter` mit Hash-Kette, `SequenceNo`,
`PrevHash`/`EntryHash`, Feld-Deltas, `VerifyAsync`) und der Zugangsverwaltung inkl.
individueller Rechte (Schritte 1/5) wird jetzt die **lesende Audit-Ansicht** produktiv:
owner-isolierte Abfrage-API, Timeline mit Drilling, Integritätsprüfung, Export und die
Nutzungsübersicht je Zugang — F-0601…F-0603 aus
[product-backlog.md](../../../planung/mcp-integration/product-backlog.md) EP-06.

**Normative Quellen:**
`audit-konzept.md` §6 (Benutzer-Audit-Ansicht, Export, Nachvollziehbarkeit) / §7
(WORM-/Integritätsaspekte); `ui-konzept.md` §4.5 (Audit-Ansicht `/card/mcp-audit`,
Timeline, Drilling, Filter, Integritätsaktion, Export) / §6.3 (API-Skizze
`McpAuditController`); `architekturkonzept.md` §6/§8 (lesende API owner-isoliert);
`inventory.md` (Controller-/List-/Card-/Export-Konventionen).

## Vorhandene Basis (wird genutzt)
- `McpAuditEntry`/`McpAuditEntryChange` (5 Kategorien, CorrelationId, PayloadJson,
  Delta-Zeilen, `SequenceNo`/`PrevHash`/`EntryHash`) — Append-only, kein Update-/Delete-Pfad.
- `IMcpAuditWriter.VerifyAsync(ownerUserId)` — intakt/Bruchstelle/Lücke.
- `McpAccessService` + `McpAccessesController` (Owner-Isolation, `ApiErrorDto`, `Err_Mcp_*`),
  `IApiClient`-Partials, UI `/list/mcp-accesses` + `/card/mcp-accesses/{id}` mit
  `McpAccessExtrasPanel` (Nutzung-Section mit Platzhalter).

## Funktionale Anforderungen

### FA-1: Audit-Abfrage-API (F-0601)
- `IMcpAuditQueryService` (Application) + `McpAuditQueryService` (Infrastructure):
  owner-isolierte Abfrage von `McpAuditEntries`; Filter (Zeitraum, Kategorie,
  `McpAccessId`, `CorrelationId`, Tool-Name, Ergebnisstatus) mit AND-Semantik;
  Paging `skip`/`take` ≤ 200; Detail inkl. `Changes`-Zeilen; `VerifyAsync` delegiert an
  den Schreibpfad (eine Implementierung der Hash-Regel); Export liefert serialisierte
  Daten (keine Binärpayloads).
- `McpAuditController` (`/api/mcp-audit`, `[Authorize(JwtBearer)]`,
  `ICurrentUserService`):
  `GET` Liste+Filter, `GET {id}` Detail (404 bei Fremd-/Fehlid), `GET verify`
  (intakt/Bruchstelle/Lücken), `GET export?format=csv|json` (Datei-Download),
  `GET usage/{accessId}` (Nutzungsaggregat, 404 bei Fremd-/Fehlid).
- Der Export schreibt selbst ein Audit-Ereignis (`AuditExported`, SecurityEvent) in die
  Owner-Kette — ohne den Lesezugriff zu blockieren, wenn der Audit-Write fehlschlägt.
- Kein Mutationspfad: die API ist ausschließlich lesend (Append-only-Konvention).

### FA-2: Audit-Timeline & Drilling (F-0602)
- `/card/mcp-audit` (`McpAuditViewModel`, `CardRoute("mcp-audit")`) mit
  `McpAuditPanel.razor` als eingebettetem Panel:
  - Timeline gruppiert nach Tag; je Zeile: Uhrzeit, Kategorie-Badge, lokalisierte
    Klartext-Ereigniszeile, Ergebnisstatus.
  - Filterleiste: Zugang (Dropdown eigener Zugänge), Kategorie, Ergebnisstatus,
    Tool, Zeitraum von/bis, CorrelationId; Schnellfilter „nur Verweigertes";
    Anwenden/Zurücksetzen; Server-Paging „Mehr laden" (50er-Seiten).
  - Drilling: Expand → Detail (Tool + Katalogversion, Zugangsname, CorrelationId,
    Quell-IP, Dauer, betroffene Datensätze, Request-Id, Entry-Hash, Feld-Deltas als
    Vorher/Nachher-Tabelle, Payload-Rohdaten hinter Detail-Expandierung).
  - „Kette prüfen" → `GET /verify`, Ergebnisanzeige intakt/Bruchstelle + Sequenzlücken.
  - „Export (CSV)"/„Export (JSON)" → Download über `/api/mcp-audit/export` mit
    aktuellem Filterstand (Cookie-Auth, `financeManager.downloadFromUrl`).
  - Deep-Link `/card/mcp-audit?accessId={id}` (und `correlationId`) selektiert Filter.
  - Leer- und Disabled-Zustände freundlich; Einstieg über Ribbon der Zugangsliste
    („Protokoll") und Nutzungs-Section der Zugangskarte („Verlauf öffnen").

### FA-3: Nutzungsübersicht je Zugang (F-0603)
- `GET /api/mcp-audit/usage/{accessId}`: Aufrufzahlen, Erfolgs-/Fehlschlagzahlen,
  Fehlquote, erste/letzte Nutzung, Top-5-Tools, letzte Aufrufe — aus `McpAuditEntries`
  (Kategorie ToolCall) aggregiert.
- `McpAccessCardViewModel.Usage` + `McpAccessExtrasPanel` Nutzung-Section zeigt die
  Kennzahlen (ersetzt den Platzhalter) + Link in die gefilterte Audit-Ansicht;
  Leerzustand „Noch keine Aufrufe".

## Nicht-funktionale Anforderungen
- XML-Docs an allen neuen öffentlichen API-Flächen; DTO-Enum-Konvention (short).
- resx de/en/neutral für ALLE neuen UI-Strings; `Err_*`-Codes bleiben maschinenlesbar.
- UI ausschließlich via `IApiClient`; `IConfirmationService` nicht nötig (nur Reads).
- Audit-Daten niemals modifizierbar über API/UI.
- `dotnet build` + `dotnet test` grün; neue Tests für Query-Service (Isolation, Filter,
  Paging, Export-Selbstereignis, Usage-Aggregat), Controller (Owner-Übergabe, 404,
  Formatvalidierung, Datei-Resultate) und das ViewModel.
