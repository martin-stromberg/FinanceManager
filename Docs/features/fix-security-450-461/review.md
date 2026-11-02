# Plan-Review (Iteration 2)

## Ergebnis

**Status:** Offene Aufgaben vorhanden

## Umgesetzte Planelemente

Alle in Iteration 1 (`review.1.md`) als umgesetzt bestätigten Planelemente bleiben unverändert vorhanden; die drei damals offenen Punkte wurden in Iteration 2 wie folgt geschlossen:

### Neue Klassen / Dateien (Iteration-2-Zugänge)

- [x] `FinanceManager.Tests.E2E/Tests/Auth/RegistrationGatePlaywrightTests.cs` — angelegt: `RegisterPage_WhenSelfRegistrationDisabled_ShouldRedirectToLogin` (UI-Redirect `/register` → `/login` bei vorhandenem Benutzer) und `RegisterApi_WhenSelfRegistrationDisabled_ShouldReturnForbidden` (API → 403 + `Err_RegistrationDisabled`); nutzt `TestUserSeeder` zum Vorbelegen eines Nicht-Erstbenutzers
- [x] `FinanceManager.Tests.E2E/Infrastructure/RegistrationGateWebAppFixture.cs` — angelegt: isolierter Host mit `Auth__AllowSelfRegistration=false`; Overrides werden in `PlaywrightWebAppFixture` (Zeile 347–350) **nach** den Defaults angewendet und gewinnen damit gegen `Auth__AllowSelfRegistration=true` (Zeile 330); `RegistrationGateCollection` serialisiert die Tests
- [x] `FinanceManager.Infrastructure/Auth/AuthErrorCodes.cs` — angelegt: `RegistrationDisabled = "Err_RegistrationDisabled"` (schließt die Fehlercode-Semantik aus Iteration-1-Review)

### Geänderte Klassen / Dateien (Iteration-2-Nachbesserungen aus `review-code.1.md`)

- [x] `.gitignore` — `Backup*/` → `/Backup*/` (Zeile 274): `FinanceManager.Web/Infrastructure/Backups/BackupUploadSizeLimitAttribute.cs` ist nicht mehr ignoriert und damit committbar (`git check-ignore` exit 1); `[BackupUploadSizeLimit]` auf `BackupsController.UploadAsync` ist damit checkout-sicher
- [x] `ProgramExtensions.ConfigureMiddleware` — `ForwardedHeadersOptions` mit `ForwardLimit = 1`, `KnownProxies`/`KnownNetworks` geleert und aus `ReverseProxy:KnownProxies`/`ReverseProxy:KnownNetworks` (appsettings-Section vorhanden) befüllt; Rate-Limit-Partition und `IpBlockMiddleware` können nicht mehr per gefälschtem `X-Forwarded-For` umgangen werden
- [x] `UserAuthService.RegisterAsync` — nach `CreateAsync` werden alle Folge-Schreibvorgänge (Domain-User, Re-Check/Admin-Strip, Self-Contact) mit `CancellationToken.None` (`commit`) ausgeführt → keine verwaisten Identity-User bei Request-Abbruch
- [x] `AuthController.RegisterAsync` — `Err_RegistrationDisabled` wird auf HTTP 403 gemappt (statt 409 `Err_Conflict_UserAlreadyExists`)
- [x] `CookieCsrfProtectionMiddleware.RequiresValidation` — `IAllowAnonymous`-Metadaten werden vor `IAuthorizeData` geprüft (Zeile 69–72) → anonyme Unsafe-Endpunkte unter `[Authorize]`/Fallback-Policy verlangen kein Antiforgery-Token
- [x] `CircuitAuthTokenStore.TryTakeHandoff` — Lookup + Remove unter `_handoffLock` (Zeile 65–74) → Single-Use-Garantie atomar; `Size = 1` auf Cache-Einträgen (SizeLimit-Kompatibilität)
- [x] `JwtRefreshMiddleware` — `<remarks>` aktualisiert (Cookie + serverseitige Registry, keine Header-Exposition mehr)
- [x] `CookieCsrfProtectionTests` — toter Code (`await Task.CompletedTask`) entfernt

### Bestätigte Planelemente aus Iteration 1 (Stichproben-Verifikation, unverändert)

- [x] `UserAuthService`: `s_registrationGate`-Semaphore, Gate `!isFirst && !AllowSelfRegistration` → `Result.Fail(AuthErrorCodes.RegistrationDisabled)`, Re-Check mit `SetAdmin(false)`, Demo-Data an korrigiertes `isFirst` gebunden (#450/#456)
- [x] Pipeline-Reihenfolge in `ConfigureMiddleware` (`ProgramExtensions.cs:492–597`): `UseForwardedHeaders` → `IpBlockMiddleware` → `UseRateLimiter` → `E2E:*` nur unter `IsDevelopment()` → globaler Security-Header-Block mit `HelpSecurityPolicy`-CSP-Vorrang auf `/help` → `UseAuthentication` → `UseAuthorization` → `UseAntiforgery` → `CookieCsrfProtectionMiddleware` (#453/#456/#459/#461)
- [x] `X-Auth-Token`/`AcceptBrowserToken`/`ReceiveTokenAsync`/`setTokenReceiver`/`RequestSizeLimit(long.MaxValue)` im Produktivcode vollständig entfernt (verbleibende Treffer nur noch in Abwesenheits-Assertions der `ApiClientAuthTests` und in Doku) (#451/#455/#460)
- [x] Alle übrigen Planelemente gemäß `review.1.md` (Mass-Import-Limits, `AttachmentUploadSizeLimit`-Umstellung, `BackupService`-Isolation + `SchemaPatcher.MigrateBackupStorageAsync`, `SecurityHeadersPolicy`, `ExportFileHelper.CsvEscape`, `JwtCookieAuthTokenProvider` mit `PersistedTokenKey`/Registry, `financeManager.js`/`AuthRedirect.razor`/`App.razor`/`attachments.js`, appsettings-Änderungen, alle Unit-/Integrationstests, `TestWebApplicationFactory`-Overrides)

## Offene Aufgaben

- [ ] E2E-Regressionslauf der Bestandssuiten (`HomeMassImportPlaywrightTests`, `CollectionAccountImportPlaywrightTests`, `BudgetReportDemoDataE2ETests`, `StatementDraftExportE2ETests`, `AccountsOverviewStatisticsPlaywrightTests`, neu: `RegistrationGatePlaywrightTests`) — teilweise umgesetzt: `FinanceManager.Tests.E2E` kompiliert fehlerfrei und `test-results.md` dokumentiert den Lauf als „in CI", ein tatsächlich ausgeführter E2E-Lauf ist im Working Tree jedoch nicht nachweisbar; Unit- (1520/1520) und Integrationstests (150/150) sind grün

## Hinweise

- `FinanceManager.Web/backups/` (Laufzeit-Backupdaten, div. `.zip` + GUID-Verzeichnisse) erscheint nun als untracked-committbar, weil `/Backup*/` nur noch root-verankert greift — ggf. eine Ignore-Regel für das Laufzeit-Verzeichnis ergänzen, damit keine Testdaten versehentlich committet werden.
- `E2ESwitchesProductionTests` setzt `ReverseProxy:KnownProxies`/`KnownNetworks`-Overrides — die neue Forwarded-Headers-Konfiguration ist damit testseitig abgedeckt.
- `AllowedHosts` in `appsettings.Production.json` bleibt bewusster Platzhalter (`localhost`) gemäß Offenem Punkt 2 des Plans.
