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

## Iteration 2: Review-Feedback PR #592 (lokale Abnahme durch Anwender)

Der Anwender meldete auf dem PR drei Befundgruppen aus einem lokalen
Debugger-Lauf:

1. **JSException `Failed to resolve module specifier 'js/profile.js'`** beim
   Start/Register (`Register.razor` `OnAfterRenderAsync`). Ursache: Commit
   `23898593` (PathBase-Umstellung auf staging) hatte `import("/js/profile.js")`
   in `import("js/profile.js")` geändert — ein Bare-Module-Specifier, den
   Browser nicht auflösen können. Die Screenshot-Werkzeuge hatten den Fehler
   durch eine injizierte Importmap kaschiert, daher fiel er bei der Abnahme
   nicht auf. **Fix:** `./js/profile.js` bzw. `./js/attachments.js` — ein
   relativer Specifier, der gegen `<base href>` aufgelöst wird und damit
   PathBase-sicher bleibt (3 Stellen: `Register.razor`,
   `SetupProfileTab.razor`, `AttachmentsPanel.razor`).
2. **`The POST request does not specify which form is being submitted`** beim
   Registrieren des ersten Anwenders. Ursache war dieselbe: Die JSException in
   `OnAfterRenderAsync` tötete den Blazor-Circuit, das Formular fiel auf einen
   plain HTTP-POST zurück (fehlender `formname`-Handler). Nach dem Import-Fix
   läuft der Submit wieder über den Circuit — der Fehler tritt nicht mehr auf.
   Zusätzlich wurde der Modul-Import defensiv abgesichert (Fehler beim Laden
   des optionalen Locale-/Zeitzonen-Moduls darf die Registrierung nicht mehr
   lahmlegen).
3. **E2E-Fehlschläge (5 gemeldete, 8 dokumentierte Baseline):**
   - `button#Save:enabled`-Timeout (`KpiLocalStorageE2ETests`) — Folge des
     toten Circuits auf `/card/setup` (gleicher Import-Fehler in
     `SetupProfileTab.razor`). Mit dem Fix aktiviert der Dirty-State das
     Ribbon-Speichern wieder.
   - `a[href='/list/accounts']`/`nav.sidebar a[href='/list/accounts']`-
     Timeouts (`SessionExpiryPlaywrightTests`, `LoadingBarPlaywrightTests`)
     — seit der PathBase-Umstellung rendert die Sidebar relative hrefs
     (`list/accounts`, Home: `href=""`, aufgelöst via `<base href>`); die
     Testselektoren verlangten noch `/`-präfigierte Attribute. Selektoren an
     die reale Auszeichnung angepasst; `InitializationVisibilityProbe`
     ebenso (die Sichtbarkeitsprüfung war dadurch zuvor wirkungslos).
   - `href`-Assertion `/card/statement-drafts/`
     (`HomeMassImportPlaywrightTests`) — die App liefert bewusst relative
     URLs; Erwartung auf `card/statement-drafts/` korrigiert.
   - `BudgetReportDemoDataE2ETests`-Timeout beim Registrierungs-Submit —
     gleiche Circuit-Kaskade wie Befund 2.

### Verifikation Iteration 2

| Suite | Ergebnis |
|-------|----------|
| `KpiLocalStorageE2ETests.DisableCache_RemovesKpiDataFromLocalStorage` | **bestanden** (21 s, zuvor Timeout) |
| `LoadingBarPlaywrightTests.InternalLinkClick_ShouldStartLoadingBarAutomatically` | **bestanden** |
| `HomeMassImportPlaywrightTests.UploadIngCsvNewFormat_ViaUi_ShouldImportWithoutReviewDialog` | **bestanden** |
| `SessionExpiryPlaywrightTests` (alle 5, inkl. der 2 gemeldeten) | **bestanden** (74 s) |
| `SessionExpiryPlaywrightTests` + `BudgetReportDemoDataE2ETests` (Restlauf, 10 Tests) | **bestanden** (15 m 56 s) |

### Reale End-to-End-Abnahme (Iteration 2)

Die Anwendung wurde real betrieben (Debug-Build, frische SQLite-DB, Playwright
ohne Importmap-Workaround):

- `/register`: Rendern ohne Circuit-Fehler, Submit-Button aktiv erst bei
  Interaktivität; Erstanwender-Registrierung inkl. Demodaten-Checkbox
  übermittelt erfolgreich → Redirect `/` → `/login`.
- `/login`: Form-Login funktioniert → `/` authentifiziert.
- Sidebar: `nav.sidebar a[href='list/accounts']` vorhanden und klickbar →
  `/list/accounts` mit Tabelle.
- `/card/setup`: kein Circuit-Fehler, Ribbon/Sections rendern.
- Browser-Konsole: kein `Failed to resolve module specifier` mehr.

Screenshots: `01-register-page.png`, `02-after-registration.png`,
`03-accounts-list.png`, `04-setup.png`, `05-home-after-login.png`
(Nachweis-Ablage des Versuchs).

### Sichtprüfung der Abnahme-Screenshots

Alle 5 Screenshots einzeln gesichtet — keine Layout-Defekte: Signed-out-Shell
(Brand-Leiste + Karte) und App-Chrome (Label-Sidebar 13er-Nav, Topbar,
Ribbon-Gruppen) überdeckungsfrei, Texte unbeschnitten, Tabellen/Statistik-
Karten korrekt breitengefüllt.
