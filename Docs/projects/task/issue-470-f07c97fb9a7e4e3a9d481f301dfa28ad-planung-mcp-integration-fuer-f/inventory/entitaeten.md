# Detaildokument – Entitätskatalog

Ergänzend zu `../inventory.md` (Abschnitt 2). Alle Entitäten erben – sofern nicht anders
vermerkt – von `Entity` (`FinanceManager.Domain/Entity.cs`: `Id:Guid`,
`CreatedUtc`, `ModifiedUtc`). Spalte „Owner“ gibt an, wie der Benutzerbezug hergestellt
wird. Enum-Werte liegen überwiegend in `FinanceManager.Shared/Dtos/**` (Shared-Vertrag).

## Konten

### `Account` (`Domain/Accounts/Account.cs`, IAggregateRoot)
`OwnerUserId` • `Type: AccountType` (`Giro=0, Savings=1`) • `Name` (unique je Owner, max 150)
• `Iban?` (unique je Owner, max 34, filtered index) • `CurrentBalance` • `BankContactId→Contact`
• `SymbolAttachmentId?` • `SavingsPlanExpectation` • `SecurityProcessingEnabled` (default true)
• `IsCollectionAccount` (default false). Methoden: Rename, SetIban, SetBankContact, SetType,
AdjustBalance, SetSymbolAttachment, SetSavingsPlanExpectation, SetSecurityProcessingEnabled,
SetIsCollectionAccount; `AccountBackupDto`.

### `AccountLinkedIban` (`Domain/Accounts/AccountLinkedIban.cs`)
über `AccountId` • `Iban` (unique je Account, max 34), Cascade-Delete.

### `AccountShare` (`Domain/Accounts/AccountShare.cs`)
`UserId` (Teilnehmer) • `AccountId` • `Role: AccountShareRole` (`Read=0, Write=1, Admin=2`,
`Domain/Enums.cs`) • `GrantedUtc` • `RevokedUtc?`.
**Hinweis:** Entität/DbSet vorhanden; Freigabe zwischen Benutzern als Datenmodell angelegt.

## Kontakte

### `Contact` (`Domain/Contacts/Contact.cs`, IAggregateRoot)
`OwnerUserId` • `Name` (max 200) • `Type: ContactType` (`Self=0, Bank=1, Person=2,
Organization=3, Other=9`; unique: max. ein Self je Owner) • `CategoryId?→ContactCategory`
(SetNull) • `Description?` • `IsPaymentIntermediary` • `SymbolAttachmentId?`.

### `ContactCategory` (`Domain/Contacts/ContactCategory.cs`)
`OwnerUserId` • `Name` (unique je Owner, max 150). = „Kontaktgruppen“ der Anforderung.

### `AliasName` (`Domain/Contacts/AliasName.cs`)
über `ContactId` • `Pattern` (unique je Contact, max 200) – Matching-Muster beim Import.

## Kontoauszüge / Import

### `StatementDraft` (`Domain/Statements/StatementDraft.cs`, IAggregateRoot)
`OwnerUserId` • `OriginalFileName` (max 255) • `AccountName?` • `Description?` •
`DetectedAccountId?` • `Status: StatementDraftStatus` (`Draft=0, Committed=1, Expired=2`)
• `IsPreliminary` • `UploadGroupId?` (Index) • `Entries→StatementDraftEntry` (Cascade).

### `StatementDraftEntry` (`Domain/Statements/StatementDraft.cs`)
über `DraftId` • `EntryNumber` • `BookingDate` • `ValutaDate?` • `Amount` • `Subject`
(max 500) • `RecipientName?` • `CurrencyCode` (default EUR) • `BookingDescription?` •
`IsAnnounced` • `IsCostNeutral` • `Status: StatementDraftEntryStatus`
(`Open=0, Announced=1, Accounted=2, AlreadyBooked=3`) • `ContactId?` • `SavingsPlanId?` •
`ArchiveSavingsPlanOnBooking` • `SplitDraftId?` (unique filtered → Split-Entwürfe) •
`SecurityId?` • `SecurityTransactionType?` • `SecurityQuantity?` • `SecurityFeeAmount?` •
`SecurityTaxAmount?`.

