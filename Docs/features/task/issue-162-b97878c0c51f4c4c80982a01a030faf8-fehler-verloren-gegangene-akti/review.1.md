# Plan-Review

## Ergebnis

**Status:** Vollständig umgesetzt

## Umgesetzte Planelemente

- [x] Feld `_sectionViewModels` (`Dictionary<string, BaseViewModel>`) in `SetupCardViewModel` — vorhanden (`SetupCardViewModel.cs`, Zeile 25), mit `StringComparer.OrdinalIgnoreCase` initialisiert
- [x] Methode `LoadAsync` in `SetupCardViewModel` — angepasst: Guard `_sectionViewModels.Count == 0` vorhanden; alle vier `CreateSubViewModel<T>()`-Aufrufe implementiert (`SetupProfileViewModel`, `SetupNotificationsViewModel`, `SetupBackupsViewModel`, `SetupStatementsViewModel`); Instanzen unter den Schlüsseln `"profile"`, `"notifications"`, `"backup"`, `"statements"` gecacht; `RaiseEmbeddedPanelUiAction()` wird danach aufgerufen
- [x] Methode `CreateSectionViewModel` in `SetupCardViewModel` — angepasst: prüft `_sectionViewModels.TryGetValue(key, out var cached)` vor `ActivatorUtilities.CreateInstance()`; gibt gecachte Instanz zurück wenn vorhanden, fällt sonst auf bisheriges Verhalten zurück
- [x] Hilfsmethode `BuildServices()` in `SetupCardViewModelTests` — um `Mock<IApiClient>` erweitert (`SetupCardViewModelTests.cs`, Zeile 36)
- [x] Bestehender Test `LoadAsync_Requests_EmbeddedSectionsPanel_AfterRibbon` in `SetupCardViewModelTests` — kompiliert mit erweitertem `BuildServices()` weiterhin; alle bestehenden Assertions unverändert
- [x] Neuer Test `GetRibbonRegisters_AfterLoad_IncludesAllSectionRibbonActions` in `SetupCardViewModelTests` — vorhanden; prüft alle 9 Aktions-IDs (`CreateBackup`, `UploadBackup`, `SaveNotifications`, `ResetNotifications`, `Save`, `Reset`, `DetectTimezone`, `SaveImportSplit`, `ResetImportSplit`)
- [x] Neuer Test `CreateSectionViewModel_AfterLoad_ReturnsCachedInstance` in `SetupCardViewModelTests` — vorhanden; prüft Referenzgleichheit bei zweifachem Aufruf von `CreateSectionViewModel("backup", sp)` nach `LoadAsync()`
- [x] E2E-Test `SetupPage_Ribbon_ShowsSectionActions_WithoutExpandingAnySection` in `SetupRibbonPlaywrightTests` — vorhanden; navigiert zur Setup-Seite und wartet auf Sichtbarkeit von `#CreateBackup` und `#SaveNotifications` ohne Sektion aufzuklappen

## Offene Aufgaben

Keine.

## Hinweise

- Die Implementierung enthält zusätzlich ein `ExpandSectionRequested`-Event in `SetupCardViewModel` (Zeile 31–32) sowie einen `BeforeUploadCallback` beim Erzeugen von `SetupBackupsViewModel` (Zeile 143). Dies stellt eine Teilumsetzung des **offenen Punktes #2** aus dem Plan dar („UploadBackup bei zugeklappter Sektion — Sektion automatisch aufklappen"). Diese Erweiterung stand nicht als Pflichtaufgabe im Plan, ist aber funktional kohärent und löst das beschriebene Risiko. Kein Testnachweis in den neuen Tests vorhanden; ggf. gesonderter Test sinnvoll.
- Der bestehende Test `CreateSectionViewModel_Profile_CreatesExpectedViewModel` ruft `CreateSectionViewModel` ohne vorherigen `LoadAsync`-Aufruf auf. Da `_sectionViewModels` dann leer ist, greift der Fallback auf `ActivatorUtilities.CreateInstance()` — der Test läuft plankonform unverändert weiter.
