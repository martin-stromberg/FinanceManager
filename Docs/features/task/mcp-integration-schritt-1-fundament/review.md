# Review – Teil A: Domain-/Persistenz-Fundament

## Status: umgesetzt, bereit für Commit/Push

### Geliefert
- `FinanceManager.Domain/Mcp/`:
  - `McpAccess.cs` — Aggregate mit Lebenszyklus-Methoden (`RotateToken`, `Activate`,
    `Deactivate`, `Revoke`, `RecordUse`, `IsCurrentlyUsable`, `IsExpired`,
    `ReplaceIndividualScopes`, `Rename`, `Update`) und `McpAccessBackupDto`
    (ohne `TokenHash`/`AccessKeyHint`, `ToBackupDto` → `Status = Revoked`).
  - `McpAccessScope.cs` — Scope-Tupel `DataType`×`Right` + 5 Filter-CSVs, FK `McpAccessId`,
    denormalisiertes `OwnerUserId`, `McpAccessScopeBackupDto`.
  - `McpAuditEntry.cs` — append-only, `SequenceNo`, `PrevHash`/`EntryHash`,
    `Changes`-Navigation + `AddChange`, `McpAuditEntryBackupDto`.
  - `McpAuditEntryChange.cs` — Feld-Deltas, `McpAuditEntryChangeBackupDto`.
  - 6 Enum-Dateien mit normativen Wertelisten (datenmodell §2.2).
- `AppDbContext`: 4 DbSets + Konfiguration (Unique `AccessKeyHint`, `(OwnerUserId,Name)`,
  `(OwnerUserId,SequenceNo)`; Indizes exakt datenmodell §7; Cascade Scopes/Changes;
  Enum→`short`).
- EF-Migration `20261104174706_AddMcpFoundation` (4 Tabellen, 14 Indizes); verifiziert via
  `dotnet ef database update` auf temporärer SQLite-DB.
- `EnumCoverageTests`: 6 neue `[Fact]`; `enum-coverage-check.py --all --strict` OK.
- Lifecycle-Artefakte in diesem Verzeichnis.

### Abweichungen/Entscheidungen
- `McpAccessScope.OwnerUserId`: denormalisiert hinzugefügt (Lieferumfang Vorgabe); erleichtert
  Owner-scoped Queries und `ClearUserDataAsync`-Muster; kein Konflikt zum datenmodell
  (Besitz weiterhin über `McpAccessId` abgeleitet).
- `RotateToken` reaktiviert einen widerrufenen Zugang bewusst (Status→`Active`,
  Revokationsdaten geleert) — das ist der normative Reaktivierungspfad nach Backup-Restore
  (datenmodell §10). `Activate`/`Deactivate` verweigern bei `Revoked`.
- `McpAuditEntry`/`McpAccess` wurden als `IAggregateRoot` markiert; Scopes/Changes sind
  Kind-Entitäten (Cascade, kein eigener Aggregate-Status) — wie im datenmodell festgelegt.
- Unique `(OwnerUserId, SequenceNo)`: unter SQLite erlaubt `NULL`-Owner weiterhin Duplikate;
  die globale Kette wird applikatorisch abgesichert (datenmodell §6.1, dokumentierte
  Einschränkung — Teil B muss dies beim Audit-Writer beachten).

## Risiken / offene Punkte für Teil B
1. **Backup-Service-Registrierung fehlt bewusst** — `IBackupService`/`BackupService` ist noch
   nicht erweitert (war nicht Teil dieses Laufs). Teil B muss Export (Accesses, Scopes, Audit
   inkl. Changes) + Restore einbauen. Restore braucht Platzhalter-`TokenHash`/`AccessKeyHint`
   (required, unique) — Empfehlung: zufällige, nie ausgegebene Werte generieren oder direkt
   rotieren; `AssignBackupDto` setzt `Status=Revoked` zwangsweise.
2. **`AccessKeyHint`-Unique bei Restore:** Bestands-Id/Hinweis könnten kollidieren — der
   Platzhalter-Hint muss global eindeutig sein (z. B. aus neuer Id ableiten).
3. **Audit-Writer (F-0301/F-0302):** Hash-Ketten-Kanonisierung, `SequenceNo`-Vergabe je Kette
   (Owner/global) und das NULL-Owner-Unique-Problem sind Service-Logik — hier ist Sorgfalt
   nötig (Transaktion, monotone Vergabe unter Nebenläufigkeit).
