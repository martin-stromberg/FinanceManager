# Plan-Review

## Ergebnis

**Status:** Vollständig umgesetzt

## Umgesetzte Planelemente

- [x] Feld `_sectionViewModels` (`Dictionary<string, BaseViewModel>`) in `SetupCardViewModel` — vorhanden (`SetupCardViewModel.cs`, Zeile 29), initialisiert mit `StringComparer.OrdinalIgnoreCase`
- [x] Methode `LoadAsync` in `SetupCardViewModel` — angepasst: Guard `_sectionViewModels.Count == 0` (Zeile 148) vorhanden; alle vier `CreateSubViewModel<T>()`-Aufrufe für `SetupProfileViewModel` (`"profile"`), `SetupNotificationsViewModel` (`"notifications"`), `SetupBackupsViewModel` (`"backup"`) und `SetupStatementsViewModel` (`"statements"`) implementiert (Zeilen 150–161); `RaiseEmbeddedPanelUiAction()` wird danach aufgerufen (Zeile 164)
- [x] Methode `CreateSectionViewModel` in `SetupCardViewModel` — angepasst: prüft `_sectionViewModels.TryGetValue(key, out var cached)` vor `ActivatorUtilities.CreateInstance()` (Zeilen 100–103); gecachte Instanz wird zurückgegeben wenn vorhanden, Fallback auf bisheriges Verhalten sonst
- [x] Hilfsmethode `BuildServices()` in `SetupCardViewModelTests` — erweitert um `Mock<IApiClient>` (Zeile 38)
- [x] Bestehender Test `LoadAsync_Requests_EmbeddedSectionsPanel_AfterRibbon` in `SetupCardViewModelTests` — vorhanden (Zeile 42); kompiliert mit erweitertem `BuildServices()` weiterhin; alle Assertions unverändert
- [x] Neuer Test `GetRibbonRegisters_AfterLoad_IncludesAllSectionRibbonActions` in `SetupCardViewModelTests` — vorhanden (Zeile 96); prüft alle 9 Aktions-IDs: `CreateBackup`, `UploadBackup`, `SaveNotifications`, `ResetNotifications`, `Save`, `Reset`, `DetectTimezone`, `SaveImportSplit`, `ResetImportSplit`
- [x] Neuer Test `CreateSectionViewModel_AfterLoad_ReturnsCachedInstance` in `SetupCardViewModelTests` — vorhanden (Zeile 130); prüft Referenzgleichheit (`Assert.Same`) bei zweifachem Aufruf von `CreateSectionViewModel("backup", sp)` nach `LoadAsync()`
- [x] E2E-Test `SetupPage_Ribbon_ShowsSectionActions_WithoutExpandingAnySection` in `SetupRibbonPlaywrightTests` — vorhanden; navigiert zur Setup-Seite, wartet auf Sichtbarkeit von `#CreateBackup` und `#SaveNotifications` ohne Sektion aufzuklappen

## Offene Aufgaben

Keine.

## Hinweise

- Die Implementierung enthält zusätzlich ein `ExpandSectionRequested`-Event in `SetupCardViewModel` (Zeile 35) sowie einen `BeforeUploadCallback` beim Erzeugen von `SetupBackupsViewModel` (Zeile 156–157). Dies stellt eine Teilumsetzung des **offenen Punktes #2** aus dem Plan dar („UploadBackup bei zugeklappter Sektion — Sektion automatisch aufklappen"). Diese Erweiterung stand nicht als Pflichtaufgabe im Plan, ist aber funktional kohärent und löst das beschriebene Risiko proaktiv. Kein dedizierter Testnachweis für diesen Callback in den neuen Tests vorhanden.
- Der bestehende Test `CreateSectionViewModel_Profile_CreatesExpectedViewModel` ruft `CreateSectionViewModel` ohne vorherigen `LoadAsync`-Aufruf auf. Da `_sectionViewModels` dann leer ist, greift der Fallback auf `ActivatorUtilities.CreateInstance()` — der Test läuft plankonform unverändert weiter.
- Die nicht-ribbon-beitragenden Section-ViewModels (`attachments`, `security`, `returnanalysis`) bleiben weiterhin via `ActivatorUtilities.CreateInstance()` erstellt — plankonform.
