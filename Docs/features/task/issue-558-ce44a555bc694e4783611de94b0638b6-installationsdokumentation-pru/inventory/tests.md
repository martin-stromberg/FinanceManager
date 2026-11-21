# Bestandsaufnahme — Tests

## Test-Ausgangszustand vor der Umsetzung

- Zeitpunkt (mit Zeitzone): 2026-11-21, Läufe zwischen 12:27 und ~13:00 Uhr +0100 (CET)
- Branch und Commit-ID: `task/issue-558-ce44a555bc694e4783611de94b0638b6-installationsdokumentation-pru` @ `44e9202c5a9999e4395ec99ffa110ee9aa45916b` („fix: make remaining auth/help/attachment API calls PathBase-relative (#557)")
- Uncommittete Änderungen im getesteten Stand: keine Produktiv-/Teständerungen; lediglich das untracked-Verzeichnis `Docs/features/task/issue-558-.../` (requirement.md, diese Inventory-Dateien und die Testnachweise selbst)
- Testumgebung und Runtime-/SDK-Versionen: Windows 11 (10.0.26300, win-x64), .NET SDK 11.0.100 (MSBuild 18.12.2), Projektziel `net10.0`; Playwright-Browser lokal installiert (`~/AppData/Local/ms-playwright`, u. a. chromium-1248); Testrunner `Microsoft.Testing.Platform` (`global.json`)
- Ermittelte Testsuiten und Quellen der Testbefehle: `.github/workflows/pr-staging-ci.yml` und `staging-ci.yml` definieren drei Suiten — `FinanceManager.Tests` (Unit, mit Filter `--filter-not-trait "Category=OsInterface"`), `FinanceManager.Tests.Integration`, `FinanceManager.Tests.E2E` (Playwright). Dieselben Befehle wurden lokal in `-c Debug` ausgeführt (mit Build; Coverage-Report-Flags weggelassen, TRX stattdessen ins Inventory-Verzeichnis).

### Testläufe

| Lauf | Befehl inkl. Filter | Arbeitsverzeichnis | Exit-Code | Erfolgreich | Fehlgeschlagen | Übersprungen | Nachweis |
|------|--------------------|--------------------|-----------|-------------|----------------|--------------|----------|
| Unit | `dotnet test FinanceManager.Tests/FinanceManager.Tests.csproj -c Debug --results-directory <inv>/test-results -- --filter-not-trait "Category=OsInterface" --report-xunit-trx --report-xunit-trx-filename unittests-baseline.trx` | Repo-Root | 0 | 2410 | 0 | 0 | [Log](test-results/unittests-baseline.log), [TRX](test-results/unittests-baseline.trx) |
| Integration | `dotnet test FinanceManager.Tests.Integration/FinanceManager.Tests.Integration.csproj -c Debug --results-directory <inv>/test-results -- --report-xunit-trx --report-xunit-trx-filename integrationtests-baseline.trx` | Repo-Root | 0 | 176 | 0 | 0 | [Log](test-results/integrationtests-baseline.log), [TRX](test-results/integrationtests-baseline.trx) |
| E2E (Playwright) | `dotnet test FinanceManager.Tests.E2E/FinanceManager.Tests.E2E.csproj -c Debug --results-directory <inv>/test-results -- --report-xunit-trx --report-xunit-trx-filename e2etests-baseline.trx` | Repo-Root | 2 | 167 | 9 | 0 | [Log](test-results/e2etests-baseline.log), [TRX](test-results/e2etests-baseline.trx) |

`<inv>` = `Docs/features/task/issue-558-ce44a555bc694e4783611de94b0638b6-installationsdokumentation-pru/inventory`. Dauer: Unit ~48 s, Integration ~6 min 13 s, E2E ~20 min 12 s.

### Nachgewiesene bestehende Testfehler

Alle Fehler stammen aus dem E2E-Lauf (Unit und Integration fehlerfrei). Fehlerbild überwiegend Playwright-Timeouts auf Link-/Button-Selektoren mit führendem Slash (z. B. `a[href='/list/accounts']`, `button#Save`) — auffällig konsistent mit dem unmittelbar vorausgehenden PathBase-Commit `44e9202c` (#557); ohne gesonderten Gegenlauf auf dem Vorgängercommit ist die Ursache nicht abschließend belegt, die Fehler sind aber auf diesem Stand nachgewiesen.

| Test-ID inkl. Testfall | Suite / Dateipfad | Fehlerbild / Fehlermeldung | Lauf und Nachweis |
|-----------------------|------------------|---------------------------|-------------------|
| `FinanceManager.Tests.E2E.HomeMassImportPlaywrightTests.UploadIngCsvNewFormat_ViaUi_ShouldImportWithoutReviewDialog` | `FinanceManager.Tests.E2E/Tests/Import/HomeMassImportPlaywrightTests.cs:153` | `href` `"card/statement-drafts/<guid>"` enthält nicht `"/card/statement-drafts/"` (fehlender führender Slash) | E2E-Lauf, TRX |
| `FinanceManager.Tests.E2E.KpiLocalStorageE2ETests.DisableCache_RemovesKpiDataFromLocalStorage` | `FinanceManager.Tests.E2E/Tests/Home/KpiLocalStorageE2ETests.cs:121` | `TimeoutException`: 10 s Warten auf `button#Save:enabled` | E2E-Lauf, TRX |
| `FinanceManager.Tests.E2E.AccountsOverviewStatisticsPlaywrightTests.AccountsOverview_StatisticsFailure_ShowsErrorAndLeavesTableUsable` | `FinanceManager.Tests.E2E/Tests/Accounts/AccountsOverviewStatisticsPlaywrightTests.cs:395` | `TimeoutException`: 10 s Warten auf `.accounts-statistics` Button | E2E-Lauf, TRX |
| `FinanceManager.Tests.E2E.LoadingBarPlaywrightTests.InternalLinkClick_ShouldStartLoadingBarAutomatically` | `FinanceManager.Tests.E2E/Tests/Navigation/LoadingBarPlaywrightTests.cs:156` | `TimeoutException`: 10 s Warten auf `nav.sidebar a[href='/list/accounts']` | E2E-Lauf, TRX |
| `FinanceManager.Tests.E2E.SessionExpiryPlaywrightTests.UiLogin_FirstHomeApiAndInteractiveNavigationRemainAuthenticated` | `FinanceManager.Tests.E2E/Tests/Auth/SessionExpiryPlaywrightTests.cs:21` | `TimeoutException`: 10 s Warten auf `a[href='/list/accounts']` | E2E-Lauf, TRX |
| `FinanceManager.Tests.E2E.SessionExpiryPlaywrightTests.Idle_NoKeepalive_ThenAccountsActionRedirectsWithReturnUrl` | `FinanceManager.Tests.E2E/Tests/Auth/SessionExpiryPlaywrightTests.cs:86` | `TimeoutException`: 10 s Warten auf `a[href='/list/accounts']` (Laufzeit 3m27s) | E2E-Lauf, TRX |
| `FinanceManager.Tests.E2E.SessionExpiryPlaywrightTests.RejectedKeepalive_StaysUntilProtectedAccountsAction` | `FinanceManager.Tests.E2E/Tests/Auth/SessionExpiryPlaywrightTests.cs:109` | `TimeoutException`: 10 s Warten auf `a[href='/list/accounts']` | E2E-Lauf, TRX |
| `FinanceManager.Tests.E2E.SessionExpiryPlaywrightTests.ActiveNavigation_BeyondOriginalExpiry_UsesRenewedBrowserAndCircuitToken` | `FinanceManager.Tests.E2E/Tests/Auth/SessionExpiryPlaywrightTests.cs:45` | `TimeoutException`: 10 s Warten auf `a[href='/list/accounts']` | E2E-Lauf, TRX |
| `FinanceManager.Tests.E2E.BudgetReportDemoDataE2ETests.DemoDataBudgetReport_PreviousMonth_ShouldContainAllExpectedRows(clickDispatchDelayMilliseconds: 750)` | `FinanceManager.Tests.E2E/Tests/Budget/BudgetReportDemoDataE2ETests.cs:180` | `TimeoutException`: 30 s `WaitForNavigation` nach Registrierungs-Submit | E2E-Lauf, TRX |

### Testlücken und Ausführungsprobleme

- Unit-Lauf mit `--filter-not-trait "Category=OsInterface"`: OS-abhängige Tests mit Trait `OsInterface` wurden wie in CI nicht ausgeführt (Umfang/Anzahl nicht ermittelt — kein eigener Zähllauf).
- Keine übersprungenen Tests in den ausgeführten Läufen; keine Build-/Setup-Fehler. Build-Warnungen: NU1510 (PackageReference-Kürzung) und diverse xUnit-Analyzer-Hinweise — bekannte, in CI tolerierte Warnungen.
- Der E2E-Lauf benötigt lokal installierte Playwright-Browser und startet eine echte `FinanceManager.Web`-Instanz (`PlaywrightWebAppFixture`); er ist hier vollständig gelaufen.

## Testklassen (anforderungsrelevant)

### `HelpContentCatalogTests` — `FinanceManager.Tests/Web/Help/HelpContentCatalogTests.cs`

- `Topics_ContainsReviewedUserHelpTopicsOnly` — assertiert exakt 13 Topics, Enthalten von `konten-und-buchungen`, Ausschluss von `bestandsaufnahme` (Zeilen 34–40).
- `TryResolveDocument_RejectsTechnicalOnlyDocuments` (Theory: `api`, `datenmodell`, `business-rules`, `ablauf-technisch`, `bereitstellung`) — technische Dateinamen dürfen nicht über `/help/view/systemverwaltung-und-setup/<doc>` auflösbar sein (Zeilen 47–67). **Kein `InlineData`-Eintrag für eine neue Datei nötig**, solange sie nicht im Katalog publiziert wird — nicht auflösbare Dokumente sind ohnehin nicht erreichbar.
- `TryResolveDocument_MapsTopicRouteToReviewedPrimaryDocument` — Topic-Route → Primärdokument + Dateipfad (Zeilen 73–90).
- `HelpSearchIndexBuilder_UsesCatalogTopicsAndPrimaryDocuments` — Suchindex = genau Topic-Ids (Zeilen 97–109).
- `CatalogDocuments_ExistAndDoNotPublishTechnicalOnlyFiles` — kein publiziertes Dokument aus `TechnicalOnlyDocumentNames`; jedes Katalog-Dokument existiert auf Disk (Zeilen 117–136).
- `CatalogDocuments_DoNotContainTechnicalImplementationMarkers` — publizierte Dokumente dürfen Marker wie `Controller`, `Endpunkt`, `appsettings`, `Jwt:`, `JWT`, `ViewModel`, `RepositoryOwner`, `ExecutablePath` **nicht** enthalten (Zeilen 144–164). Die neue Server-Installationsanleitung enthält solche Marker zwangsläufig → darf **nicht** in `Topics`/`SetupDocuments` landen; die vorgesehene Ergänzung von `TechnicalOnlyDocumentNames` löst keinen Testfehler aus (das HashSet wird nur als Ausschlussliste geprüft).
- Hilfsmethode `GetDocsHelpPath` — löst `Docs/help` relativ zum Test-Output auf (Zeilen 166–176).

### Weitere berührte Testbereiche

- Keine. Die Anforderung ist eine Dokumentationsaufgabe mit einer minimalen Katalog-Ergänzung; es existieren keine Tests, die `index.md`-Inhalte oder Markdown-Verlinkungen prüfen.

## Hilfsmethoden

- `PlaywrightWebAppFixture` (`FinanceManager.Tests.E2E/Infrastructure/PlaywrightWebAppFixture.cs`): startet die gebaute `FinanceManager.Web.dll` als echten Prozess auf freiem Port (WorkingDirectory = `FinanceManager.Web`-Quellordner, Zeilen 316, 461–473).
