# Plan-Gegenprüfung

## Ergebnis

**Status:** Plan vollständig

## Abgleich Akzeptanzkriterien

| Akzeptanzkriterium | Umsetzung im Plan | Testnachweis im Plan | Status |
|--------------------|-------------------|----------------------|--------|
| `msTools.Updater` 0.10.1 → `0.11.0-rc.8` (Zielversion verbindlich; `0.10.4` ohne Fix verworfen) | Scope-Klärung legt `0.11.0-rc.8` verbindlich fest; `gh release download` + SHA-256-Verifikation + Einchecken unter `external/msTools.Updater/` (Programmablauf 1, Reihenfolge Schritt 1) | `dotnet restore`/`dotnet build` + `git status`/Diff (Akzeptanzszenario 1) | Abgedeckt |
| Drei `PackageReference` synchron auf Zielversion (`Web` Z. 68, `Tests` Z. 27, `Tests.Integration` Z. 17) | Programmablauf 1 Schritt 3, Reihenfolge Schritt 2 | Build + Referenzsuche (Szenario 1/4) | Abgedeckt |
| Anwendung baut fehlerfrei; kompatibilitätsbedingte Testanpassungen | `dotnet build FinanceManager.sln` in Reihenfolge Schritt 2; Anpassungen entfallen begründet — am Release-Paket verifiziert vollständig quellkompatibel (neue Ctor-Parameter optional), Designentscheidung dokumentiert, Restdeltas iterativ zu beheben | Build 0 Fehler; volle Unit-/Integrationstest-Läufe (Reihenfolge Schritt 4) | Abgedeckt |
| Update-Tests grün (`FinanceManager.Tests/Updates/*`, `UpdateControllerIntegrationTests`) | Reihenfolge Schritt 4: `dotnet test FinanceManager.Tests` (CI-Filter `--filter-not-trait "Category=OsInterface"`) und `dotnet test FinanceManager.Tests.Integration` vollständig | Testläufe selbst sind der Nachweis | Abgedeckt |
| Laufzeitwirkung: versäumte geplante Installation wird unmittelbar nachgeholt (Upstream-Fix #85) | Bibliotheksverhalten in `AutoUpdateSchedulerService`, informativ als eigener Programmablauf dokumentiert; Szenario 2 mit Ausgangszustand (Zeitpunkt verstrichen, Paket bereit/Prozess down) | FM-seitig nicht automatisierbar (HostedService via `Updates:HostedServicesEnabled=false` deaktiviert, nicht UI-ansteuerbar) — begründet auf Upstream-Tests + reale Abnahme im Betrieb verwiesen; indirekt über grüne Adapter-/Integrationstests | Abgedeckt |
| `CHANGELOG.md`-Eintrag unter `[Unreleased]` → `Changed` inkl. Hinweis auf Nachhol-Verhaltensänderung | Programmablauf „Bereinigung und Dokumentation" Schritt 2, Reihenfolge Schritt 6 | `git status`/Diff (Szenario 1) | Abgedeckt |
| Verwaiste `msTools.Updater.0.10.0-rc.1.nupkg` entfernen (nach Referenzsuche; nur dokumentarische Nennungen) | Reihenfolge Schritt 5: Referenzsuche `.csproj`/`NuGet.config`/Skripte/Workflows, dann Löschung; zusätzlich `0.10.1.nupkg` per bestätigter Anwender-Entscheidung (Ausschluss 5 zurückgenommen) | Referenzsuche 0 Build-Verweise + anschließender Restore/Build (Szenario 4) | Abgedeckt |
| Persistierte Laufzeitdateien der Vorversion bleiben lesbar (`status.json` im 0.10.1-Schema) | Scope-Klärung „wiederverwenden"; neuer Integrationstest `Status_ReadsStatusJsonWrittenByOlderSchema_ReportsMappedStatus` in `UpdateControllerIntegrationTests` (Literal-JSON im 11-Feld-Schema, `GET /api/setup/update/status` als Admin, gemapptes `UpdateStatusDto` assertiert); Hilfsmethoden `SetDownloadPath`/`AuthenticateAdminAsync` benannt | Konkreter neuer Integrationstest (Abschnitt Tests + Szenario 3) | Abgedeckt |
| Keine Anbindung neuer Bibliotheks-Features (`PreserveFile`/`MergeFile`/`ProtectFile`, IIS-Optionen, `UpdateLogPath`/`ReadyToInstallAt`) — es sei denn kompilerzwingend | Scope-Ausschluss 1, bestätigt; verifiziert nicht für Kompilierbarkeit erforderlich | Volle Testsuiten ohne Feature-Anbindung | Abgedeckt |
| Keine Änderung an REST-API (`/api/setup/update/*`), `IUpdateOrchestrator`, Update-DTOs, `ApiClient`, `SetupUpdateViewModel`, `SetupUpdateTab.razor` | Scope-Ausschluss 2, bestätigt; „Änderungen an bestehenden Klassen: Keine" | Regression `UpdateSetupPlaywrightTests` + unveränderte Integrations-/Unit-Suiten | Abgedeckt |
| Keine neue Benutzerinteraktion; Nachhollogik wirkt im Hintergrund | Scope-Ausschluss 3, bestätigt | Begründet keine neuen E2E-Tests (kein UI-erreichbarer geänderter Ablauf) | Abgedeckt |
| Keine Konfigurierbarkeit der Nachhollogik; neue `AutoUpdateOptions`-Felder laufen mit Bibliotheks-Defaults | Scope-Ausschluss 4, bestätigt; Abschnitt „Konfigurationsänderungen: Keine" (`BindConfiguration(UpdateOptions.SectionName)` unverändert) | Bestehende Settings-/Options-Tests laufen unverändert | Abgedeckt |
| Veraltete Versionsverweise in `Docs/help/` aktualisieren | Reihenfolge Schritt 6: `updates/installation.md` (Z. 8, 70, 132), `updates/ablauf-technisch.md` (Z. 7), `systemverwaltung-und-setup/installation.md` (Z. 190–191), `systemverwaltung-und-setup/ablauf-technisch.md` (Z. 291–293, `v0.3.0`-DLL-Vendoring → `.nupkg`-Verfahren) — deckt Inventur-Tabelle vollständig ab | `git status`/Diff | Abgedeckt |
| `Docs/maintenance/dependency-upgrade-process.md` als Dokumentationsnacharbeit auf `.nupkg`-Verfahren modernisieren | Reihenfolge Schritt 6 | `git status`/Diff | Abgedeckt |
| Betriebsrisiko Nachhollogik / Sprungweite / neuer Fehlercode (offene Fragen der Anforderung) | Abschnitt „Seiteneffekte und Risiken": Nachhol-Verhaltensänderung (CHANGELOG-Pflicht), neue `AutoUpdateOutcome`-Member `Canceled`/`Skipped` mit Pfad-Analyse in `CheckAsync`/`StartInstallAsync`, `AutoUpdateErrorCode.InstallationVerificationInconclusive`/`FailedMarkerFileName` unbenutzt, transitive `System.Management`-Abhängigkeit | Risiko benannt; `IAutoUpdatePackageStore` nur gemockt, `AutoUpdateOutcome` nur `==`-Vergleiche — kein Handlungsbedarf begründet | Abgedeckt |

## Fehlende oder unvollständige Testanforderungen

—

## E2E-Abdeckung

| Benutzerfluss / Akzeptanzkriterium | Geplanter E2E-Test | Status |
|------------------------------------|--------------------|--------|
| Update-Setup-UI bleibt nach Paket-Upgrade funktionsfähig | Bestehende `UpdateSetupPlaywrightTests` (`FinanceManager.Tests.E2E/Tests/Setup/UpdateSetupPlaywrightTests.cs`) als Regressionslauf in Reihenfolge Schritt 4 | Abgedeckt (Regression) |
| Nachhol-Logik versäumter Installationen (Laufzeitwirkung) | Kein E2E-Test | Nicht erforderlich mit Begründung: `AutoUpdateSchedulerService` ist interner Bibliotheks-HostedService, in Tests via `Updates:HostedServicesEnabled=false` deaktiviert und über keine UI-/Nutzeraktion erreichbar; reale Abnahme im Betrieb |
| Neue/geänderte Benutzerinteraktion | Keine neuen E2E-Tests | Nicht erforderlich mit Begründung: Anforderung enthält explizit keine neue Benutzerinteraktion; REST-API, DTOs, ViewModel und `SetupUpdateTab.razor` bleiben unverändert |

## Fehlende oder unvollständige Planbestandteile

—

## Hinweise

- **Präzisierung Scope-Klärung:** Der Satz zu persistierten Laufzeitdateien behauptet pauschal „durch neuen Kompatibilitätstest abgesichert". Der geplante Test prüft faktisch nur `status.json`. Für `settings.json` (eigene DTOs unverändert), `update.lock` (Zeitstempel) und den persistierten Paket-Descriptor (`AutoUpdatePackageDescriptor`-Signatur laut Plan verifiziert unverändert) ist das sachlich plausibel — der Wortlaut könnte jedoch präzisiert werden (z. B. „status.json durch neuen Test abgesichert, übrige Dateien schemastabil verifiziert").
- **Formulierung „Änderungen an bestehenden Klassen: Keine":** Gemeint sind kompatibilitätsbedingte Änderungen — es wird jedoch ein neuer Test in `UpdateControllerIntegrationTests` ergänzt. Eine präzisere Trennung („keine Änderungen am Bestandscode; ein neuer Test kommt hinzu") vermeidet Missverständnisse.
- **Ausschluss 4 nennt `UpdateUnitName` als nicht angebundenes neues Feld:** Laut Inventur existiert das Feld bereits in 0.10.1 und wird bereits über `WithUpdateUnitName("FinanceManagerUpdate")` (`ProgramExtensions` Z. 570–595) angebunden. Sachlich richtig bleibt: kein neu eingeführtes Feld wird angebunden — die Einordnung als „neu" ist ungenau.
- **Node-Skript-Tests** (`npm run test:release-version`) sind vom Paketwechsel nicht betroffen und im Plan nicht genannt — kein Handlungsbedarf, für CI-Vollständigkeit als „nicht betroffen" erwähnenswert.
- **`System.Management`-Transitivität:** Risiko benannt; die empfohlene einmalige Verifikation eines Publish-/Runtime-Builds auf dem Zielsystem ist nicht als Schritt der Umsetzungsreihenfolge verankert — bei Bedarf nachziehen.
