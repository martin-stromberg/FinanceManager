# Testergebnisse

## Help-relevante Tests

| Kommando | Ergebnis | Tests |
| --- | --- | ---: |
| `dotnet test FinanceManager.Tests/FinanceManager.Tests.csproj --filter "FullyQualifiedName~Help"` | Bestanden | 43/43 |
| `dotnet test FinanceManager.Tests.E2E/FinanceManager.Tests.E2E.csproj --filter "FullyQualifiedName~Help"` | Bestanden | 1/1 |

## Zusammenfassung

- Status: erfolgreich
- Erfolgreich: 44
- Fehlgeschlagen: 0
- Uebersprungen: 0
- Ausgefuehrt am: 2026-08-24

## Hinweise

Die Testlaeufe erzeugen bestehende Build-Warnungen (unter anderem NU1510 zu redundanten PackageReferences sowie verschiedene C#-, Razor- und XML-Dokumentationswarnungen). Es traten keine Testfehler auf.
