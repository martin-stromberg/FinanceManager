# Testergebnisse

Ausgefuehrt am: 2026-07-20

## Testlauf

| Projekt | Ergebnis |
|---|---|
| `FinanceManager.Tests` | 864 bestanden, 0 Fehler, 0 uebersprungen |
| `FinanceManager.Tests.Integration` | 93 bestanden, 0 Fehler, 0 uebersprungen |
| `FinanceManager.Tests.E2E` | 23 bestanden, 0 Fehler, 0 uebersprungen |
| **Gesamt** | **980 bestanden, 0 Fehler, 0 uebersprungen** |

Befehl: `dotnet test FinanceManager.sln --configuration Release --no-restore`

## Fehlgeschlagene Tests

Status: **Keine Fehler**

Es sind keine Tests fehlgeschlagen.

## Warnungen

Der Build und Testlauf war erfolgreich, meldete jedoch bestehende Warnungen, unter anderem:

- Bekannte Paket-Sicherheitswarnungen fuer `AngleSharp` und `SQLitePCLRaw.lib.e_sqlite3` (`NU1902`, `NU1903`).
- Versionskonflikte zwischen `HtmlSanitizer` und `AngleSharp` (`NU1608`).
- Weitere bestehende Paket-, Compiler-, Razor- und Analyzer-Warnungen im Web- und Testprojekt.
