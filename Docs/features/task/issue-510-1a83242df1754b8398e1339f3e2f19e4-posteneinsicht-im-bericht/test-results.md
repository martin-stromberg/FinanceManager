# Testergebnisse: Posteneinsicht im Bericht

Status: Keine Fehler

## Ausgeführte Befehle

| Befehl | Exit-Code | Dauer | Ergebnis |
|---|---|---|---|
| `dotnet build FinanceManager.sln` | 0 | ~6 s | Build erfolgreich, 0 Fehler, 10 Warnungen (nur NU1510-Hinweise zu nicht gekürzten PackageReferences) |
| `dotnet test FinanceManager.Tests --no-build` | 0 | ~43 s | gesamt: 2243, fehlgeschlagen: 0, erfolgreich: 2243, übersprungen: 0 |
| `dotnet test FinanceManager.Tests.Integration --no-build` | 0 | ~7 m 18 s | gesamt: 172, fehlgeschlagen: 0, erfolgreich: 172, übersprungen: 0 |
| `dotnet test FinanceManager.Tests.E2E --no-build --filter "FullyQualifiedName~ReportPostingsOverlayPlaywrightTests"` | 0 | ~13 s | gesamt: 3, fehlgeschlagen: 0, erfolgreich: 3, übersprungen: 0 |

Abgedeckte E2E-Tests (`FinanceManager.Tests.E2E/Tests/Reports/ReportPostingsOverlayPlaywrightTests.cs`):

- `PostingsOverlay_EntityRowColumnsAndTotalRow_ShowCellPostings`
- `PostingsOverlay_LinkedSavingsPlanSource_TypeAndTotalRows`
- `PostingsOverlay_NettedDividendRow_ShowsGroupMembers`

## Fehlgeschlagene Tests

Keine.
