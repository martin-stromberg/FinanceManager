# Code-Review — Issue #533

Status: **Keine Befunde** (3 Hinweise eines externen Reviews wurden geprüft; 2 umgesetzt, 1 als bewusstes Design bestätigt)

## Geprüfter Umfang

- 41 geänderte Dateien: 37 U+FFFD-Bereinigungen + `.githooks/pre-commit`, `.githooks/pre-push` (Verdrahtung) + `Docs/RELEASE_NOTES.md`, `CHANGELOG.md` (Einträge).
- Neue Dateien: `.githooks/no-replacement-char-check.py`, Verifikations-Screenshots.
- `git diff --check`: keine Whitespace-Probleme; alle Diff-Zeilen sind thematische Zeichen-Ersetzungen (Stichprobe vollständig über die komplette Diff-Liste verifiziert — keine Fremdänderungen).

## Review-Befunde und Auflösung

1. `git ls-files`/`git diff --cached` ohne `-C`/`--full-name` (Unterverzeichnis-Robustheit) → **umgesetzt**: `all_text_files` und `staged_files` übergeben nun `git -C <root>` und `ls-files --full-name`.
2. `inventory.md` beschrieb fälschlich Zeilen-Marker `ufffd-ok` → **korrigiert** auf Datei-Marker `ufffd-allow`.
3. Abdeckungslücken (dotfiles/extensionlose Dateien außerhalb `.githooks`, Worktree-statt-Index-Scan, Locale-Encoding bei Dateinamen) → **bewusst konsistent** mit den Schwester-Checks; U+FFFD in `.gitignore`-Artigen Dateien ist praktisch irrelevant, die Erweiterungsliste deckt alle in den Akzeptanzkriterien genannten Typen plus weitere Textformate ab.

## Explizit verifizierte kritische Ersetzungen

- `MonthlyBudgetKpi.razor`: 10× `€` direkt hinter Beträgen; `@ResultText€` parst sauber (Expression + Literal).
- `PostingsController.cs`/`PostingsQueryService.cs`: `.Replace("€", …)` — durch XML-Doc („the euro sign") bestätigt.
- `ReportsHome.razor`: `·` als fav-meta-Trenner — Repo-Konvention (GoldenCrossWidget, TrendReversalWidget, McpApprovalsPanel, AccountsStatisticsTile).
- `BudgetReport.razor`: `–` für Datumsbereiche — Konvention wie `StartDate – EndDate` in GoldenCrossWidget.
- `—` in 16 Kommentar-/Echo-Trennern — dominante Satz-Trenner-Konvention.
- CSV-Testdaten: `Empfänger`/`Währung`/`Überweisung` matchen die literalen Parser-Erwartungen in `ING_CSV_StatementFileParser.cs`; `Bäckerei`/`Einzelhändler`/`Behörde`/`Rückstellung` konsistent mit DemoDataService-Begriffen.
- CONTRIBUTING.md: 17+ Ersetzungen allesamt korrekte deutsche Wörter.
- Check-Skript: Marker-Prüfung auf Bytes vor UTF-8-Decode, strikte Dekodierung, `\ufffd`-Escape verhindert Selbstbefund, `.githooks`-Sonderregel für extensionlose Skripte, getestet in beiden Modi (staged → exit 1 bei Fund; `--all --strict` → 2039 Dateien sauber).
