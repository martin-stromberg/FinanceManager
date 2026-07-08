# Code-Review

## Ergebnis

**Status:** Befunde vorhanden

## Befunde

### SetupCardViewModel.cs (SetupCardViewModel)

- **Fehlende Kapselung / Cache-Inkonsistenz — `CreateSectionViewModel`** (Zeile 96–101)

  Trifft der Cache-Lookup nicht zu, wird via `ActivatorUtilities.CreateInstance` eine neue Instanz erstellt, diese aber **nicht** in `_sectionViewModels` gespeichert. Jeder weitere Aufruf für nicht vorinitialisierte Sektionen (z. B. `attachments`, `security`, `returnanalysis`) erzeugt eine neue Instanz, obwohl der XML-Kommentar und der Test `CreateSectionViewModel_AfterLoad_ReturnsCachedInstance` Caching-Semantik versprechen. Ribbon-Contributions dieser Sektionen gehen dabei verloren.

  Empfehlung: Nach dem Erstellen der Instanz per `ActivatorUtilities.CreateInstance` die neue Instanz sofort in `_sectionViewModels[key]` speichern, bevor sie zurückgegeben wird. Sicherstellen, dass der Rückgabetyp dabei `BaseViewModel` ist, damit die `Dictionary`-Typsicherheit gewahrt bleibt.

- **Inkonsistente Cache-Befüllung in `LoadAsync`** (Zeile 134–148)

  In `LoadAsync` werden nur 4 der 7 in `SectionDefinitions` definierten Sektionen vorab in `_sectionViewModels` eingefügt (`profile`, `notifications`, `backup`, `statements`). Die drei verbleibenden Sektionen (`attachments`, `security`, `returnanalysis`) fehlen. Da Ribbon-Contributions der SubViewModels zum Zeitpunkt von `GetRibbonRegisterDefinition` erwartet werden, liefern diese drei Sektionen keine Ribbon-Aktionen, sofern die entsprechenden Abschnitte nicht vorher per UI aufgeklappt wurden.

  Empfehlung: Alle 7 Sektionen aus `SectionDefinitions` generisch in einer Schleife initialisieren, statt nur 4 davon manuell. Alternativ den initialen Cache-Aufbau in `GetRibbonRegisterDefinition` nachladen, sodass er unabhängig von `LoadAsync` vollständig ist.

- **Stille Exception-Behandlung — `RaiseEmbeddedPanelUiAction`** (Zeile 180)

  Der leere `catch { }`-Block verschluckt alle Ausnahmen ohne Protokollierung oder Fehlerweiterleitung. Fehler bei der Panelregistrierung bleiben damit vollständig unsichtbar.

  Empfehlung: Den `catch`-Block entfernen oder — wenn Fehlerbehandlung bewusst erwünscht ist — zumindest `SetError` aufrufen und gegebenenfalls loggen.

- **Stille Exception-Behandlung — `GetRibbonRegisterDefinition`** (Zeilen 228–232, 242–249)

  Beide Ribbon-Action-Lambdas (`RebuildAggregates`, `ResetReportCache`) enthalten leere `catch { }`-Blöcke. API-Fehler werden stillschweigend ignoriert; dem Nutzer wird kein Feedback gegeben.

  Empfehlung: Im `catch`-Block `SetError` aufrufen und `RaiseStateChanged` auslösen, sodass die UI eine sichtbare Fehlermeldung anzeigt.

---

### SetupBackupsViewModel.cs (SetupBackupsViewModel)

- **Doppelter Code — BackupItem-Mapping** (Zeilen 84, 108, 184)

  Das Inline-Mapping `new BackupItem { Id = b.Id, CreatedUtc = b.CreatedUtc, FileName = b.FileName, SizeBytes = b.SizeBytes, Source = b.Source }` wird in `LoadBackupsAsync`, `CreateAsync` und `UploadAsync` dreifach wiederholt. Änderungen an `BackupItem` müssen an drei Stellen nachgezogen werden.

  Empfehlung: Eine private statische Hilfsmethode `MapToBackupItem(BackupDto dto)` einführen, die das Mapping kapselt, und sie an allen drei Stellen aufrufen.