### `StatementDraftBookingGuard` (`Infrastructure/Statements/StatementDraftBookingGuard.cs`)
`OwnerUserId` • `DraftId` • `LockToken` • `AcquiredUtc` • `ExpiresUtc` –
persistente Buchungssperre.

### `StatementImport` (`Domain/Statements/StatementImport.cs`, IAggregateRoot)
über `AccountId→Account` • `Format: ImportFormat` • `ImportedAtUtc` • `OriginalFileName`
(max 255) • `TotalEntries`.

### `StatementEntry` (`Domain/Statements/StatementEntry.cs`)
über `StatementImportId` • `BookingDate` • `ValutaDate?` • `Amount` • `Subject` (max 500)
• **`RawHash` (max 128, unique – Dublettensperre)** • `RecipientName?` • `CurrencyCode` •
`BookingDescription?` • `IsAnnounced` • `IsCostNeutral` • `ContactId?` • `SavingsPlanId?`
(SetNull) • `SecurityTransactionId?` • `Status: StatementEntryStatus`
(`Pending=0, Booked=1, IgnoredDuplicate=2`, `Domain/Enums.cs`).

## Buchungen

### `Posting` (`Domain/Postings/Posting.cs`, IAggregateRoot)
**kein `OwnerUserId`** – Besitz indirekt über `AccountId?/ContactId?/SavingsPlanId?/SecurityId?`
(je nach `Kind`) • `SourceId` • `GroupId` • `Kind: PostingKind`
(`Bank=0, Contact=1, SavingsPlan=2, Security=3`) • `BookingDate` • `ValutaDate` • `Amount`
• `OriginalAmount?` • `Subject?` • `RecipientName?` • `Description?` •
`SecuritySubType?` (`SecurityPostingSubType`) • `Quantity?` • `ParentId?` (Splits) •
`LinkedPostingId?` (Gegenbuchung Umbuchung) • `IsPreliminary` • Storno:
`ReversedByPostingId?`, `ReversalForPostingId?`, `ReversedByUserId?`, `ReversedAtUtc?`,
`IsReversed`, `IsReversal`.

### `PostingAggregate` (`Domain/Postings/PostingAggregate.cs`)
kein Owner (indirekt) • `Kind` • `AccountId?/ContactId?/SavingsPlanId?/SecurityId?` •
`SecuritySubType?` • `Period` • `PeriodStart` • `Amount` • `DateKind` – vorberechnete
Periodenaggregate (Unique-Indizes über Referenzkombinationen).

## Sparpläne

### `SavingsPlan` (`Domain/Savings/SavingsPlan.cs`)
`OwnerUserId` • `Name` (unique je Owner, max 100) • `Type: SavingsPlanType` •
`TargetAmount?` (18,2) • `TargetDate?` • `Interval: SavingsPlanInterval?` • `IsActive` •
`ArchivedUtc?` • `CategoryId?→SavingsPlanCategory` (SetNull) • `ContractNumber?` (max 100)
• `SymbolAttachmentId?`.

### `SavingsPlanCategory` (`Domain/Savings/SavingsPlanCategory.cs`)
`OwnerUserId` • `Name` (max 100).

## Wertpapiere

### `Security` (`Domain/Securities/Security.cs`)
`OwnerUserId` • `Name` (unique je Owner, max 200) • `Identifier` (WKN/ISIN, max 50) •
`Description?` • `AlphaVantageCode?` (max 50) • `CurrencyCode` (max 10, default EUR) •
`CategoryId?→SecurityCategory` (SetNull) • `Region?`/`Sector?` (max 255) • `IsActive` •
`ArchivedUtc?` • `HasPriceError`/`PriceErrorMessage`/`PriceErrorSinceUtc` •
`SymbolAttachmentId?` • `GoldenCrossNotifiedPhase` • `TrendReversalNotifiedStatus`.

### `SecurityCategory` (`Domain/Securities/SecurityCategory.cs`)
`OwnerUserId` • `Name` (unique je Owner, max 150).

### `SecurityPrice` (`Domain/Securities/SecurityPrice.cs`)
über `SecurityId` • `Date` • `Close` • `CreatedUtc` (eigenes Id-Feld, kein Entity-Erbe
für CreatedUtc nötig).

