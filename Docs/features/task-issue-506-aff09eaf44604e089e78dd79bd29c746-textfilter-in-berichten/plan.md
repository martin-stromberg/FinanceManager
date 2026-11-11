# Umsetzungsplan: Textfilter in Berichten

## Übersicht

Im Filterdialog des `ReportDashboard` werden pro aktivierter Buchungsart (`PostingKind`) beliebig viele freie Textfilter pflegbar, die auf `Posting.Subject` (Verwendungszweck) und `Posting.Description` (Buchungsbeschreibung) wirken. Die Filterlisten werden als neue Dimension durch die bestehende Filter-Pipeline (`ReportAggregatesFiltersRequest` → `ReportAggregationFilters` → `ReportAggregationService`) transportiert und am `ReportFavorite` persistiert, wodurch sie automatisch auch für die KPI-Kacheln der Startseite (`HomeKpiGrid`) gelten; der `IKpiLocalStorageCache` wird dabei versionsbasiert gegen veraltete Kacheldaten abgesichert.

## Designentscheidungen

| Komponente / Bereich | Gewählter Ansatz | Begründung |
|----------------------|------------------|------------|
| Persistenzformat der Textfilter am `ReportFavorite` | Vier neue `string?`-Spalten `BankTextFiltersJson`, `ContactTextFiltersJson`, `SavingsPlanTextFiltersJson`, `SecurityTextFiltersJson`, je ein JSON-serialisiertes `string[]` (System.Text.Json) | Das bestehende CSV-Schema scheidet für Freitext aus (Kommas/Escaping); eine eigene Spalte je `PostingKind` folgt dem vorhandenen `*Csv`-Spaltenlayout; keine separate Tabelle nötig, da die Listen nur als Ganzes gelesen/geschrieben werden. |
| Transport-DTOs | In `ReportAggregatesFiltersRequest`, `ReportAggregationFilters` und `ReportFavoriteFiltersDto` jeweils vier neue Parameter `BankTextFilters`, `ContactTextFilters`, `SavingsPlanTextFilters`, `SecurityTextFilters` (`IReadOnlyCollection<string>?`, am Ende mit `= null`-Defaults) | Identische Feldstruktur auf allen drei Ebenen ist die etablierte Konvention; Defaults halten alle positionalen Konstruktoraufrufe (`ReportFavoriteService`, `ReportsController`, `DemoDataService`, `FmReportingAggregateTool`, Tests) und JSON-Deserialisierung kompilier- bzw. ladbar. |
| Match-Semantik | ODER-Verknüpfung der Einträge je Kind; ein Eintrag matcht, wenn er als Substring im kombinierten Text `Subject + " " + Description` vorkommt (`OrdinalIgnoreCase`), via Wiederverwendung von `BudgetRulePatternMatcher.MatchesPosting(subject, description, pattern, useRegex: false)` | Das Beispiel „Zins / Dividende" impliziert Alternativbegriffe (ODER); `MatchesPosting` implementiert exakt die geforderte Feldlogik und die `OrdinalIgnoreCase`-Konvention. `RecipientName` wird nicht durchsucht (Anforderung nennt nur die zwei Felder). |
| Match-Modus | Nur Substring-Suche, kein `UseRegex`-Flag je Eintrag | Die Anforderung spricht von „Textfiltern", nicht von Mustern; ein Regex-Flag würde UI, DTO-Struktur (Objekt statt string) und Persistenzformat verkomplizieren. Durch das JSON-Listenformat ist eine spätere Erweiterung ohne Schema-Bruch möglich. |
| Aggregation bei Textfilter | Kinds mit gesetzten Textfiltern werden aus `_db.Postings` verdichtet (In-Memory-Match, anschließend Gruppierung in dasselbe Tupel-Schema wie `rawAnon`); Kinds ohne Textfilter bleiben auf dem `PostingAggregates`-Pfad | `PostingAggregate` enthält keine Textfelder; Mischform hält den Performance-Verlust auf die tatsächlich gefilterten Kinds begrenzt. Präzedenzfall für `_db.Postings`-Zugriff: `QuerySecurityDividendsNetAsync`. |
| Parität Rohpfad ↔ Aggregat | Gleiche Regeln wie `PostingAggregateService`: `Amount == 0` überspringen; Effektivdatum `BookingDate` bzw. `ValutaDate` mit `ValutaDate == DateTime.MinValue → BookingDate`-Fallback; `PeriodStart`-Bucketierung identisch zu `GetPeriodStart` (als `internal` freigeben und wiederverwenden); Dimensions-Mapping je Kind (nur die kind-eigene FK + `SecuritySubType`); keine Sonderbehandlung von `IsPreliminary`, Stornos, `OriginalAmount` | `PostingAggregateService` wertet all diese Felder ebenfalls nicht aus — so liefert der Textfilterpfad bei gesetztem Filter „alle Texte" exakt die Aggregat-Summen. |
| Ownership im Rohpfad | Vor der Posting-Ladung die Owner-FK-IDs je textgefiltertem Kind laden (`Accounts`/`Contacts`/`SavingsPlans`/`Securities` mit `OwnerUserId == query.OwnerUserId`) und `_db.Postings` direkt auf diese IDs einschränken | `Posting` hat kein `OwnerUserId`; die Einschränkung auf SQL-Ebene begrenzt Speicher und setzt AGENT.md (serverseitige Besitzerfilterung) unmittelbar um; die bestehende Besitzer-Dictionary-Filterung bleibt als zweite Stufe erhalten. |
| `LinkedAccountPostings` + `SavingsPlanTextFilters` | Abgeleitete Sparplan-Reihen werden bei gesetztem SavingsPlan-Textfilter aus den Bank-**Postings** der verknüpften Konten gebildet und gegen `SavingsPlanTextFilters` gematcht; `BankTextFilters` greifen dabei nicht | Die emittierten Zeilen tragen `Kind = SavingsPlan`, also gilt der Filter der Ziel-Buchungsart; für nicht verknüpfte Pläne filtriert der Rohpfad ohnehin die eigenen Plan-Buchungen. |
| Dividenden-Sonderpfad | `SecurityTextFilters` werden auf die Anker-Buchung (Subtyp `Dividend`) je `GroupId` angewendet: matcht die Dividend-Buchung, bleibt die ganze Gruppe inkl. Fee/Tax-Netting erhalten | Der Filter soll Zahlungsereignisse selektieren; die Gruppenzugehörigkeit wird über den Dividenden-Anker bestimmt, sonst würden Fee/Tax-Zeilen mit eigenem Text die Gruppe verzerren. |
| Kategorie-Modus-Interaktion | Textfilter gelten kumulativ je Kind — unabhängig davon, ob `IncludeCategory` die Entitätsfilter durch Kategoriefilter ersetzt | Textfilter sind eine orthogonale Dimension auf Buchungsebene und keine Alternative zur Gruppierungs-/Allow-List-Ebene. |
| KPI-Cache-Absicherung | `FavKpiData` erhält `DateTime FavoriteModifiedUtc`; `RestoreFavoriteCacheAsync` lädt den Favoriten (`Reports_GetFavoriteAsync`) und übernimmt den Cache-Wert nur bei übereinstimmendem `fav.ModifiedUtc`; sonst `RemoveAsync` und `_favData`-Miss → `LoadFavoriteKpiDataAsync` lädt frisch | Ohne Gegenmaßnahme wird ein gecachter `_favData`-Eintrag nie durch den frischen API-Call ersetzt (Ladung startet nur bei Miss). `ModifiedUtc` ändert sich zuverlässig, da `SetFilters`/`Update`/`Rename` `Touch()` aufrufen; alte Cache-Einträge ohne das Feld deserialisieren mit `default(DateTime)` und werden automatisch einmalig verworfen. |
| Filterzähler / Clear | `GetSelectedTempFiltersCount`/`GetSelectedFiltersCount` zählen Textfilter der in `SelectedKinds` enthaltenen Kinds mit; `ClearTempFilters`/`ClearFilters` leeren die neuen Sammlungen | Steuert die Disabled-Zustände von `Button_ClearFilters` und Ribbon `FiltersClear` korrekt — keine Ribbon-Änderung nötig. |
| MCP-Tools-Parität | `fm_reporting_aggregate` (Inline-Argumente `bankTextFilters`/`contactTextFilters`/`savingsPlanTextFilters`/`securityTextFilters` als `string`-Arrays) und `fm_reports_configuration_manage` (`ParseFilters` um die vier Felder erweitert) unterstützen Textfilter vollständig; neuer Helper `McpToolArguments.OptionalStringList` | Ohne Erweiterung könnten MCP-Clients Textfilter weder ad-hoc setzen noch beim Schreiben von Favoriten pflegen; ein Filter-Update via `fm_reports_configuration_manage` würde bestehende Textfilter still leeren (`ApplyFilters` mit `null`-Listen). Geringer Aufwand, verhindert Datenverlust und deckt die Semantik der neuen Filterdimension vollständig ab. (Entscheidung aus Offenem Punkt 1 — empfohlener Vorschlag übernommen, da im SCM-Lauf keine Rückfrage möglich.) |

