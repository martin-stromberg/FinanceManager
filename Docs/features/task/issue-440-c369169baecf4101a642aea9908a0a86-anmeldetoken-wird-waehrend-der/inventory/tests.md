## Test-Ausgangszustand vor der Umsetzung

- **Zeitpunkt der Zustandsaufnahme (mit Zeitzone):** 2026-10-23 19:53:54 +02:00. Testläufe fanden am selben Datum statt; die Laufprotokolle enthalten ihre Erstellungs- und Endzeit.
- **Branch und Commit-ID:** `task/issue-440-c369169baecf4101a642aea9908a0a86-anmeldetoken-wird-waehrend-der`, `e88cf3e4adb0a71042463a358c15d57c652d82da`. Der angeforderte Basisbranch `staging` war derselbe Commit; `staging` war Vorfahr von `HEAD` (Merge-Base ebenfalls `e88cf3e4adb0a71042463a358c15d57c652d82da`).
- **Uncommittete Änderungen im getesteten Stand:** Vor den Testläufen zeigte `git status --porcelain` ausschließlich das untracked Feature-Verzeichnis. Es enthielt `requirement.md` (SHA-256 `4007B4495718E1A71D7027CF1BAFF044712193F4E8CCA80DA5B2CAC4A54A724C`) und `todo.md` (SHA-256 `7949F1D42892D8E620DD6B589604CB5845502F3EA506B7D9A20D98E57EEAEE2A`). Die zugehörige, getrackte Issue-Beschreibung liegt im Repository-Root als `issue.md` (SHA-256 `E7AB4651E2C564651E69B9ACCC89885EC3A790FB002E62FBA4CE250FAA9957E1`). Produktivcode, Tests und Testkonfiguration waren unverändert.
- **Testumgebung und Runtime-/SDK-Versionen:** Windows 10.0.26300, `win-x64`; .NET SDK 10.0.402, .NET Runtime 10.0.13. Node.js 24.15.0 war installiert; Node wurde in den Testläufen nicht verwendet. Das Playwright-Projekt ist auf den Browserkanal `msedge` voreingestellt. Der E2E-Server wurde gestartet, aber der Healthcheck scheiterte vor dem Browserstart. `dotnet dev-certs https --check` meldete `No valid certificate found.`
- **Ermittelte Testsuiten und Quellen der Testbefehle:** `FinanceManager.Tests`, `FinanceManager.Tests.Integration` und `FinanceManager.Tests.E2E`. Die Projekt-/Befehlsquellen sind [Docs/development.md](../../../../development.md), [.github/workflows/pr-staging-ci.yml](../../../../../.github/workflows/pr-staging-ci.yml), [.github/workflows/staging-ci.yml](../../../../../.github/workflows/staging-ci.yml), `global.json` und die drei jeweiligen `.csproj`-Dateien. Der Integrationsordner `FinanceManager.Tests.Integration/ApiClient` ist Teil der Integration-Suite; es existiert kein separates `FinanceManager.Tests.Integration.ApiClient.csproj`.

### Testläufe

