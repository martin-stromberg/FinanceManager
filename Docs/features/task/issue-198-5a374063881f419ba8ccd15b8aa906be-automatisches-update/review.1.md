# Plan-Review - Automatisches Update

Status: **Offene Aufgaben vorhanden**

## Zusammenfassung

Die aktuelle Implementierung deckt wesentliche Teile des Plans ab: Shared-DTOs, ApiClient-Erweiterungen, Update-Controller, Health-Endpunkt, Update-Dateispeicher, Settings-/Statuslogik, Manifest-Abruf, Plattformauswahl, Hash-/ZIP-Validierung, Skripterzeugung, Executor, Hosted Services, Setup-Sektion, Release-Manifest-Skript und Release-Pipeline-Anpassungen sind vorhanden.

Der Plan ist aber noch nicht vollständig umgesetzt. Es fehlen verbindliche Bausteine und mehrere MVP-Anforderungen sind nur teilweise umgesetzt oder nicht ausreichend abgesichert.

## Offene Aufgaben

- `IUpdateServiceResolver` fehlt. Der Plan verlangt einen eigenen ServiceResolver mit Best-Effort-Ermittlung fuer Windows-Service bzw. Linux-systemd und Zusammenfuehrung mit konfigurierten Overrides. In der Implementierung validiert `UpdateScriptGenerator` nur direkt konfigurierte Dienstnamen bzw. einen Windows-EXE-Pfad; Best-Effort-Ermittlung und Mehrdeutigkeitsbehandlung sind nicht umgesetzt.

- Produktive Installationsvalidierung ist unvollstaendig gekapselt. Der Plan verlangt, dass der Orchestrator vor dem Installationsstart alle notwendigen Betriebsparameter fuer die aktuelle Plattform validiert und bei fehlender oder mehrdeutiger Dienstinformation konkrete Admin-Handlungen per `400 BadRequest` liefert. Aktuell entstehen diese Fehler erst im Skriptgenerator/Executor, und ohne ServiceResolver fehlt die vorgesehene Plattform-/Dienstauflösung.

- Die Setup-UI erfuellt den geplanten Umfang nur teilweise. Sichtbar sind Toggle, Intervall, Uhrzeit, Dienstnamen, EXE-Pfad, Status und Release Notes. Es fehlen die Anzeige der Updatequelle `martin-stromberg/FinanceManager`, eine Metadatentabelle fuer Plattform, Dateigroesse und SHA-256 sowie eine explizite Bearbeitung/Anzeige von RepositoryOwner, RepositoryName, ManifestAssetName, WorkingDirectory und HealthTimeoutSeconds, obwohl diese Felder Teil der Settings-DTOs sind.

- Der manuelle Installationsstart hat keinen Bestaetigungsdialog. `SetupUpdateViewModel.StartInstallAsync` sendet immer `ConfirmDowntime = true`; damit wird die im Plan geforderte Benutzerbestaetigung mit Downtime-Hinweis vor manueller Installation nicht umgesetzt.

- Admin-only Anzeige der Setup-Sektion ist nicht umgesetzt. `SetupCardViewModel.SectionDefinitions` registriert die `update`-Sektion allgemein; in `SetupUpdateTab.razor` gibt es keine erkennbare Nicht-Admin-Ansicht oder Ausblendlogik passend zum bestehenden Setup-Verhalten. Die API ist Admin-only, aber der UI-Planpunkt bleibt offen.

- `appsettings.Production.json` enthaelt keine `Updates`-Sektion. Der Plan verlangt Defaults in `appsettings.json` und `appsettings.Production.json`; aktuell ist die Sektion nur in `appsettings.json` vorhanden.

- Die Testabdeckung bleibt deutlich hinter dem Plan zurueck. Vorhanden sind schmale Tests fuer Versionvergleich/Assetvalidierung, Skripterzeugung, Health/Admin-Zugriff/Settings-Roundtrip und Manifeststruktur. Es fehlen insbesondere Tests fuer Manifest-Parsing ungueltiger Felder, installierte Release-Metadaten, Plattformauswahl fuer beide Zielplattformen, Lock-Datei-Verhalten, Admin-Lock-Reset-Regeln, Statusuebergaenge, ServiceResolver, Scheduler-Entscheidungen, `POST install/start` Conflict/BadRequest, ApiClient-Flows, ViewModel-/bUnit-Szenarien sowie Release-Skript-Fehlerfaelle.

- README-/Hilfedokumentation zum administrativen Updateverhalten ist nicht aktualisiert. Die vorhandenen Release-/Bereitstellungsdokumente beschreiben weiterhin den alten einplattformigen Windows-ZIP-Stand und kein Self-Update-Verhalten, obwohl Umsetzungsschritt 14 dies vorsieht.

## Verifikation

- `dotnet test FinanceManager.Tests\FinanceManager.Tests.csproj --no-restore --filter "FullyQualifiedName~Updates"`: bestanden, 13 Tests.
- `dotnet test FinanceManager.Tests.Integration\FinanceManager.Tests.Integration.csproj --no-restore --filter "FullyQualifiedName~UpdateControllerIntegrationTests"`: bestanden, 3 Tests.
- `node --test scripts\generate-update-manifest.test.mjs`: bestanden, 1 Test.

