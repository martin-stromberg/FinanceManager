# Datenmodell

Alle Basisklassen erben — sofern nicht anders angegeben — von `Entity` (`FinanceManager.Domain/Entity.cs`), das `Id` (Guid, `protected set`), `CreatedUtc`, `ModifiedUtc` sowie `Touch()`/`SetDates(...)` bereitstellt. `IAggregateRoot` ist eine Marker-Schnittstelle in derselben Datei.

## `BudgetRule`
Datei: `FinanceManager.Domain/Budget/BudgetRule.cs`

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `OwnerUserId` | `Guid` | Besitzer der Regel (Z. 57) |
| `BudgetPurposeId` | `Guid?` | Optionaler Zweck-Bezug; XOR mit `BudgetCategoryId` erzwungen im Konstruktor (Z. 41–44) |
| `BudgetCategoryId` | `Guid?` | Optionaler Kategorie-Bezug (Z. 67) |
| `Amount` | `decimal` | Erwarteter Betrag (Z. 72) |
| `PurposePattern` | `string?` | Optionales Muster zum Matching von Buchungstexten; persistiert, **fehlt im Backup-DTO** (Z. 77) |
| `PurposePatternIsRegex` | `bool` | Flag, ob `PurposePattern` als Regex gilt; persistiert, **fehlt im Backup-DTO** (Z. 82) |
| `Interval` | `BudgetIntervalType` | Intervalldefinition (Z. 87) |
| `CustomIntervalMonths` | `int?` | Monatsanzahl bei `CustomMonths` (Z. 92) |
| `StartDate` / `EndDate` | `DateOnly` / `DateOnly?` | Gültigkeitszeitraum (Z. 97/102) |

Zugehörige Member:
- Konstruktor `BudgetRule(...)` (Z. 27–52): nimmt bereits `purposePattern`/`purposePatternIsRegex` als optionale Parameter entgegen.
- `SetPurposePattern(string? pattern, bool isRegex)` (Z. 158): validiert Regex, Trim, max. 500 Zeichen.
- `BudgetRuleBackupDto` (Z. 223–232): `Id, OwnerUserId, BudgetPurposeId, BudgetCategoryId, Amount, Interval, CustomIntervalMonths, StartDate, EndDate` — **ohne** `PurposePattern`/`PurposePatternIsRegex`.
- `ToBackupDto()` (Z. 238) / `AssignBackupDto(BudgetRuleBackupDto)` (Z. 245–256): bilden nur die DTO-Felder ab; `AssignBackupDto` setzt die Pattern-Felder nicht.

## `Security`
Datei: `FinanceManager.Domain/Securities/Security.cs`

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `OwnerUserId` | `Guid` | Besitzer (Z. 39) |
| `Name` | `string` | Anzeigename (Z. 45) |
| `Description` | `string?` | Optionale Beschreibung (Z. 51) |
| `Identifier` | `string` | WKN/ISIN (Z. 57) |
| `AlphaVantageCode` | `string?` | Externer Provider-Code (Z. 63) |
| `CurrencyCode` | `string` | ISO-Währung (Z. 69) |
| `CategoryId` | `Guid?` | Optionale Kategorie (Z. 75) |
| `Region` / `Sector` | `string?` | Region/Sektor für Portfolioanalyse (Z. 81/87) |
| `IsActive` / `ArchivedUtc` | `bool` / `DateTime?` | Archivstatus (Z. 93/99) |
| `HasPriceError` | `bool` | Aktiver Kursabruf-Fehler; persistiert, **fehlt im Backup-DTO** (Z. 105) |
| `PriceErrorMessage` | `string?` | Fehlertext; **fehlt im Backup-DTO** (Z. 111) |
| `PriceErrorSinceUtc` | `DateTime?` | Fehler-Zeitpunkt; **fehlt im Backup-DTO** (Z. 117) |
| `SymbolAttachmentId` | `Guid?` | Symbol-Attachment (Z. 123) |
| `GoldenCrossNotifiedPhase` | `GoldenCrossPhase` | Zuletzt notifizierte Golden-Cross-Phase, Default `Far`; **fehlt im Backup-DTO** (Z. 130) |
| `TrendReversalNotifiedStatus` | `TrendStatus` | Zuletzt notifizierter Trendstatus, Default `Intact`; **fehlt im Backup-DTO** (Z. 146) |