| Lauf | Befehl inkl. Filter | Arbeitsverzeichnis | Exit-Code | Erfolgreich | Fehlgeschlagen | Übersprungen | Nachweis |
|------|--------------------|--------------------|-----------|-------------|----------------|--------------|----------|
| Unit, erster Baseline-Lauf | `dotnet test FinanceManager.Tests/FinanceManager.Tests.csproj --no-build -c Debug --results-directory Docs/features/task/issue-440-c369169baecf4101a642aea9908a0a86-anmeldetoken-wird-waehrend-der/inventory/test-results -- --filter-not-trait "Category=OsInterface" --report-xunit-trx --report-xunit-trx-filename baseline-unit.trx` | Repository-Root | 2 | 1467 | 1 | 0 | [Log](test-results/unit.log), [TRX](test-results/baseline-unit.trx) |
| Integration | `dotnet test FinanceManager.Tests.Integration/FinanceManager.Tests.Integration.csproj --no-build -c Debug --results-directory Docs/features/task/issue-440-c369169baecf4101a642aea9908a0a86-anmeldetoken-wird-waehrend-der/inventory/test-results -- --report-xunit-trx --report-xunit-trx-filename baseline-integration.trx` | Repository-Root | 0 | 137 | 0 | 0 | [Log](test-results/integration.log), [TRX](test-results/baseline-integration.trx) |
| E2E | `dotnet test FinanceManager.Tests.E2E/FinanceManager.Tests.E2E.csproj --no-build -c Debug --results-directory Docs/features/task/issue-440-c369169baecf4101a642aea9908a0a86-anmeldetoken-wird-waehrend-der/inventory/test-results -- --report-xunit-trx --report-xunit-trx-filename baseline-e2e.trx` (Umgebung: `PLAYWRIGHT_TRACE=1`, `PLAYWRIGHT_ARTIFACTS=1`) | Repository-Root | 2 | 0 | 112 | 0 | [Log](test-results/e2e.log), [TRX](test-results/baseline-e2e.trx), [vollständige fehlgeschlagene IDs](test-results/e2e-failures.md) |
| Unit-Wiederholung nach Solution-Build, auf ausdrücklichen Wunsch des Lifecycle-Koordinators abgebrochen | `dotnet test FinanceManager.Tests/FinanceManager.Tests.csproj --no-build -c Debug --results-directory Docs/features/task/issue-440-c369169baecf4101a642aea9908a0a86-anmeldetoken-wird-waehrend-der/inventory/test-results -- --filter-not-trait 'Category=OsInterface' --report-xunit-trx --report-xunit-trx-filename baseline-unit-after-build.trx` | Repository-Root | 1 | unbekannt | unbekannt | unbekannt | [Ausgabe bis zum Abbruch](test-results/unit-after-build.log); vor dem Abbruch kein TRX erstellt |

Die Baseline-Aufrufe entsprechen dem Umfang der CI-Suiten und behalten deren Unit-Filter bei. `--coverage` aus der CI wurde nicht gesetzt. Der erste Unit-Lauf verwendete die zum Arbeitsstand vorhandenen Debug-Binaries; anschließend wurden die Testprojekte und die Solution ohne Restore erfolgreich gebaut, danach liefen Integration und E2E. Es gab keine Änderungen an Produktivcode, Tests oder Testkonfiguration.

### Nachgewiesene bestehende Testfehler

| Test-ID inkl. Testfall | Suite / Dateipfad | Fehlerbild / Fehlermeldung | Lauf und Nachweis |
|-----------------------|------------------|---------------------------|-------------------|
| `FinanceManager.Tests.Web.Help.HelpAssetIntegrityValidatorTests.BuildManifest_CoversAndHashesAllDeliveredHelpAssets` | Unit; `FinanceManager.Tests/Web/Help/HelpAssetIntegrityValidatorTests.cs:170` | `Assert.Equal()`-Stringabweichung beim Asset-Hash. Erwartet beginnt `3B6E1E8B59629F70A793946A6C4AED244759707F5AF42E6229`; tatsächlich beginnt `D22C4C5BCE7D0C9E70A0C5CD41346F842372683DBF39D3816A`. Der Stacktrace steht im TRX. | Erster Unit-Baseline-Lauf; [TRX](test-results/baseline-unit.trx), [Log](test-results/unit.log) |

Damit ist ein tatsächlicher Unit-Testfehler im Ausgangslauf nachgewiesen. Die 112 als fehlgeschlagen gezählten E2E-Fälle erreichten keine Testaktion: sie scheiterten identisch bei der gemeinsamen Fixture-Einrichtung. Sie sind als Ausführungs-/Infrastrukturproblem und mit allen vollständigen Test-IDs separat in [e2e-failures.md](test-results/e2e-failures.md) erfasst.

### Testlücken und Ausführungsprobleme