4. **`Mcp:Enabled`-Flag:** noch nicht angebunden — `appsettings`-Default `false` in Teil B
   setzen, damit Migration ≠ Aktivierung bleibt.
5. **`ClearUserDataAsync`:** neue Tabellen sind dort noch nicht berücksichtigt (Löschung
   Benutzerdaten) — Teil B ergänzen (Scopes/Audit via OwnerUserId bzw. Cascade).
6. **Ältere EF-Tools-Version** (10.0.10 vs. Runtime 10.0.14): nur Warnung, funktional ok.
7. **Enum-Coverage-Tests** prüfen nur Namen — Wertebelegung (0..n) wird vom normativen
   Dokument sichergestellt; Änderungen an Enum-Werten sind konzeptpflichtig.

---

# Review – Teil B: Endpoint, PAT-Auth, ScopeEvaluator, Registry, Verwaltungs-API

## Status: umgesetzt, bereit für Commit/Push

### Geliefert
- **PAT (F-0102):** `McpTokenIssuer` (`fm_mcp_<hint8>_<256bit-b64url>`),
  `McpTokenHasher` (`pbkdf2|iters|salt|key`, `Mcp:TokenHash:Iterations`,
  `FixedTimeEquals`), `McpAuthHandler` (Scheme `McpPat`, pro Request Status/Expiry/
  Revokation/IP-Bindung, `LastUsedUtc` throttled ~1/min, `IIpBlockService`-Fehlversuche,
  einheitliches 401 via `HandleChallengeAsync`), `IMcpAccessContext` (Owner nie
  client-seitig), `McpIpRangeMatcher` (Single-IP/CIDR/IPv4-mapped).
- **`/mcp` (F-0103):** JSON-RPC 2.0 `initialize`/`ping`/`tools/list`/`tools/call`,
  Notifications → 202, Batch-Arrays, `Mcp-Session-Id`-Session-Hub (idle-Timeout, 404 bei
  unbekannter Session, DELETE→204), GET→405, 1-MiB-Envelope-Limit. Kein Cookie/JWT-Pfad.
- **Registry+Dispatcher (F-0106):** `IMcpToolDescriptor` (Scope-Tupel ODER `MetaScope`,
  `McpToolSecurityClass`, `InputSchema`, `IsDeprecated`), `McpToolRegistry` (`katalog-v1`),
  `McpToolDispatcher` mit Reihenfolge known → WriteStop → Klassen-RateLimit → Scope → Invoke.
  Live: **T32 `fm_capabilities_describe`** (effektives Profil des Zugangs) und
  **T33 `fm_task_status`** (eigene `BackgroundTaskInfo`-Metadaten; AiActionRequest-Status
  folgt mit dem Approval-Feature).
- **ScopeEvaluator (F-0105):** `McpScopeMatrix` (F3-Server-Wahrheit, Buchung nur
  StatementDrafts, Postings schreibgeschützt, Imports/AccountTypes read-only),
  `McpLevelProfiles` (Analyse/Assistent/Sachbearbeiter/Buchhalter-Laufzeitprofile),
  `CheckRecordAccessAsync` (Anker-Ableitung: Account/Share-Decke, Contact, SavingsPlan,
  Security, StatementImport→Account, Draft→Owner; Verdict Allowed/Denied/NotFound),
  Filter AND je Dimension / OR über Rows / unklassifiziert nicht gematcht.
- **Verwaltung (F-0104):** `IMcpAccessService`/`McpAccessService` (Create+Plaintext einmalig,
  Rotate=sofortige Invalidierung, Revoke/Activate/Deactivate, Expiry/IP, individuelle Scopes
  mit F3-Validierung `Err_Mcp_InvalidScope`/`Err_Mcp_NameInUse`/`Err_Mcp_InvalidState`,
  `levels`/`options` inkl. Kategorielisten+TLS-Flag), `McpAccessesController`
  (`/api/mcp-accesses`, JWT-`[Authorize]`, `ICurrentUserService`, `ApiErrorDto`/`Err_Mcp_*`),
  `Shared/Dtos/Mcp/*` (short-Enum-Transport), `ApiClient.Mcp.cs` + `IApiClient`.
- **Sicherheits-Infra (F-0107):** `McpOptions` (`Mcp:Enabled=false` in `appsettings.json`;
  Development **nicht** angefasst), `McpDisabledMiddleware` (503 via `UseWhen`,
  IOptionsMonitor → reload-wirksam), Pipeline RequestLogging→IpBlock→RateLimit→
  KillSwitch→McpAuth, `McpEndpoint`-RateLimit-Policy (hint-Partition authentifiziert /
  IP-Partition anonym), `McpToolRateLimiter` je Klasse, `McpAnomalyTracker`
  (Scope-Verweigerungs-Schwelle → Owner EventDriven + Admin SystemAlert).
