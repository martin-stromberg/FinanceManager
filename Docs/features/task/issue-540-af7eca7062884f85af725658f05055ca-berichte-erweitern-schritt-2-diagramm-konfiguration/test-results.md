# Testergebnisse – Schritt 2

- `dotnet build FinanceManager.sln --no-restore`: erfolgreich, 0 Fehler.
- `dotnet test FinanceManager.Tests/FinanceManager.Tests.csproj --filter "FullyQualifiedName~ReportFavoriteServiceTests|FullyQualifiedName~ReportFavoriteBackupTests|FullyQualifiedName~EnumCoverageTests|FullyQualifiedName~ReportDashboardViewModelTests"`: 90 erfolgreich, 0 fehlgeschlagen.
- `dotnet format FinanceManager.sln --verify-no-changes --no-restore --severity error`: erfolgreich.
- EF-Migration `20261118192311_AddReportFavoriteChartSettings` wurde per `dotnet ef migrations add` erzeugt und fügt beide Spalten mit Standard `0` hinzu.
