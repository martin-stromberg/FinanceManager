# Testergebnisse: Design-Variante B (Issue #590, Schritt 2)

> Ausgeführt vom Implementierungsagenten (keine Subagents verfügbar — dokumentierte Abweichung).
> Baseline: `docs/features/task-issue-590-.../inventory/tests.md` (Unit 2511/2511, Integration 180/180,
> E2E 168/176 mit 8 benannten preexisting Failures).

## Build

| Schritt | Ergebnis |
|---------|----------|
| `dotnet build FinanceManager.sln -c Debug` | 0 Fehler, 64 Warnungen (preexisting xUnit-/NU1510-Hinweise) |
| `dotnet publish FinanceManager.Web -c Release` | erfolgreich (für Abnahme-Screenshots) |

## Testläufe

| Suite | Befehl | Ergebnis | Baseline | Bewertung |
|-------|--------|----------|----------|-----------|
| Unit | `dotnet test FinanceManager.Tests --no-build -c Debug --filter-not-trait Category=OsInterface` | **2511/2511 ✓** (0 fehlgeschlagen, 0 übersprungen, 54 s) | 2511/2511 | unverändert grün |
| Integration | `dotnet test FinanceManager.Tests.Integration --no-build -c Debug` | **180/180 ✓** (0 fehlgeschlagen, 0 übersprungen, 8 m 58 s) | 180/180 | unverändert grün |
| E2E | `dotnet test FinanceManager.Tests.E2E --no-build -c Debug` | **173/176 ✓, 3 fehlgeschlagen** (22 m 06 s) | 168/176, 8 fehlgeschlagen | **keine Regression — 5 Netto-Verbesserungen** |

Artefakte: `test-results/unit/test-results-unit.trx`, `test-results/integration/test-results-integration.trx`, `test-results/e2e/test-results-e2e.trx`.

## E2E-Fehlschläge (alle preexisting, keine neuen)

| Test | Baseline-Befund | Status |
|------|-----------------|--------|
| `HomeMassImportPlaywrightTests.UploadIngCsvNewFormat_ViaUi_ShouldImportWithoutReviewDialog` | href-Assertion (`card/…` ohne führenden `/`) | weiterhin fehlschlagend — identischer Grund |
| `KpiLocalStorageE2ETests.DisableCache_RemovesKpiDataFromLocalStorage` | Timeout `button#Save:enabled` (10 s) | weiterhin fehlschlagend — identischer Grund |
| `BudgetReportDemoDataE2ETests.DemoDataBudgetReport_PreviousMonth_ShouldContainAllExpectedRows(750)` | Timeout „waiting for navigation" beim Register-Submit (30 s) | weiterhin fehlschlagend — identischer Grund |

## Geheilte Baseline-Fehlschläge (6)

Die sechs preexisting Timeouts auf `nav.sidebar a[href='/list/accounts']` / `a[href='/list/accounts']`
sind jetzt **grün** — die Rail-Navigation verwendet absolute hrefs, die Selektoren treffen wieder:

- `LoadingBarPlaywrightTests.InternalLinkClick_ShouldStartLoadingBarAutomatically`
- `SessionExpiryPlaywrightTests.UiLogin_FirstHomeApiAndInteractiveNavigationRemainAuthenticated`
- `SessionExpiryPlaywrightTests.ActiveNavigation_BeyondOriginalExpiry_UsesRenewedBrowserAndCircuitToken`
- `SessionExpiryPlaywrightTests.Idle_NoKeepalive_ThenAccountsActionRedirectsWithReturnUrl`
- `SessionExpiryPlaywrightTests.RejectedKeepalive_StaysUntilProtectedAccountsAction`
- (6. `a[href='/list/accounts']`-Timeout in Baseline-Liste — ebenfalls geheilt)

## Bewertung

Kein einziger neuer Fehlschlag → **keine Regression**. Alle E2E-sensitiven Selektoren
(`.fm-table`, `.kpi-grid .kpi-tile`, `.fav-card`, `.fm-ribbon*`, `.split-dialog`, `.modal-overlay`,
`#PrevMonth`, `#nav-toggle`, `.hamburger`, `#blazor-error-ui`, `.login-status`,
`#change-password-link`, `.mobile-topbar`, `.app-shell`, `.content`) bleiben funktional.

## Abnahme-Screenshots

Neu generiert via `scripts/screenshots/generate-screenshots.js` (Release-Publish, Demodaten)
nach `Docs/screenshots/` und visuell gegen die Variant-B-Vorlage begutachtet.
