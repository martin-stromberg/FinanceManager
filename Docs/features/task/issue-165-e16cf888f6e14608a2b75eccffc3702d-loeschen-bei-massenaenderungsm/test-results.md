# Testergebnisse - Loeschen bei Massenaenderungsmodus

Ausgefuehrt am: 2026-07-26

## Status

Keine Fehler

## Ausgefuehrte Pruefungen

| Ergebnis | Kommando | Umfang |
|----------|----------|--------|
| Bestanden | `dotnet build FinanceManager.sln --no-restore` | Vollstaendiger Solution-Build |
| Bestanden | `dotnet test FinanceManager.Tests\FinanceManager.Tests.csproj --no-build --filter FullyQualifiedName~StatementDraft` | StatementDraft-bezogene Unit-, ViewModel-, Service- und API-Client-Tests |
| Bestanden | `dotnet test FinanceManager.Tests.Integration\FinanceManager.Tests.Integration.csproj --no-build --filter FullyQualifiedName~StatementDraft` | StatementDraft-bezogene Integrationstests |

## Zusammenfassung

- Build: 0 Fehler, 31 Warnungen.
- Unit-/ViewModel-/Service-/API-Client-Tests: 126 bestanden, 0 fehlgeschlagen, 0 uebersprungen.
- Integrationstests: 9 bestanden, 0 fehlgeschlagen, 0 uebersprungen.

## Fehlgeschlagene Tests

Keine.

## Hinweise

Der Build meldet bestehende Paket-/Analyzer-Warnungen, insbesondere bekannte NuGet-Sicherheitswarnungen fuer `SQLitePCLRaw.lib.e_sqlite3`, `System.Security.Cryptography.Xml` und `AngleSharp` sowie `NU1510`-Hinweise zu kuerzbaren PackageReferences in `FinanceManager.Web`. Diese Warnungen haben die Pruefung nicht fehlschlagen lassen.
