# Plan-Review - Loeschen bei Massenaenderungsmodus

Geprueft am: 2026-07-26
Gepruefter Stand: Iteration 2

## Status

Vollstaendig umgesetzt

## Zusammenfassung

Die Planvorgaben aus `plan.md` sind im aktuellen Stand umgesetzt. Die offenen Punkte aus dem ersten Plan-Review sind geschlossen:

- `IsAnnounced` wird in `StatementDraftEntryItem` gemappt und sperrt QuickEdit-Bearbeitung sowie lokale QuickEdit-Loeschung.
- Create-Datumswerte werden im API-Client als `yyyy-MM-dd` serialisiert.
- ViewModel- und API-Client-nahe Tests fuer QuickEdit-Delete/Create, Placeholder, Announced-Sperre und Datumsserialisierung sind ergaenzt.
- Styles fuer neue QuickEdit-Zeilen und Placeholder-Zeilen sind vorhanden.

## Pruefung Gegen Plan

### 1. Shared DTOs erweitern

Erfuellt.

`BatchUpdateRequestDto` enthaelt `Updates`, `Deletes` und `Creates`. `EntryCreateDto` enthaelt `ClientId`, Datumsfelder, Betrag und Textfelder. `EntryErrorDto` kann Fehler ueber `EntryId` oder `ClientId` zuordnen. Die Application-DTO-Kopie ist synchron gehalten, waehrend Service und Controller den Shared-Vertrag verwenden.

### 2. API-Client an kombinierten Request anpassen

Erfuellt.

`StatementDrafts_BatchUpdateDetailedAsync` serialisiert Updates, Deletes und Creates gemeinsam. Datumswerte in Update-Feldern sowie `Creates.BookingDate` und `Creates.ValutaDate` werden date-only als `yyyy-MM-dd` serialisiert. Ein API-Client-Test prueft die Create-Serialisierung inklusive Entfernen der Uhrzeit.

### 3. Server-Validierung und Persistenz erweitern

Erfuellt.

Der Batch-Endpunkt akzeptiert kombinierte QuickEdit-Requests und weist leere Requests nur noch ab, wenn Updates, Deletes und Creates leer sind. Der Service laedt den Draft mit Entries, prueft Ownership und Draft-Status, validiert Updates, Deletes und Creates vor der Persistenz und wendet die Aenderungen innerhalb einer Transaktion an. Deletes werden gegen fehlende Entries, gleichzeitige Updates sowie `AlreadyBooked`/`Announced`/`IsAnnounced` validiert. Creates werden mit Pflichtfeldern und Laengenregeln validiert. Nach Persistenz erfolgen Klassifizierung und Parent-/Split-Status-Neubewertung innerhalb des Transaktionspfads.

### 4. QuickEdit-Zustand im ListViewModel erweitern

Erfuellt.

Das Entries-ListViewModel verwaltet Pending Deletes, neue lokale Zeilen und eine Placeholder-Zeile. `BeginQuickEditAsync` initialisiert den lokalen Zustand, `EndQuickEditAsync` verwirft ihn vollstaendig. Placeholder-Eingaben werden zu neuen lokalen Zeilen promoviert, eine neue Placeholder-Zeile wird angehaengt. `MarkRowForDeletion` blendet Bestandszeilen lokal aus, entfernt neue lokale Zeilen direkt und verhindert Delete fuer nicht erlaubte Zeilen. Sammel- und Validierungsmethoden fuer kombiniertes Speichern sind vorhanden.

### 5. QuickEditTable UI erweitern

Erfuellt.

Die Tabelle rendert im QuickEdit-Modus die sichtbare QuickEdit-Liste inklusive neuer und Placeholder-Zeilen. Der Delete-Button nutzt das vorhandene Delete-Icon und lokalisierte Labels. Placeholder-Zeilen erhalten keine Delete-Aktion. `AlreadyBooked`, `Announced` und `IsAnnounced` sind nicht lokal loeschbar. Validation-Hints koennen ueber `EntryId` oder `ClientId` zurueck in die Liste gemappt werden; Fokus auf die erste fehlerhafte Zeile ist vorgesehen.

### 6. CardViewModel-Save und Ribbon anpassen

Erfuellt.

`SaveQuickEditAsync` validiert die gesamte QuickEdit-Sitzung, sammelt den kombinierten Request und sendet ihn nur, wenn mindestens eine Aenderung vorhanden ist. Serverfehler werden auf bestehende oder neue Zeilen gemappt. Nach erfolgreichem Save wird der Draft neu geladen, die eingebettete Liste neu aufgebaut und QuickEdit beendet. `CancelQuickEditAsync` verwirft den lokalen Zustand ueber `EndQuickEditAsync`. Die Ribbon-Logik nutzt `HasPendingQuickEditChanges()` und `QuickEditRowsAreValid()`.

### 7. Tests ergaenzen

Erfuellt.

Abgedeckt sind unter anderem:

- Placeholder-Zeile beim QuickEdit-Start.
- Lokales Delete ohne API-Aufruf.
- Cancel/End QuickEdit stellt geloeschte Zeilen wieder her und entfernt neue lokale Zeilen.
- Save-Aktivierung fuer reine Loeschung und valide reine Neuanlage.
- Ungueltige neue Zeile blockiert Save.
- Kombinierter Server-Request mit Update, Delete und Create.
- Atomaritaet bei ungueltiger Create-Zeile.
- Delete-Sperre fuer Announced-Eintraege.
- API-Client-Serialisierung von Create-Datumswerten als `yyyy-MM-dd`.

Das vorhandene `test-results.md` dokumentiert erfolgreiche Build-, Unit- und Integrationstestlaeufe. Nach Iteration 2 wurden zusaetzlich StatementDraft-nahe Tests erfolgreich gemeldet.

### 8. Manuelle Pruefung

Nicht durch diesen Review ausgefuehrt.

Die im Plan beschriebenen manuellen UI-Szenarien sind als manuelle QA-Pruefpunkte zu verstehen. Aus Code- und Teststand ergeben sich keine offenen Implementierungsaufgaben. Eine Browser-/UI-Pruefung bleibt fuer die fachliche Abnahme sinnvoll.

## Offene Aufgaben

Keine.

## Hinweise

- `review.1.md` und `review-code.1.md` enthalten die Ergebnisse der ersten Iteration und wurden durch den weiteren Lauf abgeloest.
- `review-code.md` liegt aktuell nicht am Zielpfad vor; fuer diesen Plan-Review wurde der aktuelle Code direkt gegen `plan.md` und `inventory.md` geprueft.
