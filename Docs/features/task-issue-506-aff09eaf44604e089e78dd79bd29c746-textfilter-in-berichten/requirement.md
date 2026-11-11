# Übersetzte Anforderung

## Fachliche Zusammenfassung

Im Filterdialog des Berichts-Dashboards (`ReportDashboard`) soll für jede aktivierte Buchungsart (`PostingKind`: `Bank`, `Contact`, `SavingsPlan`, `Security`) unterhalb der Auswahlliste die Pflege beliebig vieler Textfilter möglich sein. Sind für eine Buchungsart Textfilter gesetzt, werden die ausgewerteten `Posting`-Datensätze dieser Buchungsart auf `Subject` (Verwendungszweck) und `Description` (Buchungsbeschreibung) gefiltert, sodass z. B. nur Zinsbuchungen („Zins / Dividende", „Abschluss") in den Bericht einfließen. Die Textfilter sind Teil der Filterkonfiguration eines `ReportFavorite` und sollen sich damit automatisch auch auf die KPI-Kacheln der Startseite (`HomeKpi` mit `ReportFavoriteId`, gerendert über `HomeKpiGrid`) auswirken.

## Betroffene Klassen und Komponenten

### Datenmodellklassen

- `FinanceManager.Domain/Reports/ReportFavorite.cs` — neue persistierte Filterfelder für Textfilter je `PostingKind`; Erweiterung von `SetFilters(...)`/`GetFilters()`. Gemäß AGENT.md ist `ReportFavoriteBackupDto` um die neuen Felder mit Default-Werten zu erweitern sowie `ToBackupDto()`/`AssignBackupDto()` anzupassen, damit alte Backups ladbar bleiben.
- `FinanceManager.Domain/Postings/Posting.cs` — Datenquelle der Textfelder `Subject` (Verwendungszweck) und `Description` (Buchungsbeschreibung); unverändert.
- `FinanceManager.Domain/Postings/PostingAggregate.cs` — vorberechnete Aggregatzeilen (Kind, Periode, Entität, `SecuritySubType`, `DateKind`); enthält **keine** Textfelder und kann daher nicht direkt textgefiltert werden (siehe Implementierungsansatz).
- EF-Migration für die neuen `ReportFavorite`-Spalten, ausschließlich per `dotnet ef migrations add` (AGENT.md); Entity-Konfiguration in `FinanceManager.Infrastructure/AppDbContext.cs` (~Zeile 699).

### DTOs / Interfaces

- `FinanceManager.Shared/Dtos/Reports/ReportAggregatesFiltersRequest.cs` — Erweiterung um Textfilterlisten je `PostingKind` (z. B. `BankTextFilters`, `ContactTextFilters`, `SavingsPlanTextFilters`, `SecurityTextFilters`; konkrete Form in Planung).
- `FinanceManager.Shared/Dtos/Reports/ReportAggregationFilters.cs` — Transport der Textfilter in die Aggregationslogik.
- `FinanceManager.Shared/Dtos/Reports/ReportFavoriteFiltersDto.cs` — Persistenz-DTO der Favoriten-Filter.
- `FinanceManager.Shared/Dtos/Reports/ReportFavoriteCreateApiRequest.cs`, `ReportFavoriteUpdateApiRequest.cs` sowie `ReportFavoriteCreateRequest`/`ReportFavoriteUpdateRequest` — Filter werden über das verschachtelte `Filters`-Objekt transportiert.
- Wiederverwendbare Vorbild-Implementierung: `FinanceManager.Shared/Dtos/Budget/BudgetRulePatternMatcher.cs` — `MatchesPosting(subject, description, pattern, useRegex)` matcht den kombinierten Text aus `Subject`+`Description`, Substring `OrdinalIgnoreCase` oder Regex (mit Timeout), Längenlimit 500 Zeichen (Konvention aus `BudgetRule`/`BudgetRuleCreateRequest`).

### Logikklassen / Services

- `FinanceManager.Infrastructure/Reports/ReportAggregationService.cs` — `QueryAsync` wendet die bestehenden Entity-/Kategorie-/Subtyp-Filter an (~Zeilen 282–362); hier ist die Textfilterung der Postings je `PostingKind` anzubinden.
- `FinanceManager.Infrastructure/Reports/ReportFavoriteService.cs` — `ToDtoFilters`, `ApplyFilters`, `ParseCsv`-Hilfsfunktionen sowie die anonymen Projektionen in `ListAsync`/`GetAsync` (neue CSV-/Textfelder ergänzen).
- `FinanceManager.Web/Controllers/ReportsController.cs` — `QueryAsync` (Mapping `ReportAggregatesFiltersRequest` → `ReportAggregationFilters`, ~Zeile 101–106) sowie `CreateFavoriteAsync`/`UpdateFavoriteAsync` (Mapping `Filters`-DTO).
- `FinanceManager.Web/Mcp/Tools/FmReportingAggregateTool.cs` — `FmReportingAggregateHandler` baut `ReportAggregationFilters` aus den Favoriten-Filtern; neue Filterdimension dort ebenfalls durchreichen.
- `FinanceManager.Infrastructure/Backups/BackupService.cs` / `SetupImportService` — `ReportFavorites`-Export/-Import über `ToBackupDto`/`AssignBackupDto` (AGENT.md: Backup/Restore-Tests belegen).

