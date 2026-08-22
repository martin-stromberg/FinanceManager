# Testergebnisse: Vorläufige Buchungen für Sparkonten

## Zusammenfassung

| Test-Assembly | Gefilterte Tests | Erfolgreich | Fehlgeschlagen | Übersprungen | Dauer |
|---|---|---|---|---|---|
| `FinanceManager.Tests.dll` | `StatementDraftBookingTests` | 37 | 0 | 0 | ~33 s |
| `FinanceManager.Tests.E2E.dll` | `PreliminaryStatementDraftE2ETests` | 3 | 0 | 0 | ~6 s |

## Erfolgreiche Tests

### Unit-Tests

Alle 37 Tests in `StatementDraftBookingTests` bestanden:

- Erzeugung und Buchen vorläufiger Kontoauszüge
- Übertragung des `IsPreliminary`-Merkmals auf alle Posten
- Automatische Stornierung bei Buchung eines realen Auszugs
- Keine Stornierung bei Buchung eines weiteren vorläufigen Auszugs
- `OriginalAmount` bleibt erhalten, Betrag wird auf `0` gesetzt
- `IsReversed` wird korrekt gesetzt

### E2E-Tests

Alle 3 Tests in `PreliminaryStatementDraftE2ETests` bestanden:

- `CreatePreliminaryDraft_ViaRibbon_ShouldCreateAndOpenDraftWithQuickEdit`
- `BookPreliminaryDraft_ShouldCreatePreliminaryPostings`
- `BookRealStatement_ShouldReversePreliminaryPostings`

## Fehlgeschlagene Tests

Keine.

## Build-Status

- `dotnet build FinanceManager.Web/FinanceManager.Web.csproj`: 0 Fehler, 686 Warnungen (vorbestehende XML-Doku-/Package-Warnungen)
