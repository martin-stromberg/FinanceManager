# Offene Aufgaben

Erstellt am: 2026-08-03
Abbruchgrund: Maximale Iterationsanzahl erreicht

Die folgenden Aufgaben konnten im automatisierten Zyklus nicht abgeschlossen werden
und müssen manuell oder in einem erneuten Lauf bearbeitet werden.

## Offene Planelemente

- [ ] Branch-Protection-Rules manuell konfigurieren (GitHub Web-UI oder API)
- [ ] Bestehende offene PRs überprüfen und zu `staging` umleiten
- [ ] Team benachrichtigen über neuen Workflow
- [ ] Lokale Entwickler-Setups aktualisieren (Dokumentation abgedeckt, manuelle Umsetzung)
- [ ] Manueller Verifizierungslauf durchführen

## Code-Review-Befunde

- [ ] `.github/workflows/staging-to-master.yml` (Job `promote`): Der Schritt „Ensure automated-promotion label exists" ruft `gh label create` auf, was die Token-Berechtigung `issues: write` voraussetzt. Der `permissions`-Block gewährt aktuell nur `contents: read` und `pull-requests: write`, wodurch der Schritt beim ersten Lauf mit 403 fehlschlägt. Empfehlung: `issues: write` zum `permissions`-Block hinzufügen.
- [ ] `.github/workflows/staging-to-master.yml` (Job `promote`): Kein `concurrency`-Block vorhanden. Bei zwei nahezu zeitgleichen erfolgreichen „Tests"-Läufen auf `staging` können zwei parallele `promote`-Jobs entstehen, von denen einer beim `gh pr create` fehlschlägt. Empfehlung: `concurrency: { group: staging-to-master-promotion, cancel-in-progress: false }` ergänzen.
- [ ] `FinanceManager.Web/wwwroot/help/help-assets.sha256`: Hash-Werte für mehrere Dateien unter `Docs/help/systemverwaltung-und-setup/` weichen vom Stand auf `master` ab, obwohl die referenzierten Markdown-Dateien inhaltlich unverändert sind. Ursache: Der `GetFileHash`-MSBuild-Task hasht auf einem Windows-Checkout mit `core.autocrlf=true` zeilenendungsabhängig (CRLF statt LF wie im committeten Blob), wodurch das Manifest bei jedem lokalen Windows-Build erneut driftet. Diese Datei sollte vor dem finalen Commit auf den `master`-Stand zurückgesetzt werden; die zugrundeliegende Zeilenenden-Sensitivität des `GetFileHash`-Tasks ist ein vorbestehendes, feature-fremdes Problem und sollte separat behoben werden (z. B. `.gitattributes`-`eol=lf` für `Docs/help/**/*.md`).

## Fehlgeschlagene Tests

- [ ] `FinanceManager.Tests.E2E.UpdateSetupPlaywrightTests.Admin_OpensUpdateTab_ShowsStatus` — System.TimeoutException: Timeout 10000ms exceeded. waiting for Locator(".setup-update-tab [data-testid='update-status-value']"). Vermutlich vorbestehende Regression aus der vorherigen Updater-Auslagerung (Commit „feat: updater in separates repository auslagern"), nicht durch diese Anforderung verursacht.
- [ ] `FinanceManager.Tests.E2E.UpdateSetupPlaywrightTests.Admin_SavesSettings_PersistsAcrossReload` — System.TimeoutException: Timeout 10000ms exceeded. waiting for Locator(".setup-update-tab [data-testid='update-save-settings']"). Gleiche vermutete Ursache wie oben.
- [ ] `FinanceManager.Tests.E2E.UpdateSetupPlaywrightTests.Admin_TriggersCheck_ShowsAvailableUpdate` — System.TimeoutException: Timeout 10000ms exceeded. waiting for Locator(".setup-update-tab [data-testid='update-check-now']"). Gleiche vermutete Ursache wie oben.
