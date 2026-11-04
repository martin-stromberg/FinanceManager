# Plan – Schritt 4: UI-, Architektur- und Datenmodellkonzept

## Lieferdokumente (alle unter `Docs/planung/mcp-integration/`)

### `ui-konzept.md` (F11)

Gliederung:

1. Zielbild und UX-Leitlinien (Standardfall zuerst, Expertenmodus als
   bewusste Eskalation, Verständlichkeit, `IConfirmationService`, kein
   Modellbruch)
2. Einordnung in die UI-Architektur (Blazor Server `InteractiveServer`,
   `ViewModelBase`/`BaseListViewModel`/`BaseCardViewModel`,
   `CardRoute`/`ListViewModelFactory`, `IApiClient`-only, resx de/en,
   `UiOverlaySpec`, `Notification`, Trennung `/api` vs. `/mcp`)
3. Platzierung in Navigation/Ribbon (neuer Nav-Eintrag „KI-Zugänge",
   Badge für offene Requests, Genehmigungscenter via EventDriven-
   Notification, Admin-Sicherheitsübersicht)
4. Komponenten im Detail:
   - 4.1 Zugangsverwaltung (Liste `/list/mcp-accesses` +
     `McpAccessListViewModel`, Karte `/card/mcp-accesses/{id}` +
     `McpAccessCardViewModel` mit Sections nach Setup-Vorbild)
   - 4.2 Tokenverwaltung (Anlage-Assistent, Einmalanzeige-Dialog mit
     Pflicht-Bestätigung, Rotation, Widerruf, HTTP/TLS-Hinweis)
   - 4.3 Rechteverwaltung (Stufen-Auswahlkarten mit Klartext,
     Scope-Matrix-Editor 18×7 nur auf ✓/◑-Zellen der F3-Matrix,
     Implikationshilfe, Genehmigungsmodus)
   - 4.4 Filterverwaltung (5 Filterdimensionen, Mehrfachauswahl,
     Semantik-Hinweise inkl. „ohne Kategorie nicht erfasst")
   - 4.5 Audit-Ansicht (Timeline, Filter, Drilling über CorrelationId/
     Entity-Referenzen, Export JSON/CSV, Integritätsprüfung)
   - 4.6 Genehmigungscenter (Eingangskorb, Wirkungsvorschau aus
     ParameterSnapshot, Freigeben/Ablehnen, Batch, Historie, ReasonText
     als untrusted content markiert)
5. Benutzer- vs. Admin-Sichten (kein Fremdzugriff; Admin =
   Sicherheitsmetadaten + Schalter-Zustand, Schalter bleiben
   `appsettings`-Hoheit)
6. Fachliche Skizze der API-Erweiterungen (`McpAccessesController`,
   `AiActionRequestsController`, `McpAuditController`; `IApiClient`-
   Partial `ApiClient.Mcp.cs`, DTOs `Shared/Dtos/Mcp/`, `Err_Mcp_*`)
7. Verständlichkeits-Mapping (Konzeptbegriff → UI-Vermittlung)
8. Konsistenzanforderungen

### `architekturkonzept.md` (F12)

Gliederung:

1. Zielbild und Grundsätze (parallele Zugriffsschicht, Pipeline-Defense,
   Single-Instance-Konformität, Wiederverwendung, keine Nicht-Ziele)
2. Betriebsform: **Entscheidung in-process in `FinanceManager.Web`,
   Route `/mcp`** (behandelte Annahme 1) mit Begründung und verworfenen
   Alternativen (separater Prozess, entferntes Hosting, nur-stdio)
3. Transport-Entscheidung: **Streamable HTTP** (Annahme A4 verbindlich),
   Alternativen (Legacy-SSE, stdio, WebSocket, reines Request/Response)
   mit Begründung; Framework-Hinweis MCP-C#-SDK vs. Eigenbau
4. Struktur des MCP-Server-Layers (Komponentendiagramm, Projekt-
   Platzierung Application/Domain/Infrastructure/Web, Service-Layer-
   Anbindung inkl. offenem Baustein `IStatementImportQueryService`)
5. Sicherheitslayer: verbindliche 10-Stufen-Pipeline (RequestLogging →
   IpBlock → RateLimiter → Kill-Switch → McpAuth → Dispatcher →
   ScopeEvaluator → ApprovalGate → Adapter → AuditWriter),
   Token→UserId-Auflösung, Fehlerbilder, Session-Zustandslosigkeit
6. Tool-Registrierung (`McpToolRegistry`, `katalog-v<n>`, Descriptor-
   Prinzip, „kein Tool ohne Scope" build-erzwingbar) und
   Ausführungsmodell (direkt vs. `AiActionRequest`-Anlage vs.
   `AiActionExecutor`-Queue nach `BackgroundTaskRunner`-Muster,
   SQLite-Claim, Idempotenz)
7. Logging-/Audit-Anbindung (Festlegung: transaktional gekoppelt;
   Alternativen begründet verworfen; Bereinigungs-Executor)
8. Persistenzmodell-Übersicht (neue DbSets, `ApplyMigrationsAndSeed`,
   SQLite-Serialisierung, `*BackupDto`-Einbindung)
9. Fehler-/Notfallschalter (`Mcp:*`-Konfigurationssektion mit Defaults,
   Schalter-Wirkung je Pipeline-Position, Admin-Anzeige)
10. Request-Pipeline-Skizze eines Toolaufrufs (Sequenzdiagramm)
11. Deployment-/Betriebssicht (HTTP-LAN vs. Reverse-Proxy/TLS,
    Forwarded-Headers-Pflicht, Auto-Update, Neustart-Verhalten)
12. Abgrenzung/Nicht-Ziele, 13. Konsistenzanforderungen

### `datenmodell-erweiterungen.md` (Artefakt 7)

Gliederung:

1. Verbindliche Persistenz-Konventionen (`Entity`-Basisklasse,
   `OwnerUserId`, DbSet+OnModelCreating+Migration, Enum→short,
   `*BackupDto`, `ReportFavorite`-CSV-Muster, keine Änderung an
   Fachentitäten)
2. Übersicht: `McpAccess`, `McpAccessScope`, `AiActionRequest`,
   `McpAuditEntry`, `McpAuditEntryChange` + Enums (`McpAccessLevel`,
   `McpDataType` 18 Werte, `McpRightKind` 7 Werte, `McpApprovalMode`,
   `McpAccessStatus`, `AiActionRequestStatus` 6 Zustände,
   `McpAuditCategory` 5 Kategorien); bewusst keine Tabelle für
   `Mcp:*`-Schalter (appsettings)
3. `McpAccess` Feldliste (Name, `AccessKeyHint`, `TokenHash` pbkdf2-Format,
   Level, ApprovalMode, Status, `ExpiresUtc`, `AllowedIpRanges`,
   `LastUsedUtc`, `TokenIssuedUtc`, `RevokedUtc`) + Indizes
4. `McpAccessScope` Feldliste (Datentyp × Rechteart + 5 Filter-CSVs) +
   Festlegung: Standardstufen ohne Zeilen (Laufzeit-Ableitung), Zeilen
   nur bei „Individuell"
5. `AiActionRequest` Feldliste (6 Zustände, `ParameterSnapshot`,
   `IdempotencyKey` unique, `CorrelationId`, Claim-Felder,
   `McpAccessName`-Snapshot + Soft-Referenz)
6. Audit-Tabellen: `McpAuditEntry` (Grundgerüst + `SequenceNo`,
   `PrevHash`/`EntryHash`) + `McpAuditEntryChange` (Feld-Deltas als
   eigene Zeilen für Drilling) + Begründung Zweitabellen-Entscheidung
7. Indizes-/Constraints-Übersicht
8. Retention-/Löschverhalten (12/36 Monate, `RetentionPurged`,
   Hash-Kette unter Retention, Request-Fristen)
9. Migration-Strategie (DbSet+OnModelCreating+`dotnet ef migrations`,
   `ApplyMigrationsAndSeed`, kein SchemaPatcher, additive Erweiterung)
10. Backup-/Restore-Einbindung (Konfiguration ja, **Token-Hash nein** –
    Restore erfordert Rotation; AiActionRequest + Audit ja)
11. Auswirkungen auf Bestands-Tabellen: **keine** (Soft-Referenzen nur
    in Richtung neu→alt)
12. Konsistenzanforderungen

## Verbindliche Kanten, die eingehalten werden

- Scope-Tupel (Datentyp × Rechteart × Filter), Prüfstufen 0–3,
  „einschränkend gewinnt", keine Deny-Scopes (Schritt 1)
- 4 Sicherheitsklassen, 33 Tools T01–T33, `katalog-v<n>`, Meta-Scope,
  `IStatementImportQueryService` als offener Baustein (Schritt 2)
- PAT `fm_mcp_<kürzel>_<secret>`, `pbkdf2|…`-Hash, Kill-Switches
  `Mcp:Enabled`/`Mcp:WriteAccess:Enabled`, Rate-Limit-Partitionen,
  IP-Bindung, TLS-Ziel/HTTP-Fallback (Schritt 3)
- `AiActionRequest` 6 Zustände inkl. `Expired`, Tabelle = persistente
  Queue, `IdempotencyKey`, `ParameterSnapshot`-Treue (Schritt 3)
- Audit: 5 Ereigniskategorien, abgestufte Detailtiefe, Hash-Verkettung,
  Retention 12/36 Monate (Schritt 3)
- UI: nur `IApiClient`, MVVM-ViewModels, `IConfirmationService`,
  resx de/en, keine Direkt-Service-Aufrufe

## Code-Anker, die geprüft wurden

`ViewModelBase`/`BaseListViewModel`/`BaseCardViewModel`/`CardRoute`,
`ListViewModelFactory`, `SetupCardViewModel`+`SetupSections.razor`
(Section-Accordion-Muster), `MainLayout.razor` (NavLinks),
`IApiClient`/`ApiClient.*.cs` (Partial-Muster, `Admin_ListUsersAsync`),
`AppDbContext` (DbSet+inline `OnModelCreating`, Enum→short, Unique-
Indizes), `Entity`-Basisklasse, `IBackupService`/`*BackupDto`,
`IConfirmationService`, `NotificationType`/`NotificationTarget`,
`ProgramExtensions` (Middleware-Reihenfolge, `ApplyMigrationsAndSeed`),
`appsettings.json` (Feature-Flag-Muster `BackgroundTasks:Enabled`,
`ReverseProxy:KnownProxies`).
