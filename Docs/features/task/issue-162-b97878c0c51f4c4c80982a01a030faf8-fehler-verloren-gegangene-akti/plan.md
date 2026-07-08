# Umsetzungsplan: Verloren gegangene Ribbon-Aktionen in den Einstellungen

## Übersicht

`SetupCardViewModel.CreateSectionViewModel()` erzeugt die Section-ViewModels derzeit via `ActivatorUtilities.CreateInstance()`, ohne sie über `CreateSubViewModel<T>()` als Kind-ViewModels zu registrieren. Dadurch trägt keines der Section-ViewModels seine Ribbon-Definitionen in das übergeordnete Ribbon bei. Der Fix besteht darin, die vier ribbon-beitragenden Section-ViewModels (`SetupProfileViewModel`, `SetupNotificationsViewModel`, `SetupBackupsViewModel`, `SetupStatementsViewModel`) innerhalb von `LoadAsync` explizit über `CreateSubViewModel<T>()` zu instanziieren und in einem internen Cache vorzuhalten, damit `GetRibbonRegisters()` sie automatisch aggregiert.

## Designentscheidungen

| Komponente / Bereich | Gewählter Ansatz | Begründung |
|---|---|---|
| Instanziierung der Section-ViewModels | Option A: Explizite `CreateSubViewModel<T>()`-Aufrufe für alle 4 ribbon-beitragenden Typen in `LoadAsync`, Cache in neuem `_sectionViewModels`-Dictionary | Nutzt den bestehenden Aggregationsmechanismus in `BaseViewModel.GetRibbonRegisters()` ohne Logik-Duplizierung. Option B (Ribbon-Buttons direkt in `SetupCardViewModel`) wird verworfen, da sie Callback-Logik aus den Section-ViewModels herausreißen und duplizieren würde. |
| Nicht-ribbon-beitragende Sections | `ActivatorUtilities.CreateInstance()` wie bisher | Die drei Typen `SetupAttachmentCategoriesViewModel`, `SetupSecurityViewModel` und `SetupReturnAnalysisViewModel` benötigen keine Änderung; kein Kind-ViewModel-Eintrag erforderlich. |
| Guard gegen Doppel-Erstellung | Prüfung `_sectionViewModels.Count == 0` vor Pre-Erstellung | `LoadAsync` kann durch Re-Navigation mehrfach aufgerufen werden; Doppel-Registrierung in `_childViewModels` muss verhindert werden. |

## Programmabläufe

### Ribbon-Initialisierung mit Section-Aktionen

Schritt-für-Schritt-Beschreibung des Ablaufs beim erstmaligen Laden der Setup-Karte:

1. `SetupCardViewModel.LoadAsync(Guid id)` wird aufgerufen.
2. `LoadAsync` prüft `_sectionViewModels.Count == 0`; da noch keine Instanzen vorhanden, werden die 4 ribbon-beitragenden Section-ViewModels über `CreateSubViewModel<T>()` erzeugt:
   - `CreateSubViewModel<SetupProfileViewModel>()`
   - `CreateSubViewModel<SetupNotificationsViewModel>()`
   - `CreateSubViewModel<SetupBackupsViewModel>()`
   - `CreateSubViewModel<SetupStatementsViewModel>()`
3. `BaseViewModel.CreateSubViewModel<T>()` registriert jede Instanz automatisch in `_childViewModels` und verdrahtet `StateChanged`-, `AuthenticationRequired`- und `UiActionRequested`-Events.
4. `LoadAsync` speichert jede Instanz im neuen `_sectionViewModels`-Dictionary (`"profile"`, `"notifications"`, `"backup"`, `"statements"` als Schlüssel).
5. `RaiseEmbeddedPanelUiAction()` wird aufgerufen.
6. Die UI rendert Ribbon und ruft `GetRibbonRegisters()` auf `SetupCardViewModel` auf.
7. `GetRibbonRegisters()` (von `BaseViewModel`) ruft zunächst `GetRibbonRegisterDefinition()` des eigenen ViewModels auf → liefert `RebuildAggregates` (Large) und `ResetReportCache` (Small).
8. Anschließend iteriert `GetRibbonRegisters()` über alle `_childViewModels` (jetzt 4 Section-ViewModels) und ruft für jedes `GetRibbonRegisters()` rekursiv auf.
9. Alle 9 Section-Ribbon-Aktionen werden aggregiert und im Ribbon angezeigt.

Beteiligte Klassen/Komponenten: `SetupCardViewModel`, `BaseViewModel`, `SetupProfileViewModel`, `SetupNotificationsViewModel`, `SetupBackupsViewModel`, `SetupStatementsViewModel`