## Programmabläufe

### Textfilter im Filterdialog pflegen und anwenden (Ad-hoc)

1. `OpenFilterDialog` kopiert `Selected*TextFilters` → `Temp*TextFilters` (je `PostingKind`), setzt `ActiveFilterTabKind` und lädt `FilterOptionsByKind`.
2. Im Filterdialog rendert unterhalb der Optionsliste je aktivem Tab die neue Textfilter-Sektion: Liste der `GetTempTextFilters(kind)`-Einträge mit Entfernen-Button (`RemoveTempTextFilter`), Eingabefeld (`NewTextFilterInput`, `maxlength=500`) plus Hinzufügen-Button (`AddTempTextFilter` — Trim, leer/Duplikat `OrdinalIgnoreCase` ignorieren).
3. `ApplyTempAndReloadAsync` kopiert `Temp*TextFilters` → `Selected*TextFilters`, schließt den Dialog und ruft `ReloadAsync` → `BuildFiltersPayload` setzt `*TextFilters` im `ReportAggregatesFiltersRequest` (inkl. `hasText`-Prüfung, damit alleinige Textfilter nicht `null` liefern).
4. POST `api/report-aggregates` → `ReportsController.QueryAsync` mappt die neuen Felder auf `ReportAggregationFilters` → `ReportAggregationService.QueryAsync`.
5. `ClearTempFilters` (Dialog-Button) und `ClearFilters` (Ribbon `FiltersClear`) leeren die neuen Sammlungen; die Zähler steuern die Disabled-Zustände.

Beteiligte Klassen/Komponenten: `ReportDashboardViewModel`, `ReportDashboard.razor`, `ReportsController`, `ReportAggregationService`.

### Aggregation mit Textfilter (Standardpfad `QueryAsync`)

