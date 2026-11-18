# Datenmodell — Bestandsaufnahme (Issue #541 „Postenart Zinsen")

Alle Angaben beziehen sich auf den Stand des Branches `task/issue-541-46e10511d26f483c98ea388f27436ac2-postenart-zinsen`. Es existiert aktuell **keine** Entität, Eigenschaft oder Spalte für eine „Postenart" (Buchungsart Eingang/Ausgang/Zins/Steuer) und **keine** Entität für Kontierungsregeln.

## `Posting`
Datei: `FinanceManager.Domain/Postings/Posting.cs`

Aggregate-Root (`Entity`, `IAggregateRoot`). Relevante Eigenschaften:

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `Id` | `Guid` (via `Entity`) | Primärschlüssel |
| `SourceId` | `Guid` | Quell-Id (beim Buchen = `StatementDraftEntry.Id`) |
| `GroupId` | `Guid` | Gruppiert Bank-/Kontakt-/Wertpapier-Postings einer Buchung; gesetzt via `SetGroup` |
| `Kind` | `PostingKind` | Ziel-Domäne des Postens (`Bank`, `Contact`, `SavingsPlan`, `Security`) — andere Achse als die geforderte Postenart |
| `AccountId` | `Guid?` | Referenziertes Bankkonto (bei `PostingKind.Bank` gesetzt) |
| `ContactId` | `Guid?` | Referenzierter Kontakt |
| `SavingsPlanId` | `Guid?` | Referenzierter Sparplan |
| `SecurityId` | `Guid?` | Referenziertes Wertpapier |
| `BookingDate` | `DateTime` | Buchungsdatum |
| `ValutaDate` | `DateTime` | Valutadatum |
| `Amount` | `decimal` | Betrag (Vorzeichen bestimmt aktuell implizit Ein-/Ausgang) |
| `OriginalAmount` | `decimal?` | Ursprungsbetrag bei auf 0 gesetzten Buchungen (`ZeroAmount`) |
| `Subject` | `string?` | Verwendungszweck — eines der zwei regel-relevanten Textfelder |
| `RecipientName` | `string?` | Empfängername |
| `Description` | `string?` | Buchungsbeschreibung — zweites regel-relevantes Textfeld |
| `SecuritySubType` | `SecurityPostingSubType?` | Wertpapier-Unterart (Buy/Sell/Dividend/Fee/Tax) — Fachmuster für „SubType"-Eigenschaft |
| `SavingsPlanSubType` | `SavingsPlanPostingSubType?` | Sparplan-Unterart (ActualReport); Set-Methode `SetSavingsPlanSubType` (Posting.cs:265) |
| `Quantity` | `decimal?` | Menge (Wertpapier) |
| `ParentId` | `Guid?` | Split-Parent-Referenz |
| `LinkedPostingId` | `Guid?` | Gegenposten bei Selbstüberträgen |
| `ReversedByPostingId` / `ReversalForPostingId` / `ReversedByUserId` / `ReversedAtUtc` | `Guid?` / `Guid?` / `Guid?` / `DateTime?` | Storno-Verfolgung; abgeleitete Flags `IsReversed`, `IsReversal` |
| `IsPreliminary` | `bool` | Vorläufige Buchung; Set via `SetIsPreliminary` (Posting.cs:452) |

Set-/Fluent-Methoden (Muster für eine neue `SetPostingType`-Methode): `SetGroup` (Posting.cs:328), `SetParent` (:344), `SetLinkedPosting` (:360), `SetValutaDate` (:375), `SetSavingsPlanSubType` (:265), `SetIsPreliminary` (:452), `SetOriginalAmount` (:464), `ZeroAmount` (:476), `SetReversedBy` (:389), `SetReversalFor` (:414).

Backup: `PostingBackupDto` (Posting.cs:517) — Record mit optionalen Endparametern `bool IsPreliminary = false`, `SavingsPlanPostingSubType? SavingsPlanSubType = null` (Default-Muster für Alt-Backups). `ToBackupDto()` (:523), `AssignBackupDto()` (:531).

## `StatementDraft`
Datei: `FinanceManager.Domain/Statements/StatementDraft.cs:11`

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `OwnerUserId` | `Guid` | Besitzer (AGENT.md-Benutzerbezug) |
| `OriginalFileName` | `string` | Upload-Dateiname |
| `AccountName` | `string?` | Erkannter Kontoname/-nummer |
| `Description` | `string?` | Beschreibung |
| `DetectedAccountId` | `Guid?` | Erkanntes/zugeordnetes `Account.Id` — Bezugspunkt für Kontierungsregeln |
| `Status` | `StatementDraftStatus` | `Draft`/`Committed`/`Expired` |
| `IsPreliminary` | `bool` | Vorläufiger Entwurf |
| `Entries` | `ICollection<StatementDraftEntry>` | Zeilen (Cascade-Delete) |
| `UploadGroupId` | `Guid?` | Gruppierung zusammengehöriger Uploads |

Backup: `StatementDraftBackupDto` (StatementDraft.cs:224) mit Default `bool IsPreliminary = false`; `ToBackupDto()` (:230), `AssignBackupDto(dto, includeEntries)` (:239).