Zugehörige Member:
- `SetPriceError(string)` (Z. 212): setzt `HasPriceError`, `PriceErrorMessage` und `PriceErrorSinceUtc = DateTime.UtcNow` — der Zeitstempel lässt sich damit nicht originalgetreu wiederherstellen.
- `ClearPriceError()` (Z. 222), `SetGoldenCrossNotifiedPhase(GoldenCrossPhase)` (Z. 136), `SetTrendReversalNotifiedStatus(TrendStatus)` (Z. 152), `SetSymbolAttachment(Guid?)` (Z. 233), `Archive()`/`Unarchive()` (Z. 190/200).
- `SecurityBackupDto` (Z. 271): `Id, OwnerUserId, Name, Identifier, Description, AlphaVantageCode, CurrencyCode, CategoryId, IsActive, CreatedUtc, ModifiedUtc, ArchivedUtc, SymbolAttachmentId, Region = null, Sector = null` — `Region`/`Sector` sind bereits optional mit Default (Vorbild für Defaults).
- `ToBackupDto()` (Z. 277) / `AssignBackupDto(SecurityBackupDto)` (Z. 285–293): bilden nur die DTO-Felder ab; `AssignBackupDto` nutzt `Update(...)`, `Archive()/Unarchive()`, `SetDates(...)`.

## `Account`
Datei: `FinanceManager.Domain/Accounts/Account.cs` (Kontext für `AccountLinkedIban`)

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `OwnerUserId` | `Guid` | Besitzer (Z. 31) |
| `Type` | `AccountType` | Kontotyp (Z. 36) |
| `Name` / `Iban` | `string` / `string?` | Anzeigename / IBAN (Z. 41/46) |
| `CurrentBalance` | `decimal` | Saldo (Z. 51) |
| `BankContactId` | `Guid` | Bank-Kontakt (Z. 56) |
| `SymbolAttachmentId` | `Guid?` | Symbol (Z. 61) |
| `SavingsPlanExpectation` | `SavingsPlanExpectation` | Sparplan-Erwartung (Z. 66) |
| `SecurityProcessingEnabled` | `bool` | Wertpapier-Verarbeitung erlaubt (Z. 71) |
| `IsCollectionAccount` | `bool` | Sammelkonto-Flag; Grundlage für Unter-IBANs (Z. 76) |

Zugehörige Member: `AccountBackupDto` (Z. 198, inkl. `SecurityProcessingEnabled` und `IsCollectionAccount` mit Defaults), `ToBackupDto()` (Z. 204), `AssignBackupDto(...)` (Z. 213), `SetIsCollectionAccount(bool)` (Z. 171). Das Entity besitzt **keine** `LinkedIbans`-Navigation; die Liste wird in `AccountService`/`AccountDto` zusammengesetzt.

## `AccountLinkedIban`
Datei: `FinanceManager.Domain/Accounts/AccountLinkedIban.cs`

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `AccountId` | `Guid` | Id des Sammelkontos; **kein `OwnerUserId`** — Benutzerbezug nur über `Account` (Z. 22) |
| `Iban` | `string` | Verknüpfte Unter-IBAN, getrimmt (Z. 27) |
| `Account` | `Account?` | Navigation zum Konto (Z. 32) |

Zugehörige Member:
- `AccountLinkedIbanBackupDto` (Z. 42): `Id, AccountId, Iban` — existiert bereits, ist aber **top-level** in dieser Datei deklariert (nicht nested wie die anderen `*BackupDto`-Records).
- `ToBackupDto()`/`AssignBackupDto()`: **nicht vorhanden**.
- EF-Config (`AppDbContext` Z. 457–467): Unique-Index `(AccountId, Iban)`, FK `Account` mit `DeleteBehavior.Cascade` (DB-Kaskade — löscht Unter-IBANs beim Konto-Delete, auch per `ExecuteDeleteAsync`).

## `PortfolioKpiConfiguration`
Datei: `FinanceManager.Domain/Portfolio/PortfolioKpiConfiguration.cs`

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `OwnerUserId` | `Guid` | Besitzer; Unique-Index in `AppDbContext` Z. 725 → eine Zeile pro Benutzer (Z. 31) |
| `ActiveTileIds` | `string` | JSON-Array aktiver KPI-Kacheln (Z. 36) |
| `TileOrder` | `string` | JSON-Array der Kachelreihenfolge (Z. 41) |
| `UpdatedUtc` | `DateTime` | Letzter Änderungszeitpunkt (Z. 46) |

Zugehörige Member: Konstruktor `(ownerUserId, activeTileIds, tileOrder)` (Z. 20), `Update(activeTileIds, tileOrder)` (Z. 53). **Kein Backup-DTO, kein `ToBackupDto`/`AssignBackupDto` vorhanden.** Zugriff produktiv über `PortfolioKpiConfigurationRepository` (Upsert-Semantik).

## `StatementImport`
Datei: `FinanceManager.Domain/Statements/StatementImport.cs`

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `AccountId` | `Guid` | Konto des Imports; kein eigenes `OwnerUserId` (Z. 33) |
| `Format` | `ImportFormat` | Erkanntes Importformat (Z. 39) |
| `ImportedAtUtc` | `DateTime` | Importzeitpunkt (Z. 45) |
| `OriginalFileName` | `string` | Originaldateiname, Pflicht (Z. 51) |
| `TotalEntries` | `int` | Anzahl geparster Einträge (Z. 57) |

