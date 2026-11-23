← [Zurück zur Übersicht](index.md)

# Wertpapiermanagement — Datenmodell

## Entitäten

### `Security`

| Eigenschaft | Typ | Beschreibung |
|-------------|-----|--------------|
| `Id` | `Guid` | Wertpapier-ID |
| `OwnerUserId` | `Guid` | Eigentümer |
| `Name` | `string` | Name |
| `Identifier` | `string` | Kennung (z. B. ISIN/WKN) |
| `AlphaVantageCode` | `string?` | Externer Kurscode |
| `CurrencyCode` | `string` | Währung |
| `CategoryId` | `Guid?` | Kategorie |
| `Region` | `string?` | Optionale Region (max. 255 Zeichen), Basis für die regionale Verteilung im Depot-Analysebericht |
| `Sector` | `string?` | Optionaler Sektor (max. 255 Zeichen), Basis für die Sektorverteilung im Depot-Analysebericht |
| `HasPriceError` | `bool` | Preisfehler-Flag |
| `SymbolAttachmentId` | `Guid?` | Symbol |
| `GoldenCrossNotifiedPhase` | `GoldenCrossPhase` | Zuletzt per Startseiten-Hinweis gemeldete Goldenes-Kreuz-Phase (Standard `Far`); verhindert Mehrfach-Hinweise innerhalb eines Zyklus |

`GoldenCrossPhase` ist ein Enum mit den Werten `InsufficientData`, `Far`,
`Approaching`, `Crossed`. Die Benutzereinstellung
`User.GoldenCrossNotificationsEnabled` (`bool`, Standard `true`) steuert, ob
Goldenes-Kreuz-Hinweise auf der Startseite erzeugt werden.

### `PortfolioKpiConfiguration`

| Eigenschaft | Typ | Beschreibung |
|-------------|-----|--------------|
| `Id` | `Guid` | Konfigurations-ID |
| `OwnerUserId` | `Guid` | Eigentümer (eindeutig, ein Datensatz pro Benutzer) |
| `ActiveTileIds` | `string` | JSON-serialisiertes Array sichtbarer `PortfolioTileId`-Werte |
| `TileOrder` | `string` | JSON-serialisiertes Array mit der Anzeigereihenfolge aller Kachel-IDs |
| `UpdatedUtc` | `DateTime` | Zeitpunkt der letzten Änderung |

`PortfolioTileId` ist ein Enum mit den Werten `Structure`, `Performance`,
`Cashflow`, `Risk`, `DepotDevelopment`.

### `ReportCacheEntry` (erweitert)

| Eigenschaft | Typ | Beschreibung |
|-------------|-----|--------------|
| `CacheValidUntilUtc` | `DateTime?` | Zeitpunkt, bis zu dem der Cache-Eintrag gültig ist (z. B. Monatsende für den Depot-Analysebericht); `null` bedeutet keine zeitliche Begrenzung (bestehendes Verhalten anderer Berichte). Der Depot-Analysebericht wird unter dem Schlüssel `portfolio-analysis-report-{OwnerUserId:N}` abgelegt; `Parameter` trägt dabei die DTO-Schema-Version (`CacheSchemaVersion`, aktuell `"6"`) — abweichende Versionen gelten als Cache-Miss. |

### `SecurityPrice`

| Eigenschaft | Typ | Beschreibung |
|-------------|-----|--------------|
| `Id` | `Guid` | Kurs-ID |
| `SecurityId` | `Guid` | Wertpapierreferenz |
| `Date` | `DateTime` | Kursdatum |
| `Close` | `decimal` | Schlusskurs |
| `CreatedUtc` | `DateTime` | Erzeugungszeit |

### `SecurityCategory`

| Eigenschaft | Typ | Beschreibung |
|-------------|-----|--------------|
| `Id` | `Guid` | Kategorie-ID |
| `OwnerUserId` | `Guid` | Eigentümer |
| `Name` | `string` | Kategoriename |

### `CurrencyExchangeRate`