- **Audit:** `IMcpAuditWriter` + Call-Sites (AuthFailed, ToolCall inkl. Duration/Correlation,
  AccessEvent) — registriert ist der dokumentierte No-Op `NullMcpAuditWriter`; persistenter
  Hash-Ketten-Writer kommt mit F-0301/F-0302 (Schritt 3).
- **Backup/Cleanup:** Export der 4 Mcp-Tabellen als optionale Payload-Arrays
  (`McpAccessBackupDto` exportiert nie Tokenmaterial, Status immer Revoked); Restore mit
  eindeutigem Platzhalter-Hint (`rst<9hex>`) + nie-verifizierendem Platzhalter-Hash →
  Zugang erst nach Rotation nutzbar; `ClearUserDataAsync` erweitert (30 Schritte).
- **Tests:** 93 neue/grüne Fakten (Token-Issuer/Hasher, Access-Service-Lebenszyklus+Owner-
  Isolation, Scope-Evaluator-Kernfälle, IP-Matcher, Enum-Coverage +2 Enums) + 5 Integration
  (PAT-Flow `tools/list`/`tools/call`, einheitliches 401, GET→405, `Enabled=false`→503,
  JWT authentifiziert MCP nicht). `dotnet test FinanceManager.Tests`: **1582/1582**;
  Integration: **155/155**.

### Abweichungen/Entscheidungen
- **Eigenes JSON-RPC-Transport-Layer** statt MCP-C#-SDK (dokumentierter Fallback
  Architekturkonzept §5): kein NuGet für `ModelContextProtocol` im Projekt; der Endpoint
  implementiert nur benötigte Methoden + Session-Grundlage, SSE-Streaming bewusst ausgelassen.
- **Token-Parsing:** erstes `_` nach `fm_mcp_` trennt Hint/Secret (Base64url-Secret darf `_`
  enthalten) — Bug in erstem Entwurf gefunden und gefixt.
- **Rate-Limit 429 nur Endpoint-Ebene;** Klassen-Limit im Dispatcher liefert definierbaren
  `Err_Mcp_RateLimited`-JSON-RPC-Fehler (Request bereits angenommen).
- **Sachbearbeiter-Write-Set** konkretisiert: `Complete/Modify/Create` auf
  `StatementDrafts`, `Contacts`, `ContactCategories` (vervollständigende Stammdaten).
- **`POST /mcp` liefert immer 200** mit JSON-RPC-Fehlerobjekt (MCP-Konvention);
  HTTP-Fehler bleiben 401/405/503/429/404-Session.
- **`McpAccessUpdate`**: Scopes nur über `/scopes` + `/filters/{scopeId}` änderbar
  (Filter pro persistierter Scope-Row, nicht pro Tupel — einfacheres UI-Modell).
- **`McpAccessesController` nutzt `JwtBearerDefaults.AuthenticationScheme`** — exakt die
  Konvention aller bestehenden Controller (ApiClient sendet Bearer; Cookie-/CSRF-Pfad
  unverändert getrennt vom MCP-Endpoint).

## Risiken / offene Punkte für Teil C
1. **`NullMcpAuditWriter` ist No-Op:** alle Call-Sites leben, aber es wird **nichts**
   persistiert — kein revisionsfestes Protokoll bis F-0301/F-0302 (Chain-Kanonisierung,
   SequenceNo-Vergabe unter Nebenläufigkeit, NULL-Owner-Unique-Problem aus Teil A).
2. **Session-Rebind:** `McpSessionHub` kennt Sessions ohne Access-Bindung; nach
   Token-Rotation bleibt die Session formal gültig — Bindung Access↔Session in Teil C.
3. **Fach-Tools T01–T31 fehlen:** Registry/Dispatcher/Evaluator sind generisch;
   Adapter folgen im Fachkonzept-Schritt. `tools/list` liefert aktuell nur die 2 Meta-Tools
   (katalog-v1-Vollkatalog erst mit Fach-Tools).
4. **Rate-Limit-In-Memory:** Klassen-Limits sind prozess-lokal — kein Multi-Instance-Teilen
   (Selbsthosting-Szenario okay, bei HA-Betrieb beachten).