1. `kinds` auflösen (unverändert); Sonderpfad-Weichen (Security-Dividend) laufen vorab unverändert.
2. `textFiltersByKind` aus `query.Filters` aufbauen: `PostingKind.Bank → BankTextFilters` usw., nur Kinds ∈ `kinds`, nur nicht-leere getrimmte Patterns.
3. `PostingAggregates` nur für Kinds ohne Textfilter laden (`kinds ∖ textKinds`); die bisherige Early-Return-Bedingung gilt nur noch, wenn weder Aggregat- noch Rohzeilen entstehen.
4. Je textgefiltertem Kind: Owner-FK-IDs laden (`Accounts`/`Contacts`/`SavingsPlans`/`Securities` mit `OwnerUserId == query.OwnerUserId`), `_db.Postings` auf `p.Kind == kind` und die kind-eigene FK ∈ Owner-IDs einschränken, benötigte Felder projizieren (`BookingDate`, `ValutaDate`, `Amount`, FK-Ids, `SecuritySubType`, `Subject`, `Description`), im Speicher mit `BudgetRulePatternMatcher.MatchesPosting` (OR über die Einträge) filtern.
5. Treffer-Postings in Tupel-Zeilen verdichten: `Amount == 0` überspringen; Effektivdatum `query.UseValutaDate ? (ValutaDate == MinValue ? BookingDate : ValutaDate) : BookingDate`; `PeriodStart` via `PostingAggregateService.GetPeriodStart` (neu `internal`) je `sourcePeriod`; Gruppierung nach `(PeriodStart, kind-eigene FK, SecuritySubType)`; Zeilen in `rawAnon` einfügen (Kind-eigene FK gesetzt, übrige `null` — identisch zum Aggregat-Schema).
6. `LinkedAccountPostings`-Ableitung (siehe eigener Ablauf), anschließend unverändert: Besitzer-Dictionary-Filterung, Kategorie-Mappings, Top-Level-Allow-Listen (Z. 282–362), Punktaufbau, Vergleiche/Transform.

Beteiligte Klassen/Komponenten: `ReportAggregationService`, `BudgetRulePatternMatcher`, `PostingAggregateService` (`GetPeriodStart`).

### `SavingsPlanPostingSource.LinkedAccountPostings` mit SavingsPlan-Textfilter

1. `linksByPlan` laden (unverändert, `OwnerUserId`-gefiltert).
2. Wenn `SavingsPlanTextFilters` nicht-leer: Bank-Postings der `linkedAccountIds` laden (projiziert), `MatchesPosting` anwenden, nach `(PeriodStart, AccountId)` verdichten → ersetzt die bisherige `bankRows`-Aggregat-Abfrage; Ableitung `derived` je Plan unverändert.
3. Wenn keine `SavingsPlanTextFilters`: bestehender Aggregatpfad unverändert.

Beteiligte Klassen/Komponenten: `ReportAggregationService`.

### Dividenden-Sonderpfad (`QuerySecurityDividendsNetAsync`) mit Security-Textfilter

1. Tritt nur ein bei `Security`-only + (`IncludeDividendRelated`/`CompareProjection`/YTD+Dividend-Subtyp); bei nicht-leeren `SecurityTextFilters` die Dividend-Anker-Buchungen (`SecuritySubType == Dividend`, Owned-Securities, Datumsfenster) mit Textfeldern laden.
2. `MatchesPosting` über die `SecurityTextFilters` (OR) → `allowedGroupIds` (Menge der `GroupId` gematchter Dividend-Buchungen).
3. `groupNets` auf `allowedGroupIds` einschränken; Netting (Dividend+Fee+Tax je `GroupId`), Entity-/Kategoriefilter und Punktaufbau unverändert.

Beteiligte Klassen/Komponenten: `ReportAggregationService`, `BudgetRulePatternMatcher`.

### Favorit speichern, aktualisieren und laden (inkl. KPI-Wirkung)

1. `SubmitFavoriteDialogAsync` → `SaveFavoriteAsync`/`UpdateFavoriteAsync` (VM) mappt `filters.*TextFilters` → `ReportFavoriteFiltersDto` → `ReportsController.CreateFavoriteAsync`/`UpdateFavoriteAsync` → `ReportFavoriteService.ApplyFilters` → `ReportFavorite.SetFilters` (Trim/Dedupe/Validierung, JSON-Serialisierung, `Touch()` → `ModifiedUtc` aktualisiert).
2. `GetAsync`/`ListAsync` projizieren die vier neuen Spalten, rekonstruieren die Entity über `SetFilters` mit deserialisierten Listen und liefern sie via `ToDtoFilters` in `ReportFavoriteDto.Filters`.
3. `ApplyFavorite` (razor) überträgt `fav.Filters.*TextFilters` in die `Selected*TextFilters` des VM (Clear+Fill-Muster wie bisher); `BuildFilters()` reicht die Listen in den Favoriten-Payload weiter.

Beteiligte Klassen/Komponenten: `ReportDashboardViewModel`, `ReportDashboard.razor`, `ReportsController`, `ReportFavoriteService`, `ReportFavorite`.

### KPI-Kachel mit Favoriten-Textfilter und Cache-Invalidierung

1. `LoadFavoriteKpiDataAsync` baut den `ReportAggregatesFiltersRequest` um `fav.Filters.*TextFilters` erweitert und schreibt `FavKpiData` inklusive `fav.ModifiedUtc` (Speicher + `home.kpi.favorite-{k.Id}`-Cache).
2. `RestoreFavoriteCacheAsync` lädt je ReportFavorite-KPI den Favoriten via `Reports_GetFavoriteAsync` und prüft `cached.FavoriteModifiedUtc == fav.ModifiedUtc`: Treffer → `_favData[k.Id]` aus Cache; Mismatch/Favorit weg → Eintrag nicht übernehmen und `RemoveAsync` aufrufen.
3. Ohne `_favData`-Eintrag startet `LoadFavoriteKpiDataAsync` automatisch (bisheriges Miss-Verhalten) → aktualisierte Kacheldaten mit den neuen Filtern.

