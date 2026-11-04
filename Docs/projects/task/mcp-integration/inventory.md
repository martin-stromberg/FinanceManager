# Inventory – FinanceManager (Ist-Stand für Issue #470, MCP-Integration)

**Zweck:** Bestandsaufnahme des tatsächlichen Code-Stands als Grundlage für das Fach-,
Sicherheits- und Architekturkonzept zur MCP-Integration (Anforderung: `requirement.md`).

**Stand:** Branch `task/issue-470-…-planung-mcp-integration-fuer-f`, .NET 10 (`net10.0`).

**Kernaussage vorab:** Es existiert **keinerlei** MCP-/KI-/Token-/Audit-Infrastruktur im
Code (Suche nach `Mcp`, `Ai*`, `Audit`, `ApiKey`, `ActionRequest` erbrachte nur
AlphaVantage-API-Key-Handling). `AiActionRequest` ist **nicht** vorhanden und muss im
Konzept neu entworfen werden. Vorhandene, wiederverwendbare Bausteine sind in
[Abschnitt 9](#9-wiederverwendbare-bausteine) aufgelistet.

Detaildokument: [inventory/entitaeten.md](inventory/entitaeten.md) – vollständiger
Entitätskatalog mit Feldlisten und Owner-Bezug.

---

## 1. Architekturüberblick

### 1.1 Projektstruktur und Verantwortlichkeiten

Solution `FinanceManager.sln` mit fünf Produktiv- und vier Testprojekten:

| Projekt | Typ / Rolle | Bemerkung |
|---|---|---|
| `FinanceManager.Domain` | Bibliothek; Entitäten, Enums, `Entity`-Basisklasse, `Guards`, `Result`, Marker `IAggregateRoot` | Ordner pro Fachbereich (Accounts, Contacts, Statements, Postings, Savings, Securities, Budget, Attachments, Reports, Notifications, Portfolio, Security, Users, WellKnown). Keine Infrastruktur-Abhängigkeiten; Ausnahme: `User` erbt `IdentityUser<Guid>` |
| `FinanceManager.Application` | Bibliothek; Service-Contracts (`I*Service`), Hintergrundtask-Infrastruktur (`BackgroundTaskManager`, `BackgroundTaskRunner`), `ICurrentUserService`, Dtos der Anwendungsschicht | Implementierungen liegen ausschließlich in Infrastructure/Web |
| `FinanceManager.Infrastructure` | Bibliothek; `AppDbContext`, EF-Migrations, alle Service-Implementierungen, Auth (`Jwt*`, `Pbkdf2IdentityPasswordHasher`, `User*Service`), Statement-Import (Parser, Files, Orchestrator, `StatementDraftService*`), `SchemaPatcher`, `AutoInitializationService` | DI-Registrierung zentral in `ServiceCollectionExtensions.AddInfrastructure` |
| `FinanceManager.Shared` | Bibliothek; DTOs (`Dtos/<Bereich>/`), `IApiClient` + `ApiClient` (Partial-Klassen `ApiClient.<Bereich>.cs`), Shared-Enums (z. B. `Dtos/Accounts/AccountType.cs`) | Vertrag zwischen Blazor-UI und REST-API |
| `FinanceManager.Web` | ASP.NET-Core-Host; **Blazor Server** (Razor Components, `InteractiveServer`), ~35 API-Controller, ViewModels (MVVM), Middleware, Services | Alles in einem Prozess; UI ruft die eigene REST-API über `IApiClient` (HttpClient „Api“ mit `AuthenticatedHttpClientHandler`) auf – kein Direktzugriff der UI auf Services |

Verwaist/obsolet: Root-Verzeichnis `FinanceManager.Shared.Dtos.Budget/` enthält zwei
`.cs`-Dateien (`ActualPostingDto.cs`, `ExpectedBudgetOccurrenceDto.cs`), ist aber kein
eigenes Projekt und nicht in der `.sln` – wird nicht kompiliert.

### 1.2 Dependency Injection und Bootstrap

- `FinanceManager.Web/Program.cs`: `ConfigureLogging()` → `RegisterAppServices()` →
  `ApplyMigrationsAndSeed()` → `ApplyPersistedUpdateSettings()` → `ConfigureMiddleware()`.
- `ProgramExtensions.RegisterAppServices` (Web): Razor Components, `AddControllers`,
  `AddInfrastructure(connStr)`, `IHttpContextAccessor`, `ICurrentUserService`,
  Background-Task-Executoren (7 Stück als Singletons + `BackgroundTaskRunner` als
  Hosted Service), Rate Limiter, Data Protection, JWT/Identity, HttpClient „Api“,
  Notifications, AlphaVantage, Auto-Update (`msTools.Updater`), Monthly-Reminder-Scheduler.
- Lebensdauern: Scoped für EF-Context + Services; Singleton für `IBackgroundTaskManager`,
  `KnownContactCatalog`, Caches, Zeit-/Provider-Services.
- ViewModels (`FinanceManager.Web/ViewModels/…`, Basisklasse `ViewModelBase`,
  `IAsyncDisposable`, `IRibbonProvider`) werden **nicht** per DI registriert, sondern in
  den Razor-Pages per `IServiceProvider`/`ActivatorUtilities` erzeugt und kommunizieren
  ausschließlich über `IApiClient` mit der API.

### 1.3 Konventionen

- **Benutzerkontext:** Controller lesen nie Claims selbst, sondern `ICurrentUserService`
  (`_current.UserId`, `_current.IsAdmin`); sämtliche Service-Methoden erhalten
  `ownerUserId` als ersten Parameter → Besitzprüfung erfolgt in der Service-/EF-Schicht,
  **nicht** über EF Global Query Filters.
- **DTO-Muster:** `sealed record`-Dtos in `FinanceManager.Shared/Dtos/<Bereich>/`;
  Request-Dtos `*CreateRequest` / `*UpdateRequest`; domänenseitige `*BackupDto`-Records
  innerhalb der Entitäten für Backup/Restore.
- **Fehlerformat:** `ApiErrorDto.Create(origin, code, message)` mit stabilen Codes
  `Err_*` (z. B. `Err_InvalidCredentials`, `Err_Invalid_q`) + `IStringLocalizer`-
  Übersetzung (Ressourcen `Resources/Controller.*.resx`, `Pages.*.resx`).
- **API-Muster:** `[ApiController]`, `[Route("api/<bereich>")]`,
  `[Produces(MediaTypeNames.Application.Json)]`,
  `[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]` auf
  Klassenebene; Paging per `skip`/`take` (Clamp 1..200); 404 wenn Datensatz nicht
  existiert **oder** nicht dem User gehört (kein Informationsleck).
- **Build:** `WarningsAsErrors` enthält `CS1591` → XML-Dokumentationspflicht für alle
  public Members; `GenerateDocumentationFile` aktiv.

---

## 2. Domänenmodell

Basisklasse `FinanceManager.Domain/Entity.cs`: `Guid Id` (NewGuid), `CreatedUtc`,
`ModifiedUtc`, `Touch()`; `IAggregateRoot` als Marker. Nahezu alle Fachentitäten tragen
`OwnerUserId` – Ausnahmen und indirekte Besitzverhältnisse sind markiert. Vollständige
Feldlisten: [inventory/entitaeten.md](inventory/entitaeten.md).

| Entität | Datei | Owner-Bezug | Wesentliche Felder / Besonderheiten |
|---|---|---|---|
| `Account` | `Domain/Accounts/Account.cs` | `OwnerUserId` | `Type` (enum `AccountType`: **nur Giro=0, Savings=1** – „Kontotypen“ aus F3/F4 sind also genau diese zwei Werte, keine eigene Entität), `Iban` (unique je User), `CurrentBalance`, `BankContactId`→Contact, `SymbolAttachmentId`, `SavingsPlanExpectation`, `SecurityProcessingEnabled`, `IsCollectionAccount` |
| `AccountLinkedIban` | `Domain/Accounts/AccountLinkedIban.cs` | über `AccountId` | Unter-IBANs für Sammelkonten |
| `AccountShare` | `Domain/Accounts/AccountShare.cs` | `UserId` (Teilnehmer) | `AccountId`, `Role` (`AccountShareRole` Read/Write/Admin), `GrantedUtc`, `RevokedUtc` – **bereits vorhandenes Freigabemodell** (DbSet existiert; Auswertung im Service prüfen) |
| `Contact` | `Domain/Contacts/Contact.cs` | `OwnerUserId` | `Type` (`ContactType` Self=0, Bank=1, Person=2, Organization=3, Other=9; unique: ein Self pro User), `CategoryId`→ContactCategory, `Description`, `IsPaymentIntermediary`, `SymbolAttachmentId` |
| `ContactCategory` | `Domain/Contacts/ContactCategory.cs` | `OwnerUserId` | „Kontaktgruppen“ der Anforderung = ContactCategory; unique (Owner,Name) |
| `AliasName` | `Domain/Contacts/AliasName.cs` | über `ContactId` | `Pattern` – Matching-Muster für Kontoauszugs-Klassifizierung |
| `StatementDraft` | `Domain/Statements/StatementDraft.cs` | `OwnerUserId` | `Status` (Draft/Committed/Expired), `OriginalFileName`, `DetectedAccountId`, `UploadGroupId`, `IsPreliminary`, `Description` |
| `StatementDraftEntry` | `Domain/Statements/StatementDraft.cs` (gleiche Datei) | über `DraftId`→Draft | `EntryNumber`, `BookingDate`, `ValutaDate`, `Amount`, `Subject`, `RecipientName`, `CurrencyCode`, `Status` (Open/Announced/Accounted/AlreadyBooked), `ContactId`, `SavingsPlanId`(+`ArchiveSavingsPlanOnBooking`), `SecurityId`, `SecurityTransactionType`, `SecurityQuantity`, `SecurityFeeAmount`, `SecurityTaxAmount`, `SplitDraftId`, `IsAnnounced`, `IsCostNeutral` |
| `StatementDraftBookingGuard` | `Infrastructure/Statements/StatementDraftBookingGuard.cs` | `OwnerUserId` | `DraftId`, `LockToken`, `AcquiredUtc`, `ExpiresUtc` – persistenter Sperrmechanismus gegen Doppelverbuchung |
| `StatementImport` | `Domain/Statements/StatementImport.cs` | über `AccountId`→Account | `Format` (`ImportFormat`), `ImportedAtUtc`, `OriginalFileName`, `TotalEntries` |
| `StatementEntry` | `Domain/Statements/StatementEntry.cs` | über `StatementImportId`→Import→Account | `RawHash` (**unique-Index** = technischer Dublettenschutz), `BookingDate`, `Amount`, `Subject`, `ContactId`, `SavingsPlanId`, `SecurityTransactionId`, `Status` (Pending/Booked/IgnoredDuplicate) |
| `Posting` | `Domain/Postings/Posting.cs` | **kein** `OwnerUserId`; indirekt über `AccountId`/`ContactId`/`SavingsPlanId`/`SecurityId` | `Kind` (`PostingKind` Bank=0, Contact=1, SavingsPlan=2, Security=3), `SourceId`, `GroupId`, `BookingDate`, `ValutaDate`, `Amount`, `OriginalAmount`, `Subject`, `RecipientName`, `Description`, `Quantity`, `SecuritySubType`, `ParentId`, `LinkedPostingId`, `IsPreliminary`, Storno-Felder (`ReversedByPostingId`, `ReversalForPostingId`, `ReversedByUserId`, `ReversedAtUtc`) |
| `PostingAggregate` | `Domain/Postings/PostingAggregate.cs` | indirekt über Referenz-Ids | Vorberechnete Aggregate (`Kind`, `Period`, `PeriodStart`, `Amount`, `DateKind`, `SecuritySubType`); Rebuild via `RebuildAggregatesTaskExecutor` |
| `SavingsPlan` | `Domain/Savings/SavingsPlan.cs` | `OwnerUserId` | `Type` (`SavingsPlanType`), `TargetAmount`, `TargetDate`, `Interval`, `IsActive`, `ArchivedUtc` (Archivieren statt Löschen), `CategoryId`, `ContractNumber`, `SymbolAttachmentId` |
| `SavingsPlanCategory` | `Domain/Savings/SavingsPlanCategory.cs` | `OwnerUserId` | unique (Owner,Name) |
| `Security` | `Domain/Securities/Security.cs` | `OwnerUserId` | `Identifier` (WKN/ISIN), `AlphaVantageCode`, `CurrencyCode`, `CategoryId`, `Region`, `Sector`, `IsActive`, `ArchivedUtc`, Kursfehler-Felder, `GoldenCrossNotifiedPhase`, `TrendReversalNotifiedStatus` |
| `SecurityCategory` | `Domain/Securities/SecurityCategory.cs` | `OwnerUserId` | unique (Owner,Name) |
| `SecurityPrice` | `Domain/Securities/SecurityPrice.cs` | über `SecurityId` | `Date`, `Close` – historische Kurse (Quelle: AlphaVantage/ING-Import) |
| `BudgetCategory` | `Domain/Budget/BudgetCategory.cs` | `OwnerUserId` | `Name` |
| `BudgetPurpose` | `Domain/Budget/BudgetPurpose.cs` | `OwnerUserId` | `Name`, `SourceType` (`BudgetSourceType` → verweist auf Contact/SavingsPlan/Security/Account), `SourceId`, `BudgetCategoryId`, `ValuationType` |
| `BudgetRule` | `Domain/Budget/BudgetRule.cs` | `OwnerUserId` | `BudgetPurposeId`, `BudgetCategoryId`, `Amount`, `Interval` (`BudgetIntervalType`+`CustomIntervalMonths`), `StartDate`/`EndDate`, **`PurposePattern`+`PurposePatternIsRegex` (regelbasiertes Auto-Matching auf Buchungstexte)** |
| `BudgetOverride` | `Domain/Budget/BudgetOverride.cs` | `OwnerUserId` | `BudgetPurposeId`, `PeriodYear`/`PeriodMonth`, `Amount` – manuelle Monatsüberschreibungen |
| `ReportFavorite` | `Domain/Reports/ReportFavorite.cs` | `OwnerUserId` | Persistierte Report-Konfiguration inkl. Filter-CSVs (AccountIds, ContactIds, CategoryIds, SecuritySubTypes …), `PostingKind`, `Interval`, `Take` |
| `HomeKpi` | `Domain/Reports/HomeKpi.cs` | `OwnerUserId` | `Kind` (`HomeKpiKind`), `ReportFavoriteId`, `PredefinedType`, `DisplayMode`, `SortOrder`, `Title` – KPI-Konfiguration Startseite |
| `PortfolioKpiConfiguration` | `Domain/Portfolio/PortfolioKpiConfiguration.cs` | `OwnerUserId` | `ActiveTileIds`, `TileOrder` – KPI-Konfiguration Portfolio-Report |
| `ReportCacheEntry` | `Domain/Reports/ReportCacheEntry.cs` | `OwnerUserId` | `CacheKey`, `CacheValue`, `Parameter`, `CacheValidUntilUtc`, `NeedsRefresh` |
| `Attachment` | `Domain/Attachments/Attachment.cs` | `OwnerUserId` | Polymorphe Zuordnung: `EntityKind` (`AttachmentEntityKind` – 12 Zieltypen u. a. StatementDraftEntry, Posting, Contact, Account, Kategorien) + `EntityId`; `FileName`, `ContentType`, `SizeBytes`, `Sha256`, `Content` (**byte[] inline in DB**), `Url`, `CategoryId`, `Role` (Regular/Symbol) |
| `AttachmentCategory` | `Domain/Attachments/AttachmentCategory.cs` | `OwnerUserId` | |
| `Notification` | `Domain/Notifications/Notification.cs` | `OwnerUserId` nullable (**null = global**) | `Title`, `Message`, `Type` (MonthlyReminder/EventDriven/SystemAlert), `Target`, `ScheduledDateUtc`, `IsEnabled`, `IsDismissed`, `TriggerEventKey` |
| `User` | `Domain/Users/User*.cs` (partial) | – | `IdentityUser<Guid>` + `IsAdmin`, `Active`, `LastLoginUtc`, `PreferredLanguage`, Import-Split-Einstellungen (`ImportSplitMode`, `ImportMaxEntriesPerDraft`, `ImportMonthlySplitThreshold`, `ImportMinEntriesPerDraft`), `MassImportDialogPolicy`, `KnownContactAutoCreateEnabled`, `ShowConfirmations`, `CacheKpisInLocalStorage`, `AlphaVantageApiKey`(+`ShareAlphaVantageApiKey`), Benachrichtigungs-/Return-Analysis-/Zeitzonen-Settings, `BenchmarkSecurityId` |
| `IpBlock` | `Domain/Security/IpBlock.cs` | – (global) | `IpAddress`, `IsBlocked`, `BlockReason`, Fehlversuch-Zähler |
| `BackupRecord` | `Infrastructure/Backups/BackupRecord.cs` | `OwnerUserId` | Backup-Dateien |
| `SecurityTxtSettings`, `WellKnownSettings` | `Domain/Security/`, `Domain/WellKnown/` | – (global, Singletons) | Admin-Einstellungen |

**Wichtig für das Berechtigungskonzept (F2/F3):**

- „Kontotypen“, „Kontaktgruppen“, Kategorien etc. der Anforderung mappen auf:
  AccountType-Enum (2 Werte), ContactCategory, SecurityCategory, SavingsPlanCategory,
  AttachmentCategory, BudgetCategory.
- `Posting` hat **keinen** OwnerUserId – jede benutzerbezogene Freigabe für Buchungen
  muss über die referenzierten Aggregate (Account/Contact/SavingsPlan/Security)
  aufgelöst werden.
- `StatementImport`/`StatementEntry` besitzen nur indirekt über `AccountId` einen
  Benutzerbezug.
- `Attachment.Content` liegt als Byte-Array **in der SQLite-Datenbank** – für
  MCP-Zugriffe relevant (Datenmenge, Prompt-Injection über Dateiinhalte).

---

## 3. Benutzer- und Rechtemodell

### 3.1 Benutzerverwaltung

- **ASP.NET Core Identity** mit `User : IdentityUser<Guid>` und `IdentityRole<Guid>`;
  `AppDbContext : IdentityDbContext<User, IdentityRole<Guid>, Guid>`.
  Konfiguration in `ProgramExtensions` (`AddIdentity`, Lockout 5 min/3 Versuche,
  Password-Policy aus `appsettings` `Identity:*`).
- Eigener Passwort-Hasher `Pbkdf2IdentityPasswordHasher`
  (`Infrastructure/Auth/Pbkdf2IdentityPasswordHasher.cs`, hinter
  `IPasswordHasher<User>` und internem `IPasswordHashingService`).
- Registrierung: `AuthController` (`/api/auth/register|login|logout`);
  Selbstregistrierung nur für den **ersten** Benutzer bzw. bei
  `Auth:AllowSelfRegistration=true` (default `false`); weitere Benutzer werden von
  Admins angelegt (`AdminController` → `IUserAdminService`/`UserAdminService`,
  Endpunkte `Roles="Admin"`).
- `User.Active` (Deaktivierung sperrt Login) und `User.IsAdmin` (Domain-Flag, wird beim
  Start via `EnsureAdminRole` mit der Identity-Rolle „Admin“ synchronisiert).

### 3.2 Authentifizierung

- **JWT Bearer** (HMAC-SHA256, symmetrischer `Jwt:Key`, Issuer/Audience
  `financemanager`, `LifetimeMinutes` 30). `JwtTokenService` erzeugt Claims: `sub`,
  `nameidentifier`, `name`, `security_stamp`, optional `role=Admin`, `pref_lang`, `tz`.
- Token-Transport: `Authorization: Bearer` **oder** HttpOnly-Cookie
  `FinanceManager.Auth` (Cookie-Fallback im `OnMessageReceived`-Event, nur wenn kein
  Bearer-Header vorhanden). Blazor-UI verwendet `JwtCookieAuthTokenProvider` +
  `CircuitAuthTokenStore` (in-circuit Token-Cache).
- **Serverseitige Invalidierung:** `OnTokenValidated` prüft pro Request
  `User.Active`, Security-Stamp-Gleichheit und Admin-Rollen-Drift → Token wird bei
  Änderung sofort ungültig (`ProgramExtensions` ~Z. 301–333).
- `JwtRefreshMiddleware` + `IJwtRefreshService`/`JwtRefreshService`
  (`Web/Infrastructure/Auth/`): erneuert JWTs gegen aktuellen User-State.
- `ICurrentUserService` liefert `UserId` (NameIdentifier/sub), `IsAdmin`,
  `IsAuthenticated`, `PreferredLanguage` aus `HttpContext.User`.

### 3.3 Datenzugriffsabsicherung

- **Kein** EF Global Query Filter, **keine** Policy-basierte Autorisierung. Muster:
  Controller → `_current.UserId` → Service-Methode `…(ownerUserId, …)` → EF-Query mit
  `.Where(x => x.OwnerUserId == ownerUserId)`. Einzelobjekte → 404 statt 403.
- Rollen: nur **„Admin“** vs. normaler Benutzer; kein Feinrechte-/Claim-Modell.
  Admin-Prüfung zusätzlich manuell in einzelnen Controllern
  (`userId != _current.UserId && !_current.IsAdmin`, z. B. `UsersController`).
- `AccountShare`/`AccountShareRole` (Read/Write/Admin) ist als Entität vorhanden –
  ein bereits angelegtes Teile-Rechtemodell, auf das das MCP-Konzept Bezug nehmen
  kann (Vorschrift F2: MCP-Rechte dürfen Benutzerrechte nur einschränken).

### 3.4 Perimeter-Sicherheit (für F8 relevant)

- `IpBlockMiddleware` + `IpBlock`-Tabelle + `IIpBlockService` (Admin-CRUD,
  Fehlversuchszähler).
- `AddRateLimiter` mit Policy `RateLimitPolicies.AuthSensitive` (Fixed Window pro
  Remote-IP, default 20/10 s) – bislang nur Registrierung/User-Probes.
- `CookieCsrfProtectionMiddleware` + Antiforgery (`FinanceManager.Antiforgery`,
  `/api/antiforgery` Minimal-Endpoint).
- `SecurityHeadersPolicy` (CSP etc.), `UseHsts`+`UseHttpsRedirection` (außer
  Dev/E2E-Ausnahme), Forwarded-Headers nur für konfigurierte Proxies
  (`ReverseProxy:KnownProxies|KnownNetworks`).
- `AllowedHosts: "*"` und Kommentar in `appsettings.json`: Zugriff per LAN-IP/HTTP
  ist explizit unterstütztes Szenario → **Deployments können ohne TLS laufen**
  (relevant für MCP-Token-Transport!).
- `RequestLoggingMiddleware`: Request-Timing/Status-Logging mit Redaction
  sensibler Query-Parameter (`token` → `[REDACTED]`); `FileLoggerProvider`
  (`Web/Infrastructure/Logging/`) schreibt optionales File-Logging.

---

## 4. Kontoauszugs-Import und Entwürfe

Komplette Pipeline unter `Infrastructure/Statements/` + `IStatementDraftService`
(`Application/Statements/IStatementDraftService.cs`) + `StatementDraftsController`
(`/api/statement-drafts`):

1. **Upload:** `POST /upload` (Datei-Upload → `IAsyncEnumerable<StatementDraftDto>`),
   `POST /mass-import` (Mehrdatei-Import via `IMassImportOrchestrator`/
   `MassImportOrchestrator`, Limits aus `MassImport:*`, Upload-Gruppierung via
   `UploadGroupId`), `POST` (leerer Entwurf `CreateEmptyDraftAsync`),
   `POST /preliminary` (vorläufige Buchungen).
2. **Format-/Parser-Auswahl:** `IStatementFileFactory`/`StatementFileFactory` wählt
   `IStatementFile`-Implementierung, `IStatementFileParser` parst:
   ING CSV/PDF, Barclays PDF, Sparkasse PDF, Wüstenrot PDF, Backup-JSON
   (Dateien `Statements/Files/*`, `Statements/Parsers/*`; `TemplateStatementFileParser`
   als Vorlage). **Keine Bankanbindung** – nur Datei-Upload.
3. **Entwurf:** `StatementDraft` (Status Draft/Committed/Expired) +
   `StatementDraftEntry` (Status Open/Announced/Accounted/AlreadyBooked). Benutzer-
   Einstellungen für Draft-Splitting (`ImportSplitMode`,
   `ImportMaxEntriesPerDraft`, `ImportMonthlySplitThreshold`,
   `ImportMinEntriesPerDraft`), `MassImportDialogPolicy`.
4. **Klassifizierung** (`ClassifyAsync`, `StatementDraftService.Classification.cs`):
   Kontakt-Matching über Empfänger/`AliasName.Pattern` (optional Auto-Anlage bekannter
   Kontakte via `KnownContactCatalog` + `KnownContactAutoCreateEnabled`),
   Security-Auto-Matching, **Dublettenerkennung** über Vergleich
   BookingDate+Amount+Subject gegen `Postings` (Kind=Bank, gleiches Konto) und
   `StatementEntries` → Status `AlreadyBooked`; technischer Schutz zusätzlich über
   `StatementEntry.RawHash` (unique). `reset-duplicate`-Endpoint.
5. **Vervollständigung:** Entry-Endpunkte für `edit-core`, `contact`, `costneutral`,
   `savingsplan` (+`archive-on-booking`), `security` (TxType/Quantity/Price/Fee),
   `split`, `save-all`, Batch-Update `ApplyBatchEntryUpdatesAsync`,
   `SetAccountAsync`, `SetDescriptionAsync`.
6. **Validierung:** `GET …/validate` → `DraftValidationResultDto` (Warnings/Errors;
   `forceWarnings` beim Buchen).
7. **Verbuchung:** `POST …/book` (einzelner Entry oder Draft), `POST …/commit`
   (erzeugt `StatementImport` + `StatementEntries`), Massenverbuchung
   `POST /book-all` als **BackgroundTask** (`BookingTaskExecutor`,
   `BackgroundTaskType.BookAllDrafts`/`ClassifyAllDrafts`), Konkurrenzschutz via
   `StatementDraftBookingGuard` (LockToken+Expiry). Ergebnis: `BookingResult` –
   erzeugt `Posting` (Kind Bank/SavingsPlan/Security), aktualisiert
   `Account.CurrentBalance`, aggregierte Daten via `PostingAggregate`
   (`RebuildAggregatesTaskExecutor` als Task), Budget-Auswirkungen via
   `IBudgetImpactEvaluationService`.
8. **Storno:** `PostingReversalService` (`POST /api/postings/{id}/reverse`) –
   Gegenbuchung statt Löschung; Postings tragen Reversal-Referenzen
   (`ReversedBy*`, `IsReversed`/`IsReversal`).

→ Für **F7 (Kontoauszugs-Assistent)** steht fachlich bereits alles bereit:
`IStatementDraftService` deckt Analyse (Get*/Validate), Vervollständigung
(Set*/Save*/Batch), Dublettenerkennung (Classify/AlreadyBooked) und Verbuchung
(Book) vollständig ab – ein MCP-Tool-Layer kann als dünner Adapter darauf aufsetzen.

---

## 5. Bestehende KI-/MCP-/Audit-Infrastruktur

**Befund: nicht vorhanden.**

- Keine Vorkommen von `Mcp`, `ModelContextProtocol`, `AiActionRequest`, Audit-Log,
  Personal-Access-Token oder generischem API-Key-Mechanismus im gesamten Repo
  (Volltextsuche; einziger Treffer: `AiActionRequest` im Requirement-Text selbst).
- Einzige „KI-/externer-Dienst“-Integration: **AlphaVantage** (Wertpapierkurse,
  `Services/AlphaVantage*`, `IAlphaVantageSecretProtector`, User-Feld
  `AlphaVantageApiKey`). Kein generischer Secret-/Token-Store.
- `Notification`/`NotificationWriter` (CreateForUser/CreateForAdmins/CreateGlobal)
  kann als Benachrichtigungskanal für Genehmigungen/Sicherheitsalarme dienen.
- `RequestLoggingMiddleware` + `FileLoggerProvider` sind technisches Logging –
  **kein** revisionssicheres Audit-Trail (keine Wer/Was/Wann-Persistenz).
- `IBackgroundTaskManager` (In-Memory-Queue, `Enqueue(type, userId, payload)`,
  Status/Progress) + `IBackgroundTaskExecutor`-Pattern: naheliegendes Modell für
  asynchrone Ausführung genehmigter Aktionen (F10) – aber **In-Memory**, nicht
  persistent; eine `AiActionRequest`-Queue müsste persistent neu gebaut werden
  (Vorbild: `StatementDraftBookingGuard` als DB-gestützter Lock).

---

## 6. Persistenz

- **`AppDbContext`** (`Infrastructure/AppDbContext.cs`, ~950 Zeilen):
  `IdentityDbContext<User, IdentityRole<Guid>, Guid>`; ~30 `DbSet`s; gesamte
  Model-Konfiguration inline in `OnModelCreating` (Indizes, Unique-Constraints,
  DeleteBehavior, Precisions, Enum-Konvertierungen als `short`, Defaults+Sentinels).
- **Provider: ausschließlich SQLite** (`UseSqlite`, ConnectionString
  `Default`, Fallback `Data Source=financemanager.db`); `DbContext` **und**
  `AddDbContextFactory` (scoped). Filtered-Index-Syntax ist SQL-Server-artig
  geschrieben (`[Iban] IS NOT NULL`), läuft unter SQLite.
- **Migrationen:** klassische EF-Code-Migrations in
  `Infrastructure/Migrations/` (laufend, zuletzt `20261018121308_AddTrendReversal`);
  `AppDbContextModelSnapshot.cs`. Anwendung beim Start:
  `db.Database.Migrate()` in `ApplyMigrationsAndSeed`, danach
  `SchemaPatcher.RunPostMigrationPatches` (Laufzeit-Nacharbeiten, z. B. Aggregate-
  Rebuilds) und `IAutoInitializationService.Run()` (Seed).
- **Neue Entitäten** werden registriert durch: `DbSet` + Konfigurationsblock in
  `OnModelCreating` + `dotnet ef migrations add` (EF-Design-Package im Web-Projekt);
  zusätzlich `*BackupDto`-Record in der Entität + Backup-Service-Erweiterung,
  wenn die Entität in Backups enthalten sein soll.
- Anhänge-Binärdaten liegen **in der DB** (`Attachment.Content byte[]`);
  Backup-Dateien als Dateipfad (`BackupRecord.StoragePath`).

---

## 7. API-Oberfläche

- **~35 `ApiController`** unter `FinanceManager.Web/Controllers/` (`/api/accounts`,
  `/api/contacts`, `/api/statement-drafts`, `/api/postings`, `/api/savings-plans`,
  `/api/securities`, `/api/budget-*`, `/api/attachments`, `/api/reports`,
  `/api/home-kpis`, `/api/notifications`, `/api/admin`, `/api/backups`,
  `/api/background-tasks`, `/api/auth`, `/api/users`, `/api/user-settings`,
  `/api/update`, Meta/WellKnown/Health/SecurityTxt …). Routen in
  Abschnitt 4 exemplarisch für Statement-Drafts.
- **Alle** fachlichen Controller mit `[Authorize(JwtBearer)]`; Admin-Endpunkte
  zusätzlich `Roles="Admin"`. Kein Versioning, kein Swagger/OpenAPI im Projekt
  (kein Swashbuckle-Package), kein external-facing API-Konzept – die REST-API ist
  faktisch **BFF für die eigene Blazor-UI** (DTOs/ApiClient in Shared), kann aber
  technisch schon heute mit Bearer-JWT von außen angesprochen werden.
- **Postings sind über die API nicht direkt schreibbar:** nur GET-Listen/Exporte
  und `POST /{id}/reverse` – Schreibpfad ausschließlich über Draft-Verbuchung
  (domänenscharfe Invariante, guter Ausgangspunkt für F6-Grundsatz „Geschäfts-
  prozesse statt CRUD“).
- Upload-Limits: `Kestrel:Limits:MaxRequestBodySize` 20 MB,
  `Attachments:MaxSizeBytes`, `MassImport:*` Limits.
- `FinanceManager.Shared/IApiClient` + `ApiClient.*.cs`: typisierter Client aller
  Endpunkte (wird von ViewModels genutzt; Integrationstests fahren die API über
  `WebApplicationFactory` hoch) – zeigt die komplette fachliche API-Fläche in
  kompilierter Form.

---

## 8. Tests, CI und Einschränkungen

### 8.1 Testprojekte

| Projekt | Inhalt | Technik |
|---|---|---|
| `FinanceManager.Tests` | Unit-/Komponententests, Ordnerstruktur spiegelt Src (Domain, Infrastructure, Web/ViewModels, Components, Controllers, Statements …) | xUnit v3, FluentAssertions, Moq, bUnit, EF InMemory + Sqlite |
| `FinanceManager.Tests.Integration` | API-Integrationstests über `WebApplicationFactory` (ApiClient-basiert, Ordner `ApiClient/*Tests.cs`) | Mvc.Testing, xUnit v3 |
| `FinanceManager.Tests.Integration.ApiClient` | ApiClient-Hilfsprojekt | |
| `FinanceManager.Tests.E2E` | End-to-End-Tests | Microsoft.Playwright |

### 8.2 CI

`.github/workflows/`: `pr-staging-ci.yml`, `staging-ci.yml` (Anzeigename
„Pre-Release“), `security-scan.yml`, `release.yml`,
`staging-to-main-promotion.yml`, `sync-staging-with-main.yml`,
`verify-pr-source.yml`. Branch-Modell: `main` ⇐ `staging` (Promotion-Workflow);
Node-22-Skripte unter `scripts/` für Release-Versionierung.

### 8.3 Constraints für das MCP-Konzept

- **Deployment:** Self-hosted Single-Instance (Kestrel) mit SQLite-Datei; Auto-Update
  über `msTools.Updater` (GitHub-/LocalFolder-Quelle). Kein Container-/Cloud-
  Hosting sichtbar; Home-/LAN-Betrieb ausdrücklich unterstützt (siehe
  `AllowedHosts:"*"` + Kommentar) → MCP-Server muss mit **HTTP-only-LAN**-Betrieb
  und mit Reverse-Proxy-Betrieb klarkommen; Token im Klartext-Transport ist ein
  reales Risiko.
- **Multi-User:** Ja, aber „persönliche Finanzdaten“: jeder User sieht nur seine
  Daten (OwnerUserId-Filterung); Admin = einzige Rolle. MCP-Zugänge müssen
  strikt benutzerbezogen sein.
- **Skalierung/Concurrency:** In-Memory-Queue (`BackgroundTaskManager`), kein
  verteiltes Locking außer `StatementDraftBookingGuard`; SQLite-Write-Serialisierung
  → Approval-/Ausführungspfade entsprechend einfach halten.
- **Kultur:** UI de/en über `IStringLocalizer` + resx; XML-Doc Pflicht (CS1591 als
  Error); Feature-Flags über `appsettings` (`BackgroundTasks:Enabled`,
  `Workers:SecurityPriceWorker:Enabled`, `Auth:AllowSelfRegistration`,
  `Updates:Enabled`, `FileLogging:Enabled`) → Vorbild für ein
  „MCP global enabled/disabled“-Flag (F8-Notfallmaßnahme).
- Doku-Konvention: deutschsprachige Markdown unter `Docs/` (`Docs/features`,
  `Docs/help`, `Docs/projects/task/<issue>`); Requirement-Annahme A1 sieht
  `Docs/planung/mcp-integration/` als Ablageort der Konzeptdokumente vor.

---

## 9. Wiederverwendbare Bausteine (Referenzen für das Konzept)

| Baustein | Ort | Relevanz für MCP-Konzept |
|---|---|---|
| `ICurrentUserService` / `CurrentUserService` | `Application/ICurrentUserService.cs`, `Web/Services/CurrentUserService.cs` | Einheitlicher Benutzerkontext; ein MCP-Äquivalent (Token→UserId) sollte gleiche Schnittstelle bedienen |
| `IStatementDraftService` | `Application/Statements/IStatementDraftService.cs` | Fachliche Oberfläche für F7 (Analyse, Vervollständigung, Dubletten, Verbuchung) – MCP-Tools als Adapter |
| `IMassImportOrchestrator`, `IStatementFileParser`/`IStatementFileFactory` | `Application/Statements/`, `Infrastructure/Statements/` | Importpfad bleibt einziger Eingang für Kontoauszüge (kein Direktzugriff) |
| `IPostingsQueryService` | `Web/Services/IPostingsQueryService.cs` (+ Application-Alias) | Lesender Posting-Zugriff für Read-Only-Tools |
| `PostingReversalService` | `Infrastructure/Postings/PostingReversalService.cs` | „Löschen“ von Buchungen = Storno – einzig legitimer Rücknahmeweg |
| `IBackgroundTaskManager` + `IBackgroundTaskExecutor` + `BackgroundTaskRunner` | `Application/BackgroundTaskManager.cs`, `BackgroundTaskRunner.cs`, `Web/Services/*TaskExecutor.cs` | Muster für asynchrone, benutzerzugeordnete Aufträge (Queued/Running/Completed, Progress, Cancel) – Basisidee für Ausführung nach Freigabe (F10), aber persistent neu zu bauen |
| `StatementDraftBookingGuard` | `Infrastructure/Statements/StatementDraftBookingGuard.cs` | Vorbild persistenter Lock/Idempotenz gegen Doppelausführung |
| `INotificationWriter`/`INotificationService`, `Notification` (SystemAlert/EventDriven) | `Application/Notifications/`, `Infrastructure/Notifications/` | Genehmigungs- und Sicherheitsbenachrichtigungen (Approval-Center) |
| `IConfirmationService`/`ConfirmationService` | `Web/Services/` | UI-Bestätigungsmuster (Severity) – relevant für Genehmigungscenter-UX |
| `IIpBlockService` + `IpBlockMiddleware` | `Application/Security/`, `Web/Infrastructure/` | IP-Sperrinfrastruktur; erweiterbar für MCP-Missbrauch |
| Rate-Limiter-Infrastruktur (`AddPolicy`, `RateLimitPolicies`) | `Web/ProgramExtensions.cs`, `Web/Infrastructure/RateLimitPolicies.cs` | Vorlage für MCP-Rate-Limits (pro Token statt pro IP) |
| `DataProtectionAlphaVantageSecretProtector` (`dp:v1:`-Prefix) | `Web/Services/IAlphaVantageSecretProtector.cs` | Präzedenzfall Secret-Schutz via `IDataProtectionProvider`; für PAT-Hashes eher Einweg-Hash (vgl. `Pbkdf2IdentityPasswordHasher`) |
| JWT-Validierungs-Pipeline (`OnTokenValidated`, Security-Stamp-Claim, `JwtRefreshService`) | `ProgramExtensions`, `Web/Infrastructure/Auth/` | Vorlage für serverseitige Sofort-Invalidierung (Widerruf) und Token-Lebenszyklus |
| `IUserAdminService`/`UserAdminService`, Admin-UI (`ViewModels/Setup/*`) | `Infrastructure/Auth/`, `Web/ViewModels/Setup/` | Admin-/Setup-UI-Konventionen für MCP-Verwaltungsoberflächen |
| `AccountShare`/`AccountShareRole` | `Domain/Accounts/AccountShare.cs`, `Domain/Enums.cs` | Vorhandenes Rechte-(Read/Write/Admin)-Modell auf Kontoebene – Referenz für Scope-/Filterdesign (F2/F4) |
| `BudgetRule.PurposePattern(+IsRegex)`, `AliasName.Pattern` | `Domain/Budget/BudgetRule.cs`, `Domain/Contacts/AliasName.cs` | Bestehende regelbasierte Auto-Zuordnung – Anknüpfung für KI-Vorschlagsrechte vs. deterministische Regeln |
| Feature-Flag-/Settings-Muster (`appsettings` + User-Flags) | `appsettings.json`, `Domain/Users/User*.cs` | „Globale MCP-Deaktivierung“ (F8) und benutzerbezogene Opt-ins |
| `ApiClient`/`IApiClient` (typisierter Client) | `Shared/ApiClient*.cs` | Referenz für vollständige fachliche API-Fläche und DTO-Konventionen (Tool-Ein-/Ausgaben) |

---

## Anhang: Nicht untersuchte/verwaiste Bereiche

- `FinanceManager.Shared.Dtos.Budget/` (Repo-Root): keine Projektdatei, nicht in
  Solution → vermutlich verwaister Überrest von `Shared/Dtos/Budget`.
- `external/msTools.*`: lokal eingebundene Paketquellen (Updater, Blazor-Controls) –
  nicht Teil des Domänencodes.
- `task.json`, `issue.md`, `changes.log`, `build_output.txt`, `test_results.txt`,
  `refactoring-checklist.md` im Root: Arbeits-/CI-Artefakte, für das Konzept
  nicht relevant.
