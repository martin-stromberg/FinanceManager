# Umsetzungsplan - Automatische Updates von Vorabversionen

## Zielbild

Die Anwendung verwendet eine aktualisierte Version von `msTools.Updater`, deren reale API fuer Vorabversionen vor der Implementierung verifiziert wird. Nutzer koennen in den Update-Einstellungen explizit steuern, ob Vorabversionen bei automatischen Update-Pruefungen beruecksichtigt werden. Der Standard bleibt `false`, sodass stabile Updates ohne aktive Nutzerentscheidung wie bisher funktionieren.

## Vorgehen

### 1. Neue Updater-Version verifizieren und beschaffen

1. Paketquelle beziehungsweise internes Artefakt fuer `msTools.Updater` pruefen und die neu verfuegbare Version identifizieren.
2. Die Version lokal beschaffen:
   - Wenn ein NuGet-Paket existiert: `FinanceManager.Web/FinanceManager.Web.csproj` von der lokalen DLL-Referenz auf eine `PackageReference` mit der verifizierten Version umstellen.
   - Wenn weiterhin vendorte DLLs verwendet werden: neues Verzeichnis unter `external/msTools.Updater/<version>/` ablegen und den `HintPath` in `FinanceManager.Web/FinanceManager.Web.csproj` umstellen.
3. Die echte Public-API der neuen Version dokumentiert pruefen:
   - XML-Dokumentation und Assembly per Reflection auf Properties/Methoden wie `IncludePrereleases`, `Prerelease`, `AllowPrerelease`, `IncludePreReleases` oder Source-Factory/Konstruktorparameter untersuchen.
   - Mindestens `AutoUpdateOptions`, `AutoUpdateGithubSource` und vorhandene Builder-/Source-Typen pruefen.
4. Ergebnis der API-Pruefung fuer die Implementierung festhalten:
   - Option liegt auf `AutoUpdateOptions`: Mapping direkt in `AutoUpdateOptionsMapper.ApplySettings`.
   - Option liegt auf `AutoUpdateGithubSource` oder Source-Factory: Source-Erzeugung in `AutoUpdateOptionsMapper.ApplySettings` und ggf. `ProgramExtensions.SetInitialConfiguration` anpassen.
   - Option liegt auf Builder/Initialkonfiguration: Initialisierung in `ProgramExtensions` erweitern und Runtime-Aenderungen weiterhin ueber eine testbare Source-/Options-Aktualisierung sicherstellen.

Akzeptanz fuer diesen Schritt: Die referenzierte Version ist eindeutig, das Projekt baut gegen diese Version, und die konkrete Prerelease-API ist bekannt. Falls die neue Version wider Erwarten keine oeffentliche Prerelease-API anbietet, muss die Implementierung abbrechen und dies als Blocker melden.

### 2. Shared DTOs erweitern

1. `FinanceManager.Shared/Dtos/Update/UpdateDtos.cs` erweitern:
   - `UpdateSettingsDto` bekommt ein boolesches Feld `IncludePrereleases`.
   - `UpdateSettingsUpdateRequest` bekommt dasselbe boolesche Feld.
2. Feldposition konservativ am Ende der bestehenden Settings-Records einfuegen, damit die fachliche Bedeutung klar bleibt und alle Konstruktoraufrufe bewusst angepasst werden muessen.
3. JSON-Kompatibilitaet sicherstellen: Fehlende Werte aus bestehenden Settings-Dateien muessen `false` ergeben.

### 3. Persistenz und Defaults anpassen

1. `FinanceManager.Web/Services/Updates/UpdateSettingsStore.cs` erweitern:
   - `Defaults()` baut Requests mit `IncludePrereleases = false`, sofern kein gespeicherter Wert existiert.
   - `Build(UpdateSettingsUpdateRequest request)` uebernimmt den Wert unveraendert in `UpdateSettingsDto`.
   - Legacy-Migration setzt `IncludePrereleases` explizit auf `false`.
2. Sicherstellen, dass `SaveScheduleAsync` das neue Feld durch `with` unveraendert beibehaelt.

### 4. Runtime-Mapping zur Updater-Library implementieren

1. `FinanceManager.Web/Services/Updates/AutoUpdateOptionsMapper.cs` an die in Schritt 1 verifizierte API anpassen.
2. `ToSettingsDto(...)` muss den aktuell wirksamen Prerelease-Wert zurueckgeben:
   - Falls die Library den Wert lesbar auf `AutoUpdateOptions` haelt, dort auslesen.
   - Falls der Wert nur FinanceManager-seitig persistiert wird, `ToSettingsDto` um einen expliziten Parameter erweitern oder den Default `false` nur im Store setzen. Die gewaehlte Variante muss verhindern, dass gespeicherte Werte beim Start verloren gehen.
3. `ApplySettings(...)` muss `settings.IncludePrereleases` unmittelbar auf die Runtime-Konfiguration uebertragen, damit Speichern in der UI ohne Neustart fuer die naechste Update-Pruefung wirkt.
4. Bei GitHub-Source-basierter API muss die Source neu erzeugt werden, wenn Repository, Manifest oder Prerelease-Option geaendert wurden. Der LocalFolder-Pfad darf dadurch nicht veraendert oder unabsichtlich in eine GitHub-Source umgewandelt werden.
5. `FinanceManager.Web/ProgramExtensions.cs` nur dann erweitern, wenn die verifizierte API eine Initialkonfiguration benoetigt. Der persistierte Wert muss danach weiterhin ueber `ApplyPersistedUpdateSettings()` beim Start angewendet werden.

