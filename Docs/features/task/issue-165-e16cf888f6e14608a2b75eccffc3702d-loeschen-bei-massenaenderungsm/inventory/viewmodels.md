# ViewModels und lokaler QuickEdit-Zustand

## Relevante Dateien

- `FinanceManager.Web/ViewModels/StatementDrafts/StatementDraftCardViewModel.cs`
- `FinanceManager.Web/ViewModels/StatementDrafts/StatementDraftEntriesListViewModel.cs`
- `FinanceManager.Web/ViewModels/StatementDrafts/StatementDraftEntryItem.cs`
- Basisklassen unter `FinanceManager.Web/ViewModels/Common/`

## StatementDraftCardViewModel

`SaveQuickEditAsync` fuehrt aktuell diesen Ablauf aus:

1. Prueft `DraftId` und dass `EmbeddedList` ein `StatementDraftEntriesListViewModel` ist.
2. Ruft `ValidateAllChangedRows()` auf.
3. Sammelt `CollectChangedRows()`.
4. Wenn keine geaenderten Zeilen vorhanden sind, wird ohne API-Aufruf beendet.
5. Baut `BatchUpdateRequestDto` aus den geaenderten Feldern.
6. Sendet an `ApiClient.StatementDrafts_BatchUpdateDetailedAsync`.
7. Bei Erfolg wird der Draft neu geladen, die eingebettete Liste neu aufgebaut und QuickEdit beendet.

Fuer die Anforderung muss diese Logik erweitert werden, weil reine Loeschungen und reine Neuanlagen keine `changed rows` im aktuellen Sinne sind. Save-Aktivierung im Ribbon nutzt derzeit `sevm.HasChangedRows() && sevm.ChangedRowsAreValid()`.

`CancelQuickEditAsync` ruft nur `EndQuickEditAsync()` auf. Das loescht derzeit Edit-Snapshots, aber keine neuen lokalen Sammlungen, weil es diese noch nicht gibt.

## StatementDraftEntriesListViewModel

Aktueller QuickEdit-Zustand:

- `_editValues`: aktuelle Werte je vorhandener Entry-ID.
- `_originalValues`: Snapshot beim Start.
- `_allEntries`: vom Draft geladene DTOs.
- `Items`: sichtbare `StatementDraftEntryItem`-Liste aus der Basisklasse.
- `_entryHints`: Validierungshinweise je Entry-ID.

Wichtige Methoden:

- `BeginQuickEditAsync()` erstellt Snapshots fuer alle aktuell geladenen `Items`.
- `EndQuickEditAsync()` loescht Snapshots.
- `SetEditValue()` setzt vorhandene Edit-Werte.
- `ResetRow()` stellt Originalwerte wieder her.
- `CollectChangedRows()` erzeugt Diffs fuer vorhandene Entries.
- `ValidateRow()` validiert BookingDate, Amount, Subject- und Recipient-Laengen.
- `HasChangedRows()` und `ChangedRowsAreValid()` steuern Ribbon-Save.

## Erforderliche Erweiterungspunkte

- Pending-Delete-Sammlung, z. B. `HashSet<Guid> _pendingDeleteIds`.
- Neue lokale Entries, z. B. eigene Collection mit temporaerer ID und Flag `IsNew`.
- Immer eine leere Eingabezeile, die erst zu einer neuen lokalen Entry wird, wenn ein fachlich relevantes Feld gesetzt wird.
- `HasPendingQuickEditChanges()` statt nur `HasChangedRows()`.
- `ValidateAllQuickEditRows()` fuer geaenderte bestehende und neue Zeilen.
- `CollectQuickEditSaveRequest()` oder getrennte Collect-Methoden fuer Updates, Deletes, Creates.

## StatementDraftEntryItem

Das Item enthaelt derzeit persistierte Entry-Daten und Navigation. Moegliche Erweiterungen:

- `IsNew`
- `IsPlaceholder`
- `IsPendingDelete`
- Optional `CanDelete`

Wenn geloeschte Zeilen direkt aus `Items` entfernt werden, ist `IsPendingDelete` auf dem Item nicht zwingend noetig; der Zustand sollte aber im ViewModel erhalten bleiben, damit Save ihn senden kann.
