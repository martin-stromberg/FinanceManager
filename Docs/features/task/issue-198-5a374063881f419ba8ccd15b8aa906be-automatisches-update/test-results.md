# Testergebnisse

Datum: 2026-07-19

## Status

Keine Fehler

## Ausgefuehrte Pruefungen

| Kommando | Ergebnis |
| --- | --- |
| `/run-tests` | Ausgefuehrt durch die untenstehenden fokussierten Testbefehle |
| `dotnet restore FinanceManager.sln` | Erfolgreich, mit NuGet-Warnungen |
| `dotnet build FinanceManager.sln --no-restore` | Erfolgreich, 12 Warnungen, 0 Fehler |
| `dotnet test FinanceManager.Tests\FinanceManager.Tests.csproj --no-restore --filter "FullyQualifiedName~Updates\|FullyQualifiedName~SetupUpdateTab\|FullyQualifiedName~SetupCardViewModel"` | Erfolgreich, 48 bestanden, 0 fehlgeschlagen |
| `dotnet test FinanceManager.Tests.Integration\FinanceManager.Tests.Integration.csproj --no-restore --filter "FullyQualifiedName~UpdateControllerIntegrationTests"` | Erfolgreich, 3 bestanden, 0 fehlgeschlagen |
| `node --test scripts\generate-update-manifest.test.mjs scripts\resolve-release-version.test.mjs` | Erfolgreich, 23 bestanden, 0 fehlgeschlagen |

## Hinweise

- Restore, Build und Testlaeufe melden bestehende NuGet-Warnungen zu `SQLitePCLRaw.lib.e_sqlite3` sowie nicht gekuerzten PackageReferences in `FinanceManager.Web`.
- Der Build meldet zusaetzlich zwei bestehende `xUnit1051`-Warnungen in `FinanceManager.Tests.E2E\Tests\Import\CollectionAccountImportPlaywrightTests.cs`.
- Es wurden keine fehlgeschlagenen Tests gefunden.
