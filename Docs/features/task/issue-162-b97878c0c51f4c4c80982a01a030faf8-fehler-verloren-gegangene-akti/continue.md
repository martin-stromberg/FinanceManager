# Offene Aufgaben

Erstellt am: 2026-07-08
Abbruchgrund: Kein Fortschritt zwischen den letzten zwei Iterationen

Die folgenden Aufgaben konnten im automatisierten Zyklus nicht abgeschlossen werden
und müssen manuell oder in einem erneuten Lauf bearbeitet werden.

## Offene Planelemente

(keine — Plan vollständig umgesetzt)

## Code-Review-Befunde

- [ ] **SetupBackupsViewModel.cs — Doppelter Code**: `CreateAsync` inlint das Muster `Backups ??= new List<BackupItem>(); Backups.Insert(0, ...)`, obwohl die private Hilfsmethode `AddBackup` existiert und in `UploadAsync` korrekt verwendet wird. In `CreateAsync` den Inline-Block durch `AddBackup(MapToBackupItem(created));` ersetzen.
- [ ] **SetupCardViewModel.cs — Fehlerbehandlung**: Im `catch`-Block von `LoadAsync` wird die Exception nur per `SetError(null, ex.Message)` gesetzt, aber nicht geloggt. `_logger?.LogError(ex, "LoadAsync failed");` ergänzen.
- [ ] **SetupSections.razor — Einrückungsfehler**: Zeile mit `uploadTrigger.TriggerUploadRequest();` ist um 8 Leerzeichen zu weit eingerückt. Auf korrekte Einrückungsebene korrigieren.
- [ ] **SetupCardViewModelTests.cs — Fehlende Testabdeckung (Upload-Trigger-Flow)**: Der Kausalzusammenhang `ExpandSectionRequested`-Event → `OnExpandSectionRequested` → `_pendingExpandSectionKey` → `OnAfterRenderAsync` → `TriggerUploadRequest()` ist nicht getestet. Test hinzufügen, der `ExpandSectionRequested` mit Schlüssel `"backup"` und anschließenden `IUploadTrigger`-Aufruf prüft.
- [ ] **SetupCardViewModelTests.cs — Latentes Designrisiko Guard**: `CreateSectionViewModel_Profile_CreatesExpectedViewModel` ruft `CreateSectionViewModel("profile", sp)` vor `LoadAsync` auf — der Guard `_sectionViewModels.Count == 0` in `LoadAsync` überspringt dann die Sub-ViewModel-Initialisierung. Guard verfeinern (z. B. `!_sectionViewModels.ContainsKey("profile")`) oder Test ergänzen, der diese Reihenfolge abdeckt.

## Fehlgeschlagene Tests

(keine — alle 757 Tests bestanden)
