# Datenmodell – Bestand

## `Security`
Datei: `FinanceManager.Domain/Securities/Security.cs`

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `Id` | `Guid` | Datensatz-Id (geerbt von `Entity`, im Konstruktor `Guid.NewGuid()`, Zeile 28) |
| `OwnerUserId` | `Guid` | Besitzer des Wertpapiers (Zeile 39) |
| `Name` | `string` | Anzeigename (Zeile 45) |
| `Description` | `string?` | Optionale Beschreibung (Zeile 51) |
| `Identifier` | `string` | Primärkennung, z. B. WKN/ISIN (Zeile 57) |
| `AlphaVantageCode` | `string?` | Code für externe Kursabrufe (Zeile 63) |
| `CurrencyCode` | `string` | ISO-Währungscode (Zeile 69) |
| `CategoryId` | `Guid?` | Optionale Wertpapierkategorie — Filterdimension des `McpRecordScopeFilter` (Zeile 75) |
| `Region` | `string?` | Optionale Region für Regionenverteilung (Zeile 81) |
| `Sector` | `string?` | Optionaler Sektor für Sektorverteilung (Zeile 87) |
| `IsActive` | `bool` | Aktiv-Flag (Zeile 93) |
| `ArchivedUtc` | `DateTime?` | Archivierungszeitpunkt (Zeile 99) |
| `HasPriceError` | `bool` | Fehlerflag beim letzten Kursabruf (Zeile 105) — wird vom MCP-Handler bereits als `hasPriceError` ausgegeben |
| `PriceErrorMessage` | `string?` | Fehlermeldung (Zeile 111) — **nicht** Teil des `SecurityDto` |
| `PriceErrorSinceUtc` | `DateTime?` | Zeitpunkt seit Fehlerflag (Zeile 117) — **nicht** Teil des `SecurityDto` |
| `SymbolAttachmentId` | `Guid?` | Symbol-Anhang (Zeile 123) |
| `GoldenCrossNotifiedPhase` | `GoldenCrossPhase` | Benachrichtigungsstand Goldenes Kreuz (Zeile 130) |
| `TrendReversalNotifiedStatus` | `TrendStatus` | Benachrichtigungsstand Trendwende (Zeile 146) |

Methoden relevant für den Fehlerzustand: `SetPriceError(string)` (Zeilen 212–217) setzt `HasPriceError = true` samt Message/Zeitstempel; `ClearPriceError()` (Zeilen 222–227) setzt alle drei Felder zurück.

Kein Feld „letzter Kurs" — die Anforderung sieht ausdrücklich keine Persistenz/ein denormalisiertes Feld vor. `Security.SecurityBackupDto` (Zeile 276) enthält die Fehlerfelder bereits mit Defaults.

## `SecurityPrice`
Datei: `FinanceManager.Domain/Securities/SecurityPrice.cs`

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `Id` | `Guid` | Datensatz-Id (Zeile 12) |
| `SecurityId` | `Guid` | Zugehöriges Wertpapier (Zeile 18) |
| `Date` | `DateTime` | Kursdatum; Konstruktor schneidet auf `date.Date` (Zeilen 24, 54) |
| `Close` | `decimal` | Schlusskurs (Zeile 30) |
| `CreatedUtc` | `DateTime` | Anlagezeitpunkt (Zeile 36) |

EF-Konfiguration (`FinanceManager.Infrastructure/AppDbContext.cs` Zeilen 718–724): Primärschlüssel `Id`, **Unique-Index auf `{ SecurityId, Date }`** (`b.HasIndex(x => new { x.SecurityId, x.Date }).IsUnique()`, Zeile 721) — pro Wertpapier und Datum existiert höchstens ein Kurs. `Close` mit `HasPrecision(18, 4)`. Damit ist die in der Anforderung offene Tie-Break-Frage (Mehrfachkurse am selben Datum) im Modell bereits beantwortet: Der Unique-Index verhindert Duplikate; die Bestimmung „letzter Kurs" über `Max(Date)` bzw. `OrderByDescending(Date).First()` ist deterministisch. `DbSet<SecurityPrice> SecurityPrices` unter `AppDbContext.cs` Zeilen 202–206.

