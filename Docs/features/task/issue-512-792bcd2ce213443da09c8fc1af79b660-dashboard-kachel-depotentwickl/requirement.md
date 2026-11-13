# Übersetzte Anforderung – Issue #512: Dashboard-Kachel „Depotentwicklung & Dividenden"

## Fachliche Zusammenfassung

Der Depot-Bericht (`PortfolioAnalysisReportPage`, Route `/portfolio/analysis-report`) wird um eine neue interaktive Kachel „Depotentwicklung Jahreswechsel" erweitert. Die Kachel zeigt pro Kalenderjahr ein kombiniertes Diagramm (Liniendiagramm: Depotwert zum 31.12.; Balkendiagramm: Netto-Dividenden) sowie eine Detailtabelle mit Depotwert, Veränderung zum Vorjahr (absolut und prozentual), Netto-Dividende und einer Ausschüttungsrendite, deren Basis der Anwender zwischen „Jahresanfangswert" und „Eingezahltem Kapital" (Yield on Cost) umschalten kann. Die Kachel integriert sich in den bestehenden Kachel-Mechanismus (`PortfolioTileId`, `PortfolioKpiConfiguration`) und wird – wie die übrigen Kacheln – serverseitig aus den Wertpapierbuchungen und Kursen des angemeldeten Benutzers berechnet.

## Fachlicher Scope

