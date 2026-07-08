# Code-Review

## Ergebnis

**Status:** Befunde vorhanden

## Befunde

### SetupCardViewModel.cs (SetupCardViewModel)

- **Schwacher Rückgabetyp (Fehlende Kapselung)** — `CreateSectionViewModel` (Zeile 93) gibt `object?` zurück, obwohl die Methode im Erfolgspfad stets eine `BaseViewModel`-Instanz liefert. Aufrufer in `SetupSections.razor` (Zeile 111) müssen nicht casten, weil das Objekt direkt in einen Dictionary-Eintrag für `DynamicComponent` fließt — trotzdem ist der Rückgabetyp irreführend und zwingt andere Aufrufer (z. B. Tests, Zeile 89 in `SetupCardViewModelTests.cs`) zu impliziten `IsType`-Prüfungen statt einer typsicheren Verwendung.

  Empfehlung: Rückgabetyp auf `BaseViewModel?` ändern.

- **Wiederholte Listenallokation im Render-Pfad (Fehlende Kapselung)** — Die Property `SettingSections` (Zeilen 58–65) erzeugt bei jedem Zugriff via `Select(...).ToList()` eine neue Liste. Da sie im Blazor-Render-Loop in `SetupSections.razor` (Zeile 13: `@foreach (var kv in Provider.SettingSections)`) bei jeder Darstellung aufgerufen wird, entstehen unnötige Allokationen.

  Empfehlung: Liste beim Initialisieren (z. B. in `LoadAsync`) einmalig materialisieren und als `IReadOnlyList<KeyValuePair<string, string>>` cachen. Bei Lokalisierungsänderungen gezielt invalidieren.

### SetupBackupsViewModel.cs (SetupBackupsViewModel)

- **Inkonsistente Busy-Behandlung bei `StartApplyAsync`** — `StartApplyAsync` (Zeile 131) führt einen API-Aufruf durch, ruft aber im Gegensatz zu `CreateAsync` (Zeile 107), `DeleteAsync` (Zeile 157) und `UploadAsync` (Zeile 187) weder `BeginBusyOperation()` noch `Busy = false` auf. Die UI zeigt daher während der (potenziell lang laufenden) Wiederherstellung keinen Busy-Zustand.

  Empfehlung: `BeginBusyOperation()` am Anfang von `StartApplyAsync` aufrufen und `Busy = false` in den `finally`-Block verschieben (analog zu den anderen Methoden).

- **Unnötig öffentliche Methode `AddBackup`** — `AddBackup` (Zeile 207) ist `public`, wird jedoch ausschließlich intern aus `UploadAsync` (Zeile 193) aufgerufen. Kein anderer Aufrufer außerhalb der Klasse existiert im aktuellen Branch.

  Empfehlung: Sichtbarkeit auf `private` reduzieren, sofern kein externer Aufrufer beabsichtigt ist.

### SetupSections.razor (SetupSections)

- **Irreführender Feldname `_pendingUploadRequestKey`** — Das Feld (Zeile 42) wird in `OnExpandSectionRequested` für beliebige Sektionen gesetzt und in `OnAfterRenderAsync` ausgewertet, um ggf. den Upload-Dialog zu öffnen. Der Name suggeriert, es handle sich immer um eine Upload-Anfrage, obwohl das Feld für alle Arten von Expand-Aktionen zuständig ist.

  Empfehlung: Umbenennen in `_pendingExpandSectionKey` o. Ä., das den allgemeineren Zweck beschreibt.

### SetupCardViewModelTests.cs (SetupCardViewModelTests)

- **Gemischte Assertion-Stile** — Die Testklasse verwendet in verschiedenen Testmethoden sowohl xUnit-Assertions (`Assert.True`, `Assert.NotNull`, `Assert.Equal`, `Assert.IsType`, `Assert.Same`) als auch FluentAssertions (`Should().Be`, `Should().ContainKey`, `Should().Contain`). Im Rest der Test-Suite ist `Assert.*` der dominierende Stil (z. B. `UserAuthServiceTests.cs`, `UserAdminServiceTests.cs`).

  Empfehlung: Einheitlich auf den projektweit dominierenden Stil (`Assert.*`) umstellen oder konsequent FluentAssertions verwenden. Mischung innerhalb der gleichen Klasse vermeiden.

- **`GetRibbonRegisters_AfterLoad_IncludesAllSectionRibbonActions` prüft zu viele Fälle** — Der Test (Zeilen 96–127) verifiziert in einem einzigen Testfall neun verschiedene Ribbon-Action-IDs. Ein Fehler lässt sich nicht direkt einer einzelnen Sektion zuordnen.

  Empfehlung: Je Sektion (Backup, Notifications, Profile, Statements) einen eigenen Testfall anlegen, damit ein Fehlschlag sofort die fehlerhafte Sektion benennt.

## Geprüfte Dateien

- `FinanceManager.Web/ViewModels/Setup/SetupCardViewModel.cs`
- `FinanceManager.Web/ViewModels/Setup/SetupBackupsViewModel.cs`
- `FinanceManager.Web/Components/Pages/SetupSections.razor`
- `FinanceManager.Tests/ViewModels/SetupCardViewModelTests.cs`