Beteiligte Klassen/Komponenten: `HomeKpiGrid`, `IKpiLocalStorageCache`, `IApiClient`.

### Backup und Restore

1. `BackupService.BuildBackupDataAsync` exportiert `ReportFavorites` über `ToBackupDto()` — die vier neuen `*TextFiltersJson`-Felder sind im `ReportFavoriteBackupDto` enthalten (keine neue Sektion, kein `RequiredDataProperties`-Eintrag).
2. `SetupImportService` deserialisiert `ReportFavoriteBackupDto` wie bisher über die `ReportFavorites`-Sektion; die neuen Parameter tragen `= null`-Defaults → Alt-Backups bleiben ladbar; `AssignBackupDto` übernimmt die Felder.

Beteiligte Klassen/Komponenten: `ReportFavorite`, `BackupService`, `SetupImportService`.

### MCP-Pfad (Favoriten)

1. `FmReportingAggregateHandler.InvokeAsync` (Favoriten-Pfad) mappt `favorite.Filters` → `ReportAggregationFilters` inklusive der vier `*TextFilters`-Felder → gespeicherte Textfilter wirken auch bei MCP-Ausführung des Favoriten.

Beteiligte Klassen/Komponenten: `FmReportingAggregateTool`.

## Neue Klassen

Keine. (Der Match erfolgt über die vorhandene `BudgetRulePatternMatcher`-Hilfsklasse; Textfilter sind schlichte `string`-Einträge — kein eigenes Wertobjekt nötig.)

## Änderungen an bestehenden Klassen

### `ReportFavorite` (Domain-Entity)

- **Neue Eigenschaften:** `BankTextFiltersJson`, `ContactTextFiltersJson`, `SavingsPlanTextFiltersJson`, `SecurityTextFiltersJson` (`string?`, private set) — JSON-serialisierte `string[]` der Textfilter je `PostingKind`.
- **Geänderte Methoden:**
  - `SetFilters(...)` — vier neue optionale Parameter `IEnumerable<string>? bankTextFilters = null, contactTextFilters = null, savingsPlanTextFilters = null, securityTextFilters = null` am Ende; serialisiert via neuem `ToJson`-Helper; Validierung je Eintrag (Trim, `ArgumentException` bei >500 Zeichen oder leerem Pattern nach Trim — siehe Validierungsregeln); `Touch()`.
  - `GetFilters()` — Rückgabetupel von 9 auf 13 Elemente erweitert (die vier `IReadOnlyCollection<string>?` ans Ende, deserialisiert via `FromJson`-Helper).
- **Backup:** `ReportFavoriteBackupDto` um `string? BankTextFiltersJson = null, ContactTextFiltersJson = null, SavingsPlanTextFiltersJson = null, SecurityTextFiltersJson = null` (Trailing-Defaults, AGENT.md); `ToBackupDto()`/`AssignBackupDto()` mappen die Felder 1:1.
- **Neue Hilfsmethoden:** private statische `ToJson`/`FromJson` (analog `ToCsv`/`FromCsv`, `System.Text.Json`).

### `ReportAggregatesFiltersRequest` (Shared-Record)

- **Neue Parameter:** `IReadOnlyCollection<string>? BankTextFilters = null, ContactTextFilters = null, SavingsPlanTextFilters = null, SecurityTextFilters = null` (Trailing-Defaults).

### `ReportAggregationFilters` (Shared-Record)

- **Neue Parameter:** dieselben vier Felder mit `= null` (alle Parameter des Records haben bereits Defaults).

### `ReportFavoriteFiltersDto` (Shared-Record)

- **Neue Parameter:** dieselben vier Felder mit `= null`.

### `ReportAggregationService` (Infrastructure-Service)

- **Geänderte Methoden:**
  - `QueryAsync` — Aufbau `textFiltersByKind`; Aggregat-Ladung auf nicht-textgefilterte Kinds beschränken; Early-Return nur bei insgesamt leerem `rawAnon`; neue private Hilfsmethode `LoadTextFilteredRowsAsync` liefert Tupel-Zeilen aus `_db.Postings` (Owner-eingeschränkt, `MatchesPosting`-OR-Match, `Amount==0`-Skip, Valuta-Fallback, `GetPeriodStart`-Bucketierung, Gruppierung); `LinkedAccountPostings`-Block wählt bei gesetzten `SavingsPlanTextFilters` den Postings-basierten `bankRows`-Pfad.
  - `QuerySecurityDividendsNetAsync` — bei nicht-leeren `SecurityTextFilters` Dividend-Anker-Buchungen in-memory matchen und `groupNets` auf die `GroupId`s der Treffer einschränken.
- **Neue Methoden:** `LoadTextFilteredRowsAsync(PostingKind, IReadOnlyCollection<string> patterns, AggregatePeriod sourcePeriod, bool useValutaDate, IReadOnlyCollection<Guid> ownerEntityIds, CancellationToken)` — private; liefert Tupel-Liste im `rawAnon`-Schema.

### `PostingAggregateService` (Infrastructure-Service)

- **Geänderte Methoden:** `GetPeriodStart` von `private static` auf `internal static` heben — Wiederverwendung durch `ReportAggregationService` garantiert identische Bucketierung.

### `ReportFavoriteService` (Infrastructure-Service)

- **Geänderte Methoden:**
  - `ListAsync`/`GetAsync` — anonyme Projektionen um die vier `*TextFiltersJson`-Spalten erweitern; `SetFilters`-Aufrufe beim Entity-Wiederaufbau um die deserialisierten Listen ergänzen (JSON-Parse-Helfer).
  - `ToDtoFilters` — `GetFilters()`-Destrukturierung auf 13 Elemente; `null`-Bedingung um „alle Textfilter-Listen leer" erweitern; neue Felder in `ReportFavoriteFiltersDto` mappen.
  - `ApplyFilters` — neue Felder an `SetFilters` weiterreichen (`null` leert weiterhin alle Filter inkl. Textfilter).