5. **`LastUsedUtc`-Race:** `ExecuteUpdate` ist atomar, aber ohne Rowversion — bewusst
   akzeptiert (Statistikfeld, kein Sicherheitszustand).
6. **Approval-Modus `PerAction`** ist deklariert, aber ohne AiActionRequest-Feature noch ohne
   Wirkung (keine schreibenden Tools live) — Konzept-Verweis für Schritt 3.

---

# Review – Teil C: UI-Zugangsverwaltung und Token-Wizard (F-0108)

## Status: umgesetzt, bereit für Commit/Push

### Geliefert
- **Navigation:** Eintrag „KI-Zugänge" (`Nav_McpAccesses`) in `MainLayout.razor` nach
  „Setup" und vor den Admin-Einträgen → `/list/mcp-accesses` (alle authentifizierten
  Benutzer, kein Admin-Gate — jeder verwaltet eigene Zugänge).
- **Liste `/list/mcp-accesses`:** `McpAccessListViewModel : BaseListViewModel<McpAccessListItem>`
  + `case "mcp-accesses"` in `ListViewModelFactory`. Spalten Name, Freigabestufe (Klartext),
  Status (aktiv/deaktiviert/abgelaufen/widerrufen lokalisiert), Ablaufdatum, `LastUsedUtc`
  („nie" bei Neu); deaktivierte/abgelaufene Zeilen `muted`; Suchfilter serverseitig (`q`),
  Paging via `skip`/`take`. Ribbon: „Neu" (öffnet Anlage-Assistent-Overlay), „Aktualisieren".
  `IListItemNavigation` → `/card/mcp-accesses/{id}`.
- **Karte `/card/mcp-accesses/{id}`:** `McpAccessCardViewModel : BaseCardViewModel` mit
  `[CardRoute("mcp-accesses")]`. CardRecord-Stammdaten editierbar (Name, Beschreibung,
  Stufe als `Enum:McpAccessLevel`-Lookup mit `levels`-Klartext-Hint, Genehmigungsmodus,
  Ablaufdatum, IP-Bindung) + readonly Status/Letzte-Nutzung/TokenIssued/Erstellt.
  Sections via `GetEmbeddedPanelSpecs(AfterCard)` → `McpAccessExtrasPanel`:
  Token (Issued/Status, TLS-Warnung bei `!IsTlsRequest`, „Token rotieren"),
  Rechte (Stufe + effektives Profil + Deckel-Hinweistext), Status-Aktionen
  (Aktivieren/Deaktivieren/Widerrufen), Nutzung (`LastUsedUtc` + Platzhalter für
  Verlauf/Audit-Ansicht EP-06). Alle kritischen Aktionen via `IConfirmationService`
  (Rotation+Widerruf+Deaktivierung `Critical`, Stufen-Downgrade `Warning`, Aktivierung
  `Default`). Ribbon spiegelt: Back, Save, Rotate, Activate/Deactivate, Revoke.
- **Token-Wizard:** `McpAccessCreateWizard.razor` (UiOverlaySpec, läuft auf Listen- und
  Karten-OverlayHost): Schritt 1 Stammdaten (+„Erweiterte Einstellungen" Ablaufdatum/
  IP-Bindung), Schritt 2 Freigabestufe als Auswahlkarten mit Klartext-Erklärung
  (`levels`-Endpunkt + `Mcp_LevelDesc_*`) + Genehmigungsmodus-Auswahl ab Sachbearbeiter,
  Schritt 3 Einmalanzeige via `McpTokenDisplayPanel` (Copy-Box + `financeManager.copyText`,
  „nur einmal sichtbar"-Warnung, Pflicht-Checkbox „gespeichert" vor Schließen). Danach
  Navigation auf die neue Karte. Klartext nur in Dialog-Instanz, kein Caching/LocalStorage.
- **`Mcp:Enabled=false`:** `Err_Mcp_Disabled` wird zu freundlichem lokalisiertem Hinweis —
  Liste rendert dedizierte Hint-Row, Karte zeigt `LastError`-Banner (SetError→resx).
- **Lokalisierung:** 134 neue Keys je `Pages.resx`/`Pages.de.resx`/`Pages.en.resx`
  (Stufen-Klartexte, 7 Rechtearten, 18 Datentypen, Status/Genehmigungsmodus, Wizard,
  Token-Panel, Sections, 6 Confirmations, `EnumType_Mcp*` für Lookup-Dropdowns,
  `Err_Mcp_*` ×10, `Btn_Back`/`Btn_Next`/`Err_NotFound` ergänzt). Kein hartkodierter
  UI-String in .razor (razor-l10n-check OK).
- **Enum-Coverage:** `McpDispatchResultKind` (Success/DefinedError/ProtocolError/ToolError)
  in `EnumCoverageTests` ergänzt — `--all --strict` OK.
- **Format:** `dotnet format --verify-no-changes --severity error` war durch Teil-B-Reste
  rot → Whitespace-Normalisierung `McpEndpointExtensions.cs`, `FmTaskStatusTool.cs`,
  `McpEndpointTests.cs` (nur Formatierung).

### Abweichungen/Entscheidungen
- **Erstellung ausschließlich über Wizard-Overlay** (nicht `/card/.../new`): entspricht
  UI-Konzept „Standardfall in einer Minute"; die Karte ist reine Bestands-Verwaltung.
- **Genehmigungsmodus-Auswahl** im Wizard erst ab Stufe Sachbearbeiter (darunter ohne
  Wirkung); Karte erlaubt Bearbeitung immer (Server validiert).
- **Scope-Matrix-Editor/Filterverwaltung (Expertenmodus)** bewusst auf späteren Task
  verschoben — Rechte-Section zeigt das effektive Profil lesend (Endpoints
  `/scopes`/`/filters` existieren bereits in API+ApiClient).
- **`McpAccessExtrasPanel`/`McpAccessCreateWizard`** werden über `typeof(...)` in den
  Title-/Label-Maps von `OverlayHost`/`ListPage`/`EmbeddedPanelHost` referenziert
  (razor-usage-check --strict verlangt .razor-Verweis, `DynamicComponent` zählt nicht).

## Risiken / offene Punkte für Folge-Tasks
1. **Expertenmodus fehlt:** individuelle Rechte/Filter (Matrix-Editor, Kategorie-Picker)
   — Section „Rechte" ist aktuell lesend.
2. **Offene-Anfragen-Badge** am Nav-Eintrag + Genehmigungscenter fehlen (Approval-Feature).
3. **Audit-/Verlaufsansicht** ist Platzhalter-Text (EP-06 / F-0301/0302).
4. **`copyText`-Fallback** (`execCommand`) für Nicht-Clipboard-Kontexte; modern.
5. **Kein E2E-Test** für die neuen Seiten — manuelle Abnahme empfohlen (Wizard→Token→
   Rotation→Widerruf).

---

# Korrekturen Abnahme 1

Gegenstand: `Docs/projects/task/mcp-integration/acceptance-schritt-1.md` (3 Abweichungen).

- [x] **`untrusted_data`-Kennzeichnung in Meta-Tool-Ausgaben** (Abweichung 2):
  T32 `fm_capabilities_describe` liefert die benutzerbelegten Freitextfelder
  (`name`, `description`, `allowedIpRanges`) jetzt ausschließlich im markierten
  `untrusted_data`-Wrapper unter `access`; T33 `fm_task_status` verpackt
  `message`/`errorDetail`/`warnings`/`errors` je Task in `untrusted_data`.
  Konvention sicherheitskonzept.md §5.2 (Wrapper-Objekt, Inhaltstrennung:
  Systemfelder oben, Nutzdaten-Freitext markiert) — verbindlich für die
  Fach-Adapter ab EP-02.
- [x] **„Kein Tool ohne Scope" durchgesetzt** (Abweichung 3): `McpToolRegistry`
  validiert im Konstruktor jeden `IMcpToolDescriptor` — genau eines von
  `RequiredScopes` (≥1 Tupel) oder `MetaScope` muss deklariert sein, sonst
  `InvalidOperationException`. Zusätzlich Start-up-Check: `ConfigureMiddleware`
  löst die Singleton-Registry einmal auf, damit ein ungültiger Katalog den
  Start abbricht statt erst beim ersten `/mcp`-Aufruf. Tests:
  `FinanceManager.Tests/Mcp/McpToolRegistryTests.cs`.
- [ ] **SSE-Upgrade bewusst in EP-11/F-1101** (Abweichung 1): dokumentierte
  Einschränkung — `GET /mcp` bleibt 405, Request/Response +
  `fm_task_status`-Polling ist der Frühphasen-Pfad (architekturkonzept.md
  §3.1/§3.2, eigener Transport-Layer statt MCP-C#-SDK). Das SSE-Upgrade für
  Server-Benachrichtigungen ist im Backlog-Feature F-1101 (EP-11) eingeplant.
