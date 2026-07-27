# Umsetzungsplan - Update-Einstellungen

## Zielbild

Die Update-Einstellungsseite verhaelt sich wie die uebrigen Setup-Sektionen: editierbare Werte werden ueber den globalen Ribbon-Button `Speichern` persistiert, Update-Aktionen liegen im Ribbon und der Tab selbst enthaelt nur Formular, Status und Release-Informationen. Die entfernten technischen Konfigurationswerte sind fuer Anwender nicht mehr sichtbar oder editierbar und werden serverseitig auf die geforderten Festwerte normalisiert. Das Servicename-Feld bietet plattformspezifische Autocomplete-Vorschlaege aus Systemdiensten an.

## Umsetzungsschritte

### 1. Update-Tab bereinigen

- In `FinanceManager.Web/Components/Pages/Setup/SetupUpdateTab.razor` die Formularfelder fuer `ExecutablePath`, `RepositoryOwner`, `RepositoryName`, `ManifestAssetName`, `WorkingDirectory` und `HealthTimeoutSeconds` entfernen.
- Die Tab-eigenen Buttons `Einstellungen speichern`, `Jetzt pruefen`, `Update installieren` und `Update-Lock zuruecksetzen` entfernen.
- Die nicht mehr benoetigten Change-Handler fuer entfernte Felder loeschen.
- Den Statuswert nicht mehr als rohen Enum ausgeben, sondern ueber eine lokale Hilfsmethode auf Localizer-Keys wie `UpdateStatusKind_NoUpdate`, `UpdateStatusKind_Checking`, `UpdateStatusKind_Downloading`, `UpdateStatusKind_Ready`, `UpdateStatusKind_Installing` und `UpdateStatusKind_Failed` abbilden.
- Den Health-Polling-Timeout weiterhin intern mit dem geladenen DTO-Wert oder einem konstanten Fallback von `120` Sekunden verwenden, ohne das Feld in der UI anzubieten.

### 2. Update-ViewModel an Setup-Speicherpattern angleichen

- In `FinanceManager.Web/ViewModels/Setup/SetupUpdateViewModel.cs` einen Original-Snapshot der geladenen `UpdateSettingsDto`-Werte einfuehren.
- Eine `Dirty`-Property ergaenzen, die nur die verbleibenden editierbaren Werte beruecksichtigt: `Enabled`, `CheckIntervalMinutes`, `ScheduledInstallTime` und `ServiceName`.
- `UpdateSettings(...)` so erweitern, dass nach jeder Aenderung `Dirty` neu berechnet wird.
- `SaveAsync(...)` nach erfolgreicher API-Antwort den Original-Snapshot aktualisieren und `Dirty` zuruecksetzen.
- Eine `Reset()`-Methode ergaenzen, die auf den letzten geladenen Stand zuruecksetzt.
- `SaveAsync(...)` soll beim Erzeugen von `UpdateSettingsUpdateRequest` fuer entfernte Felder nicht von UI-Eingaben abhaengen. Rueckwaertskompatible DTO-Felder koennen aus dem aktuellen geladenen DTO uebernommen werden, die Server-Normalisierung bleibt aber massgeblich.

### 3. Update-Sektion in globales Setup-Speichern integrieren

- In `FinanceManager.Web/ViewModels/Setup/SetupCardViewModel.cs` `SetupUpdateViewModel` in `LoadAsync(...)` als Core-Child per `CreateSubViewModel<SetupUpdateViewModel>()` vorinitialisieren, sofern die Update-Sektion fuer den aktuellen Benutzer sichtbar ist.
- Sicherstellen, dass `CreateSectionViewModel("update", ...)` dieselbe gecachte Instanz zurueckgibt, damit Ribbon-Actions und sichtbarer Tab dieselbe ViewModel-Instanz nutzen.
- `HasPendingChanges`, `SaveAllAsync(...)` und `ResetAll()` um die Update-Sektion erweitern.
- Bei nicht sichtbarer Update-Sektion darf kein Update-ViewModel erzeugt werden, damit Admin-Sichtbarkeit und bestehende Rechtepruefung erhalten bleiben.

### 4. Update-Aktionen in das Ribbon verschieben

