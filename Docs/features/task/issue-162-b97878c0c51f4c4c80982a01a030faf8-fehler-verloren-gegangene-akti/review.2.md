# Plan-Review

## Ergebnis

**Status:** Vollständig umgesetzt

## Umgesetzte Planelemente

- [x] Feld `_sectionViewModels` (`Dictionary<string, BaseViewModel>`) in `SetupCardViewModel` — vorhanden (`SetupCardViewModel.cs`, Zeile 29), initialisiert mit `StringComparer.OrdinalIgnoreCase`
- [x] Methode `LoadAsync(Guid id)` in `SetupCardViewModel` — angepasst: Guard `_sectionViewModels.Count == 0` vorhanden (Zeile 143); alle vier `CreateSubViewModel<T>()`-Aufrufe implementiert für `SetupProfileViewModel` (→ `"profile"`), `SetupNotificationsViewModel` (→ `"notifications"`), `SetupBackupsViewModel` (→ `"backup"`), `SetupStatementsViewModel` (→ `"statements"`); `RaiseEmbeddedPanelUiAction()` wird danach aufgerufen (Zeile 159)
- [x] Methode `CreateSectionViewModel(string key, IServiceProvider services)` in `SetupCardViewModel` — angepasst: prüft `_sectionViewModels.TryGetValue(key, out var cached)` vor `ActivatorUtilities.CreateInstance()` (Zeile 100–103); gibt gecachte Instanz zurück wenn vorhanden, fällt sonst auf bisheriges Verhalten zurück
- [x] Hilfsmethode `BuildServices()` in `SetupCardViewModelTests` — um `Mock<IApiClient>` erweitert (`SetupCardViewModelTests.cs`, Zeile 38)
- [x] Bestehender Test `LoadAsync_Requests_EmbeddedSectionsPanel_AfterRibbon` in `SetupCardViewModelTests` — kompiliert mit erweitertem `BuildServices()` weiterhin; bestehende Assertions unverändert (Zeile 43–68)
- [x] Neuer Test `GetRibbonRegisters_AfterLoad_IncludesAllSectionRibbonActions` in `SetupCardViewModelTests` — vorhanden (Zeile 96–127); prüft alle 9 Aktions-IDs: `CreateBackup`, `UploadBackup`, `SaveNotifications`, `ResetNotifications`, `Save`, `Reset`, `DetectTimezone`, `SaveImportSplit`, `ResetImportSplit`
- [x] Neuer Test `CreateSectionViewModel_AfterLoad_ReturnsCachedInstance` in `SetupCardViewModelTests` — vorhanden (Zeile 129–155); prüft Referenzgleichheit der zurückgegebenen `SetupBackupsViewModel`-Instanz nach `LoadAsync()`
- [x] E2E-Test `SetupPage_Ribbon_ShowsSectionActions_WithoutExpandingAnySection` in `SetupRibbonPlaywrightTests` — vorhanden (`FinanceManager.Tests.E2E/Tests/Navigation/SetupRibbonPlaywrightTests.cs`); navigiert zur Setup-Seite und wartet auf Sichtbarkeit von `#CreateBackup` und `#SaveNotifications` ohne Sektion aufzuklappen

## Offene Aufgaben

Keine.

## Hinweise

- Die Implementierung enthält zusätzlich ein `ExpandSectionRequested`-Event in `SetupCardViewModel` (Zeile 35) sowie einen `BeforeUploadCallback` beim Erzeugen von `SetupBackupsViewModel` (Zeile 151–152). Dies stellt eine Teilumsetzung des **offenen Punktes #2** aus dem Plan dar („UploadBackup bei zugeklappter Sektion — Sektion automatisch aufklappen bevor das Event ausgelöst wird"). Diese Erweiterung stand nicht als Pflichtaufgabe im Plan, ist funktional kohärent und löst das beschriebene Risiko proaktiv. Ein dedizierter Testnachweis für diesen Callback fehlt noch — ggf. gesonderter Test sinnvoll.
- Der bestehende Test `CreateSectionViewModel_Profile_CreatesExpectedViewModel` ruft `CreateSectionViewModel` ohne vorherigen `LoadAsync`-Aufruf auf. Da `_sectionViewModels` dann leer ist, greift der Fallback auf `ActivatorUtilities.CreateInstance()` — der Test läuft plankonform unverändert weiter.
- Die Tasks-Datei `issue-162-b97878c0c51f4c4c80982a01a030faf8-fehler-verloren-gegangene-akti-tasks.md` ist vollständig auf `Erledigt` gesetzt und entspricht dem Prüfergebnis.
