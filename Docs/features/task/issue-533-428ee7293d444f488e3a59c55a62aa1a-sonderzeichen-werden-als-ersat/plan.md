# Umsetzungsplan — Issue #533: U+FFFD-Bereinigung

## Akzeptanzszenarien

1. **Happy Path Bereinigung:** Repo-weiter Scan nach `EF BF BD` liefert in getrackten Textdateien (`.cs`, `.razor`, `.css`, `.md`, `.githooks/*`) keinen Treffer mehr; alle 37 betroffenen Dateien enthalten die rekonstruierten Zeichen (Umlaute, `€`, `–`, `·`, `—`).
2. **UI-Wirkung:** Budget-Kachel zeigt `€` hinter Beträgen; Berichtsübersicht zeigt `·` zwischen Postenart und Intervall; BudgetReport zeigt `–` im Datumsbereich.
3. **Tests grün:** `dotnet build` der Solution erfolgreich; betroffene Testprojekte (FinanceManager.Tests, FinanceManager.Tests.Integration.ApiClient) laufen ohne neue Fehler.
4. **Prävention:** `.githooks/no-replacement-char-check.py` schlägt beim Commiten einer Datei mit U+FFFD fehl (pre-commit, staged) und blockiert repo-weit im pre-push mit `--all --strict`; Marker `ufffd-allow` erlaubt legitime Ausnahmen.

## Umsetzungsschritte

1. Python-Fixskript (temporär, nicht committed): wendet die Wortmap aus `inventory.md` plus kontextspezifische Symbol-Ersetzungen auf die 37 Dateien an; assertiert anschließend 0 Restvorkommen pro Datei.
2. Neuer Check `.githooks/no-replacement-char-check.py` (Muster: `no-notimplemented-check.py`); Erweiterungs-Allowlist: `.cs .razor .cshtml .html .css .js .ts .resx .md .sh .py .json .xml .yml .yaml .svg .txt .csv` + extensionless Dateien in `.githooks/`; Skip bei nicht-UTF-8-dekodierbarem Inhalt (Binärdateien); Datei-Opt-out via Marker `ufffd-allow`.
3. Verdrahtung: `pre-commit` (nach den Python-Checks, harter Fehler) und `pre-push` (`--all --strict`).
4. Verifikation: Rescan, `dotnet build`, `dotnet test` der betroffenen Projekte.
5. Release Notes / CHANGELOG-Eintrag, Commit, Push, PR gegen `staging` mit `Fixes #533`.

## Offene Punkte

- Keine.

## Scope-Klärung

- **Ausgeschlossen:** `issue.md` (ungetrackt), PNG-Binärdateien (valide), bereits bei Erstcommit falsche Zeichen ohne U+FFFD-Marker (`?` in CONTRIBUTING.md — dokumentierte Stichprobe). Bestätigung nicht erforderlich: entspricht exakt den Akzeptanzkriterien des Issues.
