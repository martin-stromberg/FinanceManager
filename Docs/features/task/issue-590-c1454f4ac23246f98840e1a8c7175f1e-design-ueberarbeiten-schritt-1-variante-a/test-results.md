# Testergebnisse: Design-Variante A (Issue #590, Schritt 1)

**Stand:** nach Implementierung; Baseline-Vergleich gegen
`Docs/features/task-issue-590-c1454f4ac23246f98840e1a8c7175f1e-design-ueberarbeiten/inventory/tests.md`
(unveränderter Ausgangsstand, 8 dokumentierte Vorbefunde).

## Übersicht

| Suite | Ergebnis | Baseline | Bewertung |
|-------|----------|----------|-----------|
| Unit (`FinanceManager.Tests`) | **2511/2511 bestanden** (0 fehlgeschlagen, 46 s) | 2511 bestanden | identisch, keine Regression |
| Integration (`FinanceManager.Tests.Integration`) | **180/180 bestanden** (0 fehlgeschlagen, 10m 19s) | 180 bestanden | identisch, keine Regression |
| E2E (`FinanceManager.Tests.E2E`, Playwright) | **166/176 bestanden, 10 fehlgeschlagen** (~19 min) | 8 dokumentierte Vorbefunde | 8 Baseline-Treffer + 1 flaky + 1 umgebungsbedingt (beide aufgelöst, s. u.) |

TRX: [test-results/e2e/test-results-e2e.trx](test-results/e2e/test-results-e2e.trx)

## E2E-Fehlschläge im Detail

### Baseline-Fehlschläge (8, unverändert reproduziert)

Alle 8 entsprechen exakt den in `inventory/tests.md` der Entwurfsphase
dokumentierten Vorbefunden (gleiche Tests, gleiche Signaturen):

| Test | Signatur |
|------|----------|
| `LoadingBarPlaywrightTests.InternalLinkClick_ShouldStartLoadingBarAutomatically` | Timeout 10 s — `nav.sidebar a[href='/list/accounts']` |
| `SessionExpiryPlaywrightTests.UiLogin_FirstHomeApiAndInteractiveNavigationRemainAuthenticated` | Timeout 10 s — `a[href='/list/accounts']` |
| `SessionExpiryPlaywrightTests.ActiveNavigation_BeyondOriginalExpiry_UsesRenewedBrowserAndCircuitToken` | Timeout 10 s — `a[href='/list/accounts']` |
| `SessionExpiryPlaywrightTests.Idle_NoKeepalive_ThenAccountsActionRedirectsWithReturnUrl` | Timeout 10 s — `a[href='/list/accounts']` |
| `SessionExpiryPlaywrightTests.RejectedKeepalive_StaysUntilProtectedAccountsAction` | Timeout 10 s — `a[href='/list/accounts']` |
| `BudgetReportDemoDataE2ETests.DemoDataBudgetReport_PreviousMonth_ShouldContainAllExpectedRows(750)` | Timeout 30 s — Navigation `Load` beim Registrierungs-Submit |
| `KpiLocalStorageE2ETests.DisableCache_RemovesKpiDataFromLocalStorage` | Timeout 10 s — `button#Save:enabled` |
| `HomeMassImportPlaywrightTests.UploadIngCsvNewFormat_ViaUi_ShouldImportWithoutReviewDialog` | Assertion `href` enthält kein `/card/statement-drafts/` (führender Schrägstrich) |

Hinweis: Die `a[href='/list/accounts']`-Timeouts sind **keine** Regression
durch die neue Sidebar — der Selektor existiert im neuen Chrome unverändert
(`nav.sidebar .nav-links a[href='/list/accounts']`); die Baseline belegt das
identische Bild auf dem Ausgangsstand (Circuit-Authentifizierung liefert in
der Harness-Umgebung `IsAuthenticated=false`).

### Nicht-Baseline-Fehlschläge (2, aufgelöst)

| Test | Erstbefund | Auflösung |
|------|-----------|-----------|
| `UpdateSetupPlaywrightTests.Admin_SavesSettings_PersistsAcrossReload` | Timeout 15 s — `.setup-update-tab` | **Flaky** — isolierter Wiederholungslauf bestanden. Kein Codepfad der Umstellung betroffen (Setup-Update-Tab ist feature-flag-abhängig). |
| `HelpPagePlaywrightTests.HelpHub_ShouldShowDocumentationContent` | `featureCards` = 12 statt 13 | **Umgebungsartefakt, kein Regression:** Die Help-Doku (`Docs/help/**`) wurde *nach* dem Build des E2E-Laufs editiert. `HelpAssetIntegrityValidator.IsTrustedHelpFile` prüft Datei-Hashes gegen ein Build-Manifest → geänderte `beschreibung.md` (Topic `benutzeroberflaeche`) galt als nicht vertrauenswürdig → Topic fiel aus dem Hub. Nach Rebuild (`dotnet build FinanceManager.Web`) + Wiederholungslauf: **bestanden** (13 Karten). |

## Visuelle Abnahme (Inventory-Neuerfassung)

Voller Lauf `scripts/screenshots/generate-inventory.js` nach der Implementierung:
**140/142 Captures**, Desktop + Mobile, in `Docs/design/inventory/` (Screenshots,
HTML-Snapshots, `index.json`/`index.md`, `data-gaps.md` aktualisiert).

- **2 Datenlücken** = bekannte Bestands-Limitierung `home` (KPI-Kacheln laden
  ausschließlich interaktiv; SSR-DOM leer) — unverändert zu Baseline, kein
  Regressionsbefund.
- Stichproben-Sichtprüfung gegen `Docs/design/proposals/variantA/`:
  Chrome (Sidebar 13er-Nav, Logo-Badge, Topbar), Ribbon (Icon-über-Label,
  horizontale Gruppen), Signed-out-Shell (Brand-Leiste + Karte),
  Tabellen/Forms/Overlays — übereinstimmend (Details in `review.md`).

## Ergebnis

**Alle durch die Umstellung prüfbaren Bereiche ohne Regression.** Unit- und
Integrationssuiten vollständig grün; E2E-Fehlschläge decken sich exakt mit der
dokumentierten Baseline; die zwei neuen Erstbefunde waren ein Flaky-Test bzw.
ein selbst verursachtes Manifest-Alterungsartefakt — beide per Wiederholungslauf
verifiziert bestanden.