- **Verarbeitungseinheit:** Ein Kalenderjahr der Depot-Historie des angemeldeten Benutzers („Jahreswechsel"), aggregiert über alle wertpapierbezogenen `Posting`-Datensätze (`SecuritySubType` Buy/Sell/Dividend/Fee/Tax) und `SecurityPrice`-Kursen aller Wertpapiere des Benutzers. Pro Jahr entsteht eine Ergebniszeile aus: Depotwert zum Jahresende, Depotwert zum Jahresanfang (bzw. Startkapital im ersten Jahr), Netto-Dividende des Jahres, kumuliertem eingezahlten Kapital bis Jahresende.
- **Auslösende Ereignisse:**
  - Nutzeraktion: Öffnen des Depot-Berichts `/portfolio/analysis-report` (Kachel wird als Teil des zwischengespeicherten Gesamtberichts geladen).
  - Nutzeraktion innerhalb der Kachel: Umschalten des Dropdowns „Rendite-Basis" (aktualisiert die Spalten *Verhältnis (€/€)* und *Rendite (%)* clientseitig, ohne Neuladen der Seite) sowie Auswahl eines Zeitraums im Dropdown „Zeitraum-Filter" (Standard: „Gesamte Historie").
  - Systemereignis (indirekt): Ribbon-Aktion „Refresh" bzw. Cache-Ablauf invalidieren den Berichts-Cache; die Kacheldaten werden beim nächsten Laden neu berechnet.
- **Erwartete Wirkung:** Der Anwender sieht in der neuen Kachel die Entwicklung seines Depots über die Jahre (Linie, Tooltip je Jahreswechsel mit exaktem Wert), die pro Jahr vereinnahmten Netto-Dividenden (Balken) und eine Tabelle mit sieben Spalten (Jahreswechsel, Depotwert €, Veränderung €, Veränderung %, Netto-Dividende €, Verhältnis €/€, Rendite %). Die Rendite-Spalten passen sich in Echtzeit der gewählten Basis an; unter der Tabelle erläutert eine modusabhängige Fußnote die verwendete Berechnungsgrundlage. Die Kachel kann – wie die bestehenden – im Bearbeitungsmodus des Berichts ein-/ausgeblendet und in der Reihenfolge verschoben werden.
- **Umgang mit fehlenden Objekten:**
  - *Benutzer ohne Wertpapierbuchungen / leere Historie:* Kachel zeigt eine Leeranzeige (Muster wie `MiniBarChart.EmptyText` bzw. bestehende `portfolio-tile`-Leerzustände); es werden keine Jahr-Zeilen erzeugt.
  - *Fehlende Kurse zu einem Stichtag:* Depotwert je Position = gehaltene Stückzahl × letzter bekannter Kurs am oder vor dem Stichtag (`SecurityValuationHelper.LatestPriceOnOrBefore`); liegt gar kein Kurs vor, trägt die Position mit 0 zum Depotwert bei (bestehende Konvention von `GetPortfolioValueOnDate` in `PortfolioAnalysisReportService`).
  - *Kein Vorjahres-Depotwert (Startjahr):* Als Rendite-Basis im Modus „Jahresanfangswert" dient das initial eingezahlte Kapital (Anschaffungskosten) des Startjahres; als Bezugsgröße der Spalten *Veränderung (€/%)* dient ebenfalls das Anfangskapital.
  - *Fehlende Kachel-Konfiguration (`PortfolioKpiConfiguration`):* Die Server-Standardkonfiguration (`PortfolioAnalysisReportController.DefaultConfigurationDto`) muss die neue Kachel enthalten. Bei bereits gespeicherten Benutzerkonfigurationen ist die neue TileId nicht enthalten — der Umgang (nachträglich einblenden vs. erst nach Benutzeraktivierung sichtbar) ist unter „Offene Fragen" zu klären bzw. in der Planung festzulegen.
  - *Veraltete Cache-Einträge:* Durch die neue DTO-Sektion ist `CacheSchemaVersion` in `PortfolioAnalysisReportCacheService` zu erhöhen, damit alte Cache-Stände als Miss gelten und nicht als teilweise `null`-deserialisiertes DTO ausgeliefert werden.
- **Scope-Ausschlüsse:**
  - *Keine Änderung bestehender Kacheln/Berechnungen:* Die Sektionen `Structure`, `Performance`, `Cashflow`, `Risk` und ihre Berechnungslogik bleiben unverändert; die neue Kachel ist eine zusätzliche Sektion des Berichts.
  - *Keine Drill-down-/Posteneinsicht:* Ein Klick auf Diagramm- oder Tabellenwerte öffnet keine Postenliste (andere Anforderung, siehe Issue #510).
  - *Keine Positionsebene:* Die Werte sind ausschließlich depotaggregiert; eine Aufschlüsselung pro Wertpapier innerhalb der Kachel wird nicht gefordert.
  - *Kein Export* der Kacheldaten (Diagramm/Tabelle) und *keine Bearbeitungsfunktion* (reine Anzeige).
  - *Keine Berücksichtigung im Home-Dashboard (`HomeKpiGrid`):* Die Kachel betrifft nur den Depot-Bericht, nicht die Startseiten-KPIs.
  - *Persistierung der Filterwahl:* Das Issue verlangt keine Speicherung der zuletzt gewählten Rendite-Basis/Zeitraums; ohne abweichende Entscheidung bleiben die Dropdown-Einstellungen sitzungslokal (siehe Offene Fragen).

## Betroffene Klassen und Komponenten

- **DTOs / Shared:**
  - `FinanceManager.Shared/Dtos/Portfolio/PortfolioTileId.cs` — neues Enum-Mitglied für die Kachel (z. B. `DepotDevelopment`); anschließend in `EnumCoverageTests` (`FinanceManager.Tests/Common/EnumCoverageTests.cs`) als `AssertValues<PortfolioTileId>`-Eintrag ergänzen (aktuell noch nicht abgedeckt).
  - `FinanceManager.Shared/Dtos/Portfolio/PortfolioAnalysisReportDto.cs` — neues Record-Argument mit der Jahresserie (z. B. `PortfolioDepotDevelopmentDto` mit Liste von Jahreszeilen: Jahr, Depotwert Jahresanfang/-ende, Netto-Dividende, kumuliertes eingezahltes Kapital; optional `IsYtd`-Flag analog `PortfolioAnnualReturnPoint`).
  - Ggf. neue DTO-Datei `PortfolioDepotDevelopmentDto.cs` unter `FinanceManager.Shared/Dtos/Portfolio/`.
- **Logik / Services:**
  - `FinanceManager.Infrastructure/Portfolio/PortfolioAnalysisReportService.cs` — neue `BuildDepotDevelopment(...)`-Methode; Wiederverwendung von `GetPortfolioValueOnDate`/`SecurityValuationHelper` (Jahresendwerte), `_fifo.Calculate(...)` auf Transaktions-Teilmengen (kumuliertes eingezahltes Kapital je Stichtag; Muster: `BuildCashflow` mit `beforeYear`), Netto-Dividendenkonvention `Σ Dividend − Σ |Tax|` (wie `ReturnAnalysisService.BuildAnnualDividends`, `AnnualDividendPoint`).
  - `FinanceManager.Infrastructure/Portfolio/PortfolioAnalysisReportCacheService.cs` — `CacheSchemaVersion` erhöhen.
  - `FinanceManager.Web/Controllers/PortfolioAnalysisReportController.cs` — `DefaultConfigurationDto()` um die neue TileId erweitern.
- **Enums:** `PortfolioTileId` (neues Mitglied). Evtl. ein Client-Enum für die Rendite-Basis (nur UI-seitig, z. B. innerhalb der Karten-Komponente).
- **UI-Komponenten:**
  - Neue Karte `FinanceManager.Web/Components/Pages/Portfolio/PortfolioDepotDevelopmentCard.razor` (eingebettet in `PortfolioKpiCard`), mit zwei Dropdowns (Zeitraum, Rendite-Basis), kombiniertem Diagramm und Tabelle.
  - Neues gemeinsames Diagramm unter `FinanceManager.Web/Components/Shared/` für Linie+Balken-Kombination (bestehende Muster: `MiniBarChart.razor`, `BudgetLineChart.razor` — beide CSS/SVG-basiert, keine JS-Chartbibliothek im Projekt).
  - `FinanceManager.Web/Components/Pages/Portfolio/PortfolioAnalysisReportPage.razor` — Render-Verzweigung für die neue TileId und `GetTileLabel`-Switch.
  - `FinanceManager.Web/Resources/Pages.resx`, `Pages.de.resx`, `Pages.en.resx` — alle neuen Texte (Kacheltitel, Dropdown-Labels/-Optionen, Spaltenköpfe, Fußnoten, Leeranzeige, Aria-Labels) in de + en.
  - `FinanceManager.Web/wwwroot/css/` — Stil für die neue Kachel/das Diagramm (Farbkonventionen des Themes; dunkelblaue Linie, grüne Balken).
- **Tests:**
  - `FinanceManager.Tests/Portfolio/PortfolioAnalysisReportServiceTests.cs` — Jahreswerte, Netto-Dividenden, kumuliertes Kapital, Startjahr-Sonderfall.
  - `FinanceManager.Tests/Controllers/PortfolioAnalysisReportControllerTests.cs` — neue TileId in Standardkonfiguration/Validierung.
  - `FinanceManager.Tests/Web/ViewModels/Portfolio/PortfolioAnalysisReportPageViewModelTests.cs` — ggf. Konfigurations-/Editierverhalten.
  - `FinanceManager.Tests/Components/Portfolio/` — bUnit-Tests der neuen Karte (Umschalten der Rendite-Basis, Formatierungen, Fußnoten).
  - `FinanceManager.Tests/Common/EnumCoverageTests.cs` — Abdeckung `PortfolioTileId`.
  - `FinanceManager.Tests.E2E/Tests/Portfolio/PortfolioAnalysisReportE2ETests.cs` — ggf. Erweiterung um die neue Kachel.
- **Nicht betroffen:** Datenbankschema/Migration (keine neuen persistierten Felder; `PortfolioKpiConfiguration` speichert TileIds bereits als JSON-String, neue Enum-Werte sind kompatibel). Damit entfallen auch Backup-/Restore-Anpassungen — vorausgesetzt, es wird keine neue Persistenz für die Filterwahl eingeführt.

## Implementierungsansatz

- **Datenbeschaffung serverseitig:** Die Kachel nutzt den bestehenden, monatlich gecachten Berichtsendpunkt `GET api/portfolio/analysis-report`; keine neue API nötig. `PortfolioAnalysisReportService` lädt bereits alle Positionen (`LoadPositionsAsync`) inkl. Transaktionen und Kurse — die Jahresserie wird aus denselben `PositionSnapshot`-Daten berechnet:
  - Depotwert zum 31.12. (bzw. zum aktuellen Datum im laufenden Jahr): `GetPortfolioValueOnDate(positions, yearEnd)`; Jahresanfangswert = Depotwert am 31.12. des Vorjahres (Konvention: Startwert des Zeitraums, vgl. `ComputePeriodMetrics` mit `periodStart.AddDays(-1)`).
  - Netto-Dividende je Jahr: `Σ Amount` der `SecurityPostingSubType.Dividend`-Buchungen minus `Σ |Amount|` der `Tax`-Buchungen des Jahres (Projektkonvention aus `ReturnAnalysisService`).
  - Kumuliertes eingezahltes Kapital bis Jahresende: FIFO-`TotalCostBasis` (ggf. abzgl. `StandaloneFeeTotal`, Konvention wie `BuildStructure`) über die Transaktionen bis zum Stichtag — zu klären, ob stattdessen kumulierte Netto-Einzahlungen (`Σ |Buy| − Σ Sell` plus Gebühren) gemeint sind (siehe Offene Fragen).
  - Spalten *Verhältnis* und *Rendite* können clientseitig aus den gelieferten Jahreswerten abgeleitet werden (Modus 1: Dividende / Jahresanfangswert bzw. Startkapital; Modus 2: Dividende / kumuliertes Kapital), sodass das Umschalten ohne Server-Roundtrip funktioniert.
- **UI-Erweiterungspunkte:** Neues Mitglied in `PortfolioTileId`; Render-Block in `PortfolioAnalysisReportPage.razor`; Eintrag in `GetTileLabel`; Standardkonfiguration im Controller. Die Interaktivität (Dropdown-Umschaltung, `StateHasChanged`) ist reine Blazor-Server-Logik innerhalb der Karte — `@rendermode InteractiveServer` ist auf der Seite bereits gesetzt.
- **Diagramm:** Neue geteilte Komponente (z. B. kombiniertes SVG-Balken-/Liniendiagramm) im Stil von `BudgetLineChart` (SVG mit Achsen, Tooltips via `<title>`) bzw. `MiniBarChart`; Farbwerte über CSS-Variablen des Themes, um AC „Farbgestaltung passt zur Anwendung" zu erfüllen.
- **Formatierung:** Kulturabhängige Zahlenformate (`N0`/`N2` bzw. Prozent mit Vorzeichen und zwei Nachkommastellen) wie in den bestehenden Karten; Prozentwerte mit `+`-Vorzeichen.
- **Abhängigkeiten:** `IPortfolioAnalysisReportService`/`PortfolioAnalysisReportCacheService`, `ApiClient.Portfolio_GetAnalysisReportAsync`, `IFifoCostBasisCalculator`, `SecurityValuationHelper`, `PortfolioKpiConfiguration`-Repository, resx-Localizer `Pages`.

## Konfiguration

- **Kachel-Sichtbarkeit und -reihenfolge:** Nutzt die bestehende benutzerspezifische `PortfolioKpiConfiguration` (`ActiveTileIds`/`TileOrder` als JSON); die neue TileId wird in die Server-Standardkonfiguration (`DefaultConfigurationDto`) aufgenommen. Für Nutzer mit bereits gespeicherter Konfiguration ist festzulegen, ob die neue Kachel automatisch aktiv wird (z. B. durch Zusammenführen unbekannter TileIds in `TileOrder`) oder standardmäßig verborgen bleibt (siehe Offene Fragen).
- **Rendite-Basis & Zeitraum-Filter:** Vom Issue nicht als persistent gefordert — Vorschlag: sitzungslokaler UI-Status innerhalb der Karte (kein neues Persistenzfeld, kein Backup-Impact). Alternativ benutzerspezifisch in `PortfolioKpiConfiguration` persistierbar, falls gewünscht.
- **Keine Anwendungseinstellungen** erforderlich; keine neuen Einträge in `appsettings`.

## Offene Fragen

1. **„Eingezahltes Kapital" / YoC-Nenner:** Ist das kumulierte eingezahlte Kapital als FIFO-Anschaffungskostenbasis der gehaltenen Bestände (Projektkonvention `TotalCostBasis − StandaloneFeeTotal`, sinkt bei Verkäufen) oder als kumulierte Netto-Einzahlungen (`Σ Käufe − Σ Verkaufserlöse`, ggf. inkl. Gebühren) zu verstehen? Das Issue beschreibt nur „bei neuen Einzahlungen anwachsend".
2. **Depotwert-Definition:** Nur Wertpapier-Marktwert (Konvention der bisherigen Kacheln) oder inklusive Verrechnungskonto-Saldo (`LoadDepotCashBalanceAsync` existiert bereits für die Liquiditätsquote)?
3. **Laufendes Jahr:** Erscheint das aktuelle, unvollständige Jahr als eigene Zeile/Balken (ggf. mit `*`-Markierung wie `IsYtd` in der Performance-Kachel) oder nur abgeschlossene Kalenderjahre?
4. **Zeitraum-Filter-Optionen:** Welche Auswahlmöglichkeiten enthält das Dropdown neben „Gesamte Historie" (z. B. letzte 5/10 Jahre, fester Jahresbereich)? Im Issue sind keine konkreten Optionen benannt.
5. **Neue Kachel bei Bestandskonfigurationen:** Soll sie für Benutzer mit bereits gespeicherter `PortfolioKpiConfiguration` automatisch sichtbar werden (und an welcher Position), oder erst, nachdem sie der Benutzer im Bearbeitungsmodus aktiviert?
6. **Persistenz der Dropdown-Wahl:** Soll die zuletzt gewählte Rendite-Basis (und ggf. der Zeitraum) pro Benutzer gespeichert werden oder bei jedem Aufruf auf „Jahresanfangswert"/„Gesamte Historie" zurückfallen?
7. **„Verhältnis (€/€)":** Ist damit die Anzeige als Dezimalbruch (z. B. `0,0695`) oder als konkretes Wertepaar „Netto-Dividende / Basis" (z. B. `1.234 € / 17.760 €`) gemeint? Die Angabe „Mathematischer Bruch" ist mehrdeutig.
