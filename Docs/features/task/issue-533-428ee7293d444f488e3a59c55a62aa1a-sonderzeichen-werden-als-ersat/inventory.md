<!-- ufffd-allow: dokumentiert die Fundstellen legitimerweise mit Originalzeichen -->
# Bestandsaufnahme — U+FFFD-Vorkommen und Rekonstruktion

Scan des gesamten Repos (Bytes `EF BF BD`, ohne `.git`, `bin`, `obj`, node_modules): **40 Dateien / 177 Vorkommen**, davon 37 getrackte Textdateien / ~165 Vorkommen. `issue.md` ist ungetrackt, beide PNGs sind valide Binärdateien (Koinzidenz in Bilddaten).

## Rekonstruktionsregeln

Jedes `�` ersetzt genau ein CP1252-Byte, das als UTF-8 nicht dekodierbar war. Zuordnung aus Kontext und Repo-Konventionen:

### Wortinterne Umlaute/ß (substring-basiert, global)

| Korrupt | Korrekt | Beispiele |
|---|---|---|
| `B�ckerei` | `Bäckerei` | Testdaten (25×) |
| `Einzelh�ndler` | `Einzelhändler` | Testdaten |
| `Beh�rde` | `Behörde` | Testdaten |
| `mpf�nger` | `mpfänger` | `Empfänger`, `Testempfänger` (CSV-Header, Strings) |
| `W�hrung` | `Währung` | CSV-Header |
| `�berweisung` | `Überweisung` (vor `�ber`→`über` anwenden) | CSV-Buchungstext |
| `�ber` | `über` | CONTRIBUTING.md (`aktuell über`) |
| `R�ckstellung` | `Rückstellung` | Testdaten |
| `g�ltig` | `gültig` | `Allgemeingültige`, `Ungültiges` |
| `unzul�ssig` | `unzulässig` | CONTRIBUTING.md |
| `chl�ssel` | `chlüssel` | `Schlüssel`, `Lokalisierungsschlüssel` |
| `m�ssen` | `müssen` | CONTRIBUTING.md |
| `f�r`/`F�r` | `für`/`Für` | Kommentare, CONTRIBUTING.md, CSS |
| `�nderungen` | `Änderungen` | CONTRIBUTING.md |
| `ge�ndert` | `geändert` | CONTRIBUTING.md |
| `anschlie�end` | `anschließend` | CONTRIBUTING.md |
| `aufgel�st` | `aufgelöst` | CONTRIBUTING.md |
| `zur�ck` | `zurück` | CONTRIBUTING.md |
| `geh�ren` | `gehören` | CONTRIBUTING.md |
| `Pr�fe`/`Pr�fen` | `Prüfe`/`Prüfen` | CONTRIBUTING.md, pre-commit |
| `hinzugef�gt` | `hinzugefügt` | CONTRIBUTING.md |
| `verf�gbar` | `verfügbar` | pre-commit |
| `L�sungssuche` | `Lösungssuche` | pre-commit |
| `l�scht` | `löscht` | LoginStatus.razor |
| `gro�` | `groß` | StatementDraftImportSplitTests |
| `w�hlen` | `wählen` | StatementDraftImportSplitTests |
| `Verkn�pfte` | `Verknüpfte` | BankAccountCardViewModel |

### Symbolische Zeichen (kontextspezifisch)

| Stelle | Ersatz | Begründung |
|---|---|---|
| `MonthlyBudgetKpi.razor` (10×, z. B. `@(...)�`, `@ResultText�`) | `€` | Issue-Screenshot zeigt `2234,17�`; Beträge ohne Leerzeichen davor → `€` direkt angehängt. |
| `PostingsController.cs:249`, `PostingsQueryService.cs:43` — `Replace("�", …)` | `€` | TryParseAmount entfernt Leerzeichen + Währungssymbol; XML-Doc sagt explizit „the euro sign". |
| `ReportsHome.razor:38` fav-meta `…}"] � @Localizer[…]` | `·` (U+00B7) | Etablierter Meta-Trenner im Repo (`GoldenCrossWidget`, `TrendReversalWidget`, `McpApprovalsPanel`, `AccountsStatisticsTile`). |
| `BudgetReport.razor:104`, `:122` Datumsbereich `from � to` | `–` (U+2013) | En-Dash für Datumsbereiche, Konvention wie `GoldenCrossWidget` (`StartDate – EndDate`). |
| Prosa-Trenner in Kommentaren/`echo` (16×: `) � do`, `process � callers`, `found � skipping`, `(Booking+Valuta � Dividend+Tax)` u. a.) | `—` (U+2014) | Dominanter Satz-Trenner in Repo-Kommentaren. |

## Stiller Datenverlust (Stichprobe)

- `CONTRIBUTING.md` `ArgumentException ? Err_Invalid_{ParamName}`: `?` steht seit dem Erstcommit so — kein U+FFFD, kein Teil dieses Events; bleibt unverändert.
- Betroffene Zeilen wurden bereits korrupt committed (`git show` der Einführungscommits zeigt `�` in den `+`-Zeilen); es existiert kein sauberer Vor-Stand in der Historie. Rekonstruktion daher rein kontextuell.

## Prävention

- Neuer Check `.githooks/no-replacement-char-check.py` nach dem Muster der bestehenden `*-check.py`-Skripte: prüft Textdateien (Quellcode-Erweiterungen + `.md` + `.githooks`-Skripte) auf U+FFFD; Default = staged Dateien (harter Fehler — U+FFFD ist in Quelltexten nie legitim), `--all` + `--strict` für pre-push (Repo-weit, blockierend).
- Verdrahtung in `.githooks/pre-commit` und `.githooks/pre-push` analog zu den übrigen Checks.
- Allowlist: Marker `ufffd-ok` auf der Zeile für legitime Ausnahmen (z. B. diese Doku) + Datei-Allowlist im Skript.