- **Neue Hilfsmethoden:** `ParseJsonList` (JSON-`string[]` → `IReadOnlyCollection<string>?`).

### `ReportsController` (Web-Controller)

- **Geänderte Methoden:** `QueryAsync` (Mapping `ReportAggregatesFiltersRequest` → `ReportAggregationFilters` um die vier Felder), `CreateFavoriteAsync`/`UpdateFavoriteAsync` (Mapping auf `ReportFavoriteFiltersDto` um die vier Felder).

### `FmReportingAggregateHandler` / `FmReportingAggregateTool` (MCP)

- **Geänderte Methoden:** `InvokeAsync` — Favoriten-Pfad: `ReportAggregationFilters`-Konstruktion um `f.*TextFilters` ergänzen.

### `ReportDashboardViewModel` (ViewModel)

- **Neue Eigenschaften:** `SelectedBankTextFilters`, `SelectedContactTextFilters`, `SelectedSavingsPlanTextFilters`, `SelectedSecurityTextFilters` (`List<string>`); `TempBankTextFilters`, `TempContactTextFilters`, `TempSavingsPlanTextFilters`, `TempSecurityTextFilters` (`List<string>`); `NewTextFilterInput` (`string`, Eingabefeld-Binding des Dialogs).
- **Neue Methoden:** `GetTempTextFilters(PostingKind)`, `AddTempTextFilter(PostingKind, string?)`, `RemoveTempTextFilter(PostingKind, string)`, je eine `GetSelectedTextFilterList`/`GetTempTextFilterList`-Map intern.
- **Geänderte Methoden:**
  - `BuildFiltersPayload` — `*TextFilters` in beiden Zweigen (`IncludeCategory`/Entität) befüllen; `hasText`-Bedingung in die `null`-Early-Returns aufnehmen.
  - `OpenFilterDialog` — `Temp*TextFilters` aus `Selected*TextFilters` kopieren, `NewTextFilterInput` zurücksetzen.
  - `ApplyTempAndReloadAsync` — `Temp*TextFilters` → `Selected*TextFilters` übernehmen.
  - `ClearTempFilters`, `ClearFilters` — neue Sammlungen leeren.
  - `GetSelectedTempFiltersCount`, `GetSelectedFiltersCount` — Textfilter der in `SelectedKinds` enthaltenen Kinds mitzählen.
  - `SaveFavoriteAsync`, `UpdateFavoriteAsync` — `ReportFavoriteFiltersDto`-Konstruktion um die vier Felder erweitern.

### `ReportDashboard.razor` (Seite)

- **Filterdialog (Z. ~447 ff.):** neue Textfilter-Sektion je aktivem `PostingKind`-Tab unterhalb der Optionsliste (Muster wie `FilterGroup_SecurityTypes`-Block): Gruppentitel `FilterGroup_TextFilters`, Liste `GetTempTextFilters(kind)` mit Entfernen-Buttons, Eingabe `NewTextFilterInput` + Hinzufügen-Button (`AddTempTextFilter`).
- **`ApplyFavorite` (Z. ~1020):** `fav.Filters.*TextFilters` → `Selected*TextFilters` (Clear+Fill).
- **`BuildFilters` (Z. ~1058):** beide `ReportAggregatesFiltersRequest`-Zweige um die `Selected*TextFilters`-Listen erweitern.

### `HomeKpiGrid.razor` (Komponente)

- **`FavKpiData` (Record):** neuer Parameter `DateTime FavoriteModifiedUtc` — Versionsmarke des Favoriten (Cache-Invalidierung).
- **`RestoreFavoriteCacheAsync`:** je ReportFavorite-KPI `Reports_GetFavoriteAsync` aufrufen; Cache-Wert nur übernehmen, wenn `FavoriteModifiedUtc` mit `fav.ModifiedUtc` übereinstimmt; sonst `RemoveAsync` und Eintrag verwerfen.
- **`LoadFavoriteKpiDataAsync`:** `ReportAggregatesFiltersRequest` um `fav.Filters.*TextFilters` erweitern; `FavKpiData` mit `fav.ModifiedUtc` befüllen.

### Ressourcen (`ReportDashboard.de.resx`, `ReportDashboard.en.resx`)

- **Neue Schlüssel (de + en):** `FilterGroup_TextFilters` (Gruppentitel), `TextFilter_Placeholder` (Eingabefeld), `TextFilter_Add` (Hinzufügen-Button), `TextFilter_Remove` (Entfernen-Button/Aria), `TextFilter_Hint` (Hinweistext: wirkt auf Verwendungszweck und Buchungsbeschreibung).

### `AppDbContext` (Infrastructure)

- Keine explizite Property-Konfiguration nötig (wie die `*Csv`-Spalten: Standard-String-Mapping); die neuen Spalten entstehen über die EF-Migration.

## Datenbankmigrationen

| Migrationsname | Betroffene Tabellen/Spalten | Beschreibung der Änderung |
|----------------|----------------------------|---------------------------|
| `AddReportFavoriteTextFilters` | `ReportFavorites`: `BankTextFiltersJson`, `ContactTextFiltersJson`, `SavingsPlanTextFiltersJson`, `SecurityTextFiltersJson` (nullable `nvarchar`) | Vier neue Nullable-String-Spalten für JSON-serialisierte Textfilterlisten je `PostingKind`; Erzeugung ausschließlich per `dotnet ef migrations add AddReportFavoriteTextFilters` (AGENT.md). |

## Validierungsregeln

