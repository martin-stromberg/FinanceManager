# Datenmodell / DTOs – Hochrechnung (CompareProjection)

## `ReportAggregationQuery`
Datei: `FinanceManager.Shared/Dtos/Reports/ReportAggregationQuery.cs`

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `OwnerUserId` | `Guid` | Besitzer der zu aggregierenden Daten (serverseitige Besitzerfilterung). |
| `PostingKind` | `PostingKind` | Primäre Buchungsart (Legacy-Einzelfeld). |
| `Interval` | `ReportInterval` | Aggregationsintervall (Month/Quarter/HalfYear/Year/Ytd/AllHistory). |
| `Take` | `int` | Max. Anzahl Perioden. |
| `IncludeCategory` | `bool` | Kategoriegruppierung einschalten. |
| `ComparePrevious` | `bool` | Vorperiodenvergleich. |
| `CompareYear` | `bool` | Vorjahresvergleich. |
| `CompareProjection` | `bool = false` | Fordert die Hochrechnung an; XML-Doku spricht noch von „dividend projection". |
| `PostingKinds` | `IReadOnlyCollection<PostingKind>?` | Mehrfachauswahl (Fallback: `PostingKind`). |
| `AnalysisDate` | `DateTime?` | Analysemonat (Anker). |
| `UseValutaDate` | `bool` | Effektivdatum = ValutaDate mit BookingDate-Fallback. |
| `Filters` | `ReportAggregationFilters?` | Entitäts-/Kategorie-/Subtyp-/Textfilter. |
| `SavingsPlanPostingSource` | `SavingsPlanPostingSource` | Quelle für Sparplan-Reihen (`PlanPostings`/`LinkedAccountPostings`). |

## `ReportAggregationResult`
Datei: `FinanceManager.Shared/Dtos/Reports/ReportAggregationResult.cs`

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `Interval` | `ReportInterval` | Verwendetes Intervall. |
| `Points` | `IReadOnlyList<ReportAggregatePointDto>` | Aggregierte Punkte. |
| `ComparedPrevious` | `bool` | Vorperiodenvergleich aktiv. |
| `ComparedYear` | `bool` | Vorjahresvergleich aktiv. |
| `ComparedProjection` | `bool` | Hochrechnung tatsächlich aktiv (Server-Entscheid). |

