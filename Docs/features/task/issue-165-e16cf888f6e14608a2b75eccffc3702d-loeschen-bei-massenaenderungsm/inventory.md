# Bestandsaufnahme - Loeschen bei Massenaenderungsmodus

Erstellt am: 2026-07-26

## Kurzfazit

Der bestehende QuickEdit-Massenmodus fuer Kontoauszugsentwuerfe ist aktuell auf Aenderungen bestehender Zeilen begrenzt. Die UI rendert nur vorhandene `StatementDraftEntryItem`-Zeilen, `StatementDraftEntriesListViewModel` verwaltet nur Edit-Snapshots pro bestehender Entry-ID, und `StatementDraftCardViewModel.SaveQuickEditAsync` sendet ausschliesslich `BatchUpdateRequestDto.Updates` an `/api/statement-drafts/{draftId}/entries/batch-update`.

Einzelne Anlage und Loeschung existieren bereits ausserhalb des Massenmodus, aber sie persistieren sofort. Fuer die Anforderung muss daher ein lokaler QuickEdit-Zustand fuer vorgemerkte Loeschungen und neue Zeilen eingefuehrt und der Batch-Speichervertrag um Deletes und Creates erweitert werden.

## Detaildokumente

- [UI und QuickEdit-Tabelle](inventory/ui.md)
- [ViewModels und lokaler QuickEdit-Zustand](inventory/viewmodels.md)
- [API-Client, DTOs und Controller](inventory/api.md)
- [Service, Domain und Persistenz](inventory/service.md)
- [Tests und Pruefpunkte](inventory/tests.md)

## Betroffene Hauptpfade

| Bereich | Pfade |
|---------|-------|
| UI | `FinanceManager.Web/Shared/QuickEdit/QuickEditTable.razor`, `FinanceManager.Web/Components/Pages/GenericCardPage.razor` |
| ViewModels | `FinanceManager.Web/ViewModels/StatementDrafts/StatementDraftCardViewModel.cs`, `StatementDraftEntriesListViewModel.cs`, `StatementDraftEntryItem.cs` |
| DTO/API | `FinanceManager.Shared/Dtos/Statements/*`, `FinanceManager.Shared/ApiClient.StatementDrafts.cs`, `FinanceManager.Web/Controllers/StatementDraftEntriesController.cs`, `StatementDraftsController.cs` |
| Persistenz | `FinanceManager.Application/Statements/IStatementDraftService.cs`, `FinanceManager.Infrastructure/Statements/StatementDraftService.BatchUpdate.cs`, `StatementDraftService.cs` |
| Tests | `FinanceManager.Tests/ViewModels/StatementDraftCardViewModelTests.cs`, `FinanceManager.Tests/Statements/StatementDraftServiceTests.cs`, `StatementDraftPersistenceTests.cs`, Integrationstests unter `FinanceManager.Tests.Integration/ApiClient/` |

## Zentrale technische Beobachtungen

- `QuickEditTable.razor` ist faktisch auf `StatementDraftEntriesListViewModel` zugeschnitten und rendert fixe Spalten inklusive Aktionen. Der Aktionsbereich bietet bisher Reset und Reset-Duplicate, aber keine lokale Loeschaktion.
- `StatementDraftEntriesListViewModel` haelt `_editValues` und `_originalValues` nur fuer vorhandene `Items`. Es gibt keine Sammlung fuer `PendingDelete` oder `NewRows`.
- `SaveQuickEditAsync` bricht ab, wenn `CollectChangedRows()` leer ist. Reine Loeschungen oder reine Neuanlagen koennten damit aktuell nicht gespeichert werden.
- Der bestehende Batch-Endpunkt nimmt `BatchUpdateRequestDto` mit `Updates` entgegen. Serverseitig validiert `StatementDraftService.BatchUpdate.cs` Aenderungen zuerst und wendet sie danach an. Das ist eine gute Basis fuer einen erweiterten, atomaren kombinierten Save.
- Einzelanlage (`StatementDrafts_AddEntryAsync`) und Einzelloeschung (`StatementDrafts_DeleteEntryAsync`) existieren bereits, sind aber fuer den Massenmodus ungeeignet, weil sie sofort persistieren.

## Risiken fuer die Umsetzung

- Neue QuickEdit-Zeilen brauchen temporare IDs, damit die Tabelle und Edit-Dictionaries sie stabil rendern koennen.
- `AlreadyBooked`-Zeilen sind aktuell nicht editierbar, koennen aber per Reset-Duplicate lokal entsperrt werden. Die Loeschbarkeit fuer `AlreadyBooked`/`Announced` muss im Plan explizit geregelt werden.
- Batch-Update arbeitet aktuell mit generischen `Dictionary<string, object?>`-Feldern. Fuer Neuanlagen ist ein typisiertes DTO robuster als weitere generische Felder.
- Validierung muss sowohl clientseitig (Save aktivieren, Hints, Fokus) als auch serverseitig (Owner/Draft-Status/Entry-Zugehoerigkeit) erweitert werden.

## Vorschlag fuer den naechsten Schritt

Die Planung sollte einen kombinierten Request bevorzugen, z. B. `QuickEditSaveRequestDto` mit `Updates`, `Deletes` und `Creates`, und den bestehenden Batch-Endpunkt entweder kompatibel erweitern oder durch einen klar benannten QuickEdit-Save-Endpunkt ersetzen.