| Feld / Objekt | Regel | Fehlerfall |
|---------------|-------|------------|
| Textfilter-Eintrag (`string`) | Getrimmt nicht leer; Länge ≤ 500 Zeichen (Konvention `BudgetRule.PurposePattern`) | Leerer/Whitespace-Eintrag → wird verworfen (kein Fehler); >500 Zeichen → `ArgumentException` in `SetFilters` → `400 BadRequest` via `ApiErrorFactory.FromArgumentException` in `CreateFavoriteAsync`/`UpdateFavoriteAsync`/`QueryAsync` |
| Textfilter-Eintrag (UI) | `maxlength=500` auf dem Eingabefeld; Hinzufügen-Button deaktiviert bei leerem Input | Verhindert Client-seitig invalide Eingaben |
| Duplikate je Kind-Liste | `OrdinalIgnoreCase`-Dedupe beim Setzen/Übernehmen | Duplikat wird ignoriert |
| `null`/leere Liste | Keine Filterung für diese Buchungsart (Allow-List-Konvention der bestehenden Filter) | — |

## Konfigurationsänderungen

Keine. Die Textfilter sind Datensatz-Konfiguration am `ReportFavorite` (benutzerbezogen), keine anwendungsweite Einstellung.

## Seiteneffekte und Risiken

- **Performance Standardpfad:** Unverändert für Abfragen ohne Textfilter. Bei gesetztem Textfilter werden die projizierten Postings des betroffenen Kinds (Owner-eingeschränkt) in den Speicher geladen — proportional zur Buchungsanzahl des Users, akzeptabel und nur bei aktivem Filter.
- **Parität Rohpfad ↔ Aggregat:** Die Summen eines „Alles-matcht"-Textfilters entsprechen den Aggregat-Summen, da dieselben Regeln gelten (`Amount==0`-Skip, Datums-/Periodenlogik, Dim-Mapping, keine Storno-/Preliminary-Sonderbehandlung). Zu beachten bei der Implementierung: `PostingAggregate`-Zeilen können Null-Summen durch +/-Aufhebung enthalten (`Add(delta)` ignoriert nur Delta 0) — der Rohpfad bildet Gruppensummen analog ohne Zusatzfilter.
- **Filterdialog-Edge:** Steht für kein `SelectedKinds`-Kind eine Option bereit (`FilterOptionsByKind` leer), zeigt der Dialog weiterhin `NoData` und der Editor erscheint nicht — ohne Entitäten existieren aber auch keine filterbaren Buchungen; Verhalten bleibt konsistent.
- **`fm_reports_configuration_manage` (MCP-Write-Pfad):** `ParseFilters` baut `ReportFavoriteFiltersDto` ohne die neuen Felder → ein Filter-Update via MCP leert vorhandene Textfilter still mit (siehe Offene Punkte).
- **`FmReportingAggregateTool` Inline-Pfad:** Ad-hoc-MCP-Abfragen ohne Favorit unterstützen keine Textfilter-Argumente (siehe Offene Punkte).
- **KPI-Cache:** `FavKpiData`-Schema ändert sich → vorhandene LocalStorage-Einträge werden einmalig verworfen (fehlende `FavoriteModifiedUtc` → Mismatch → `RemoveAsync`); jede Favoriten-Änderung (auch `Rename`) invalidiert die Kachel-Anzeige — bewusst konservativ.
- **`DemoDataService`:** erzeugt Demo-Favoriten ohne Textfilter — kompiliert durch Defaults unverändert, keine Anpassung nötig.
- **`EnumCoverageTests`:** keine neuen Enum-Member → keine Anpassung.
- **Backup-Kompatibilität:** neue Felder liegen innerhalb der bestehenden `ReportFavorites`-Sektion; `RequiredDataProperties` bleibt unverändert; Alt-Backups ohne die Felder deserialisieren über die `= null`-Defaults.

## Umsetzungsreihenfolge

1. **Shared-DTOs erweitern** (`ReportAggregatesFiltersRequest`, `ReportAggregationFilters`, `ReportFavoriteFiltersDto`)
   - Voraussetzungen: Keine (Trailing-Defaults halten alle Aufrufer kompilierbar).
   - Beschreibung: Vier `IReadOnlyCollection<string>?`-Parameter (`BankTextFilters`, `ContactTextFilters`, `SavingsPlanTextFilters`, `SecurityTextFilters`) je Record ergänzen.

2. **`ReportFavorite`-Entity und Backup-DTO erweitern**
   - Voraussetzungen: Keine.
   - Beschreibung: `*TextFiltersJson`-Properties, `SetFilters`/`GetFilters` erweitern (inkl. Validierung + `ToJson`/`FromJson`), `ReportFavoriteBackupDto` + `ToBackupDto`/`AssignBackupDto` mit Defaults.

3. **EF-Migration erzeugen**
   - Voraussetzungen: Schritt 2 (neue Entity-Properties); `dotnet ef`-Tooling laut AGENT.md (Developer PowerShell).
   - Beschreibung: `dotnet ef migrations add AddReportFavoriteTextFilters`; generierte Migration prüfen (vier Nullable-String-Spalten).

4. **`ReportFavoriteService` + `ReportsController` anpassen**
   - Voraussetzungen: Schritte 1–2.
   - Beschreibung: Projektionen, `ToDtoFilters`, `ApplyFilters`, `ParseJsonList`; Controller-Mappings für Query/Create/Update.

5. **`ReportAggregationService`-Rohpfad implementieren**
   - Voraussetzungen: Schritt 1 (DTO-Felder); `BudgetRulePatternMatcher` (vorhanden); `PostingAggregateService.GetPeriodStart` auf `internal` heben.
   - Beschreibung: `textFiltersByKind`, eingeschränkte Aggregat-Ladung, `LoadTextFilteredRowsAsync`, Early-Return-Anpassung, `LinkedAccountPostings`-Interaktion.