- In `SetupUpdateViewModel.GetRibbonRegisterDefinition(...)` eine Actions-Registerdefinition fuer die Update-Aktionen ergaenzen.
- Die Ribbon-Actions sollen `CheckAsync`, `StartInstallAsync(confirmDowntime: true)` und `ResetLockAsync` aufrufen.
- Deaktivierungslogik:
  - `Jetzt pruefen`: deaktiviert bei `Busy` oder fehlendem Status.
  - `Update installieren`: deaktiviert bei `Busy`, fehlendem Status oder Status ungleich `UpdateStatusKind.Ready`.
  - `Update-Lock zuruecksetzen`: deaktiviert bei `Busy`, fehlendem Status oder `IsLocked == false`.
- Fuer `Update installieren` muss die bestehende Downtime-Bestaetigung erhalten bleiben. Da ViewModels keinen `IJSRuntime` nutzen sollen, ist dafuer eine kleine UI-Callback-Property oder ein bestaetigender Wrapper im Komponenten-/Host-Kontext einzuplanen. Falls die vorhandene Ribbon-Infrastruktur keine UI-Bestaetigung unterstuetzt, wird der Callback im ViewModel als `Func<Task<bool>>? ConfirmInstallAsync` modelliert und von `SetupUpdateTab.razor` gesetzt.
- Neue bzw. wiederverwendete Localizer-Keys fuer Ribbon-Labels und Hints in `FinanceManager.Web/Resources/Pages.resx`, `.de.resx` und `.en.resx` pflegen.

### 5. Entfernte Serverwerte fest normalisieren

- In `FinanceManager.Web/Services/Updates/UpdateSettingsStore.cs` `Normalize(UpdateSettingsUpdateRequest)` so anpassen, dass folgende Werte unabhaengig vom Request gesetzt werden:
  - `RepositoryOwner`: `martin-stromberg`
  - `RepositoryName`: `FinanceManager`
  - `ManifestAssetName`: `update.json`
  - `WorkingDirectory`: `updates`
  - `HealthTimeoutSeconds`: interner Festwert aus `UpdateOptions` mit Fallback `120` und Clamp `10..600`
- `ExecutablePath` nicht mehr aus Anwenderaenderungen fortschreiben. Fuer Legacy-Kompatibilitaet kann beim Lesen bestehender Settings ein vorhandener Wert weiter in DTOs auftauchen; neue Speichervorgaenge sollten ihn auf `null` oder einen explizit intern definierten Wert normalisieren.
- `Defaults()` und Legacy-Migration konsistent halten: alte Dateien duerfen lesbar bleiben, speichern fuehrt aber auf die neuen Festwerte zurueck.
- Auswirkungen auf `IUpdateFileStore.UseWorkingDirectory(...)` beachten: nach Speichern soll immer das feste Verzeichnis `updates` verwendet werden.

### 6. Service-Autocomplete bereitstellen

- In `FinanceManager.Web/Services/Updates/UpdateContracts.cs` eine kleine Schnittstelle `IUpdateServiceCatalog` ergaenzen, z. B. `Task<IReadOnlyList<string>> ListServiceNamesAsync(string? query, int take, CancellationToken ct)`.
- Eine Implementierung neben `DefaultUpdateServiceProbe` anlegen, die plattformspezifisch arbeitet:
  - Windows: `sc.exe query type= service state= all` ausfuehren und Dienstnamen robust aus `SERVICE_NAME:`-Zeilen extrahieren.
  - Linux: `systemctl list-units --type=service --all --no-legend --no-pager` ausfuehren und `*.service`-Namen aus der ersten Spalte extrahieren.
  - Andere Plattformen, fehlende Tools, Timeouts oder Prozessfehler liefern eine leere Liste.
- Die Vorschlaege nach Query filtern, deduplizieren, stabil sortieren und auf `take` begrenzen.
- Die Implementierung in `FinanceManager.Web/ProgramExtensions.cs` registrieren.
- In `FinanceManager.Web/Controllers/UpdateController.cs` einen Admin-Endpunkt ergaenzen, z. B. `GET api/setup/update/services?query=&take=20`.
- In `FinanceManager.Shared/ApiClient.Update.cs` eine passende Methode wie `Updates_GetServiceNamesAsync(string? query, int take, CancellationToken ct)` ergaenzen.
- In `SetupUpdateViewModel` eine Vorschlagsliste und `LoadServiceSuggestionsAsync(...)` ergaenzen.
- In `SetupUpdateTab.razor` das Servicename-Feld mit einem `datalist` aus den Vorschlaegen verbinden und Vorschlaege bei Fokus sowie bei Eingabe nachladen.