---

### Section-ViewModel-Bereitstellung für SetupSections.razor

Schritt-für-Schritt-Beschreibung beim Aufklappen einer Sektion:

1. Benutzer klappt eine Sektion im Akkordeon auf.
2. `SetupSections.razor.ResolveViewModel(key)` wird aufgerufen.
3. `ResolveViewModel` prüft den lokalen `_viewModels`-Cache; bei Cache-Miss ruft es `Provider.CreateSectionViewModel(key, Services)` auf.
4. `SetupCardViewModel.CreateSectionViewModel()` prüft `_sectionViewModels[key]`.
5. **Für gecachte Typen** (`profile`, `notifications`, `backup`, `statements`): gibt die vorhandene, bereits in `_childViewModels` eingetragene Instanz zurück.
6. **Für nicht-gecachte Typen** (`attachments`, `security`, `returnanalysis`): erstellt via `ActivatorUtilities.CreateInstance(services, sectionDefinition.ViewModelType)` wie bisher.
7. `SetupSections.razor` speichert die Instanz in `_viewModels` und rendert die Sektion.

Beteiligte Klassen/Komponenten: `SetupSections.razor`, `SetupCardViewModel`, `BaseViewModel`

## Neue Klassen

Keine.

## Änderungen an bestehenden Klassen

### `SetupCardViewModel` (Card-ViewModel)

- **Neues Feld:** `_sectionViewModels` (`Dictionary<string, BaseViewModel>`) — Cache für die vorab erzeugten, ribbon-beitragenden Section-ViewModels; ermöglicht `CreateSectionViewModel()` die Rückgabe der bereits registrierten Instanz.
- **Geänderte Methode:** `LoadAsync(Guid id)` — Pre-erstellt die 4 ribbon-beitragenden Section-ViewModels via `CreateSubViewModel<T>()` vor dem Aufruf von `RaiseEmbeddedPanelUiAction()`; Guard `_sectionViewModels.Count == 0` verhindert Doppel-Registrierung bei wiederholtem Aufruf.
- **Geänderte Methode:** `CreateSectionViewModel(string key, IServiceProvider services)` — Prüft `_sectionViewModels` vor der `ActivatorUtilities.CreateInstance()`-Erstellung; gibt gecachte Instanz zurück wenn vorhanden, fällt ansonsten auf bisheriges Verhalten zurück.

### `SetupCardViewModelTests` (Testklasse)

- **Geänderte Hilfsmethode:** `BuildServices()` — Muss um einen `IApiClient`-Mock erweitert werden, damit `LoadAsync` die Section-ViewModels via `CreateSubViewModel<T>()` instanziieren kann, ohne eine `InvalidOperationException` zu werfen.

## Datenbankmigrationen

Keine.

## Validierungsregeln

Keine.

## Konfigurationsänderungen

Keine.

## Seiteneffekte und Risiken