### UI-Komponenten

- `FinanceManager.Web/Components/Pages/ReportDashboard.razor` — Filterdialog (~Zeilen 408–468): unterhalb der Optionsliste je aktivem Buchungsarten-Tab ein Textfilter-Editor (beliebig viele Einträge); zudem Favoriten-Übernahme beim Laden (`fav.Filters` → Selected-Sets, ~Zeilen 1026–1040) und `BuildFilters()` (~Zeile 1058 ff.).
- `FinanceManager.Web/ViewModels/Reports/ReportDashboardViewModel.cs` — neue Temp-/Selected-Textfilter-Sammlungen je `PostingKind`; Anpassung von `BuildFiltersPayload`, `OpenFilterDialog`, `ApplyTempAndReloadAsync`, `ClearTempFilters`, `ClearFilters`, `GetSelectedTempFiltersCount`, `GetSelectedFiltersCount` sowie `SaveFavoriteAsync`/`UpdateFavoriteAsync` (Mapping in `ReportFavoriteFiltersDto`).
- `FinanceManager.Web/Components/Shared/HomeKpiGrid.razor` — baut den KPI-Request bereits aus `fav.Filters` (~Zeilen 590–608), Textfilter fließen nach DTO-Erweiterung automatisch; **aber** der LocalStorage-KPI-Cache (`IKpiLocalStorageCache`, Schlüssel `home.kpi.favorite-{k.Id}`, Prefix `fm.kpi.`) liefert ohne Gegenmaßnahme veraltete Kacheldaten, wenn sich die Favoriten-Filter ändern — hier ist die geforderte Sicherstellung anzubringen.
- `FinanceManager.Web/Resources/Components/Pages/ReportDashboard.de.resx` und `ReportDashboard.en.resx` — alle neuen UI-Texte (AGENT.md: resx de + en, keine hartkodierten Strings).

### Tests

- `FinanceManager.Tests/Reports/ReportAggregationServiceTests.cs`, `ReportAggregationServiceMultiKindTests.cs`, `ReportAggregation_ValutaBookingTests.cs` — Textfilter-Verhalten je Buchungsart, Mehrfach-Kinds, Valuta-Modus.
- `FinanceManager.Tests/ViewModels/ReportDashboardViewModelTests.cs` — Dialog-Temp-Zustand, Payload-Bau, Zähler.
- `FinanceManager.Tests/Components/HomeKpiGridTests.cs` — Filter-Propagation auf KPI-Kacheln.
- `FinanceManager.Tests/Infrastructure/*Backup*Tests.cs` — Backup/Restore der neuen `ReportFavorite`-Felder inkl. Alt-Backups (AGENT.md).
- `FinanceManager.Tests/EnumCoverageTests` — nur anzupassen, falls ein Enum-Mitglied ergänzt wird (aktuell nicht erwartet).

## Implementierungsansatz

- **Neue Filterdimension entlang der bestehenden Pipeline:** Textfilterlisten je `PostingKind` wandern vom Filterdialog (`Temp*`/`Selected*`-Sammlungen im `ReportDashboardViewModel`) über `ReportAggregatesFiltersRequest` → `ReportAggregationFilters` → `ReportAggregationService`; dieselben Listen werden am `ReportFavorite` persistiert (neue Felder + `SetFilters`/`GetFilters` + BackupDto). Speicherformat in der Planung festlegen — das bisherige CSV-Schema ist für freie Texte problematisch (Trennzeichen), naheliegend ist ein serialisiertes Format (z. B. JSON) oder ein Trennzeichen-/Escape-Schema.
- **Match-Mechanismus (Annahme):** Wiederverwendung der `BudgetRulePatternMatcher`-Konvention — Treffer, wenn der kombinierte Text aus `Posting.Subject` und `Posting.Description` das Muster enthält (`OrdinalIgnoreCase`, optional Regex). Verknüpfung mehrerer Filter einer Buchungsart vermutlich ODER (siehe Offene Fragen).
- **Aggregation über Rohpostings statt Aggregaten:** `PostingAggregates` führen keine Buchungstexte; für Buchungsarten mit gesetztem Textfilter muss `ReportAggregationService` die Basisdaten aus `_db.Postings` laden und in dasselbe Tupel-Schema wie `rawAnon` bringen (`Kind`, `PeriodStart` je `sourcePeriod`/`Interval`, `Amount`, `AccountId`/`ContactId`/`SavingsPlanId`/`SecurityId`, `SecuritySubType`). Präzedenzfall: `QuerySecurityDividendsNetAsync` liest ebenfalls direkt `_db.Postings`. Dabei sind zu beachten:
  - Datumsauswahl `BookingDate` vs. `ValutaDate` nach `query.UseValutaDate` (inkl. Fallback-Logik des Bestandscodes).
  - Besitzerfilterung: `Posting` hat kein `OwnerUserId` — die Ownership-Prüfung erfolgt im Bestand über die Besitzer-Dictionaries der referenzierten Entitäten (`accountNames`, `contactNames`, `savingsNames`, `securityNames`); der Rohpfad muss diese Kette ebenfalls anwenden. Die `OwnerUserId` stammt weiterhin aus `_current.UserId` (`ReportsController.QueryAsync`).
  - Parität zum Aggregat-Aufbau prüfen: Stornierungen (`ReversalForPostingId`/`ReversedByPostingId`), `OriginalAmount`, vorläufige Buchungen (`IsPreliminary`) — in der Planung klären, wie `PostingAggregate` diese Fälle berücksichtigt, damit der Textfilterpfad identische Summen liefert.
  - Interaktion mit `SavingsPlanPostingSource.LinkedAccountPostings` (Sparplan-Reihen aus verknüpften Bank-Buchungen) und mit `IncludeCategory`-Kategoriefiltern sowie `SecuritySubTypes`/`IncludeDividendRelated`.
