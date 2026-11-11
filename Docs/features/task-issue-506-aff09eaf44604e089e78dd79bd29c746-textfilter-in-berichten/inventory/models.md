# Datenmodell

## `ReportFavorite`
Datei: `FinanceManager.Domain/Reports/ReportFavorite.cs`

Benutzerbezogener Favorit einer Berichts-Konfiguration (`IAggregateRoot`, `OwnerUserId` im Konstruktor via `Guards.NotEmpty`). Persistierte Filter werden ausschließlich als **CSV-Strings** gehalten.

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `OwnerUserId` | `Guid` | Besitzer des Favoriten (AGENT.md-Benutzerbezug) |
| `Name` | `string` | Anzeigename, `HasMaxLength(120)`, eindeutig je `OwnerUserId` (Index) |
| `PostingKind` | `PostingKind` | Primäre Buchungsart |
| `PostingKindsCsv` | `string?` | CSV der gewählten Buchungsarten (Multi-Kind), gelesen via `GetPostingKinds()`, geschrieben via `SetPostingKinds` |
| `IncludeCategory` | `bool` | Kategorie-Gruppierung aktiv |
| `Interval` | `ReportInterval` | Aggregationsintervall (`HasConversion<int>`) |
| `Take` | `int` | Anzahl Perioden, geclamped auf 1..120 via `SetTake` |
| `ComparePrevious` / `CompareYear` / `CompareProjection` | `bool` | Vergleichs-Flags |
| `ShowChart` / `Expandable` | `bool` | UI-Flags |
| `AccountIdsCsv` | `string?` | CSV gefilterter Konto-IDs |
| `ContactIdsCsv` | `string?` | CSV gefilterter Kontakt-IDs |
| `SavingsPlanIdsCsv` | `string?` | CSV gefilterter Sparplan-IDs |
| `SecurityIdsCsv` | `string?` | CSV gefilterter Wertpapier-IDs |
| `ContactCategoryIdsCsv` | `string?` | CSV gefilterter Kontakt-Kategorie-IDs |
| `SavingsPlanCategoryIdsCsv` | `string?` | CSV gefilterter Sparplan-Kategorie-IDs |
| `SecurityCategoryIdsCsv` | `string?` | CSV gefilterter Wertpapier-Kategorie-IDs |
| `SecuritySubTypesCsv` | `string?` | CSV gefilterter Security-Subtypes (int) |
| `IncludeDividendRelated` | `bool?` | Dividenden-bezogene Buchungen einschließen |
| `UseValutaDate` | `bool` | Aggregation nach `ValutaDate` statt `BookingDate` |
| `SavingsPlanPostingSource` | `SavingsPlanPostingSource` | Quelle der Sparplan-Reihen (Plan- vs. verknüpfte Konto-Buchungen) |

Methoden:
- `SetFilters(...)` (Z. 308–329): nimmt 9 Parameter (`IEnumerable<Guid>?` ×7, `IEnumerable<int>?`, `bool?`), schreibt alle `*Csv`-Spalten + `IncludeDividendRelated`, ruft `Touch()`.
- `GetFilters()` (Z. 338–359): gibt 9-Tupel zurück (`IReadOnlyCollection<Guid>?` ×7, `IReadOnlyCollection<int>?`, `bool?`).
- Hilfsfunktionen `ToCsv`/`ToCsvInt`/`FromCsv`/`FromCsvInt` (Z. 361–368) — rein ID-/int-basiert, kein Escaping für Freitext.
- `Update(...)` (Z. 229–243): setzt Basis-Konfiguration inkl. `UseValutaDate` und `SavingsPlanPostingSource`.
- `Rename`, `GetPostingKinds`, `SetPostingKinds`, `TryParseKind`.

Backup (`ReportFavoriteBackupDto`, Z. 375–399): Record mit allen Feldern; der letzte Parameter `SavingsPlanPostingSource` trägt einen Default-Wert (`PlanPostings`) — Muster für neue Felder gemäß AGENT.md. `ToBackupDto()` (Z. 405) und `AssignBackupDto()` (Z. 412–437) mappen alle Felder 1:1.

**Für die Anforderung fehlen:** persistierte Textfilter-Felder je `PostingKind` (keinerlei `*TextFilter*`-Mitglieder vorhanden).

## `Posting`
Datei: `FinanceManager.Domain/Postings/Posting.cs`

