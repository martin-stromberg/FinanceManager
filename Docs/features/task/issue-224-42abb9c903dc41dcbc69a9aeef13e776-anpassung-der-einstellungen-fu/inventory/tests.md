# Detail: Tests und Absicherung

## Vorhandene Tests

`FinanceManager.Tests/Components/SetupUpdateTabTests.cs` deckt aktuell ab:

- Health-Reload-Regel in `ShouldReloadAfterHealth_RequiresObservedOutage` ab Zeile 42
- Loading-Rendering ab Zeile 50
- Installationsphase als lokalisierter Text ab Zeile 63
- Timeout-Verhalten beim Health-Polling ab Zeile 101

`FinanceManager.Tests/Web/SetupUpdateViewModelTests.cs` deckt ab:

- API-Fehler bei Installationsstart ab Zeile 13
- Laden von Settings und Status ab Zeile 32
- Speichern geaenderter Settings ab Zeile 48
- Installationsstatus ab Zeile 66
- Installationsphasen ab Zeile 81

`FinanceManager.Tests/Updates/UpdateSettingsStoreTests.cs` prueft aktuell explizit, dass ein Custom-WorkingDirectory gespeichert und nach Neustart angewendet wird. Diese Erwartung kollidiert mit der neuen Anforderung, wenn WorkingDirectory hart auf `updates` gesetzt wird.

`FinanceManager.Tests/Updates/UpdateServiceResolverTests.cs` prueft Service-Override, Ambiguitaet bei Autodetection und Windows-Executable-Validierung.

## Erwartete Testanpassungen

- Component-Test fuer `SetupUpdateTab`: entfernte Labels/Inputs und entfernte Tab-Buttons duerfen nicht gerendert werden.
- Component- oder ViewModel-Test: Status-Enum wird lokalisiert angezeigt, nicht als roher Enum-Name.
- ViewModel-Test: `SetupUpdateViewModel` setzt Dirty-State bei Aenderungen und speichert ueber `SaveAsync`.
- SetupCardViewModel-Test: `HasPendingChanges` und `SaveAllAsync` beruecksichtigen Update-Settings.
- Ribbon-Test: Update-Aktionen erscheinen im aggregierten Setup-Ribbon und rufen `CheckAsync`, `StartInstallAsync`, `ResetLockAsync` auf.
- Store-Test: entfernte feste Werte werden beim Speichern erzwungen, auch wenn der Request abweichende Werte enthaelt.
- Service-Catalog-Test: Windows/Linux-Parser liefern Dienstnamen, leere oder fehlerhafte Plattformabfragen geben leere Listen zurueck.

## Risikobereiche fuer Regressionen

- Der globale Save-Button ist nur aktiv, wenn `HasPendingChanges` true ist. Ohne Dirty-State im Update-ViewModel wuerden Aenderungen nicht speicherbar.
- Wenn `SetupUpdateViewModel` als Child vorinitialisiert wird, muss die spaeter gerenderte Update-Sektion dieselbe Instanz verwenden, sonst koennen Ribbon-Actions und sichtbare UI auseinanderlaufen.
- Die Health-Timeout-Tests muessen angepasst werden, falls der Timeout nicht mehr aus Settings kommt.
- API-Vertragstests koennen weiterhin die alten DTO-Felder sehen; entscheidend ist das neue Server-Verhalten bei Speicherung.