## Budget

### `BudgetCategory` (`Domain/Budget/BudgetCategory.cs`, IAggregateRoot)
`OwnerUserId` • `Name`.

### `BudgetPurpose` (`Domain/Budget/BudgetPurpose.cs`, IAggregateRoot)
`OwnerUserId` • `Name` • `Description?` • `SourceType: BudgetSourceType` +
`SourceId` (polymorphe Quelle: Konto/Kontakt/Sparplan/Wertpapier) • `BudgetCategoryId?` •
`ValuationType: BudgetValuationType` (default `ExactPostings`).

### `BudgetRule` (`Domain/Budget/BudgetRule.cs`, IAggregateRoot)
`OwnerUserId` • `BudgetPurposeId?` • `BudgetCategoryId?` • `Amount` •
`PurposePattern?` + `PurposePatternIsRegex` (Auto-Matching auf Buchungstexte) •
`Interval: BudgetIntervalType` • `CustomIntervalMonths?` • `StartDate`/`EndDate?`.

### `BudgetOverride` (`Domain/Budget/BudgetOverride.cs`)
`OwnerUserId` • `BudgetPurposeId` • `PeriodYear`/`PeriodMonth` • `Amount` –
manuelle Monatsüberschreibung.

Domänen-Berechnung (keine persistierten Aggregates): `Domain/Budget/ReportCalculation/*`
(`Budgetbericht`, `MonthlyBudgetExpectation*`, `MonthlyBudgetResult`,
`BudgetReportCalculationException`).

## Reports / KPI

### `ReportFavorite` (`Domain/Reports/ReportFavorite.cs`, IAggregateRoot)
`OwnerUserId` • `Name` • `PostingKind` • `IncludeCategory` • `Interval: ReportInterval` •
`Take` (default 24) • `ComparePrevious`/`CompareYear`/`CompareProjection` • `ShowChart` •
`Expandable` • Filter-CSVs: `PostingKindsCsv`, `AccountIdsCsv`, `ContactIdsCsv`,
`SavingsPlanIdsCsv`, `SecurityIdsCsv`, `ContactCategoryIdsCsv`,
`SavingsPlanCategoryIdsCsv`, `SecurityCategoryIdsCsv`, `SecuritySubTypesCsv` •
`IncludeDividendRelated?` • `UseValutaDate`.
→ Existierendes „Filter-mit-CSV-Listen“-Muster, an dem sich MCP-Scope-Filter (F4)
orientieren können.

### `HomeKpi` (`Domain/Reports/HomeKpi.cs`, IAggregateRoot)
`OwnerUserId` • `Kind: HomeKpiKind` • `ReportFavoriteId?` • `PredefinedType:
HomeKpiPredefined?` • `DisplayMode: HomeKpiDisplayMode` • `SortOrder` • `Title?`.

### `ReportCacheEntry` (`Domain/Reports/ReportCacheEntry.cs`)
`OwnerUserId` • `CacheKey`/`CacheValue`/`Parameter` • `CacheValidUntilUtc?` •
`NeedsRefresh` – Report-Cache (Refresh via `ReportCacheRefreshTaskExecutor`).

### `PortfolioKpiConfiguration` (`Domain/Portfolio/PortfolioKpiConfiguration.cs`)
`OwnerUserId` • `ActiveTileIds`/`TileOrder` (CSV) • `UpdatedUtc`.

### `ReportEntityGroup` (`Domain/Reports/ReportEntityGroup.cs`)
Hilfstyp für Report-Gruppierung.

## Anhänge

### `Attachment` (`Domain/Attachments/Attachment.cs`)
`OwnerUserId` • `EntityKind: AttachmentEntityKind`
(`None=-1, StatementDraftEntry=0, StatementEntry=1, Contact=2, SavingsPlan=3,
Security=4, Account=5, StatementImport=6, Posting=7, StatementDraft=8,
ContactCategory=9, SavingsPlanCategory=10, SecurityCategory=11`) + `EntityId`
(polymorph!) • `FileName` • `ContentType` • `SizeBytes` • `Sha256?` • `CategoryId?` •
`UploadedUtc` • **`Content byte[]?` (inline in DB)** • `Url?` •
`Role: AttachmentRole` (`Regular=0, Symbol=1`).

