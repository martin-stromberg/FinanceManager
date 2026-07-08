# Offene Aufgaben

Erstellt am: 2026-07-08
Abbruchgrund: Maximale Iterationsanzahl erreicht

Die folgenden Aufgaben konnten im automatisierten Zyklus nicht abgeschlossen werden
und müssen manuell oder in einem erneuten Lauf bearbeitet werden.

## Offene Planelemente

(keine — Plan vollständig umgesetzt)

## Code-Review-Befunde

- [ ] **SetupCardViewModel.cs — Schwacher Rückgabetyp**: `CreateSectionViewModel` gibt `object?` zurück, obwohl stets `BaseViewModel?` geliefert wird. Rückgabetyp auf `BaseViewModel?` ändern.
- [ ] **SetupCardViewModel.cs — Wiederholte Listenallokation**: `SettingSections` erzeugt bei jedem Render-Zugriff via `Select(...).ToList()` eine neue Liste. Liste einmalig in `LoadAsync` materialisieren und als `IReadOnlyList<KeyValuePair<string, string>>` cachen.
- [ ] **SetupBackupsViewModel.cs — Inkonsistente Busy-Behandlung**: `StartApplyAsync` ruft `BeginBusyOperation()` nicht auf. `BeginBusyOperation()` am Anfang von `StartApplyAsync` ergänzen und `Busy = false` in `finally` setzen.
- [ ] **SetupBackupsViewModel.cs — Unnötig öffentliche Methode `AddBackup`**: Sichtbarkeit auf `private` reduzieren.
- [ ] **SetupSections.razor — Irreführender Feldname `_pendingUploadRequestKey`**: In `_pendingExpandSectionKey` umbenennen.
- [ ] **SetupCardViewModelTests.cs — Gemischte Assertion-Stile**: Einheitlich auf `Assert.*` oder konsequent FluentAssertions umstellen (Mischung vermeiden).
- [ ] **SetupCardViewModelTests.cs — Zu viele Fälle in einem Test**: `GetRibbonRegisters_AfterLoad_IncludesAllSectionRibbonActions` in je einen Test pro Sektion aufteilen (Backup, Notifications, Profile, Statements).

## Fehlgeschlagene Tests

(keine — alle 754 Tests bestanden)
