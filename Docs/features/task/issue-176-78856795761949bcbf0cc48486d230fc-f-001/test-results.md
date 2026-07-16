# Testergebnisse

Status: Keine Fehler

Ausgefuehrt am: 2026-07-16

## Befehle

- `dotnet test FinanceManager.Tests.Integration\FinanceManager.Tests.Integration.csproj --filter FullyQualifiedName~ApiClientUsersAdminTests --logger "console;verbosity=normal" --artifacts-path %TEMP%\fm-codex-artifacts-issue176`
- `dotnet test FinanceManager.Tests\FinanceManager.Tests.csproj --filter FullyQualifiedName~UserAdminServiceTests --logger "console;verbosity=normal" --artifacts-path %TEMP%\fm-codex-artifacts-issue176-unit`

## Ergebnis

- Integration: 4 Tests bestanden, 0 fehlgeschlagen.
- Unit: 9 Tests bestanden, 0 fehlgeschlagen.

## Fehlgeschlagene Tests

Keine.