- **E2E-Setupfehler:** `PlaywrightWebAppFixture.InitializeAsync` konnte den Testserver nicht als bereit erkennen. Der Probe unter `https://127.0.0.1:<port>/login` scheiterte mit `HttpRequestException` → `AuthenticationException` → `Win32Exception: Im Sicherheitspaket sind keine Anmeldeinformationen verfügbar.` Der Serverlog meldete einen nicht vertrauenswürdigen ASP.NET-Core-Entwicklungszertifikat. Die unabhängige Prüfung `dotnet dev-certs https --check` gab `No valid certificate found.` aus. Der Fixture-Code verwendet bereits `DangerousAcceptAnyServerCertificateValidator`; das Problem trat beim TLS-Aufbau auf. Alle 112 Test-IDs sowie Stacktrace und Report-Verweise stehen in [e2e-failures.md](test-results/e2e-failures.md). Es wurde keine Zertifikat- oder Testumgebung verändert.
- **Solution-Build mit Restore:** `dotnet build FinanceManager.sln` konnte die NuGet-Konfiguration `C:\Users\Martin\AppData\Roaming\NuGet\NuGet.Config` nicht lesen (`Access denied`), Exit 1 mit 9 Setupfehlern. Das vollständige Log ist [solution-build.log](test-results/solution-build.log).
- **Solution-Build ohne Restore:** `dotnet build FinanceManager.sln --no-restore` war erfolgreich (Exit 0, fünf `NU1510`-Warnungen, keine Fehler); [Log](test-results/solution-build-no-restore.log). Die drei CI-Testprojekt-Builds `dotnet build <Testprojekt>.csproj --no-restore -c Debug` waren ebenfalls erfolgreich: [Unit](test-results/unit-build.log), [E2E](test-results/e2e-build.log), [Integration](test-results/integration-build.log).
- **Nicht ausgeführte Testaktionen:** Die E2E-Suite startete nicht bis zum Playwright-Browser, daher gibt es keinen Laufzeitnachweis für Browserlogin/erste Startseiten-API-Anfrage, Navigation nach Ablauf des ursprünglichen Tokens, Inaktivität ohne Keepalive oder Quick-Edit-Fokus bei Refresh.
- **Codebasierte Abdeckungslücken:** Vorhandene E2E-Tests prüfen UI-Login und Near-Expiry-/Quick-Edit-Blur-Fälle. Der Ablauf beobachtet jedoch nicht ausdrücklich die erste geschützte Startseiten-API-Anfrage direkt nach dem UI-Login. In den vorhandenen Tests wurde kein expliziter Inaktivitätsfall gefunden, der fehlende Keepalive-Anfragen und eine spätere geschützte Aktion nach Ablauf gemeinsam nachweist. Ein Szenario für aktive Nutzung über den ursprünglichen Tokenablauf hinweg ist ebenfalls nicht ausgewiesen.
- Git meldete bei diesen Befehlen, dass `C:\Users\Martin/.config/git/ignore` nicht lesbar war; der Git-Status konnte dennoch ermittelt werden. Kein Testlauf wurde dadurch beeinflusst.

### Testklassen

#### `JwtCookieAuthTokenProviderTests`
- `GetAccessTokenAsync_ShouldPreferRequestCookie_WhenCacheContainsDifferentToken` — Request-Cookie hat Vorrang vor einem anderen Cache-Token.
- `GetAccessTokenAsync_ShouldReturnCachedToken_WhenHttpContextIsUnavailable` — gültiger Cache-Token ohne `HttpContext`.
- `GetAccessTokenAsync_ShouldReturnNull_WhenIssuerIsInvalid` und `GetAccessTokenAsync_ShouldReturnNull_WhenAudienceIsInvalid` — Zurückweisung nicht passender JWTs.
- `GetAccessTokenAsync_ShouldUseRefreshService_WhenTokenNearExpiry` — Refresh im Ablaufzeitfenster.
- `GetAccessTokenAsync_ShouldReturnNull_WhenRefreshIsRejected` — abgelehnter Refresh liefert keinen Token.

#### `ApiClientAuthTests`
- `Register_ShouldSetAuthCookie_AndReturnResponse`, `Login_ShouldReturnOk_AndUnauthorized_OnInvalid` und `Logout_ShouldClearCookie` — Register/Login/Logout über den HTTP-API-Client.
- `Bearer_ShouldRejectTokenWithInvalidIssuer`, `Bearer_ShouldRejectTokenWithInvalidAudience`, `Bearer_ShouldAcceptTokenWithConfiguredIssuerAndAudience`, `Bearer_ShouldRejectToken_WhenSecurityStampChanged`, `Bearer_ShouldRejectAdminClaim_WhenCurrentAdminRoleWasRevokedWithoutSecurityStampChange`, `Bearer_ShouldRejectExistingToken_WhenUserWasDeactivated` und `Login_ShouldRejectInactiveUser` — Tokenvalidierung und Benutzerzustand.
- `Keepalive_ActiveUserInteraction_ShouldRefreshNearExpiryCookie` und `JwtRefreshMiddleware_ShouldNotLoopOnRejectedRefresh` — Refresh und Ablehnung.
- `ApiClient_ShouldRaiseAuthenticationRequired_OnUnauthorized`, `ApiClient_ShouldNotRaiseAuthenticationRequired_OnOrdinaryForbidden` und `ApiClient_ShouldRaiseAuthenticationRequired_OnForbiddenWithAuthenticationCode` — Authentifizierungsereignisse bei HTTP-Fehlern.