- **KPI-Sicherstellung:** Da `HomeKpi` nur `ReportFavoriteId` referenziert und `HomeKpiGrid` den Request aus `fav.Filters` aufbaut, wirken gespeicherte Textfilter automatisch auf die Kacheln. Zusätzlich ist gegen den `IKpiLocalStorageCache` abzusichern, dass nach Filteränderung keine Alt-Daten angezeigt werden — Ansatz z. B. `ReportFavorite.ModifiedUtc` oder ein Filter-Hash im Cache-Schlüssel `home.kpi.favorite-{k.Id}` (konkrete Lösung in Planung).
- **Weitere Verbraucher:** Der MCP-Pfad (`FmReportingAggregateHandler`) nutzt dieselben Favoriten-Filter und erhält die Textfilter über die DTO-Erweiterung — Mapping anpassen, damit die Semantik konsistent bleibt.
- Keine neuen Enums erwartet; `@rendermode InteractiveServer` und Ribbon-Gruppierung sind unverändert betroffen (kein neuer Ribbon-Eintrag erforderlich — Erweiterung im bestehenden Filterdialog).

## Konfiguration

Die Textfilter sind **Datensatz-Konfiguration**: Sie gehören zu den Filter-Einstellungen des jeweiligen Berichts bzw. des gespeicherten `ReportFavorite` und sind benutzerbezogen (`OwnerUserId` am Favoriten; serverseitige Filterung nur auf eigene Postings). Eine globale/anwendungsweite Einstellung ist nicht erforderlich. Optional denkbar (Annahme): ein `UseRegex`-Flag je Textfilter analog `BudgetRule` — in der Planung zu entscheiden.

## Offene Fragen

1. **Verknüpfung mehrerer Textfilter:** Sollen die Filter einer Buchungsart ODER- oder UND-verknüpft werden? (Das Beispiel „Zins / Dividende" legt alternative Begriffe, also ODER, nahe — Annahme.)
2. **Match-Modus:** Substring-Suche (Konvention `OrdinalIgnoreCase` aus `BudgetRulePatternMatcher`) und/oder optionaler Regex-Modus wie bei `BudgetRule` (`UseRegex`, max. 500 Zeichen)? Soll Groß-/Kleinschreibung ignoriert werden?
3. **Match-Felder:** Genügt ein Treffer in `Subject` ODER `Description` (kombinierter Text, wie `MatchesPosting`), oder sollen beide Felder einzeln matchen müssen? Soll `RecipientName` (Empfänger) ebenfalls durchsucht werden? (Anforderung nennt nur Beschreibung und Verwendungszweck — Annahme: nein.)
4. **Interaktion mit Kategorie-Modus:** Bei aktivem `IncludeCategory` ersetzen Kategoriefilter die Entitätsfilter — gelten Textfilter dann zusätzlich zur Kategorieauswahl (kumulativ) je Buchungsart?
5. **`SavingsPlanPostingSource.LinkedAccountPostings`:** Wenn Sparplan-Reihen aus den Bank-Buchungen verknüpfter Konten abgeleitet werden — gilt der SavingsPlan-Textfilter dann gegen die Quell-Bankpostings oder nur gegen eigene Plan-Buchungen?
6. **Dividenden-Sonderpfad:** Sollen Security-Textfilter auch in `QuerySecurityDividendsNetAsync` (Pfad für `IncludeDividendRelated`/`CompareProjection` bzw. YTD+Dividend-Subtyp) greifen, obwohl dort gruppierte Netto-Dividenden gebildet werden?
7. **Obergrenzen:** Gibt es eine Höchstzahl bzw. Längenbegrenzung für Textfilter je Buchungsart (Performance der Rohposting-Auswertung, UI-Darstellung)? Orientierung an den 500-Zeichen-Konventionen der Budget-Regeln?
8. **Ad-hoc vs. Favorit:** Bestätigung, dass die Textfilter sowohl für die ad-hoc-Berichtsansicht (Filterdialog ohne gespeicherten Favoriten) als auch für gespeicherte Favoriten gelten — aus der Anforderung abgeleitet (KPI-Wirkung erfordert Persistenz am Favoriten).
