# Wiederverwendung, Lokalisierung und Tests

## Wiederverwendbare UI-Bausteine

| Datei | Nutzung |
|---|---|
| `FinanceManager.Web/Components/Shared/DonutChart.razor` | SVG-Tortendiagramm mit Legende, Prozentberechnung, Center-Value und ARIA-Label. Akzeptiert positive Slice-Werte. |
| `FinanceManager.Web/Components/Shared/ReportKpiTile.razor` | Bestehendes Muster für KPI-/Vergleichsdarstellung; enthält eigene Layout- und Formatierungslogik. |
| `FinanceManager.Web/Components/Shared/AggregateBarChart.razor` | Bestehendes responsives Chart-Muster mit Loading-Zustand und zugänglichen Labels. |
| `FinanceManager.Web/wwwroot/css/app.*` und `theme.*` | Bestehende Layout- und Theme-Regeln für KPI- und Chart-Komponenten. |

## Lokalisierung

Kontenressourcen existieren unter:

- `FinanceManager.Web/Resources/Components/Pages/Accounts.de.resx`
- `FinanceManager.Web/Resources/Components/Pages/Accounts.en.resx`
- `FinanceManager.Web/Resources/Pages.de.resx`
- `FinanceManager.Web/Resources/Pages.en.resx`

Neue Beschriftungen sollten über den bestehenden `IStringLocalizer<Pages>`- beziehungsweise Accounts-Ressourcenfluss eingebunden werden. Das betrifft Kachel-Titel, Summe, aktuelle Jahres-/Monatsveränderung, Diagrammtitel, leere Daten und ARIA-/Tooltip-Texte.

## Bestehende Tests

| Datei | Relevanz |
|---|---|
| `FinanceManager.Tests/ViewModels/AccountsViewModelTests.cs` | Tests für Account-Loading, Authentifizierung, Suche, Ribbon und lokalisierte Account-Typen. |
| `FinanceManager.Tests/Components/Shared/DonutChartTests_PercentCalculation.cs` | Testet Prozentberechnung, Nullwerte, All-zero-Slices und Legendendarstellung. |
| `FinanceManager.Tests/Components/GenericListPageTests_MobileFilters.cs` | Testet generische Listenfilter im mobilen Rendering. |
| `FinanceManager.Tests/Accounts/AccountServiceTests.cs` | Unit-Tests für Account-Service-Verhalten. |
| `FinanceManager.Tests.E2E/Tests/Accounts/*` | Bestehende E2E-Abdeckung des Account-Bereichs und seiner Navigation. |
| `FinanceManager.Tests.E2E/Helpers/ListPageGateway*` | Wiederverwendbare E2E-Hilfen für generische Listen. |

## Erwarteter Testbedarf

- Statistikberechnung mit mehreren Konten, Kontotypen und Bankkontakten.
- Konsistenz von Gesamtwert und beiden Gruppierungen bei negativen/Null-Salden.
- Jahres-/Monatsgrenzen einschließlich Zeitzone und gewähltem Buchungsdatum.
- API-Owner-Scope und vollständige Kontenmenge trotz Paging.
- bUnit-Rendering der Kachel, Diagrammlegenden, Lade-/Leerzustände und responsiver Struktur.
- E2E: `/list/accounts` öffnen und Summe, beide Veränderungen sowie beide Tortendiagramme neben der Tabelle sichtbar prüfen.