## `ReportAggregatePointDto`
Datei: `FinanceManager.Shared/Dtos/Reports/ReportAggregatePointDto.cs` (Z. 22–34)

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `PeriodStart` | `DateTime` | Periodenanker. |
| `GroupKey` | `string` | Stabiler Gruppenschlüssel (`Account:{id}`, `Contact:{id}`, `SavingsPlan:{id}`, `Security:{id}`/`Security:{id}:{subType}`, `Category:{kind}:{id|_none}`, `Type:{kind}`). |
| `GroupName` | `string` | Anzeigename. |
| `CategoryName` | `string?` | Kategoriename. |
| `Amount` | `decimal` | Gebuchter Betrag. |
| `ProjectionAmount` | `decimal?` | Hochgerechneter Betrag (aktuell nur Security-Netto-Dividendenpfad; XML-Doku: „for dividend reports"). |
| `ParentGroupKey` | `string?` | Eltern-Gruppenschlüssel für Hierarchie. |
| `PreviousAmount` | `decimal?` | Vorperiode. |
| `YearAgoAmount` | `decimal?` | Vorjahr. |
| `ProjectionExpectedDividends` | `IReadOnlyList<ReportProjectionExpectedDividendDto>? = null` | Erwartete Einzelereignisse der Hochrechnung (wertpapierbezogen modelliert). |
| `SecuritySubType` | `SecurityPostingSubType? = null` | Untertyp gesplitteter Security-Punkte. |

## `ReportProjectionExpectedDividendDto`
Datei: `FinanceManager.Shared/Dtos/Reports/ReportAggregatePointDto.cs` (Z. 45–50)

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `SecurityId` | `Guid` | Wertpapier-Id — **wertpapierbezogen benannt**, für andere Postenarten nicht generisch. |
| `SecurityName` | `string` | Wertpapiername (im Dashboard als Entitätsspalte angezeigt). |
| `ExpectedDate` | `DateTime` | Erwartetes Datum (+1 Jahr gegenüber Vorjahr). |
| `PriorYearDate` | `DateTime` | Vorjahresdatum. |
| `Amount` | `decimal` | Erwarteter (Netto-)Betrag. |

## `ReportAggregationFilters`
Datei: `FinanceManager.Shared/Dtos/Reports/ReportAggregationFilters.cs`

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `AccountIds` / `ContactIds` / `SavingsPlanIds` / `SecurityIds` | `IReadOnlyCollection<Guid>?` | Entitäts-Allow-Listen je Postenart. |
| `ContactCategoryIds` / `SavingsPlanCategoryIds` / `SecurityCategoryIds` | `IReadOnlyCollection<Guid>?` | Kategorie-Allow-Listen (nur i. V. m. `IncludeCategory`). |
| `SecuritySubTypes` | `IReadOnlyCollection<int>?` | Wertpapier-Untertypfilter; `2 = Dividend` steuert den Netto-Dividendenpfad. |
| `IncludeDividendRelated` | `bool?` | Fee/Tax aus Dividendengruppen einbeziehen (Netto-Dividende). |
| `BankTextFilters` / `ContactTextFilters` / `SavingsPlanTextFilters` / `SecurityTextFilters` | `IReadOnlyCollection<string>?` | Freitextfilter je Postenart (OR, Substring auf Subject/Description). |

## `ReportAggregatesQueryRequest` (API-DTO)
Datei: `FinanceManager.Shared/Dtos/Reports/ReportAggregatesQueryRequest.cs`

Feldgleich zu `ReportAggregationQuery` (ohne `OwnerUserId` — kommt aus dem Auth-Kontext in `ReportsController`, Z. 123). `CompareProjection` ist ein frei setzbares API-Feld ohne Security-Einschränkung.

## `ReportPostingsQuery` / `ReportPostingsQueryRequest`
Dateien: `FinanceManager.Shared/Dtos/Reports/ReportPostingsQuery.cs`, `ReportPostingsQueryRequest.cs`

Tragen `CompareProjection` (XML-Doku: „dividend projection comparison") für die Posteneinsicht netted-Dividendenzellen; wird in `ReportPostingsQueryService` genauso auf `onlySecurity` begrenzt (Z. 83).

## `ReportFavorite` (Domain)
Datei: `FinanceManager.Domain/Reports/ReportFavorite.cs`

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `CompareProjection` | `bool` | Persistierte Hochrechnungsoption (Z. 142); artunabhängig, keine Validierung gegen `PostingKind`. |
| `PostingKind` / `PostingKindsCsv` | `PostingKind` / `string?` | Primärart / Mehrfachauswahl. |
| `Interval`, `Take`, `UseValutaDate`, `SavingsPlanPostingSource`, Filter-CSV/JSON | diverse | Übrige Favoritenparameter; `ReportFavoriteBackupDto` (Z. 417 ff.) enthält `CompareProjection` bereits. |

## `Posting` (Domain)
Datei: `FinanceManager.Domain/Postings/Posting.cs`

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `Kind` | `PostingKind` | Postenart. |
| `AccountId` / `ContactId` / `SavingsPlanId` / `SecurityId` | `Guid?` | Entitätsverweis (mind. einer Pflicht); Posting trägt **kein** `OwnerUserId` — Besitzerfilterung läuft über die Entitäten. |
| `BookingDate` / `ValutaDate` | `DateTime` | Buchungs-/Valutadatum (`ValutaDate == MinValue` ⇒ Fallback auf BookingDate). |
| `Amount` / `OriginalAmount` | `decimal` / `decimal?` | Betrag / ursprünglicher Betrag. |
| `Subject` / `RecipientName` / `Description` | `string?` | Textfelder (Textfilter matchen Subject+Description). |
| `SecuritySubType` | `SecurityPostingSubType?` | Wertpapier-Untertyp (Anker für Dividendengruppen). |
| `SavingsPlanSubType` | `SavingsPlanPostingSubType?` | Sparplan-Untertyp (Istmeldung). |
| `Quantity` | `decimal?` | Menge (Bestandsrechnung, nur Wertpapiere). |
| `GroupId` | `Guid` | Gruppierung für Netting (Dividend+Fee+Tax). |
| `ParentId` / `LinkedPostingId` | `Guid?` | Split-/Verknüpfungsreferenzen. |
| `ReversedByPostingId` / `ReversalForPostingId` / `ReversedByUserId` / `ReversedAtUtc` | `Guid?` / `Guid?` / `Guid?` / `DateTime?` | Storno-Beziehungen (in `HasPositiveHolding` relevant). |

## `PostingAggregate` (Domain)
Datei: `FinanceManager.Domain/Postings/PostingAggregate.cs`

Vorverdichtete Summen je `Kind`, `Period`, `PeriodStart`, `DateKind` (Booking/Valuta), Entitäts-Ids und `SecuritySubType`; enthält **keine** Textfelder und **keine** Einzelbuchungen — für die Hochrechnung ohne Aggregatpfad nicht ausreichend (Rohpostings nötig).

## Private Projektions-Records in `ReportAggregationService`
Datei: `FinanceManager.Infrastructure/Reports/ReportAggregationService.cs` (Z. 1625–1637)

| Typ | Member | Zweck |
|-----|--------|-------|
| `DividendEvent` (record, Z. 1625) | `SecurityId`, `Date`, `Month`, `NetAmount`, `GroupId` | Netto-Dividendenereignis je Gruppe. |
| `HoldingTransaction` (record, Z. 1627) | `SecurityId`, `Date`, `SubType`, `Quantity`, `IsReversal` | Buy/Sell für `HasPositiveHolding`. |
| `ProjectionCandidate` (record, Z. 1629) | `SecurityId`, `ExpectedDate`, `PriorYearDate`, `NetAmount` | +1-Jahr-projiziertes Vorjahresereignis. |
| `DividendPattern` (enum, Z. 1631) | `Monthly`, `Quarterly`, `Annual`, `Irregular` | Erkanntes Auszahlungsmuster. |

Alle vier Typen tragen `SecurityId` hartkodiert und sind `private` im Service — aktuell nicht artübergreifend wiederverwendbar.