| Eigenschaft | Typ | Beschreibung |
|-------------|-----|--------------|
| `Id` | `Guid` | Kurs-ID |
| `CurrencyCode` | `string` | Fremdwährung (ISO 4217, normalisiert Großbuchstaben) |
| `Date` | `DateTime` | Datum des Referenzkurses |
| `RatePerEur` | `decimal` | Fremdwährungseinheiten je 1 EUR |
| `Source` | `string` | Kursquelle (z. B. `Frankfurter` = EZB-Referenzkurse via api.frankfurter.dev) |
| `CreatedUtc` | `DateTime` | Erzeugungszeit |
| `ModifiedUtc` | `DateTime?` | Änderungszeit (unbenutzt) |

`CurrencyExchangeRate` ist ein **globaler, benutzerübergreifender
Referenzdatenbestand** — die Entität trägt bewusst **kein** `OwnerUserId`
(dokumentierte Ausnahme von der OwnerUserId-Regel): Wechselkurse sind objektive
Markt-Referenzwerte, die für alle Benutzer identisch sind; ein gemeinsamer Pool
vermeidet doppelte Provider-Aufrufe und hält die Tabelle klein. Eindeutigkeit je
(`CurrencyCode`, `Date`, `Source`). `ClearUserDataAsync` lässt die Tabelle
unberührt; im Benutzer-Backup wandert sie als optionale Sektion
`CurrencyExchangeRates` mit (Import dedupliziert auf dem Unique-Schlüssel).

## Beziehungen

- Ein `Security` hat viele `SecurityPrice`-Einträge.
- Eine `SecurityCategory` kann vielen Wertpapieren zugeordnet sein.
- Der Depot-Analysebericht aggregiert alle `Security`-, `Posting`- (mit
  `SecuritySubType`) und `SecurityPrice`-Datensätze eines Benutzers zu einem
  `PortfolioAnalysisReportDto`, das als JSON in einem `ReportCacheEntry`
  zwischengespeichert wird.
- Für die Liquiditätsquote leitet der Depot-Analysebericht Verrechnungskonten
  aus vorhandenen `Posting.GroupId`-Beziehungen ab: Security-Postings des
  Benutzers bestimmen relevante Gruppen, Bank-Postings derselben Gruppen
  liefern die `AccountId`, und die aktuellen Salden dieser Konten werden
  dedupliziert summiert.
- Eine `PortfolioKpiConfiguration` gehört genau einem Benutzer
  (`OwnerUserId` eindeutig) und bestimmt, welche Kacheln im
  Depot-Analysebericht angezeigt werden und in welcher Reihenfolge.
- `CurrencyExchangeRate` steht ohne Beziehung zu Benutzerdaten im globalen Pool;
  `FxRateService` nutzt die Tabelle als persistenten Kurs-Cache für die
  EUR-Bewertung von Fremdwährungspositionen (`fm_securities_analysis`,
  `includeEurValue`).

## Diagramm

Es bestehen keine Fremdschlüssel zwischen `Security`, `PortfolioKpiConfiguration`
und `ReportCacheEntry` — der Depot-Analysebericht verknüpft diese Daten
ausschließlich zur Laufzeit über den gemeinsamen `OwnerUserId`, nicht über
Datenbankbeziehungen.

```mermaid
erDiagram
    SECURITY {
        Guid Id
        Guid OwnerUserId
        string Name
        string Identifier
        Guid CategoryId
        string Region
        string Sector
    }
    SECURITY_PRICE {
        Guid Id
        Guid SecurityId
        DateTime Date
        decimal Close
    }
    SECURITY_CATEGORY {
        Guid Id
        Guid OwnerUserId
        string Name
    }
    PORTFOLIO_KPI_CONFIGURATION {
        Guid Id
        Guid OwnerUserId
        string ActiveTileIds
        string TileOrder
        DateTime UpdatedUtc
    }
    REPORT_CACHE_ENTRY {
        Guid Id
        Guid OwnerUserId
        string CacheKey
        string CacheValue
        DateTime CacheValidUntilUtc
    }

    SECURITY ||--o{ SECURITY_PRICE : "hat"
    SECURITY_CATEGORY ||--o{ SECURITY : "kategorisiert"
```
