# Code-Review - Loeschen bei Massenaenderungsmodus

Erstellt am: 2026-07-26

## Status

Befunde vorhanden

## Befunde

### 1. Reset-Duplicate-Zeilen koennen nach lokaler Freigabe nicht mehr zusammen mit Feldern gespeichert werden

Schweregrad: Mittel

Fundstellen:

- `FinanceManager.Web/Shared/QuickEdit/QuickEditTable.razor:70`
- `FinanceManager.Web/Shared/QuickEdit/QuickEditTable.razor:82`
- `FinanceManager.Web/Shared/QuickEdit/QuickEditTable.razor:193`
- `FinanceManager.Web/Shared/QuickEdit/QuickEditTable.razor:307`
- `FinanceManager.Infrastructure/Statements/StatementDraftService.BatchUpdate.cs:115`
- `FinanceManager.Infrastructure/Statements/StatementDraftService.BatchUpdate.cs:116`
- `FinanceManager.Infrastructure/Statements/StatementDraftService.BatchUpdate.cs:118`

`QuickEditTable.ResetDuplicateStatus` setzt eine `AlreadyBooked`-Zeile lokal auf `Open` und schreibt das als `Status`-Diff in den QuickEdit-Puffer. Dadurch rendert dieselbe Zeile anschliessend als editierbar; der Nutzer kann also wie bisher nach dem Reset Felder wie Datum, Betrag oder Verwendungszweck korrigieren und dann speichern.

Der neue kombinierte Serverpfad validiert aber gegen den persistierten Status vor Anwendung des Status-Diffs. Sobald der Request fuer diese Zeile neben `Status` auch ein weiteres Feld enthaelt, erzeugt `entry.Status == AlreadyBooked && nonStatusFields.Count > 0` den Fehler `Entry is not editable`. Damit funktioniert der UI-Workflow nur noch fuer einen reinen Status-Reset, aber nicht mehr fuer den naheliegenden und bisher sichtbaren Ablauf "Duplicate zuruecksetzen und direkt korrigieren".

Auswirkung: Nutzer koennen eine im QuickEdit lokal entsperrte Duplicate-Zeile bearbeiten, erhalten beim Speichern aber einen Serverfehler. Das ist eine Regression im bestehenden Reset-Duplicate-Verhalten und wirkt inkonsistent, weil die UI die Bearbeitung explizit erlaubt.

Empfehlung: Die Servervalidierung sollte Status-Reset und Feldupdates fuer denselben `AlreadyBooked`-Eintrag zulassen, wenn der Request explizit `Status = Open` bzw. den lokal erlaubten Reset enthaelt. Alternativ muss die UI nach Reset-Duplicate weiterhin alle Fachfelder gesperrt lassen. Dazu einen Service-Test ergaenzen, der fuer einen `AlreadyBooked`-Eintrag `Status = Open` plus ein Fachfeld in einem Batch-Request sendet, und einen ViewModel/UI-Test fuer den Reset-Duplicate-QuickEdit-Ablauf.

## Fehlende Tests

- Kein Service-Test fuer `AlreadyBooked` plus explizitem `Status`-Reset und gleichzeitigen Fachfeld-Updates im erweiterten Batch-Request.
- Kein ViewModel/UI-Test, der `ResetDuplicateStatus` ausloest, anschliessend ein Feld editiert und den daraus entstehenden QuickEdit-Save-Request gegen die erwartete Serversemantik absichert.

## Positiv geprueft

- Die Iteration-2-Befunde zu Pending-Delete-Fehlern und unbeabsichtigter Statuslogik bei normalen Updates sind im aktuellen Stand adressiert.
- Pending-deleted Zeilen bleiben fuer Serverfehler sichtbar, weil `VisibleQuickEditItems` Eintraege mit Hint trotz Pending-Delete wieder einblendet.
- `BatchEntryUpdateProposal.Status` ist nullable und wird nur noch gesetzt, wenn das Feld `Status` im Request enthalten ist.
- Die vorhandenen Testergebnisse melden fuer den aktuellen StatementDraft-Umfang keine Fehler.

## Nicht erneut ausgefuehrt

Tests wurden in diesem Review-Schritt nicht erneut gestartet. Grundlage waren statische Codepruefung, `plan.md`, die vorherigen Review-Befunde und das vorhandene `test-results.md`.
