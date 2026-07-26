# Plan-Review - Loeschen bei Massenaenderungsmodus

Geprueft am: 2026-07-26
Gepruefter Stand: Iteration 3

## Status

Vollstaendig umgesetzt

## Zusammenfassung

Die Planvorgaben aus `plan.md` sind im aktuellen Stand umgesetzt. Die nach Iteration 2 gemeldeten planrelevanten Punkte sind geschlossen:

- Serverfehler fuer pending-deleted Zeilen bleiben sichtbar, weil Zeilen mit Validation-Hints wieder in `VisibleQuickEditItems` erscheinen und fokussiert werden koennen.
- Batch-Updates behandeln `Status` nur noch dann als Statusaenderung, wenn das Feld im Request explizit enthalten ist.
- Regressionstests fuer beide Punkte sind ergaenzt.

## Pruefung gegen Plan

### 1. Shared DTOs erweitern

Erfuellt.

`BatchUpdateRequestDto` enthaelt `Updates`, `Deletes` und `Creates`. `EntryCreateDto` enthaelt `ClientId`, `BookingDate`, `ValutaDate`, `Amount`, `Subject`, `BookingDescription` und `RecipientName`. `EntryErrorDto` kann Fehler ueber `EntryId` oder `ClientId` zuordnen. Die Application-DTO-Datei ist synchron zur Shared-Struktur gehalten, waehrend die relevanten Service-/Controller-Pfade den Shared-Vertrag nutzen.

### 2. API-Client an kombinierten Request anpassen

Erfuellt.

`StatementDrafts_BatchUpdateDetailedAsync` serialisiert Updates, Deletes und Creates gemeinsam fuer den bestehenden Batch-Endpunkt. Datumswerte in Update-Feldern sowie `Creates.BookingDate` und `Creates.ValutaDate` werden stabil als `yyyy-MM-dd` serialisiert. Die strukturierte Fehlerantwort bleibt fuer `EntryId` und `ClientId` nutzbar.

### 3. Server-Validierung und Persistenz erweitern

Erfuellt.

Der Controller akzeptiert kombinierte Requests und lehnt nur noch vollstaendig leere Requests ab. `ApplyBatchEntryUpdatesAsync` laedt den Draft mit Entries, prueft Owner und Draft-Status, validiert Updates, Deletes und Creates vor jeder Persistenz und wendet den gueltigen Gesamtrequest in einer Transaktion an.

Validiert werden insbesondere fehlende Entry-IDs, gleichzeitige Update-/Delete-Anfragen, nicht loeschbare `AlreadyBooked`-/`Announced`-/`IsAnnounced`-Eintraege sowie Pflichtfelder und Laengenregeln fuer Creates. Nach erfolgreicher Persistenz laufen Klassifizierung und Split-/Parent-Status-Neubewertung im Transaktionspfad. Iteration 3 stellt sicher, dass bestehende Statuswerte bei normalen Updates nicht als explizite Statusaenderung interpretiert werden.

### 4. QuickEdit-Zustand im ListViewModel erweitern

Erfuellt.

`StatementDraftEntriesListViewModel` verwaltet Pending Deletes, neue lokale Zeilen und eine Placeholder-Zeile. `BeginQuickEditAsync` initialisiert den lokalen Zustand, `EndQuickEditAsync` verwirft ihn vollstaendig. Placeholder-Eingaben werden zu neuen lokalen Zeilen promoviert, danach wird eine neue Placeholder-Zeile angehaengt. `MarkRowForDeletion` blendet Bestandszeilen lokal aus, entfernt neue lokale Zeilen direkt und sperrt Delete fuer nicht erlaubte Zeilen.