#### `AuthenticationFlowPlaywrightTests`
- `Register_Login_Logout_Flow_ShouldWork` und `Register_Login_Logout_Flow_ShouldWork_OnMobileViewport` — Browserfluss für Register/Login/Logout.
- `ExpiredSession_OnProtectedRoute_ShouldRedirectToLoginWithReturnUrl_AndReturnAfterLogin`, `LoginWithoutReturnUrl_ShouldNavigateToHome`, `LoginWithInvalidOrExternalReturnUrl_ShouldNavigateToHome` (vier Rücksprung-URLs) und `MultipleAuthenticationFailures_ShouldNavigateToLoginOnlyOnce` — UI-Login, Rücksprungziel und Redirect.
- `ActiveNavigationAndInteraction_ShouldRefreshNearExpirySessionWithoutLoginRedirect` und `Keepalive_FailedRefresh_ShouldNotTriggerLoginRedirect` — Aktivität kurz vor Tokenablauf und fehlgeschlagener Refresh.

#### `StatementDraftQuickEditValueTakeoverE2ETests`
- `QuickEdit_CtrlArrowUp_ShouldFocusSameFieldInPreviousVisibleRow`, `QuickEdit_CtrlArrowDown_ShouldFocusSameFieldInNextVisibleRow`, `QuickEdit_CtrlArrowUpAtFirstRow_ShouldKeepFocus`, `QuickEdit_CtrlArrowDownAtLastRow_ShouldKeepFocus`, `QuickEdit_CtrlArrowOnDateFields_ShouldMoveFocusWithoutChangingDate`, `QuickEdit_CtrlArrowDown_ShouldSkipHiddenDeletedAndLockedRows` und `CtrlArrowNavigation_OutsideQuickEdit_ShouldNotChangeFocus` — Fokusnavigation.
- `QuickEdit_BookingDateChange_ShouldCopyToEmptyOrMatchedValutaDateOnly`, `QuickEdit_SaveButton_IsEnabledWhenAllRowsComplete`, `QuickEdit_RegularInputAndF8_ShouldRemainUnaffected`, `QuickEdit_PressesF8_ShouldCopySingleFieldFromRowAbove` und `QuickEdit_PressesCtrlF8_ShouldCopyAllFieldsFromRowAboveAndOverwriteExisting` — Datumsübernahme, Speichern und Werteübernahme.
- `QuickEdit_Blur_ShouldSendKeepaliveAndKeepLocalInputValue` — Keepalive beim Blur und Erhalt eines ungespeicherten Feldwerts.
- `QuickEdit_Blur_ShouldCoalesceKeepaliveRequests` — Zusammenfassung schneller Blur-Requests.

### Hilfsmethoden

#### `AuthGateway`
- `LoginAsync` führt den Login per API-Request im Seitenkontext aus.
- `LoginThroughUiAsync` füllt Username/Passwort im Browserformular und klickt den Submit-Button; geeignet für echte UI-Login-Prüfungen.
- `LogoutAsync` löscht Browser-Cookies und öffnet die Login-Seite.

#### `TestAuthCookieHelper`
- Setzt für E2E-Szenarien direkt ein nahe am Ablauf befindliches Auth-Cookie. Das ist eine Testhilfe und kein UI-Login-Nachweis.

#### `PlaywrightWebAppFixture` / `PlaywrightTestTiming`
- Die Fixture startet die Webanwendung mit temporärer SQLite-Datenbank und lokalen Update-Daten und öffnet Playwright nach erfolgreichem Server-Healthcheck.
- `WaitForForcedKeepaliveThrottleAsync` wartet 5,2 Sekunden, damit der erzwungene Keepalive-Throttle den nächsten Ping zulässt.