6. **Dividenden-Sonderpfad `QuerySecurityDividendsNetAsync` erweitern**
   - Voraussetzungen: Schritt 5 (Match-/Lade-Muster etabliert).
   - Beschreibung: `SecurityTextFilters` auf Dividend-Anker-Buchungen anwenden, `groupNets` einschränken.

7. **`FmReportingAggregateHandler` Favoriten-Mapping erweitern**
   - Voraussetzungen: Schritt 1.
   - Beschreibung: `ReportAggregationFilters`-Konstruktion im Favoriten-Pfad um `*TextFilters` ergänzen.

7a. **MCP-Tools-Parität** (`fm_reporting_aggregate`, `fm_reports_configuration_manage`)
   - Voraussetzungen: Schritt 1.
   - Beschreibung: `fm_reporting_aggregate` um die Inline-Argumente `bankTextFilters`, `contactTextFilters`, `savingsPlanTextFilters`, `securityTextFilters` (string-Arrays im `JsonSchema`) erweitern und in `ReportAggregationFilters` durchreichen; `fm_reports_configuration_manage.ParseFilters` um die vier Felder ergänzen, damit Filter-Updates via MCP die Textfilter pflegen statt still zu leeren; neuen Helper `McpToolArguments.OptionalStringList` einführen.

8. **`ReportDashboardViewModel` erweitern**
   - Voraussetzungen: Schritt 1.
   - Beschreibung: Neue `Selected*`/`Temp*`-Listen + `NewTextFilterInput`, Add/Remove/Get-Methoden, Temp→Selected-Übernahme, Zähler, `BuildFiltersPayload`, Favoriten-Mapping.

9. **`ReportDashboard.razor` + resx erweitern**
   - Voraussetzungen: Schritt 8 (VM-API steht).
   - Beschreibung: Textfilter-Editor im Filterdialog, `ApplyFavorite`, `BuildFilters`, neue resx-Schlüssel de + en.

10. **`HomeKpiGrid` Request-Mapping + Cache-Versionierung**
    - Voraussetzungen: Schritt 1.
    - Beschreibung: `FavKpiData.FavoriteModifiedUtc`, `RestoreFavoriteCacheAsync`-Versionsprüfung, `LoadFavoriteKpiDataAsync`-Mapping.

11. **Unit-/Komponententests**
    - Voraussetzungen: Schritte 4–10.
    - Beschreibung: Neue Tests gemäß Tests-Abschnitt (Aggregation, Favoriten-Service, ViewModel, KPI-Grid, Backup).

12. **E2E-Tests**
    - Voraussetzungen: Schritte 9–10; vorhandene Playwright-Infrastruktur (`ReportDashboardGateway` ggf. erweitern).
    - Beschreibung: Happy-Path-Filterdialog, Favoriten-Roundtrip mit Textfilter, KPI-Kachel.

## Tests

### Neue Tests

| Test / Hilfsmethode | Testklasse | Was wird geprüft / bereitgestellt? |
|--------------------|------------|-------------------------------------|
| `QueryAsync_BankTextFilter_MatchesSubjectOrDescription` | `ReportAggregationTextFilterTests` (neu) | Bank-Postings werden gefiltert, wenn Pattern in `Subject` **oder** `Description` vorkommt; Nicht-Treffer fließen nicht ein |
| `QueryAsync_TextFilter_MultiplePatterns_AreOrLinked` | `ReportAggregationTextFilterTests` | ODER-Verknüpfung mehrerer Einträge je Kind |
| `QueryAsync_TextFilter_IsCaseInsensitive` | `ReportAggregationTextFilterTests` | `OrdinalIgnoreCase`-Substring-Match |
| `QueryAsync_TextFilter_PerKind_OthersUseAggregates` | `ReportAggregationTextFilterTests` | Multi-Kind: textgefiltertes Kind aus Postings, anderes Kind unverändert aus Aggregaten |
| `QueryAsync_TextFilter_RespectsOwnership` | `ReportAggregationTextFilterTests` | Postings fremder Owner fließen nicht ein (Besitzerkette über Entity-IDs) |
| `QueryAsync_TextFilter_SkipsZeroAmountPostings` | `ReportAggregationTextFilterTests` | Parität `Amount == 0`-Skip zu `PostingAggregateService` |
| `QueryAsync_TextFilter_UseValutaDate_UsesValutaWithFallback` | `ReportAggregationTextFilterTests` | Bucketierung nach `ValutaDate`, `MinValue`→`BookingDate`-Fallback |
| `QueryAsync_TextFilter_WithIncludeCategory_AppliesCumulatively` | `ReportAggregationTextFilterTests` | Textfilter + Kategoriefilter wirken gemeinsam |
| `QueryAsync_LinkedSource_SavingsPlanTextFilter_AppliesToLinkedBankPostings` | `ReportAggregationServiceSavingsPlanSourceTests` oder neue Klasse | `SavingsPlanTextFilters` matchen gegen Quell-Bankpostings bei `LinkedAccountPostings` |
| `QueryAsync_SecurityTextFilter_DividendPath_FiltersDividendGroups` | `ReportAggregationProjectionTests` oder neue Klasse | `QuerySecurityDividendsNetAsync` inkludiert nur Gruppen mit gematchter Dividend-Anker-Buchung |
| `CreateUpdateListGet_ShouldRoundtripTextFilters` | `ReportFavoriteServiceTests` | Persistenz-Roundtrip der vier Listen (Create/Update/List/Get) |
| `CreateAsync_ShouldRejectTextFilterLongerThan500` | `ReportFavoriteServiceTests` | `ArgumentException` bei >500 Zeichen |
| `UpdateAsync_NullFilters_ClearsTextFilters` | `ReportFavoriteServiceTests` | `Filters = null` leert auch Textfilter |
| `FilterDialog_TextFilters_TempApplyClearAndCounts` | `ReportDashboardViewModelTests` | Temp→Selected-Übernahme, `ClearTempFilters`/`ClearFilters`, Zähler |
| `BuildFiltersPayload_IncludesTextFilters` | `ReportDashboardViewModelTests` | Payload enthält `*TextFilters`; `null`-Rückgabe nur ohne jegliche Filter |
| `SaveFavoriteAsync_ForwardsTextFilters` | `ReportDashboardViewModelTests` | Mapping in `ReportFavoriteFiltersDto` beim Speichern/Aktualisieren |
| `ReportFavoriteKpi_ForwardsTextFiltersToAggregateRequest` | `HomeKpiGridTests` | Captured `ReportAggregatesQueryRequest.Filters.*TextFilters` aus Favoriten-DTO (Muster wie `...CompareProjectionToAggregateRequest`) |
| `FavoriteCache_StaleModifiedUtc_IsDiscardedAndRemoved` | `HomeKpiGridTests` | Gecachter `FavKpiData`-Eintrag mit abweichendem `FavoriteModifiedUtc` wird nicht übernommen und via `RemoveAsync` gelöscht; frische Daten werden geladen |
| `ReportFavorite_TextFilters_SurviveBackupRestore` | `BackupRestoreCoverageTests` | Export→Import bewahrt alle vier Listen (Muster `BudgetRule_PurposePattern_SurvivesBackupRestore`) |
| `ImportAsync_OldBackupWithoutTextFilterFields_StillWorks` | `BackupRestoreCoverageTests` | Alt-Backup ohne die neuen Felder deserialisiert (Defaults) und importiert fehlerfrei |

