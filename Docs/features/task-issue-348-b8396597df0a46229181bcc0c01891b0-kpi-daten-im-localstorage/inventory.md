# Bestandsaufnahme: KPI-Daten im LocalStorage

## Relevante Komponenten

| Datei | Bedeutung |
|-------|-----------|
| `FinanceManager.Web/Components/Pages/Home.razor` | Startseite, rendert `<HomeKpiGrid>`, `@rendermode InteractiveServer` |
| `FinanceManager.Web/Components/Shared/HomeKpiGrid.razor` | Lädt und rendert alle Home-KPI-Kacheln; ruft `Api.HomeKpis_ListAsync` |
| `FinanceManager.Web/Components/Shared/MonthlyBudgetKpi.razor` | Monatsbudget-Kachel, lädt `MonthlyBudgetKpiViewModel` |
| `FinanceManager.Web/Components/Shared/NumericKpi.razor` | Einfache Zahlen-KPIs (Kontakte, Wertpapiere, etc.) |
| `FinanceManager.Web/Components/Shared/AggregateBarChart.razor` | Balkendiagramm-KPIs (Konten, Sparpläne, Dividenden) |
| `FinanceManager.Web/ViewModels/Budget/MonthlyBudgetKpiViewModel.cs` | ViewModel für Monatsbudget, hält geladene Daten |
| `FinanceManager.Web/ViewModels/Common/AggregateBarChartViewModel.cs` | ViewModel für Balkendiagramme, hält `TimeSeriesPoint`-Liste |
| `FinanceManager.Web/Components/Pages/Setup/SetupProfileTab.razor` | UI für Profil-Einstellungen |
| `FinanceManager.Web/ViewModels/Setup/SetupProfileViewModel.cs` | ViewModel für Profil-Einstellungen |
| `FinanceManager.Web/Controllers/UserSettingsController.cs` | API für Profil-/Benutzereinstellungen |
| `FinanceManager.Web/Resources/Pages.resx` | Lokalisierungsressourcen für UI-Texte |

## Datenmodell

| Datei | Bedeutung |
|-------|-----------|
| `FinanceManager.Domain/Users/User.cs` | Benutzer-Entity; wird um `CacheKpisInLocalStorage` erweitert |
| `FinanceManager.Infrastructure/AppDbContext.cs` | EF Core `OnModelCreating` Konfiguration für `User` |
| `FinanceManager.Shared/Dtos/Users/UserProfileSettingsDto.cs` | DTO für Profil-Daten |
| `FinanceManager.Shared/Dtos/Users/UserProfileSettingsRequests.cs` | DTO für Profil-Update |
| `FinanceManager.Infrastructure/Migrations/*.cs` | Bestehende Migrations, es wird eine neue Migration nötig |

## API / Client

| Datei | Bedeutung |
|-------|-----------|
| `FinanceManager.Shared/IApiClient.cs` | `UserSettings_GetProfileAsync`, `HomeKpis_ListAsync`, `Budgets_GetMonthlyKpiAsync`, `Contacts_CountAsync`, `Securities_CountAsync`, `SavingsPlans_CountAsync`, `StatementDrafts_GetOpenCountAsync` |
| `FinanceManager.Shared/Dtos/HomeKpi/HomeKpiDto.cs` | DTO für KPI-Liste |
| `FinanceManager.Shared/Dtos/Budget/MonthlyBudgetKpiDto.cs` | DTO für Monatsbudget-KPI |

## Testlandschaft

| Datei | Bedeutung |
|-------|-----------|
| `FinanceManager.Tests/Components/HomeKpiGridTests.cs` | bUnit-Tests für `<HomeKpiGrid>` |
| `FinanceManager.Tests/Components/MonthlyBudgetKpiTests.cs` | bUnit-Tests für `<MonthlyBudgetKpi>` |
| `FinanceManager.Tests.E2E/Tests/ProfileSettings/ProfileSettingsLanguageTests.cs` | Playwright-E2E-Tests für Profil-Einstellungen |

## Fehlende Bausteine

- Keine bestehende `IJSRuntime`-basierte LocalStorage-Abstraktion im Projekt.
- Kein User-Profil-Flag für LocalStorage-Caching.
- Keine Tests für LocalStorage-KPI-Caching.
