# Bestandsaufnahme - Automatisches Update

Quelle: `docs/features/task/issue-198-5a374063881f419ba8ccd15b8aa906be-automatisches-update/requirement.md`

## Detaildokumente

- [ASP.NET-Core-Registrierung](inventory/aspnet-core-registrierung.md)
- [Controller- und API-Muster](inventory/controller-api-muster.md)
- [Blazor/Razor-UI und Setup-Seiten](inventory/blazor-setup-ui.md)
- [BackgroundTask- und HostedService-Muster](inventory/background-tasks-hosted-services.md)
- [Konfiguration und Release-Pipeline](inventory/konfiguration-release.md)
- [Download-, Backup- und Dateioperationen](inventory/download-backup-dateioperationen.md)
- [Auth, Rollen, Health/Meta und Tests](inventory/auth-health-tests.md)

## Zusammenfassung

Die Anwendung ist eine ASP.NET-Core/Blazor-Server-Anwendung mit zentraler Service- und Middleware-Registrierung in `FinanceManager.Web/ProgramExtensions.cs`. API-Endpunkte werden als klassische `[ApiController]`-Controller unter `/api/...` bereitgestellt und ueber einen partiellen `FinanceManager.Shared.ApiClient` konsumiert. Das Setup ist als card-basierte Blazor/Razor-Oberflaeche mit dynamischen Setup-Sektionen umgesetzt.

Fuer das Self-Update-Feature gibt es gute Anschlussstellen, aber noch keine vorhandene Update-Domaene:

- Kein bestehender `UpdateController`, kein `UpdateChecker`, kein `UpdateScheduler`, kein `UpdateScriptGenerator`, kein Update-Statusmodell.
- Kein Health-Endpunkt im Sinne der Anforderung; es existieren nur authentifizierte Meta-Endpunkte unter `/api/meta`.
- Die Release-Pipeline erzeugt aktuell ein einzelnes Windows-ZIP-Asset `FinanceManager-vX.Y.Z-win-x64.zip`; eine `update.json` wird nicht erzeugt.
- Periodische Hosted Services existieren bereits (`MonthlyReminderScheduler`, `SecurityPriceWorker`) und liefern passende Muster fuer scoped Service-Aufloesung, Fehlerlogging und konfigurierbare Aktivierung.
- Das vorhandene BackgroundTask-System ist fuer benutzerbezogene Queue-Aufgaben geeignet, aber nicht direkt fuer prozessweite Updateinstallation. Fuer Updates braucht es einen separaten prozessweiten Status und Lock.
- Backup- und Download-Code liefert verwertbare Muster fuer ZIP-Validierung, begrenztes Stream-Kopieren, sichere Dateinamen, Dateispeicher relativ zum `ContentRootPath`, Download-Endpunkte und Restore-Status.
- Admin-Schutz ist zweigleisig vorhanden: `[Authorize(..., Roles = "Admin")]` fuer manche Admin-Aktionen und explizite `_current.IsAdmin`-Pruefungen fuer andere. Neue Update-Konfiguration und Installationsausloesung sollten konsequent Admin-only sein.

## Empfohlene Integrationspunkte

- Registrierung in `ProgramExtensions.RegisterAppServices`: Optionen binden, HTTP-Client fuer GitHub konfigurieren, Update-Services registrieren, `UpdateChecker` und `UpdateScheduler` konditional als Hosted Services aktivieren.
- API entweder als neuer `UpdateController` unter `api/setup/update` oder als Erweiterung des Admin-/Setup-Bereichs. Das bestehende Pattern spricht fuer einen dedizierten Controller mit `[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Roles = "Admin")]`.
- Shared DTOs in `FinanceManager.Shared/Dtos/Admin` oder einem neuen `Dtos/Update`-Namensraum sowie ApiClient-Methoden in einer neuen partiellen Datei, z. B. `ApiClient.Update.cs`.
- UI als neue Setup-Sektion `update` in `SetupCardViewModel.SectionDefinitions` mit `SetupUpdateViewModel` und `SetupUpdateTab.razor`.
- Persistenz fuer globale Update-Einstellungen voraussichtlich in der Datenbank, da vorhandene Setup-Einstellungen zum Teil benutzerbezogen in `UserSettingsController` liegen und `appsettings` zur Laufzeit nicht geschrieben wird.
- Update-Dateien in einem getrennten Arbeitsverzeichnis unter `ContentRootPath`, z. B. `updates/pending`, analog zum Backup-Speicher, aber mit strengem Locking und Pfadvalidierung.

## Risiken und Klaerungsbedarf

- Die Anforderung nennt das Repository `https://github.com/martin-stromberg/FinanceManager`; vorhandene Dokumentation und Release-Code verweisen teils auf `Muesli84/FinanceManager`. Das muss vor Implementierung festgelegt werden.
- Linux-Unterstuetzung ist fachlich gefordert, die Release-Pipeline erzeugt aktuell nur `win-x64`.
- `update.json` fehlt in der Pipeline. Ohne dieses Artefakt muss der Updater entweder GitHub-Release-Metadaten direkt nutzen oder die Pipeline erweitert werden.
- Die installierte Version ist im Code noch nicht als zentraler Meta-Endpunkt oder Service sichtbar.
- Ein externer Update-Prozess, der den laufenden Webprozess beendet und Dateien ersetzt, hat hohes Betriebsrisiko. Tests sollten Skripterzeugung, Locking, Hash-Pruefung und Controller-Autorisierung abdecken; echte Prozessbeendigung sollte abstrahiert und in Tests gemockt werden.