- **`UploadBackup`-Aktion bei zugeklappter Sektion:** `SetupBackupTab.razor` ist nur im DOM wenn die Backup-Sektion aufgeklappt ist (`@if (isExpanded)`). Wenn `UploadBackup` über das Ribbon ausgelöst wird und die Sektion zugeklappt ist, löst `SetupBackupsViewModel.TriggerUploadRequest()` das `UploadRequested`-Event aus, ohne dass ein Handler registriert ist → der Datei-Picker öffnet sich nicht. (Siehe offene Punkte, #2.)
- **Gruppen-Merge im Ribbon:** Mehrere Section-ViewModels verwenden identische Gruppen-Titel (`Ribbon_Group_Actions`, `Ribbon_Group_Manage`). `Ribbon.razor` merged Gruppen gleichen Titels — alle Save/Reset/Save-Buttons landen in einer `Ribbon_Group_Manage`-Gruppe. Das kann aus Nutzersicht unübersichtlich wirken. (Siehe offene Punkte, #1 und #3.)
- **Erhöhter Speicherbedarf:** Die 4 Section-ViewModels werden nun bereits beim ersten `LoadAsync`-Aufruf erzeugt, nicht erst beim Aufklappen der jeweiligen Sektion. Das ist akzeptabel, da es sich um leichtgewichtige ViewModels ohne automatisch laufende Hintergrundoperationen handelt.
- **`LoadAsync`-Dauer:** `CreateSubViewModel<T>()` verwendet `ActivatorUtilities.CreateInstance()` und ist synchron; die zusätzliche Laufzeit ist vernachlässigbar.
- **Re-Navigation:** Beim zweiten Aufruf von `LoadAsync` greift der `_sectionViewModels.Count == 0`-Guard, sodass keine Doppel-Registrierung in `_childViewModels` erfolgt.

## Umsetzungsreihenfolge

1. **`BuildServices()` in `SetupCardViewModelTests` um `IApiClient`-Mock erweitern**
   - Voraussetzungen: Keine (Testhilfsmethode, kein externer Abhängigkeitsbedarf).
   - Beschreibung: `BuildServices()` in `SetupCardViewModelTests` um einen `Mock<IApiClient>` (oder eine Stub-Implementierung) sowie alle weiteren von den 4 Section-ViewModels benötigten Services ergänzen, damit die nachfolgend geänderte `LoadAsync`-Methode in Tests nicht wirft.

2. **`_sectionViewModels`-Dictionary in `SetupCardViewModel` hinzufügen**
   - Voraussetzungen: Keine (reines Feld, keine externen Abhängigkeiten).
   - Beschreibung: Privates Feld `_sectionViewModels` vom Typ `Dictionary<string, BaseViewModel>` mit initialem Leerinhalt in `SetupCardViewModel` anlegen.

3. **`LoadAsync` in `SetupCardViewModel` anpassen**
   - Voraussetzungen: `_sectionViewModels`-Feld aus Schritt 2 ist vorhanden.
   - Beschreibung: Vor dem Aufruf von `RaiseEmbeddedPanelUiAction()` und unter dem Guard `_sectionViewModels.Count == 0` explizite `CreateSubViewModel<T>()`-Aufrufe für `SetupProfileViewModel`, `SetupNotificationsViewModel`, `SetupBackupsViewModel` und `SetupStatementsViewModel` einfügen; Ergebnisse in `_sectionViewModels` mit den jeweiligen Sektionsschlüsseln speichern.

4. **`CreateSectionViewModel` in `SetupCardViewModel` anpassen**
   - Voraussetzungen: `_sectionViewModels`-Feld und angepasste `LoadAsync` aus Schritten 2 und 3.
   - Beschreibung: Vor dem `ActivatorUtilities.CreateInstance()`-Aufruf prüfen, ob `_sectionViewModels` einen Eintrag für den gegebenen `key` enthält; wenn ja, diesen zurückgeben; andernfalls wie bisher fortfahren.

5. **Bestehenden Test `LoadAsync_Requests_EmbeddedSectionsPanel_AfterRibbon` anpassen**
   - Voraussetzungen: Erweiterter `BuildServices()` aus Schritt 1.
   - Beschreibung: Sicherstellen, dass der Test mit dem erweiterten `BuildServices()` weiterhin besteht; ggf. Assertions überprüfen.

6. **Neuen Test `GetRibbonRegisters_AfterLoad_IncludesAllSectionRibbonActions` schreiben**
   - Voraussetzungen: Erweiterter `BuildServices()` aus Schritt 1; angepasste `SetupCardViewModel`-Klasse aus Schritten 2–4.
   - Beschreibung: In `SetupCardViewModelTests` einen neuen Test anlegen, der `LoadAsync()` aufruft, anschließend `GetRibbonRegisters()` mit einem `IStringLocalizer`-Stub aufruft und prüft, dass die Aktions-IDs `CreateBackup`, `UploadBackup`, `SaveNotifications`, `ResetNotifications`, `Save`, `Reset`, `DetectTimezone`, `SaveImportSplit` und `ResetImportSplit` in den zurückgegebenen Registern enthalten sind.

7. **Neuen Test `CreateSectionViewModel_AfterLoad_ReturnsCachedInstance` schreiben**
   - Voraussetzungen: Erweiterter `BuildServices()` aus Schritt 1; angepasste `SetupCardViewModel`-Klasse aus Schritten 2–4.
   - Beschreibung: In `SetupCardViewModelTests` einen neuen Test anlegen, der `LoadAsync()` aufruft und prüft, dass `CreateSectionViewModel("backup", sp)` nicht eine neue Instanz erzeugt, sondern dieselbe Instanz zurückgibt, die `GetRibbonRegisters()` für `CreateBackup`/`UploadBackup` liefert.

8. **E2E-Test für Ribbon-Sichtbarkeit auf der Setup-Seite schreiben**
   - Voraussetzungen: Angepasste `SetupCardViewModel`-Klasse aus Schritten 2–4; laufende Testinfrastruktur in `FinanceManager.Tests.E2E`.
   - Beschreibung: Einen E2E-Test anlegen, der zur Setup-Seite navigiert und im Ribbon mindestens eine der Section-Aktionen (z. B. `CreateBackup` oder `SaveNotifications`) auf Sichtbarkeit prüft, ohne dass die jeweilige Sektion aufgeklappt sein muss.

## Tests

### Neue Tests

| Test / Hilfsmethode | Testklasse | Was wird geprüft / bereitgestellt? |
|---|---|---|
| `GetRibbonRegisters_AfterLoad_IncludesAllSectionRibbonActions` | `SetupCardViewModelTests` | Prüft, dass `GetRibbonRegisters()` nach `LoadAsync()` die 9 Aktions-IDs der 4 Section-ViewModels enthält. |
| `CreateSectionViewModel_AfterLoad_ReturnsCachedInstance` | `SetupCardViewModelTests` | Prüft, dass `CreateSectionViewModel("backup", sp)` nach `LoadAsync()` die pre-erstellte `SetupBackupsViewModel`-Instanz zurückgibt (kein neues Objekt). |

### Betroffene bestehende Tests

| Test / Testklasse | Grund der Anpassung |
|---|---|
| `LoadAsync_Requests_EmbeddedSectionsPanel_AfterRibbon` / `SetupCardViewModelTests` | `LoadAsync` ruft nun `CreateSubViewModel<T>()` auf; `BuildServices()` muss um `IApiClient`-Mock erweitert werden, damit keine `InvalidOperationException` im DI-Container geworfen wird. |
| `CreateSectionViewModel_Profile_CreatesExpectedViewModel` / `SetupCardViewModelTests` | Kein Anpassungsbedarf wenn der Test ohne vorherigen `LoadAsync`-Aufruf bleibt: `_sectionViewModels` ist leer, Fallback auf `ActivatorUtilities.CreateInstance(sp, ...)` greift wie bisher. Nur bei Refactoring von `BuildServices()` prüfen. |

### E2E-Tests (Pflicht)

| Szenario | Testdatei / Testklasse | Abgedecktes Akzeptanzkriterium |
|---|---|---|
| Setup-Seite zeigt Section-Ribbon-Aktionen ohne Aufklappen einer Sektion | `FinanceManager.Tests.E2E` (neue Testklasse oder vorhandene Setup-Testklasse) | Ribbon enthält nach dem Laden der Setup-Seite mindestens die Aktionen `CreateBackup` und `SaveNotifications`, ohne dass Backup- oder Benachrichtigungs-Sektion geöffnet wurde. |

Welche bestehenden E2E-Tests müssen angepasst werden?

Keine.

## Offene Punkte

| # | Offener Punkt | Empfohlener Vorschlag |
|---|---|---|
| 1 | **Namenskollisionen bei Ribbon-Gruppen:** Mehrere Section-ViewModels verwenden denselben Gruppen-Titel (`Ribbon_Group_Actions`, `Ribbon_Group_Manage`). Ist der automatische Merge erwünscht oder sollen eigene Gruppen je Sektion verwendet werden? | Merge akzeptieren (eine `Ribbon_Group_Manage`-Gruppe mit allen Save/Reset-Aktionen). Gilt als übersichtlicher als viele kleine Gruppen. Eigene Gruppen-Titel nur auf ausdrücklichen Wunsch. |
| 2 | **`UploadBackup` bei zugeklappter Sektion:** `SetupBackupTab.razor` ist nur im DOM wenn aufgeklappt; `TriggerUploadRequest()` hat keinen Empfänger wenn zugeklappt. | `SetupSections.razor` auf immer-gerendertes, visuell verstecktes Rendering umstellen (`display:none`-Pattern statt `@if (isExpanded)` für die Sektions-Inhalte) — oder: beim Klick auf `UploadBackup` automatisch die Backup-Sektion aufklappen, bevor das Event ausgelöst wird. Ersteres ist einfacher, letzteres benutzerfreundlicher. |
| 3 | **Darstellung mehrerer Save/Reset-Aktionen:** `SetupProfileViewModel`, `SetupNotificationsViewModel` und `SetupStatementsViewModel` haben jeweils eigene Speichern/Zurücksetzen-Buttons. Sollen die Gruppen-Titel differenzieren (z. B. „Profil – Speichern")? | Nein — Gruppen-Merge belassen. Die Buttons in der UI werden über ihre Labels unterschieden. Differenzierte Gruppen-Titel bedürfen einer Änderung in allen drei Section-ViewModels und sollte nur auf Kundenwunsch umgesetzt werden. |