Zugehörige Member: `StatementImportBackupDto` (Z. 72, inkl. `CreatedUtc`/`ModifiedUtc`), `ToBackupDto()` (Z. 78), `AssignBackupDto(...)` (Z. 86 — setzt `AccountId` direkt, d. h. ein geremapptes DTO kann 1:1 angewendet werden). Index `(AccountId, ImportedAtUtc)` in `AppDbContext` Z. 497.

## `StatementEntry`
Datei: `FinanceManager.Domain/Statements/StatementEntry.cs`

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `StatementImportId` | `Guid` | Zugehöriger Import (Z. 49) |
| `BookingDate` / `ValutaDate` | `DateTime` / `DateTime?` | Buchungs-/Valutadatum (Z. 54/59) |
| `Amount` | `decimal` | Betrag, 0 erlaubt (Z. 64) |
| `Subject` | `string` | Verwendungszweck, Pflicht, max. 500 (Z. 69) |
| `RawHash` | `string` | Dedup-Hash; **global unique index** (`AppDbContext` Z. 503), max. 128 (Z. 74) |
| `RecipientName` | `string?` | Empfänger (Z. 79) |
| `CurrencyCode` | `string` | ISO-Währung, Default `"EUR"` (Z. 84) |
| `BookingDescription` | `string?` | Buchungstext (Z. 89) |
| `IsAnnounced` / `IsCostNeutral` | `bool` | Vorgemeldet / kostenneutral (Z. 94/99) |
| `ContactId` | `Guid?` | Aufgelöster Kontakt (Z. 104) |
| `SavingsPlanId` | `Guid?` | Zugeordneter Sparplan; FK mit `SetNull` (`AppDbContext` Z. 507–510) (Z. 109) |
| `SecurityTransactionId` | `Guid?` | Optionale Wertpapier-Transaktions-Referenz; **kein FK**, wird nur gelesen (`StatementImportQueryService` Z. 114, `FmStatementImportsReviewTool` Z. 116), kein Schreiber im Produktivcode außer `AssignBackupDto` (Z. 114) |
| `Status` | `StatementEntryStatus` | Verarbeitungsstatus, Default `Pending` (Z. 119) |

Zugehörige Member: `StatementEntryBackupDto` (Z. 142 — enthält alle o. g. Felder inkl. `ContactId`, `SavingsPlanId`, `SecurityTransactionId`, `Status`), `ToBackupDto()` (Z. 148), `AssignBackupDto(...)` (Z. 156 — setzt alle Felder direkt, auch `SecurityTransactionId`).

## `Notification`
Datei: `FinanceManager.Domain/Notifications/Notification.cs` — **kein `Entity`-Nachfahre**, eigenständige POCO-Klasse.

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `Id` | `Guid` | Public setter, Default `Guid.NewGuid()` (Z. 14) |
| `OwnerUserId` | `Guid?` | **Nullable** — `null` = globale Benachrichtigung (Z. 19) |
| `Title` / `Message` | `string` | Titel (max. 140) / Text (max. 1000) (Z. 25/31) |
| `Type` | `NotificationType` | Klassifikation (Z. 36) |
| `Target` | `NotificationTarget` | Anzeigeziel, Default `HomePage` (Z. 41) |
| `ScheduledDateUtc` | `DateTime` | Aktivierungszeitpunkt (Z. 46) |
| `IsEnabled` / `IsDismissed` | `bool` | Aktiv / verworfen (Z. 51/56) |
| `TriggerEventKey` | `string?` | Auslösendes Event (Z. 62) |
| `CreatedUtc` / `ModifiedUtc` | `DateTime` / `DateTime?` | Zeitstempel (Z. 67/72) |

Zugehörige Member: `NotificationBackupDto` (Z. 91 — enthält `OwnerUserId`), `ToBackupDto()` (Z. 97), `AssignBackupDto(...)` (Z. 105–119) — **übernimmt `OwnerUserId` nicht** (und setzt `Id` auf den Backup-Wert).

## `AccountShare`
Datei: `FinanceManager.Domain/Accounts/AccountShare.cs`

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `AccountId` | `Guid` | Freigegebenes Konto (Z. 24) |
| `UserId` | `Guid` | **Empfänger**-Benutzer der Freigabe (Z. 29) |
| `Role` | `AccountShareRole` | Vergabene Rolle (Z. 34) |
| `GrantedUtc` / `RevokedUtc` | `DateTime` / `DateTime?` | Erteilungs-/Widerrufszeitpunkt (Z. 39/44) |

Zugehörige Member: `AccountShareBackupDto` (Z. 57 — alle Felder), `ToBackupDto()` (Z. 63), `AssignBackupDto(...)` (Z. 72 — setzt alle Felder inkl. `AccountId`/`UserId` direkt).
