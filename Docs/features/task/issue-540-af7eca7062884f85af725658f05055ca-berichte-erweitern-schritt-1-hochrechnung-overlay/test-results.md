# Testergebnisse – Schritt 1

| Prüfung | Ergebnis |
|---|---|
| `dotnet restore` | Bestanden |
| `dotnet build FinanceManager.sln --no-restore` | Bestanden, 0 Fehler; nur bestehende Paket-/xUnit-Warnungen |
| `dotnet test FinanceManager.Tests/FinanceManager.Tests.csproj --filter "FullyQualifiedName~ReportSuppressedExpectationServiceTests\|FullyQualifiedName~ReportAggregationSuppressionTests\|FullyQualifiedName~ReportSuppressedExpectationBackupRestoreTests"` | 17/17 bestanden |
| `dotnet test FinanceManager.Tests.E2E/FinanceManager.Tests.E2E.csproj --filter "FullyQualifiedName~ReportSuppressedExpectationPlaywrightTests"` | 3/3 bestanden |
| `dotnet format FinanceManager.sln --verify-no-changes --no-restore --severity error` | Bestanden |
| `dotnet build FinanceManager.sln -c Debug --no-restore -p:TreatWarningsAsErrors=true -p:WarningsNotAsErrors=xUnit2000%3BxUnit2009%3BxUnit2012%3BxUnit2013%3BxUnit2029%3BxUnit2031%3BxUnit1031%3BNU1510%3BBL0005` | Bestanden |
| `dotnet list FinanceManager.sln package --vulnerable --include-transitive` | Keine verwundbaren Pakete |

## Abgedeckte Fälle

- Zwei Posten mit gleichem Konto, Datum und Betreff bleiben durch `PostingId` getrennt unterdrückbar.
- Eine Unterdrückung ohne Favoritenkontext oder in einem anderen Favoriten beeinflusst die Aggregation nicht.
- Eine Betragskorrektur ändert die Unterdrückungsidentität nicht.
- Backup/Restore mappt Favorite-, Entity- und Posting-IDs; Payloads ohne optionale Sektion bleiben lesbar.
- E2E: Unterdrücken, Anzeige im Hidden-Bereich inklusive Betrag, sofortiges Wiedererscheinen im geöffneten Overlay und Entity-Scope über zwei Konten.
