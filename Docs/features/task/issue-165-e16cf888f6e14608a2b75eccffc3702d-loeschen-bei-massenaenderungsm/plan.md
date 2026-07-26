# Umsetzungsplan - Loeschen bei Massenaenderungsmodus

Erstellt am: 2026-07-26

## Zielbild

Der QuickEdit-Massenmodus fuer Kontoauszugsentwurf-Eintraege unterstuetzt kuenftig drei lokale Aenderungsarten in einer Sitzung:

- Aenderungen an bestehenden editierbaren Eintraegen.
- Vormerkung bestehender editierbarer Eintraege zum Loeschen; diese Zeilen verschwinden sofort aus der sichtbaren Tabelle, werden aber erst beim Speichern geloescht.
- Neuanlage ueber eine stets sichtbare leere letzte Eingabezeile; neue Eintraege bleiben bis zum Speichern lokal.

`CancelQuickEditAsync` verwirft alle drei Aenderungsarten vollstaendig. `SaveQuickEditAsync` sendet Updates, Deletes und Creates gemeinsam an den Server. Der Server validiert den gesamten Request vor jeder Persistenz und fuehrt ihn atomar aus.

## Fachliche Entscheidungen

- Es gibt keine separate Undo-Aktion fuer geloeschte Zeilen. Ruecknahme erfolgt ueber `CancelQuickEditAsync`, bis die Massenänderung gespeichert wurde.
- Loeschen im QuickEdit-Modus ist nur fuer Zeilen erlaubt, die auch quick-editierbar sind. `AlreadyBooked`-Zeilen bleiben nicht loeschbar. `Announced`-Zeilen werden nicht geloescht, solange keine abweichende fachliche Freigabe existiert; sie werden im ViewModel ebenfalls als nicht loeschbar behandelt.
- Neue QuickEdit-Zeilen benoetigen `BookingDate`, `Amount` und `Subject`. `Amount` darf nicht `0` sein. `ValutaDate`, `BookingDescription` und `RecipientName` sind optional und folgen den bestehenden Laengenregeln.
- Der kombinierte Speichervorgang ist vollstaendig atomar: ein Fehler in Create, Update oder Delete verhindert alle Aenderungen.
- Die bestehende Route `POST /api/statement-drafts/{draftId}/entries/batch-update` bleibt der QuickEdit-Speicherpunkt und wird kompatibel erweitert. Bestehende reine Update-Clients koennen weiterhin nur `Updates` senden.

## Arbeitspakete

### 1. Shared DTOs erweitern

Dateien:

- `FinanceManager.Shared/Dtos/Statements/BatchUpdateDtos.cs`
- `FinanceManager.Application/Statements/Dtos/BatchUpdateDtos.cs`
- `FinanceManager.Shared/IApiClient.cs`

Umsetzung:

- `BatchUpdateRequestDto` um `List<Guid> Deletes` und `List<EntryCreateDto> Creates` erweitern.
- `EntryCreateDto` einfuehren mit:
  - `Guid ClientId`
  - `DateTime BookingDate`
  - `DateTime? ValutaDate`
  - `decimal Amount`
  - `string Subject`
  - `string? BookingDescription`
  - `string? RecipientName`
- `EntryErrorDto.EntryId` nullable oder um `Guid? ClientId` erweitern, damit Fehler fuer neue lokale Zeilen zuordenbar sind. Bestehende Update-Fehler behalten `EntryId`.
- Falls beide `BatchUpdateDtos.cs`-Dateien noch parallel existieren, die Shared-DTOs als fuehrenden Vertrag verwenden und die Application-Kopie entweder synchron halten oder auf Shared-Nutzung reduzieren, ohne Namespaces der bestehenden Aufrufer zu brechen.

### 2. API-Client an kombinierten Request anpassen

Dateien:

- `FinanceManager.Shared/ApiClient.StatementDrafts.cs`
- `FinanceManager.Shared/IApiClient.cs`

Umsetzung:

- `StatementDrafts_BatchUpdateDetailedAsync` so erweitern, dass neben `Updates` auch `Creates` und `Deletes` serialisiert werden.
- Die bestehende Datumsnormalisierung auf `yyyy-MM-dd` fuer `Updates.Fields` beibehalten und fuer `Creates.BookingDate` sowie `Creates.ValutaDate` sicherstellen.
- Die Rueckgabe von strukturierten Fehlern unveraendert nutzbar halten; neue Create-Fehler muessen ueber `ClientId` mapbar sein.

### 3. Server-Validierung und Persistenz erweitern

Dateien:

- `FinanceManager.Application/Statements/IStatementDraftService.cs`
- `FinanceManager.Infrastructure/Statements/StatementDraftService.BatchUpdate.cs`
- `FinanceManager.Infrastructure/Statements/StatementDraftService.cs`
- `FinanceManager.Web/Controllers/StatementDraftEntriesController.cs`

