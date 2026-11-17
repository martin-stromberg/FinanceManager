# Testergebnisse — Issue #533

## Statische Prüfung

- Repo-Scan nach `EF BF BD`: **0 Vorkommen** in getrackten Textdateien (Resttreffer: 2 valide PNG-Binärdateien, ungetracktes `issue.md`, Feature-Doku mit `ufffd-allow`-Marker).
- `dotnet build FinanceManager.sln`: **0 Fehler** (61 Warnungen, alle präexistent: NU1510, xUnit-Analyzer).
- `git diff --check`: keine Whitespace-Probleme.

## Tests

- `dotnet test FinanceManager.Tests --filter "StatementDraft|SubtypeAggregation"`: **176/176 bestanden** (38 s).
- `dotnet test FinanceManager.Tests.Integration --filter "ApiClientStatementDraftsTests|ApiClientContactsTests|ApiClientBudgetKpiContactsSetupTests|ApiClientBudgetReportUnbudgetedMirrorTests|ApiClientBackupsWithDemoDataTests"`: **21/21 bestanden** (71 s) — inkl. der CSV-Upload-Tests, deren korrigierte Header (`Empfänger`, `Währung`, `Überweisung`) die echte ING-Parser-Pipeline durchlaufen.

## Check-Skript-Verifikation

- `no-replacement-char-check.py` staged-Modus: Testdatei mit U+FFFD → **exit 1** mit Fundmeldung (blockiert Commit).
- `no-replacement-char-check.py --all --strict`: 2039 getrackte Textdateien → **exit 0**.

## Reale End-to-End-Abnahme (UI)

Über die etablierte `scripts/screenshots/`-Pipeline (echte App, temporäre SQLite-DB, Registrierung mit Demodaten über UI, Login, Warten auf Seed-Task, Playwright-Screenshots):

- `verification/home.png` — Budget-Kachel zeigt `2234,17€` und `0€` (Issue-Befund: `2234,17�`/`0�`).
- `verification/reports.png` — Berichtsfavoriten zeigen `Konten · Jahr` u. a. mit `·`-Trenner (Issue-Befund: `Konten � Jahr`).

Alle Akzeptanzkriterien des Issues sind erfüllt.