### 7. Ressourcen aktualisieren

- In `FinanceManager.Web/Resources/Pages.resx`, `Pages.de.resx` und `Pages.en.resx` Keys fuer lokalisierte Update-Statuswerte ergaenzen.
- Neue Ribbon-Keys fuer `Jetzt pruefen`, `Update installieren` und `Update-Lock zuruecksetzen` samt Hints ergaenzen oder bestehende `SetupUpdate_Btn_*`-Texte bewusst wiederverwenden.
- Nicht mehr verwendete Label-Keys fuer entfernte Felder muessen nicht zwingend geloescht werden, koennen aber nach erfolgreicher Umsetzung bereinigt werden, sofern keine anderen Referenzen existieren.

## Tests

- `FinanceManager.Tests/Components/SetupUpdateTabTests.cs`
  - pruefen, dass entfernte Labels und Tab-Buttons nicht gerendert werden.
  - pruefen, dass der Status lokalisiert angezeigt wird.
  - bestehende Health-Polling-Tests auf den internen Timeout-Fallback bzw. DTO-Wert anpassen.
  - falls `datalist` genutzt wird, Rendering der Service-Vorschlaege pruefen.
- `FinanceManager.Tests/Web/SetupUpdateViewModelTests.cs`
  - Dirty-State nach Aenderung eines verbleibenden Felds.
  - `SaveAsync` setzt Dirty nach erfolgreicher Antwort zurueck.
  - `Reset()` stellt den geladenen Stand wieder her.
  - Service-Suggestions rufen den neuen ApiClient-Endpunkt auf und tolerieren API-Fehler.
- Setup-Card-Tests, falls vorhanden, oder neue Tests im passenden Testbereich:
  - Update-Section wird fuer Admins als Core-Child verwendet.
  - `HasPendingChanges`, `SaveAllAsync` und `ResetAll` beruecksichtigen Update.
  - aggregiertes Ribbon enthaelt die drei Update-Aktionen.
- `FinanceManager.Tests/Updates/UpdateSettingsStoreTests.cs`
  - abweichende Request-Werte fuer Repository, Manifest, WorkingDirectory, HealthTimeout und ExecutablePath werden beim Speichern nicht uebernommen.
  - bestehende Custom-WorkingDirectory-Erwartungen auf den neuen Festwert `updates` umstellen.
  - Legacy-Settings bleiben lesbar und werden beim naechsten Speichern normalisiert.
- Neue Tests fuer den Service-Catalog:
  - Windows-Parser extrahiert Dienstnamen aus `sc.exe`-Ausgabe.
  - Linux-Parser extrahiert Dienstnamen aus `systemctl`-Ausgabe.
  - Fehler, leere Ausgabe und nicht unterstuetzte Plattformen ergeben leere Vorschlagslisten.
- `FinanceManager.Tests/Shared/ApiClientUpdateTests.cs`
  - neuen Service-Suggestions-Endpunkt pruefen.
- Abschliessend `dotnet test` ausfuehren.

## Risiken und Hinweise

- `WorkingDirectory` ist operativ relevant. Das erzwungene Zuruecksetzen auf `updates` kann bestehende Installationen mit abweichendem Pfad beim naechsten Speichern auf den Standard migrieren.
- `ExecutablePath` ist unter Windows bisher Fallback fuer das Installationsziel. Da die Anforderung nur die Anwender-Einstellung entfernt, sollte die Resolver-Logik nicht unnoetig entfernt werden; die Speicherschicht verhindert aber neue UI-getriebene Aenderungen.
- Die Ribbon-Installation benoetigt eine Downtime-Bestaetigung. Falls die vorhandene Ribbon-Infrastruktur keine direkte UI-Bestaetigung anbietet, muss die Bestaetigung ueber einen ViewModel-Callback angebunden werden.
- Service-Auflistung darf nie die Setup-Seite blockieren oder Plattformfehler anzeigen. Fehlerhafte Systemkommandos liefern leere Vorschlaege.

## Offene Punkte

Keine.