### Betroffene bestehende Tests

| Test / Testklasse | Grund der Anpassung |
|-------------------|---------------------|
| `ContactPostingsValutaDateTests`, `ReportAggregationProjectionTests`, `ReportAggregationServiceSecurityMixedSubTypesTests`, `ReportAggregationServiceTests`, `ReportAggregation_ValutaBookingTests`, `SecurityDividendNetYtdSimpleTests`, `SecurityDividendsYtdScenarioTests`, `McpMasterDataWriteToolsTests` | Konstruieren die Filter-DTOs positional — kompilieren durch Trailing-Defaults weiter; keine inhaltliche Anpassung nötig, beim Implementieren auf Compile-Fehler prüfen |
| `BackupServiceFullExportTests` | Assertiert die Property→Entity-Map; neue Felder laufen über den bestehenden `ReportFavorites`-Eintrag — voraussichtlich unverändert |
| `HomeKpiGridTests` | `FavKpiData`-Record erhält neuen Parameter; `RestoreFavoriteCacheAsync` ruft zusätzlich `Reports_GetFavoriteAsync` — Mocks/Setup ggf. erweitern |

### E2E-Tests (Pflicht)

| Szenario | Testdatei / Testklasse | Abgedecktes Akzeptanzkriterium |
|----------|------------------------|-------------------------------|
| Filterdialog: Textfilter je aktivem `PostingKind`-Tab hinzufügen, anwenden → Bericht zeigt nur Buchungen, deren `Subject`/`Description` matcht (Postings mit unterscheidbarem Suchtext per API seeden) | Neue Klasse `ReportTextFilterPlaywrightTests` in `FinanceManager.Tests.E2E/Tests/Reports` (oder Erweiterung `ReportingFlowPlaywrightTests`); `ReportDashboardGateway` um `OpenFilterDialogAsync`, `AddTextFilterAsync`, `ApplyFilterDialogAsync` erweitern | Textfilter im Filterdialog pflegbar und wirksam (Happy Path) |
| Favorit mit gesetztem Textfilter speichern → neu laden → Textfilter im Dialog sichtbar und Aggregation weiterhin gefiltert | `ReportingFlowPlaywrightTests` | Persistenz am `ReportFavorite` inkl. UI-Wiederherstellung |
| `HomeKpi`-Kachel mit Textfilter-Favorit auf der Startseite: zeigt gefilterte Summe; nach Filteränderung am Favoriten zeigt die Kachel trotz LocalStorage-Cache den aktualisierten Wert | `KpiLocalStorageE2ETests` bzw. neue Klasse unter `Tests/Home` | Automatische KPI-Wirkung + Cache-Invalidierung |

Welche bestehenden E2E-Tests müssen angepasst werden?

| Test / Testklasse | Grund der Anpassung |
|-------------------|---------------------|
| `KpiLocalStorageE2ETests` | Geändertes Cache-Verhalten (`FavKpiData`-Schema, Favoriten-Abruf beim Restore) — ggf. Erwartungen/Mocks anpassen |
| `ReportingFlowPlaywrightTests.SaveFavorite_ShouldPersistAndReload` | Favoriten-Payload unverändert abwärtskompatibel — voraussichtlich keine Anpassung, aber bei Schema-Änderungen des API-Requests zu prüfen |

## Offene Punkte

Keine.

<!-- Entscheidung: Der ursprüngliche offene Punkt (MCP-Parität für `fm_reporting_aggregate` und `fm_reports_configuration_manage`) wurde mit dem empfohlenen Vorschlag "Für Parität umsetzen" entschieden, da im SCM-Lauf keine Rückfrage an den Anwender möglich ist und ohne die Erweiterung Filter-Updates via MCP bestehende Textfilter still leeren würden. Siehe Designentscheidung "MCP-Tools-Parität". -->