### 5. ViewModel und API-Aufrufe erweitern

1. `FinanceManager.Web/ViewModels/Setup/SetupUpdateViewModel.cs` anpassen:
   - `SaveAsync` sendet `Settings.IncludePrereleases` im `UpdateSettingsUpdateRequest`.
   - `IsDirty` beruecksichtigt Unterschiede bei `IncludePrereleases`.
   - `Reset()` benoetigt keine Sonderlogik, muss aber durch Tests abgedeckt werden.
2. Alle bestehenden Konstruktoraufrufe von `UpdateSettingsDto` und `UpdateSettingsUpdateRequest` in Produktionscode und Tests auf das neue Feld erweitern.

### 6. Einstellungsoberflaeche erweitern

1. `FinanceManager.Web/Components/Pages/Setup/SetupUpdateTab.razor` in der bestehenden `setup-form-grid` um eine Checkbox fuer Vorabversionen erweitern.
2. Neue Change-Handler-Methode ergaenzen, z. B. `OnIncludePrereleasesChanged`, die `Settings.IncludePrereleases` aktualisiert.
3. Beschriftung ueber Lokalisierung ausgeben, nicht hart codieren.
4. Layout sachlich im bestehenden Formularstil halten; keine neue Seite und keine Aenderung der bestehenden Status-/Release-Anzeige.

### 7. Lokalisierung ergaenzen

1. `FinanceManager.Web/Resources/Pages.de.resx` um einen Key wie `SetupUpdate_Lbl_IncludePrereleases` erweitern.
2. Vorgeschlagener Text: `Vorabversionen beruecksichtigen`.
3. Falls im Bestand fuer Setup-Felder Hilfetexte nachgezogen werden, optional separaten Hint-Key ergaenzen. Fuer die Kernanforderung reicht das Label.

### 8. Tests erweitern

1. `FinanceManager.Tests/Updates/AutoUpdateOptionsMapperTests.cs`:
   - Mapping `IncludePrereleases = false` setzt die Library-Konfiguration auf stabile Releases.
   - Mapping `IncludePrereleases = true` aktiviert Vorabversionen gemaess verifizierter Library-API.
   - Falls die Source neu erzeugt werden muss: Test fuer Source-Austausch inklusive Prerelease-Wert.
2. `FinanceManager.Tests/Updates/UpdateSettingsStoreTests.cs`:
   - Default ist `false`.
   - Persistierter Wert `true` bleibt nach Reload erhalten.
   - Bestehendes JSON ohne Feld wird kompatibel als `false` gelesen.
   - `ApplyToOptions` uebertraegt den Wert an die Library.
3. `FinanceManager.Tests/Web/SetupUpdateViewModelTests.cs`:
   - Aenderung der Option setzt `Dirty`.
   - `SaveAsync` sendet den Wert im Request.
   - `Reset()` stellt den urspruenglichen Wert wieder her.
4. `FinanceManager.Tests/Components/SetupUpdateTabTests.cs`:
   - Checkbox und deutsches Label werden gerendert.
   - UI-Aenderung aktualisiert das ViewModel.
5. `FinanceManager.Tests/Shared/ApiClientUpdateTests.cs` und weitere betroffene Tests wegen der erweiterten Record-Konstruktoren aktualisieren.

### 9. Verifikation

1. Projekt gegen die neue `msTools.Updater`-Version bauen:
   - `dotnet build`
2. Fokussierte Tests ausfuehren:
   - `dotnet test FinanceManager.Tests/FinanceManager.Tests.csproj --filter Update`
   - `dotnet test FinanceManager.Tests/FinanceManager.Tests.csproj --filter SetupUpdate`
3. Gesamttests ausfuehren:
   - `dotnet test FinanceManager.Tests/FinanceManager.Tests.csproj`
4. Falls Dependency-Beschaffung oder NuGet-Restore Netzwerkzugriff benoetigt, den Schritt mit expliziter Freigabe ausfuehren.

## Risiken

- Die lokal vorhandene Version `v0.2.0` bietet laut Bestandsaufnahme keine sichtbare Prerelease-API. Die Implementierung darf deshalb nicht gegen angenommene Property-Namen erfolgen.
- Die neue Updater-Version kann eine brechende API-Aenderung an `AutoUpdateGithubSource.Create(...)`, `AutoUpdateOptions` oder dem Builder enthalten. Der Plan sieht deshalb vor, die reale API vor Codeanpassungen zu verifizieren und die Integrationsstelle danach konkret auszuwählen.
- Wenn Vorabversionen nur fuer GitHub-Quellen unterstuetzt werden, muss der LocalFolder-Updatepfad unveraendert bleiben. Die Einstellung kann weiterhin gespeichert werden, darf dort aber keine Regression ausloesen.
- Durch positionale Records entstehen viele Compile-Fehler, bis alle Konstruktoraufrufe angepasst sind. Das ist erwartbar und wird ueber die Testanpassungen abgearbeitet.

## Offene Punkte

Keine. Die unbekannte Prerelease-API ist als verpflichtender Verifikations- und Beschaffungsschritt vor der Implementierung eingeplant; dafuer ist derzeit keine Nutzerentscheidung erforderlich.