Umsetzung:

- `ApplyBatchEntryUpdatesAsync` als kombinierten QuickEdit-Save ausbauen.
- Controller-Guard von "mindestens ein Update" auf "mindestens ein Update, Delete oder Create" aendern.
- Draft inklusive Entries fuer `draftId` und `ownerUserId` laden und Status `Draft` erzwingen.
- Validierung ohne Persistenz:
  - Update-IDs muessen im Draft existieren und editierbar sein.
  - Delete-IDs muessen im Draft existieren, editierbar sein und duerfen nicht gleichzeitig aktualisiert werden.
  - Create-Zeilen muessen Pflichtfelder und Laengenregeln erfuellen.
  - `AlreadyBooked` und `Announced` fuer QuickEdit-Delete ablehnen.
- Erst nach erfolgreicher Gesamtvalidierung in einer Transaktion anwenden:
  - geloeschte Entries entfernen,
  - bestehende Entries mit vorhandener `UpdateEntryCoreAsync`-Logik aktualisieren,
  - neue Entries mit Domain-/Service-Logik erzeugen und danach wie bei Einzelanlage klassifizieren.
- Keine Zwischen-`SaveChangesAsync` vor abgeschlossener Gesamtvalidierung. Bestehende Zwischen-Saves in Update-/Statuspfaden pruefen und fuer den kombinierten Pfad vermeiden oder in der Transaktion belassen.
- Nach Delete/Create/Update betroffene Split- und Parent-Status analog zu bestehenden Add/Delete-Pfaden neu bewerten.
- Als Erfolg den aktualisierten Draft-Snapshot zurueckgeben.

### 4. QuickEdit-Zustand im ListViewModel erweitern

Dateien:

- `FinanceManager.Web/ViewModels/StatementDrafts/StatementDraftEntriesListViewModel.cs`
- `FinanceManager.Web/ViewModels/StatementDrafts/StatementDraftEntryItem.cs`

Umsetzung:

- Lokalen Zustand einfuehren:
  - `HashSet<Guid> _pendingDeleteIds`
  - Collection fuer neue QuickEdit-Zeilen mit temporaeren `ClientId`s
  - eine berechnete Placeholder-Zeile am Tabellenende
- `StatementDraftEntryItem` um UI-Flags erweitern:
  - `IsNew`
  - `IsPlaceholder`
  - `CanDelete`
- `BeginQuickEditAsync` initialisiert Snapshots, leert Pending Deletes und neue Zeilen und stellt die Placeholder-Zeile bereit.
- `EndQuickEditAsync` verwirft Snapshots, Pending Deletes, neue Zeilen, Placeholder und Hints.
- `VisibleQuickEditItems` oder gleichwertige Methode bereitstellen, die geloeschte Bestandszeilen ausblendet und neue/Placeholder-Zeilen anhaengt.
- `SetEditValue` so erweitern, dass Werte fuer neue und Placeholder-Zeilen akzeptiert werden. Sobald ein fachlich relevantes Feld in der Placeholder-Zeile gesetzt wird, wird daraus eine neue lokale Zeile und eine frische Placeholder-Zeile entsteht.
- `MarkRowForDeletion(Guid entryId)` ergaenzen:
  - bei Bestandszeilen ID in `_pendingDeleteIds` aufnehmen,
  - vorhandene Edit-Diffs fuer diese ID ignorieren oder entfernen,
  - bei neuen lokalen Zeilen die lokale Zeile direkt entfernen,
  - UI-Status und Records aktualisieren.
- Bestehende Methoden erhalten kompatible Semantik:
  - `CollectChangedRows()` liefert weiterhin nur Update-Diffs fuer bestehende, nicht geloeschte Zeilen.
  - Neue Methoden fuer `CollectPendingDeleteIds()`, `CollectCreateRows()` und optional `CollectQuickEditSaveRequest()`.
  - `HasPendingQuickEditChanges()` ersetzt die Ribbon-Entscheidung, zaehlt Updates, Deletes und valide befuellte Create-Zeilen.
  - `ValidateAllQuickEditRows()` validiert Updates und neue Zeilen, aber nicht die leere Placeholder-Zeile.
  - `ChangedRowsAreValid()` entweder erweitern oder durch `QuickEditRowsAreValid()` ersetzen.

### 5. QuickEditTable UI erweitern

Dateien:

- `FinanceManager.Web/Shared/QuickEdit/QuickEditTable.razor`
- `FinanceManager.Web/wwwroot/css/app.StatementDraftDetail.css`
- `FinanceManager.Web/wwwroot/css/theme.Dark.StatementDraftDetail.css`
- Ressourcen unter `FinanceManager.Web/Resources/`

Umsetzung:

