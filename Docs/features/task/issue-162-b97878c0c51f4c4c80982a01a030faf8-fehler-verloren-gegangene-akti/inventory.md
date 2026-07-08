# Bestandsaufnahme: Verloren gegangene Ribbon-Aktionen in den Einstellungen

Analysiert wurden alle Klassen und Komponenten rund um die Setup-Karte und ihre Section-ViewModels, mit Fokus auf den Ribbon-Aggregationsmechanismus von `BaseViewModel` und die Art, wie `SetupCardViewModel` seine Section-ViewModels instanziiert.

## Zusammenfassung

- **Aggregationsmechanismus vorhanden:** `BaseViewModel.GetRibbonRegisters()` aggregiert Ribbon-Definitionen aus `_childViewModels`. Nur ViewModels, die über `CreateSubViewModel<T>()` erzeugt wurden, landen in dieser Liste.
- **Kernfehler dokumentiert:** `SetupCardViewModel.CreateSectionViewModel()` verwendet `ActivatorUtilities.CreateInstance()` direkt — die erzeugten Section-ViewModels werden **nicht** in `_childViewModels` eingetragen und tragen damit nicht zum Ribbon bei.
- **Lazy Rendering in `SetupSections.razor`:** Die Sektionsinhalte werden nur gerendert wenn aufgeklappt (`@if (isExpanded)`). Die Komponenten und ihre Event-Handler (z. B. `UploadRequested` in `SetupBackupTab.razor`) sind also **nicht permanent im DOM**.
- **Lokaler ViewModel-Cache in `SetupSections.razor`:** Das `_viewModels`-Dictionary ist vollständig von `SetupCardViewModel._childViewModels` entkoppelt — kein Pfad zur Ribbon-Aggregation.
- **Ribbon-Definitionen vollständig vorhanden:** Alle vier betroffenen Section-ViewModels überschreiben `GetRibbonRegisterDefinition()` korrekt:
  - `SetupBackupsViewModel`: `CreateBackup` (Large), `UploadBackup` (Large)
  - `SetupNotificationsViewModel`: `SaveNotifications` (Large), `ResetNotifications` (Large)
  - `SetupProfileViewModel`: `Save` (Small), `Reset` (Small), `DetectTimezone` (Small)
  - `SetupStatementsViewModel`: `SaveImportSplit` (Large), `ResetImportSplit` (Large)
- **`SetupCardViewModel.GetRibbonRegisterDefinition()`** enthält nur `RebuildAggregates` (Large) und `ResetReportCache` (Small) — keine Section-Buttons.
- **`IsChildViewModelActive()`** ist in `SetupCardViewModel` nicht überschrieben (Standard: immer `true`) — der Hook für selektive Sichtbarkeit ist bereit, wird aber nicht genutzt.
- **Testlücke:** Es existiert kein Test, der die Ribbon-Aggregation der Section-ViewModels über `SetupCardViewModel` prüft. Die vorhandenen `SetupCardViewModelTests` decken nur `LoadAsync`, `TryGetSectionComponentType` und `CreateSectionViewModel` ab.

## Details

- [Datenmodell](inventory/models.md)
- [Logik](inventory/logic.md)
- [Enums](inventory/enums.md)
- [Interfaces](inventory/interfaces.md)
- [Tests](inventory/tests.md)
