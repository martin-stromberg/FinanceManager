# Code-Review - Automatisches Update

Status: Befunde vorhanden

## Befunde

### 1. UI meldet Installation trotz `NotReady`-Antwort als gestartet

Schweregrad: Mittel

Fundstellen:
- `FinanceManager.Shared/ApiClient.Update.cs:45`
- `FinanceManager.Shared/ApiClient.Update.cs:48`
- `FinanceManager.Shared/ApiClient.Update.cs:50`
- `FinanceManager.Web/ViewModels/Setup/SetupUpdateViewModel.cs:95`
- `FinanceManager.Web/ViewModels/Setup/SetupUpdateViewModel.cs:100`
- `FinanceManager.Web/ViewModels/Setup/SetupUpdateViewModel.cs:101`
- `FinanceManager.Web/Controllers/UpdateController.cs:64`
- `FinanceManager.Web/Controllers/UpdateController.cs:66`

`UpdateController.StartInstall` gibt fuer einen nicht mehr bereiten Installationsstatus korrekt `404 NotFound` mit `Err_Update_NotReady` zurueck. Der API-Client behandelt genau diese Antwort aber als harmloses `null` und ruft nicht `EnsureSuccessOrSetErrorAsync` auf. `SetupUpdateViewModel.StartInstallAsync` ersetzt den Status dann mit `?? Status` und setzt danach immer `Installing = true`.

Auswirkung: Wenn der Ready-Status zwischen UI-Anzeige und Klick veraltet ist, wenn ein Paket manuell entfernt wurde oder wenn ein anderer Vorgang den Status aendert, zeigt die UI eine laufende Installation und startet das Health-Polling, obwohl serverseitig keine Installation gestartet wurde. Der eigentliche API-Fehler geht fuer das ViewModel verloren. Das ist ein Controller/API-Client-Vertragsfehler und ein Betriebsrisiko, weil Admins in einen falschen Installationszustand gefuehrt werden und anschliessend ggf. Lock-/Health-Reset-Massnahmen auf Basis falscher UI-Signale ausfuehren.

Empfehlung: `Updates_StartInstallAsync` sollte 404 nicht als erfolgreichen Null-Rueckgabewert modellieren, sondern analog zu 400/409 den API-Fehler setzen und werfen oder ein explizites Fehlerergebnis liefern. Alternativ muss `SetupUpdateViewModel.StartInstallAsync` `Installing` nur setzen, wenn ein nicht-null `UpdateStatusDto` mit `Status == Installing` zurueckkommt. Dazu einen ViewModel- oder ApiClient-Test ergaenzen, der `404 Err_Update_NotReady` simuliert und erwartet, dass `Installing` false bleibt und `LastError` sichtbar wird.

## Vorherige Befunde aus `review-code.2.md`

Die beiden Befunde aus `review-code.2.md` wurden behoben:

- Der Executor validiert das Pending-ZIP unmittelbar vor der Skripterzeugung erneut gegen Groesse, SHA-256 und ZIP-Entry-Regeln (`UpdateExecutor.StartAsync` ruft `ValidateDownloadedAssetAsync` vor `GenerateAsync` auf). Der Fall ist durch `StartAsync_RevalidatesPendingZipBeforeGeneratingScript` abgedeckt.
- Die Release-Pipeline erzeugt `publishedAt` nun ueber `gh release view` mit UTC-Fallback, und `generate-update-manifest.mjs` behandelt leere Strings als Fallback und validiert den Zeitstempel. Die Script-Tests decken Blank-`publishedAt` ab.

## Fehlende Tests

- Kein Test fuer `Updates_StartInstallAsync`/`SetupUpdateViewModel.StartInstallAsync`, der eine `404 Err_Update_NotReady`-Antwort abdeckt und sicherstellt, dass die UI nicht in den Installations-/Health-Polling-Zustand wechselt.

## Ausgefuehrte Pruefungen

- Statische Codepruefung der aktuellen uncommitted Implementierung mit Fokus auf Self-Update-Services, Controller, Shared API-Client, Setup-ViewModel, Release-Skripte und Workflow.
- Abgleich mit `review-code.2.md`.
- `dotnet test FinanceManager.Tests\FinanceManager.Tests.csproj --no-restore --filter "FullyQualifiedName~FinanceManager.Tests.Updates|FullyQualifiedName~SetupUpdateTab|FullyQualifiedName~SetupCardViewModel"`: bestanden, 38 Tests. Bestehende NuGet-/Trim-Warnungen bleiben vorhanden.
- `dotnet test FinanceManager.Tests.Integration\FinanceManager.Tests.Integration.csproj --no-restore --filter "FullyQualifiedName~UpdateControllerIntegrationTests"`: bestanden, 3 Tests. Bestehende NuGet-/Trim-Warnungen bleiben vorhanden.
- `node --test scripts\generate-update-manifest.test.mjs scripts\resolve-release-version.test.mjs`: bestanden, 23 Tests.