- **Toter Code — `ClearUploadRequest`** (Zeilen 222–225)

  Die Methode enthält ausschließlich einen Kommentar und keinen funktionalen Code. Wenn keine externen Aufrufer vorhanden sind, sollte sie entfernt werden.

  Empfehlung: Prüfen, ob `ClearUploadRequest` noch von außen aufgerufen wird. Falls nicht, die Methode ersatzlos entfernen.

- **Stille Exception-Behandlung — `GetRibbonRegisterDefinition`** (Zeilen 245, 257)

  Die `CreateBackup`-Lambda sowie die `UploadBackup`-Lambda enthalten leere `catch`-Blöcke. API-Fehler bei Backup-Erstellung bleiben dem Nutzer gegenüber unsichtbar.

  Empfehlung: Im `catch`-Block mindestens `SetError` und `RaiseStateChanged` aufrufen.

---

### SetupSections.razor (SetupSections)

- **Direkte Typkopplung — `OnAfterRenderAsync`** (Zeile 84)

  `vmObj is SetupBackupsViewModel backupVm` koppelt den generischen `SetupSections`-Component direkt an einen konkreten ViewModel-Typ. Sollte eine andere Sektion denselben Mechanismus (Upload-Trigger nach Expand) benötigen, muss diese Datei manuell erweitert werden.

  Empfehlung: Ein Interface einführen (z. B. `IUploadTriggerable` mit Methode `TriggerUploadRequest()`), das `SetupBackupsViewModel` implementiert. In `OnAfterRenderAsync` gegen dieses Interface prüfen, um die Kopplung aufzulösen.

- **Doppelter ViewModel-Cache** (Zeile 42 + `SetupCardViewModel.cs` Zeile 25)

  `SetupSections` verwaltet ein eigenes `_viewModels`-Dictionary parallel zum `_sectionViewModels` in `SetupCardViewModel`. Beide Dictionaries cachen dasselbe Konzept (Section-ViewModels nach Key), was zu zwei Wahrheitsquellen führt.

  Empfehlung: Den lokalen Cache in `SetupSections` entfernen und vollständig auf den Cache in `SetupCardViewModel` vertrauen, sobald dort das Caching korrekt implementiert ist (siehe Befund zu `CreateSectionViewModel`).

- **Überflüssiger Ausdruck — `OnAfterRenderAsync`** (Zeile 89)

  `await Task.CompletedTask;` am Ende der Methode ist ohne Effekt und erzeugt unnötig eine State Machine.

  Empfehlung: Entweder die Methode als `void` markieren (ohne `async`/`await`) oder das `await Task.CompletedTask;` ersatzlos entfernen.

---

### SetupCardViewModelTests.cs (SetupCardViewModelTests)

- **Mehrere fachliche Fälle in einem Test — `GetRibbonRegisters_AfterLoad_IncludesAllSectionRibbonActions`** (Zeilen 107–122)

  Der Test enthält 9 separate `Assert.Contains`-Aufrufe für unterschiedliche Action-IDs. Schlägt eine Assertion fehl, werden die restlichen nicht mehr geprüft; zudem ist der Testname nicht spezifisch genug, um den Fehlfall eindeutig zu benennen.

  Empfehlung: Entweder pro Action-ID einen eigenen `[Fact]` anlegen oder — pragmatischer — alle Assertions innerhalb eines einzigen `Assert.Multiple`-Blocks (xUnit) zusammenfassen, um alle Fehlschläge in einem Lauf zu erhalten.

## Geprüfte Dateien

- `FinanceManager.Web/ViewModels/Setup/SetupCardViewModel.cs`
- `FinanceManager.Web/ViewModels/Setup/SetupBackupsViewModel.cs`
- `FinanceManager.Web/Components/Pages/SetupSections.razor`
- `FinanceManager.Tests/ViewModels/SetupCardViewModelTests.cs`
- `FinanceManager.Web/FinanceManager.Web.xml` *(automatisch generierte XML-Dokumentation, kein Review-relevanter Handcode)*