Buchungszeile, Datenquelle der Textfelder. **Kein `OwnerUserId`** — Besitzerprüfung erfolgt über die referenzierten Entitäten (vgl. Besitzer-Dictionaries in `ReportAggregationService`).

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `SourceId` | `Guid` | Quell-/Import-ID (Pflicht, `Guards.NotEmpty`) |
| `GroupId` | `Guid` | Gruppiert zusammengehörige Buchungen (z. B. Dividende+Gebühr+Steuer); genutzt in `QuerySecurityDividendsNetAsync` |
| `Kind` | `PostingKind` | Buchungsart (Bank/Contact/SavingsPlan/Security) |
| `AccountId` / `ContactId` / `SavingsPlanId` / `SecurityId` | `Guid?` | Zielreferenz; mindestens eine muss gesetzt sein (Check-Constraint `CK_Postings_HasReference`) |
| `BookingDate` | `DateTime` | Buchungsdatum |
| `ValutaDate` | `DateTime` | Valutadatum (nicht nullable; Fallback-Konvention `ValutaDate == DateTime.MinValue → BookingDate` in `PostingAggregateService`) |
| `Amount` | `decimal` | Betrag |
| `OriginalAmount` | `decimal?` | Ursprungsbetrag bei nullgestellten Buchungen (`ZeroAmount`, `SetOriginalAmount`) |
| `Subject` | `string?` | Verwendungszweck — **Textfilter-Zielfeld** |
| `RecipientName` | `string?` | Empfängername (von der Anforderung nicht gefordert) |
| `Description` | `string?` | Buchungsbeschreibung — **Textfilter-Zielfeld** |
| `SecuritySubType` | `SecurityPostingSubType?` | Subtyp für Wertpapierbuchungen |
| `SavingsPlanSubType` | `SavingsPlanPostingSubType?` | Subtyp-Marker für Sparplan-Buchungen (z. B. Istmeldung) |
| `Quantity` | `decimal?` | Stückzahl (Wertpapiere) |
| `ParentId` | `Guid?` | Eltern-Buchung (Split/Linked) |
| `LinkedPostingId` | `Guid?` | Gegenbuchung bei Selbstüberträgen |
| `ReversedByPostingId` / `ReversalForPostingId` | `Guid?` | Storno-Verknüpfungen; abgeleitete Flags `IsReversed`/`IsReversal` |
| `ReversedByUserId` / `ReversedAtUtc` | `Guid?` / `DateTime?` | Storno-Metadaten |
| `IsPreliminary` | `bool` | Vorläufige Buchung |

`PostingBackupDto` (Z. 517) enthält `Subject`, `RecipientName`, `Description`, `IsPreliminary` u. a. — die Textfelder sind bereits backup-fähig.

## `PostingAggregate`
Datei: `FinanceManager.Domain/Postings/PostingAggregate.cs`

Vorberechnete Aggregatzeile, Basis des Standard-Auswertepfads in `ReportAggregationService`. **Enthält keine Textfelder** und kann daher nicht textgefiltert werden.

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `Kind` | `PostingKind` | Buchungsart der aggregierten Postings |
| `AccountId` / `ContactId` / `SavingsPlanId` / `SecurityId` | `Guid?` | Gruppierungs-Referenz |
| `SecuritySubType` | `SecurityPostingSubType?` | Subtyp-Aufspaltung für Security-Aggregate |
| `DateKind` | `AggregateDateKind` | Booking vs. Valuta |
| `PeriodStart` | `DateTime` | Periodenbeginn |
| `Period` | `AggregatePeriod` | Granularität (Month/Quarter/HalfYear/Year) |
| `Amount` | `decimal` | Summe; `Add(delta)` ignoriert 0 |

## `HomeKpi`
Datei: `FinanceManager.Domain/Reports/HomeKpi.cs`

KPI-Kachel der Startseite; referenziert den Favoriten nur über `ReportFavoriteId` (`Guid?`, Pflicht bei `HomeKpiKind.ReportFavorite`, validiert in `Validate()`). Keine eigenen Filterfelder — die Filter wirken indirekt über den `ReportFavorite`. `HomeKpiBackupDto` vorhanden; EF-Kaskade: Löschen des Favoriten löscht abhängige KPIs (`DeleteBehavior.Cascade`, `AppDbContext` Z. 745–748).

## `SavingsPlanAccountLink`
Datei: `FinanceManager.Domain/Savings/SavingsPlanAccountLink.cs`

N:M-Verknüpfung Sparplan↔Konto (`SavingsPlanId`, `AccountId`, `OwnerUserId`), eindeutig je Paar. Wird in `ReportAggregationService` gelesen, wenn `SavingsPlanPostingSource == LinkedAccountPostings`: Bank-Aggregatzeilen der verknüpften Konten werden als `PostingKind.SavingsPlan`-Zeilen abgeleitet.

## `BudgetRule`
Datei: `FinanceManager.Domain/Budget/BudgetRule.cs`

Vorbild für die Pattern-Konvention: `PurposePattern` (`string?`, max. **500 Zeichen**, Validierung in `SetPurposePattern` Z. 158–192 inkl. Regex-Kompilierbarkeits-Prüfung) und `PurposePatternIsRegex` (`bool`). Beide Felder sind im `BudgetRuleBackupDto` mit Defaults abgesichert — Referenzmuster für Backup-Erweiterung neuer Felder.

## EF-Konfiguration
Datei: `FinanceManager.Infrastructure/AppDbContext.cs`

- `ReportFavorite` (Z. 699–708): Key, `Name` max 120 + Unique-Index `(OwnerUserId, Name)`, `PostingKind` required, `Interval` als int, `Take` required. Die `*Csv`-Spalten haben keine explizite Konfiguration (Standard-Mapping).
- `Posting` (Z. 513–520): Indizes `(AccountId, BookingDate)`, `(SecurityId, BookingDate)`, `OriginalAmount` Precision 18,2, Check-Constraint `CK_Postings_HasReference`. `Subject`/`Description`/`RecipientName` ohne Längenkonfiguration.
- `HomeKpi` (Z. 734–749): FK auf `ReportFavorite` mit `DeleteBehavior.Cascade`.
- Neue `ReportFavorite`-Spalten erfordern eine Migration per `dotnet ef migrations add` (AGENT.md).