Die Sammelmethoden fuer Updates, Deletes und Creates sind vorhanden. `ValidateAllQuickEditRows`, `QuickEditRowsAreValid` und `HasPendingQuickEditChanges` decken kombinierte QuickEdit-Sitzungen ab. Pending-deleted Bestandszeilen bleiben intern erhalten und werden bei Serverfehlern wieder sichtbar, weil `VisibleQuickEditItems` Eintraege mit Hints trotz Pending Delete rendert.

### 5. QuickEditTable UI erweitern

Erfuellt.

Die Tabelle rendert im QuickEdit-Modus `VisibleQuickEditItems` inklusive neuer und Placeholder-Zeilen. Der Delete-Button nutzt das vorhandene Delete-Icon und einen lokalisierten Accessible-/Tooltip-Text. Placeholder-Zeilen sowie `AlreadyBooked`, `Announced` und `IsAnnounced` erhalten keine Delete-Aktion. Neue und Placeholder-Zeilen haben eigene CSS-Klassen mit Styles fuer helle und dunkle Darstellung.

Validation-Hints werden weiterhin unterhalb der betroffenen Zeile angezeigt. Fuer serverseitige Fehler an pending-deleted Zeilen wird die ausgeblendete Zeile wieder gerendert; der Fokus kann auf die erste fehlerhafte Zeile gesetzt werden.

### 6. CardViewModel-Save und Ribbon anpassen

Erfuellt.

`SaveQuickEditAsync` validiert die gesamte QuickEdit-Sitzung, sammelt den kombinierten Request und sendet ihn nur, wenn Updates, Deletes oder Creates vorhanden sind. Serverfehler werden ueber `EntryId` oder `ClientId` an das Entries-ListViewModel uebergeben. Nach erfolgreichem Save wird der Draft neu geladen, die eingebettete Liste neu aufgebaut und QuickEdit beendet. `CancelQuickEditAsync` verwirft lokale Delete-/Create-/Placeholder-Zustaende ueber `EndQuickEditAsync`.

Die Ribbon-Logik nutzt `HasPendingQuickEditChanges()` und `QuickEditRowsAreValid()`, sodass reine Loeschungen und valide reine Neuanlagen den QuickEdit-Save aktivieren koennen.

### 7. Tests ergaenzen

Erfuellt.

Abgedeckt sind unter anderem:

- Placeholder-Zeile beim QuickEdit-Start.
- Lokales Delete ohne API-Aufruf.
- Cancel/End QuickEdit stellt geloeschte Zeilen wieder her und entfernt neue lokale Zeilen.
- Save-Aktivierung fuer reine Loeschung und valide reine Neuanlage.
- Ungueltige neue Zeile blockiert Save.
- Serverfehler fuer pending-deleted Zeilen werden wieder sichtbar.
- Kombinierter Server-Request mit Update, Delete und Create.
- Atomaritaet bei ungueltiger Create-Zeile.
- Delete-Sperre fuer Announced-Eintraege.
- Normale Batch-Updates behalten Status bei, wenn `Status` nicht explizit im Request enthalten ist.
- API-Client-Serialisierung von Create-Datumswerten als `yyyy-MM-dd`.

`test-results.md` dokumentiert erfolgreiche Build-, Unit-/ViewModel-/Service-/API-Client- und Integrationstestlaeufe fuer den StatementDraft-Bereich.

### 8. Manuelle Pruefung

Nicht durch diesen Review ausgefuehrt.

Die im Plan beschriebenen manuellen UI-Szenarien bleiben fachliche QA-Pruefpunkte. Aus Plan-, Inventar-, Code- und Testergebnispruefung ergeben sich keine offenen Implementierungsaufgaben.

## Offene Aufgaben

Keine.

## Hinweise

- Die frueheren Plan-Reviews liegen als `review.1.md` und `review.2.md` vor und sind durch diesen Stand abgeloest.
- Fuer diesen Plan-Review wurden keine Tests erneut ausgefuehrt; bewertet wurden aktueller Code, `plan.md`, `inventory.md` inklusive Detaildokumente und das vorhandene `test-results.md`.
