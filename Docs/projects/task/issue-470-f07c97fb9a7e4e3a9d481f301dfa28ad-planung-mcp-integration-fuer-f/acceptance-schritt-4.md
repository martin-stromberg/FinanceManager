# Abnahmeprüfung – Entwicklungsschritt 4

## Ergebnis

**Status:** Anforderung vollständig erfüllt

## Abweichungen

- Keine.

## Hinweise

**Lieferumfang (Diff `task/...-planung-mcp-integration-fuer-f...HEAD`, Commit `9fb85474`)**

- Drei deutschsprachige Lieferdokumente unter `Docs/planung/mcp-integration/`:
  `ui-konzept.md` (518 Zeilen), `architekturkonzept.md` (567 Zeilen),
  `datenmodell-erweiterungen.md` (416 Zeilen). Plus Lifecycle-Artefakte
  (`plan.md`, `requirement.md`, `review.md`, `test-results.md`, `todo.md`)
  im Feature-Verzeichnis. Keine Codeänderungen (Planungsaufgabe, A3).

**Akzeptanzkriterium 1 (Dokumente vorhanden, deutsch, am Ort)** – erfüllt.

**Akzeptanzkriterium 2 (UI-Konzept, F11)** – erfüllt. Alle sechs
Komponenten: Zugangsverwaltung (§4.1: Liste `/list/mcp-accesses`, Karte mit
Sections, Aktivieren/Deaktivieren, `ExpiresUtc`-Warnung), Tokenverwaltung
(§4.2: Anlage-Assistent, Einmalanzeige mit Pflicht-Bestätigung, Rotation,
Widerruf, HTTP/TLS-Hinweis), Rechteverwaltung (§4.3: Stufen-Auswahlkarten
mit Klartext, Experten-Matrix-Editor 18×7 nur auf ✓/◑-Zellen der
F3-Matrix, Implikationshilfe, Genehmigungsmodus), Filterverwaltung (§4.4:
5 Dimensionen, „ohne Kategorie nicht erfasst"-Hinweis), Audit-Ansicht
(§4.5: Timeline, Filter, Drilling über `CorrelationId`, Export,
Integritätsprüfung), Genehmigungscenter (§4.6: Eingangskorb,
Wirkungsvorschau aus `ParameterSnapshot`, Batch, Historie). Einfache
Führung/Expertenmodus/Verständlichkeit: §1 Leitlinien 1–6, §7
Verständlichkeits-Mapping. Konventionen verankert: §2 (Blazor Server
`InteractiveServer`, `ViewModelBase`/`BaseListViewModel`/`BaseCardViewModel`,
`CardRoute`/`ListViewModelFactory`, `IApiClient`-only, resx de/en,
`UiOverlaySpec`, `IConfirmationService`); API-Skizze §6
(`McpAccessesController`, `AiActionRequestsController`,
`McpAuditController`, Partial `ApiClient.Mcp.cs`, `Err_Mcp_*`);
Benutzer-/Admin-Sichten §5.

**Akzeptanzkriterium 3 (Architekturkonzept, F12)** – erfüllt.
MCP-Server-Struktur (§4 Komponentendiagramm + Projektplatzierung),
Betriebsform final entschieden (§2: in-process `FinanceManager.Web`,
Route `/mcp` – behandelt Projektplan „Offene Punkte" Nr. 1; Alternativen
separater Prozess/entferntes Hosting/nur-stdio begründet verworfen),
Transport Streamable HTTP mit Alternativen (§3, Annahme A4 verbindlich),
Service-Layer-Anbindung via Adapter auf `I*Service(ownerUserId)` (§4.3,
`IStatementImportQueryService` als offener Baustein T08 bestätigt),
Sicherheitslayer als verbindliche 10-Stufen-Pipeline inkl. Token→UserId-
Auflösung `IMcpAccessContext`, Scope-Prüfung, Rate-Limit, Kill-Switches
(§5), Tool-Registrierung `McpToolRegistry`/`katalog-v<n>` (§6.1),
Ausführungsmodell direkt/`AiActionRequest`/Queue-Executor + Idempotenz
(§6.2–6.3), Logging-/Audit-Anbindung transaktional festgelegt (§7),
Persistenzmodell-Übersicht (§8), `Mcp:*`-Konfiguration (§9), Deployment
HTTP-LAN vs. Reverse-Proxy/TLS, Auto-Update (§11).

**Akzeptanzkriterium 4 (Datenmodell)** – erfüllt. Neue Entitäten
`McpAccess`, `McpAccessScope`, `AiActionRequest`, `McpAuditEntry`,
`McpAuditEntryChange` + 7 Enums (`McpDataType` 18 Werte = F3-Matrix,
`McpRightKind` 7 Werte, `AiActionRequestStatus` 6 Zustände inkl.
`Expired`, `McpAuditCategory` 5 Werte = Audit-Kategorien). Owner-Bezüge,
Beziehungen (Soft-Referenzen mit `McpAccessName`-Snapshot, Cascade),
`Entity`-Konvention, Indizes/Unique-Übersicht (§7), Retention 12/36 (§8),
Migration via DbSet+`OnModelCreating`+`ApplyMigrationsAndSeed` (§9),
`*BackupDto`-Einbindung mit begründetem Token-Hash-Ausschluss (§10),
keine Änderung an Fachentitäten/indirekte Owner-Ableitung bleibt (§11).

**Konsistenz zu Schritten 1–3 (Stichproben, alle bestanden)**

- Scope-Tupel, Prüfstufen 0–3, „einschränkend gewinnt", keine Deny-Scopes,
  `AccountShare`-Deckel Read/Write/Admin, UND/ODER-Filtersemantik,
  unklassifiziert-nicht-erfasst (berechtigungsmodell §§1–8 ✓).
- 4 Sicherheitsklassen, T01–T33, `katalog-v<n>`, Meta-Scope T32/T33 nicht
  persistiert (Toolkatalog §§2, 3.6, 5.2 ✓); T08-Baustein
  `IStatementImportQueryService` übernommen.
- PAT `fm_mcp_<kürzel>_<secret>`, `pbkdf2|…`-Hash (`AccessKeyHint` als
  unique Lookup), Kill-Switches `Mcp:Enabled`/`Mcp:WriteAccess:Enabled`,
  IP-Bindung, HTTP-Fallback + UI-Hinweispflicht (sicherheitskonzept §§3,
  6, 7, 8.2 Nr. 5 ✓).
- `AiActionRequest`-Feldliste 1:1 aus approval-konzept §3 + dokumentierte
  Queue-Claim-Erweiterung; 6 Zustände, `IdempotencyKey` unique,
  72-h-Default, Widerrufs-Szenarien (§6.3) in der UI berücksichtigt.
- 5 Audit-Kategorien → `McpAuditCategory`, Grundgerüst-Felder →
  `McpAuditEntry`, Hash-Kette (`SequenceNo`/`PrevHash`/`EntryHash`),
  `RetentionPurged`-Systemereignis, Admin-Sicht ohne Fachnutzlast
  (audit-konzept §§3–7 ✓).

**Code-Stichproben (alle verifiziert)**

`Entity.cs` (Guid Id, CreatedUtc, ModifiedUtc?); `AppDbContext.cs`
(DbSet+OnModelCreating, `HasConversion<short>`); `ProgramExtensions.cs`
(Middleware: ForwardedHeaders→RequestLogging→IpBlock→RateLimiter→…→
Authentication→Antiforgery→CookieCsrf, `ApplyMigrationsAndSeed` →
`db.Database.Migrate()` Z. 643, `SchemaPatcher`, `BackgroundTasks:Enabled`,
`ReverseProxy:KnownProxies`); `ViewModelBase`/`BaseListViewModel`/
`BaseCardViewModel`/`CardRoute`/`ListViewModelFactory` (case-Pattern +
`ActivatorUtilities`); `MainLayout.razor` (NavLinks, `/list/users` Admin);
`IConfirmationService.ConfirmAsync` + `ConfirmationSeverity` +
`ShowConfirmations`-Migration; `IApiClient`-Partial-Muster;
`ApiErrorDto`/`Err_*`; `take`-Clamp-Konvention; `INotificationWriter`,
`NotificationType.EventDriven`/`SystemAlert`, `NotificationTarget`;
`StatementDraftBookingGuard`, `BackgroundTaskRunner`,
`MonthlyReminderScheduler`, `IBackgroundTaskManager`;
`Pbkdf2IdentityPasswordHasher`, `dp:v1:`-Prefix;
`ReportFavorite.*IdsCsv`-Muster; `ICurrentUserService`/`IsAdmin`;
`IPostingReversalService` (`CanReverseAsync`/`GetRelatedPostingsAsync`),
`IStatementDraftService` (`BookAsync`, `ApplyBatchEntryUpdatesAsync`),
`IMassImportOrchestrator`, `AccountShare`, `msTools.Updater`;
resx `Resources/Pages.*`/`Resources/Components.*`; Route
`/card/{Kind}` ohne Id vorhanden (`/card/setup`-Vorbild für
`/card/mcp-approvals`).

**Restbeobachtungen (kein Kriterienverstoß, kein Handlungsbedarf)**

- Reihenfolge IpBlock↔RateLimiter: `sicherheitskonzept.md` §9 nennt
  „Rate Limiter → IpBlock", das Architekturkonzept operationalisiert
  „IpBlock (Pos. 2) → RateLimiter (Pos. 3)" – exakt die reale
  Middleware-Reihenfolge (`ProgramExtensions.cs` Z. 496–498). Die
  codeverankerte Reihenfolge ist fachlich gleichwertig bzw. strenger;
  beide Schichten liegen vor der Auth.
- `approval-konzept.md` §3.3 schlägt die Entscheidungsfrist
  „konfigurierbar je Zugang" vor (als Vorschlag formuliert); Schritt 4
  sieht nur den globalen Default `Mcp:Approval:DefaultExpiryHours` vor,
  kein Feld je `McpAccess`. Zulässige Vereinfachung, ggf. in Schritt 5/
  Umsetzung nachzuziehen, falls je-Zugang-Fristen gewünscht werden.
- „high severity" im UI-Konzept ist fachliche Lesart; der reale Enum
  `ConfirmationSeverity` kennt `Default`/`Warning`/`Critical`
  (Mapping: Critical).