## `StatementDraftEntry`
Datei: `FinanceManager.Domain/Statements/StatementDraft.cs:266`

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `DraftId` | `Guid` | Eltern-Draft |
| `EntryNumber` | `int` | Laufende Nummer im Draft |
| `BookingDate` | `DateTime` | Buchungsdatum |
| `ValutaDate` | `DateTime?` | Valutadatum |
| `Amount` | `decimal` | Betrag — Vorzeichen-Fallback der Postenart |
| `Subject` | `string` | Verwendungszweck — regelbasiertes Textfeld 1 |
| `RecipientName` | `string?` | Empfänger |
| `CurrencyCode` | `string` | Default „EUR" |
| `BookingDescription` | `string?` | Buchungsbeschreibung — regelbasiertes Textfeld 2 |
| `IsAnnounced` | `bool` | Vorgemerkter Umsatz |
| `Status` | `StatementDraftEntryStatus` | Open/Announced/Accounted/AlreadyBooked |
| `ContactId` | `Guid?` | Zugeordneter Kontakt |
| `SavingsPlanId` | `Guid?` | Zugeordneter Sparplan |
| `ArchiveSavingsPlanOnBooking` | `bool` | Archivierung beim Buchen |
| `IsCostNeutral` | `bool` | Kostenneutral-Flag |
| `SplitDraftId` | `Guid?` | Split-Kind-Draft |
| `SecurityId` / `SecurityTransactionType` / `SecurityQuantity` / `SecurityFeeAmount` / `SecurityTaxAmount` | `Guid?` / `SecurityTransactionType?` / `decimal?` / `decimal?` / `decimal?` | Wertpapier-Zuordnung (Set via `SetSecurity`, :559) |

Relevante Methoden: `UpdateCore` (:433), `MarkAlreadyBooked` (:448), `MarkAccounted` (:458), `ClearContact` (:468), `ResetOpen` (:481), `MarkCostNeutral` (:492), `AssignSavingsPlan` (:501), `AssignContactWithoutAccounting` (:544), `SetSecurity` (:559), `MarkNeedsCheck` (:581). Keine Methode zum Setzen einer Postenart vorhanden.

Backup: `StatementDraftEntryBackupDto` (StatementDraft.cs:615) — Record **ohne** Default-Parameter; `ToBackupDto()` (:621), `AssignBackupDto()` (:629).

## `Account`
Datei: `FinanceManager.Domain/Accounts/Account.cs`

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `OwnerUserId` | `Guid` | Besitzer — Besitzerpfad für Regel-Abfragen (AGENT.md) |
| `Type` | `AccountType` | `Giro`/`Savings`/`CreditCard` |
| `Name` | `string` | Anzeigename |
| `Iban` | `string?` | IBAN |
| `CurrentBalance` | `decimal` | Saldo (via `AdjustBalance`) |
| `BankContactId` | `Guid` | Bankkontakt |
| `SymbolAttachmentId` | `Guid?` | Symbol |
| `SavingsPlanExpectation` | `SavingsPlanExpectation` | Sparplan-Erwartung |
| `SecurityProcessingEnabled` | `bool` | Wertpapierverarbeitung erlaubt |
| `IsCollectionAccount` | `bool` | Sammelkonto (verweist auf `AccountLinkedIban`-Muster) |

Backup: `AccountBackupDto` (Account.cs:198) mit Defaults `bool SecurityProcessingEnabled = true`, `bool IsCollectionAccount = false`; `ToBackupDto()` (:204), `AssignBackupDto()` (:213).

## `AccountLinkedIban` (Referenzmuster für die neue Regel-Entität)
Datei: `FinanceManager.Domain/Accounts/AccountLinkedIban.cs`

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `AccountId` | `Guid` | Besitzerpfad über `Account` (kein eigenes `OwnerUserId`) |
| `Iban` | `string` | Verknüpfte Unter-IBAN |
| `Account` | `Account?` | Navigation (Cascade-Delete) |

Backup: `AccountLinkedIbanBackupDto(Guid Id, Guid AccountId, string Iban)` (:42); `ToBackupDto()` (:48), `AssignBackupDto()` (:57).

## EF-Core-Konfiguration (`AppDbContext`)
Datei: `FinanceManager.Infrastructure/AppDbContext.cs`

- `DbSet<Account>` :44, `DbSet<AccountLinkedIban>` :53, `DbSet<Posting>` :116, `DbSet<StatementDraft>` :125, `DbSet<StatementDraftEntry>` :134.
- `Account`-Konfig (:447–463): Unique-Index `(OwnerUserId, Name)` und gefilterter Unique-Index `(OwnerUserId, Iban)`; `SavingsPlanExpectation` als `short` konvertiert; Defaults für `SecurityProcessingEnabled`, `IsCollectionAccount`.
- `AccountLinkedIban`-Konfig (:466–475): eigene Tabelle `AccountLinkedIbans`, Unique-Index `(AccountId, Iban)`, `Account`-Navigation mit `DeleteBehavior.Cascade` — direktes Muster für eine Regel-Entität.
- `Posting`-Konfig (:523–530): Indizes `(AccountId, BookingDate)`, `(SecurityId, BookingDate)`, Check-Constraint `CK_Postings_HasReference`; `OriginalAmount` Precision (18,2).
- `StatementDraft`-Konfig (:532–541): `Entries` HasMany mit Cascade-Delete, Index `(OwnerUserId, CreatedUtc)`.
- `StatementDraftEntry`-Konfig (:543–546, :593–599, :665–672): `Subject` MaxLength 500, Index `(DraftId, BookingDate)`, `SplitDraftId`-Unique-Index, Security-Felder inkl. Precision.

Kein `DbSet` bzw. keine Konfiguration für Kontierungsregeln vorhanden; keine Postenart-Spalte an `Postings`/`StatementDraftEntries` (Migrationsstand: letzte Migrationsserie `20250902*`, aktueller Snapshot `AppDbContextModelSnapshot`).
