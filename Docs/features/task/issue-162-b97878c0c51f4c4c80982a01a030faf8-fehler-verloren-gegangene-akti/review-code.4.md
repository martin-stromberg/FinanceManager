# Code-Review

## Ergebnis

**Status:** Befunde vorhanden

## Befunde

### SetupCardViewModelTests.cs (SetupCardViewModelTests)

- **Doppelter Code** — Die vier Testmethoden `GetRibbonRegisters_AfterLoad_IncludesBackupSectionActions` (Z. 95), `GetRibbonRegisters_AfterLoad_IncludesNotificationsSectionActions` (Z. 119), `GetRibbonRegisters_AfterLoad_IncludesProfileSectionActions` (Z. 143) und `GetRibbonRegisters_AfterLoad_IncludesStatementsSectionActions` (Z. 167) enthalten jeweils identischen Arrange-Act-Code (~15 Zeilen): `BuildServices`, VM erstellen, `LoadAsync`, `localizerMock` aufsetzen, `GetRibbonRegisters` aufrufen und `allActionIds` extrahieren. Nur die abschließenden Assertions unterscheiden sich.

  Empfehlung: Gemeinsamen Arrange-Act-Block in eine private Hilfsmethode `LoadedAllActionIds()` auslagern, die `IEnumerable<string>` zurückgibt. Jeder Test ruft nur noch diese Hilfsmethode auf und enthält ausschließlich seine spezifischen `Should().Contain(…)`-Assertions.

### SetupBackupsViewModel.cs (SetupBackupsViewModel)

- **Doppelter Code / Redundante Benachrichtigung** — In `UploadAsync` (Z. 185) ruft `AddBackup` (Z. 208) intern `RaiseStateChanged()` auf (Z. 212), danach ruft das `finally`-Block von `UploadAsync` erneut `RaiseStateChanged()` auf (Z. 201). Im Erfolgspfad wird die UI damit zweimal benachrichtigt, obwohl ein einziger Aufruf ausreicht. Im Vergleich dazu mutiert `CreateAsync` die Liste direkt ohne einen Zwischenaufruf von `RaiseStateChanged` und verlässt sich korrekt auf das `finally`.

  Empfehlung: `AddBackup` soll `RaiseStateChanged()` nicht selbst aufrufen. Die Benachrichtigung erfolgt ausschließlich im `finally`-Block des Aufrufers, konsistent mit `CreateAsync`.

### SetupSections.razor

- **Switch Statements / Type Checks (Inappropriate Intimacy)** — In `OnAfterRenderAsync` (Z. 83) wird der Rückgabewert von `CreateSectionViewModel` direkt per `is SetupBackupsViewModel` auf einen konkreten Typ geprüft, um anschließend `TriggerUploadRequest()` aufzurufen. Die generische Razor-Komponente kennt damit einen spezifischen Implementierungstyp und ist direkt an dessen API gekoppelt. Wenn künftig eine weitere Sektion ein ähnliches Verhalten benötigt, müsste die Komponente erneut angepasst werden.

  Empfehlung: Ein schmales Interface `IUploadTrigger` mit der Methode `TriggerUploadRequest()` einführen und in `SetupBackupsViewModel` implementieren. In `OnAfterRenderAsync` nur gegen dieses Interface prüfen (`is IUploadTrigger trigger`), sodass die Komponente keinerlei Abhängigkeit mehr auf konkrete Sektions-ViewModels hat.

## Geprüfte Dateien

- `FinanceManager.Tests/ViewModels/SetupCardViewModelTests.cs`
- `FinanceManager.Web/Components/Pages/SetupSections.razor`
- `FinanceManager.Web/ViewModels/Setup/SetupBackupsViewModel.cs`
- `FinanceManager.Web/ViewModels/Setup/SetupCardViewModel.cs`
- `FinanceManager.Web/FinanceManager.Web.xml` *(generierte XML-Dokumentationsdatei, kein Review-Gegenstand)*