- Rendering von `vm.Items` auf die neue sichtbare QuickEdit-Liste umstellen, sobald QuickEdit aktiv ist.
- Eingaben fuer Bestands-, neue und Placeholder-Zeilen ueber dieselben Feldnamen (`BookingDate`, `ValutaDate`, `Amount`, `BookingDescription`, `RecipientName`, `Subject`) an das ViewModel melden.
- In der Aktionsspalte fuer loeschbare Zeilen einen Delete-Button mit vorhandenem Delete-Icon (`/icons/sprite.svg#delete`) oder lokal etabliertem Button-Stil ergaenzen.
- Keine Delete-Aktion fuer Placeholder, `AlreadyBooked` und `Announced`.
- Placeholder-Zeile visuell als Eingabezeile am Tabellenende anzeigen, ohne erklaerenden Featuretext in der UI.
- Validation-Hints fuer neue Zeilen ueber `ClientId` anzeigen und Fokus auf die erste ungueltige Zeile auch fuer neue Eintraege unterstuetzen.
- Lokalisierte Texte fuer Tooltip/Accessible Label ergaenzen, z. B. `QuickEdit_DeleteRow`.

### 6. CardViewModel-Save und Ribbon anpassen

Dateien:

- `FinanceManager.Web/ViewModels/StatementDrafts/StatementDraftCardViewModel.cs`

Umsetzung:

- `SaveQuickEditAsync` von `ValidateAllChangedRows()` auf die neue Gesamtvalidierung umstellen.
- Request aus Updates, Deletes und Creates bauen.
- Fruehen Return nur noch ausfuehren, wenn der kombinierte Request komplett leer ist.
- Serverfehler fuer `EntryId` und `ClientId` zurueck ins `StatementDraftEntriesListViewModel` mappen.
- Nach erfolgreichem Save Draft neu laden, EmbeddedList neu initialisieren und QuickEdit beenden.
- `CancelQuickEditAsync` muss ueber `EndQuickEditAsync` alle lokalen Delete-/Create-Zustaende verwerfen.
- Ribbon-Disabled-Logik von `HasChangedRows() && ChangedRowsAreValid()` auf `HasPendingQuickEditChanges() && QuickEditRowsAreValid()` umstellen.

### 7. Tests ergaenzen

Dateien:

- `FinanceManager.Tests/ViewModels/StatementDraftCardViewModelTests.cs`
- `FinanceManager.Tests/Statements/StatementDraftServiceTests.cs`
- `FinanceManager.Tests/Statements/StatementDraftPersistenceTests.cs`
- `FinanceManager.Tests.Integration/ApiClient/ApiClientStatementDraftsTests.cs`

Testfaelle:

- QuickEdit-Start zeigt bestehende Zeilen plus Placeholder-Zeile.
- `MarkRowForDeletion` blendet eine bestehende editierbare Zeile lokal aus und fuehrt keinen API-Aufruf aus.
- `CancelQuickEditAsync` stellt geloeschte Zeilen wieder her und entfernt neue lokale Zeilen.
- Save ist aktiv bei reiner Loeschung.
- Save ist aktiv bei reiner valider Neuanlage.
- Ungueltige neue Zeile blockiert Save und erzeugt Hints.
- Kombinierter Request mit Updates, Deletes und Creates persistiert alle Aenderungen.
- Fehler in einer Create-Zeile verhindert Updates und Deletes.
- Delete fremder oder nicht zum Draft gehoerender Entry-IDs wird abgelehnt.
- Reine bestehende Updates ueber den erweiterten Batch-Vertrag bleiben kompatibel.
- API-Client serialisiert Create-Daten stabil mit `yyyy-MM-dd`.

### 8. Manuelle Pruefung

- Kontoauszugentwurf oeffnen und QuickEdit aktivieren.
- Bestehende editierbare Zeile loeschen: Zeile verschwindet, Draft bleibt bis Save unveraendert.
- QuickEdit abbrechen: Zeile ist wieder sichtbar.
- QuickEdit erneut aktivieren, Zeile loeschen, neue letzte Zeile befuellen, speichern: geloeschte Zeile ist entfernt, neue Zeile vorhanden.
- Reine Loeschung speichern.
- Reine Neuanlage speichern.
- Ungueltige Placeholder-/Create-Eingaben pruefen: Save blockiert bzw. Serverfehler wird an der Zeile angezeigt.
- `AlreadyBooked` und `Announced` pruefen: keine lokale Delete-Aktion im QuickEdit-Modus.

## Reihenfolge der Implementierung

1. DTOs und API-Vertrag erweitern.
2. Server-Service und Controller atomar erweitern.
3. ViewModel-Zustand inklusive Sammel-/Validierungsmethoden implementieren.
4. QuickEditTable auf sichtbare QuickEdit-Zeilen und Delete-Aktion umstellen.
5. CardViewModel Save-/Ribbon-Logik anpassen.
6. Tests fuer Service/API und ViewModel ergaenzen.
7. Build und relevante Tests ausfuehren.

## Offene Punkte

Keine.