## `SecurityDto`
Datei: `FinanceManager.Shared/Dtos/Securities/SecurityDto.cs`

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `Id` | `Guid` | Wertpapier-Id (Zeile 13) |
| `Name` | `string` | Anzeigename (Zeile 15) |
| `Description` | `string?` | Beschreibung (Zeile 17) |
| `Identifier` | `string` | ISIN/Ticker (Zeile 19) |
| `AlphaVantageCode` | `string?` | Externer Kurs-Code (Zeile 21) |
| `CurrencyCode` | `string` | Währung, Default `"EUR"` (Zeile 23) |
| `CategoryId` | `Guid?` | Kategorie-Id (Zeile 25) — Grundlage des MCP-Record-Filters |
| `CategoryName` | `string?` | Kategoriename für die Anzeige (Zeile 27) |
| `IsActive` | `bool` | Aktiv-Flag (Zeile 29) |
| `CreatedUtc` | `DateTime` | Anlagezeitpunkt (Zeile 31) |
| `ArchivedUtc` | `DateTime?` | Archivierungszeitpunkt (Zeile 33) |
| `SymbolAttachmentId` | `Guid?` | Symbol-Anhang (Zeile 35) |
| `HasPriceError` | `bool` | Preisfehler-Flag (Zeile 39) |
| `OpenQuantity` | `decimal?` | Offene Bestandsmenge, Σ Buy − Σ Sell (Zeile 41) |
| `Region` | `string?` | Region (Zeile 43) |
| `Sector` | `string?` | Sektor (Zeile 45) |

Kein `LatestPrice`-Feld vorhanden. Wird von `SecurityService.MapToDtoAsync` (`FinanceManager.Infrastructure/Securities/SecurityService.cs` Zeilen 246–273) befüllt; `OpenQuantity` wird im Listenpfad gebündelt via `Postings`-`GroupBy` berechnet (Zeilen 35–48). Das DTO speist sowohl `GET /api/securities` (Blazor-UI) als auch den MCP-Handler — relevant für die in der Anforderung offene Scope-Frage (Variante a vs. b).

## `SecurityPriceDto`
Datei: `FinanceManager.Shared/Dtos/Securities/SecurityPriceDtos.cs`

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `Date` | `DateTime` | Kursdatum (Positionsparameter des `sealed record`, Zeile 9) |
| `Close` | `decimal` | Schlusskurs |

Record ohne Id — genau die für `latestPrice` benötigte Shape `{ date, close }`. Im MCP-Handler wird daraus ein anonymes `{ date = p.Date, close = p.Close }` projiziert (`FmSecuritiesAnalysisTool.cs` Zeile 122); `DateTime` wird via `System.Text.Json` als ISO-8601-Zeitstempel serialisiert (nicht date-only `yyyy-MM-dd`).

## `SecurityPriceImportItem`
Datei: `FinanceManager.Shared/Dtos/Securities/SecurityPriceImportDtos.cs` (Zeile 33)

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `Date` | `DateTime` | Handelstag (Datumskomponente) |
| `Close` | `decimal` | Schlusskurs |
| `SourceLine` | `int` | Ursprungszeile im Import |

Nur für `UpsertDailyPricesAsync` relevant (Schreibpfad, kein Einfluss auf `latestPrice`).

## `McpRecordContext`
Datei: `FinanceManager.Application/Mcp/IMcpScopeEvaluator.cs` (Zeilen 29–66)

Record-Klassifikationswerte für den datensatzscharfen Scope-Filter; für `fm_securities_analysis` wird nur `SecurityCategoryId = s.CategoryId` gesetzt (`FmSecuritiesAnalysisTool.cs` Zeilen 111–113). Weitere Felder: `OwnerUserId`, `AccountId`, `ContactId`, `SavingsPlanId`, `SecurityId`, `StatementImportId`, `DraftId`, `AccountTypeValue`, `ContactCategoryId`, `SavingsPlanCategoryId`, `AttachmentCategoryId`.

## `McpAccessScope`
Verwendet in `McpRecordScopeFilter`: trägt die Filter-CSVs `AccountTypesCsv`, `ContactCategoryIdsCsv`, `SecurityCategoryIdsCsv` (die für `fm_securities_analysis` relevante Dimension), `SavingsPlanCategoryIdsCsv`, `AttachmentCategoryIdsCsv` (`McpRecordScopeFilter.cs` Zeilen 93–98, 160–165). Eine gesetzte CSV wirkt als Whitelist; nicht klassifizierte Datensätze (`CategoryId == null`) matchen nicht (`CsvMatchesGuid`, Zeilen 187–205).
