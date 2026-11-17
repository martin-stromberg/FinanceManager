<!-- ufffd-allow: dokumentiert den Bug legitimerweise mit Originalzeichen -->
# Anforderung — Issue #533: Sonderzeichen werden als Ersatzzeichen (U+FFFD) dargestellt

Quelle: https://github.com/martin-stromberg/FinanceManager/issues/533 (Issue-Kommentar: „korrigiere das bitte")

## Ziel

Alle literal im Quellcode eingebetteten Unicode-Ersatzzeichen `�` (U+FFFD, UTF-8 `EF BF BD`) werden durch die ursprünglich gemeinten Zeichen ersetzt. Die Korruption entstand durch einen einmaligen Encoding-Roundtrip (CP1252-Datei wurde als UTF-8 gelesen und mit Ersetzungs-Fallback erneut gespeichert); dabei ging jedes nicht-ASCII-Zeichen (Umlaute, `€`, `–`/`—`, `·`) als einzelnes `�` verloren.

## Scope

- 37 getrackte Textdateien mit insgesamt ~165 Vorkommen (`.cs`, `.razor`, `.css`, `.md`, `.githooks/pre-commit`).
- Rekonstruktion aus Kontext und Konventionen des Repos (Git-Historie ist nicht verwertbar: die betroffenen Zeilen wurden bereits korrupt committed).
- Prävention: neuer Pre-Commit-/Pre-Push-Check analog zu den bestehenden `.githooks/*-check.py`-Checks, der U+FFFD in Quelltextdateien ablehnt.

## Nicht-Scope

- `issue.md` (ungetrackte lokale Arbeitsdatei, dokumentiert den Bug legitimerweise mit `�`-Beispielen).
- Binärdateien `Docs/screenshots/statement-drafts.png` und `FinanceManager.Web/wwwroot/icons/finance_manager-securities.png`: enthalten die Bytefolge `EF BF BD` zufällig in komprimierten Bilddaten; beide sind valide PNGs (Signatur + IEND geprüft) und bleiben unverändert.
- Zeichen ohne U+FFFD-Marker, die schon bei Dateientstehung falsch waren (z. B. `?` statt Pfeil in CONTRIBUTING.md — Stichprobe gegen Historie zeigt: seit erstem Commit so, nicht Teil dieses Korruptions-Events).

## Akzeptanzkriterien (aus dem Issue)

- Kein U+FFFD mehr in Quelldateien (`.cs`, `.razor`, `.css`, `.js`, `.resx`, `.cshtml`, `.html`; zusätzlich `.md`/`.sh`/Hook-Skripte bereinigt).
- Betroffene UI-Stellen zeigen die korrekten Zeichen (Startseite: `€`; Berichtsübersicht: `·` als Trennzeichen).
- Build und betroffene Tests grün.
- Pre-Commit-/Pre-Push-Check verhindert Wiedereinführung.