### `AttachmentCategory` (`Domain/Attachments/AttachmentCategory.cs`)
`OwnerUserId`.

## Benutzer & System

### `User` (`Domain/Users/User.cs` + partials `.AlphaVantage/.Notifications/.ReturnAnalysis/.TimeZone`)
`IdentityUser<Guid>` (UserName unique max 100, PasswordHash required) • `IsAdmin` •
`Active` • `LastLoginUtc` • `PreferredLanguage?` • `SymbolAttachmentId?` •
Import-Einstellungen: `ImportSplitMode` (`MonthlyOrFixed` default;
`Shared/Dtos/Statements/ImportSplitMode.cs`), `ImportMaxEntriesPerDraft` (250),
`ImportMonthlySplitThreshold?`, `ImportMinEntriesPerDraft` • `MassImportDialogPolicy` •
`KnownContactAutoCreateEnabled` (default true) • `CacheKpisInLocalStorage` •
`ShowConfirmations` (default true) • `AlphaVantageApiKey?` (max 2048, DataProtection-
geschützt via `IAlphaVantageSecretProtector`) • `ShareAlphaVantageApiKey` •
Notifications-Settings (`GoldenCrossNotificationsEnabled`,
`TrendReversalNotificationsEnabled`) • Return-Analysis-Settings (`BenchmarkSecurityId?→
Security` SetNull, `ShowSharpeRatio`, `RiskFreeRate`) • Zeitzone (partial TimeZone).

### `Notification` (`Domain/Notifications/Notification.cs`)
`OwnerUserId?` (**null = global**) • `Title`/`Message` • `Type: NotificationType`
(`MonthlyReminder=0, EventDriven=1, SystemAlert=2`) • `Target: NotificationTarget`
(default HomePage) • `ScheduledDateUtc` • `IsEnabled` • `IsDismissed` •
`TriggerEventKey?` • eigene Id-/Timestamps (kein Entity-Erbe).

### `IpBlock` (`Domain/Security/IpBlock.cs`)
global • `IpAddress` • `IsBlocked` • `BlockedAtUtc?` • `BlockReason?` •
`UnknownUserFailedAttempts`/`UnknownUserLastFailedUtc` (Login-Fehlversuchszähler).

### `BackupRecord` (`Infrastructure/Backups/BackupRecord.cs`)
`OwnerUserId` • `FileName` • `SizeBytes` • `Source` (Upload|System) • `StoragePath`.

### `SecurityTxtSettings` / `WellKnownSettings` (`Domain/Security/`, `Domain/WellKnown/`)
globale Singleton-Einstellungen (security.txt-Direktiven, Change-Password-Weiterleitung).

## Enums – Übersicht (für F3/F4-Mapping)

| Anforderungs-Begriff | Code-Entität | Werte/Ort |
|---|---|---|
| Kontotypen | `AccountType`-Enum auf `Account.Type` | `Giro=0, Savings=1` (`Shared/Dtos/Accounts/AccountType.cs`) – **keine eigene Entität** |
| Kontaktgruppen | `ContactCategory` | Entität, OwnerUserId |
| Sparplankategorien | `SavingsPlanCategory` | Entität |
| Wertpapierkategorien | `SecurityCategory` | Entität |
| Anhangkategorien | `AttachmentCategory` | Entität |
| Budgetkategorien | `BudgetCategory` | Entität |
| Budgets | `BudgetPurpose` (+`BudgetRule`, `BudgetOverride`) | „Budget“ = Purpose+Rules+Overrides |
| Budgetregeln | `BudgetRule` | Entität |
| Reports | `ReportFavorite`, `ReportCacheEntry`, `PostingAggregate` | |
| KPI-Konfigurationen | `HomeKpi`, `PortfolioKpiConfiguration` | |
| Kontoauszugsentwürfe | `StatementDraft`(+`StatementDraftEntry`) | |
| Kontoauszüge | `StatementImport`(+`StatementEntry`) | |
| Posting-Arten | `PostingKind` | `Bank=0, Contact=1, SavingsPlan=2, Security=3` |
| Kontakttypen | `ContactType` | `Self/Bank/Person/Organization/Other` |
