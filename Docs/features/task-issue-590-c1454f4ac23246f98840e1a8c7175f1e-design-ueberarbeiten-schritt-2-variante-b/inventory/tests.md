# Inventar: Tests und Test-Baseline

Detaildokument zur Inventur (Commit `67121fa8`, 2026-11-27). Dokumentiert die bestehende
Testabdeckung rund ums Ribbon sowie die Baseline-Läufe vor der Umsetzung.

## 1. Bestehende Tests, die Ribbon-Struktur/Verhalten assertieren

### Unit/bUnit — `FinanceManager.Tests`

- `FinanceManager.Tests/Components/RibbonTests.cs` (bUnit, ~18 Facts): Rendern der Gruppen/Aktionen,
  Callback-Auslösung, **Gruppenreihenfolge = Tab-Listenreihenfolge**
  (`Groups_RenderInTabListOrder`), mobile Shortcuts (`MobileShortcut`), Disabled-Darstellung.
- `FinanceManager.Tests/Common/EnumCoverageTests.cs`: assertiert vollständige Memberliste von
  `UiRibbonItemSize` (`Small`, `Large`) und `UiRibbonRegisterKind` (`QuickAccess`, `Actions`,
  `LinkedInfo`, `Reports`, `Custom`) — neue Enum-Mitglieder müssten hier ergänzt werden (für dieses
  Vorhaben nicht nötig, alle benötigten Member existieren).
- VM-Tests mit Ribbon-Bezug: `FinanceManager.Tests/ViewModels/AccountDetailViewModelTests.cs`,
  `PostingsCardViewModelReversalTests.cs`, `SavingsPlanCardLinkTests.cs`,
  `SetupCardViewModelTests.cs`, `Web/ViewModels/ContactsViewModelTests.cs`.

### E2E — `FinanceManager.Tests.E2E` (Playwright)

Selektoren hängen an `UiRibbonAction.Id` als Button-`id`. Verwendete Ribbon-IDs auf bzw. nahe den
betroffenen Ansichten:

- `button#Save` (Account-Posting-Rules, Collection-Account, KpiLocalStorage-Test `button#Save:enabled`)
- `button#New`, `button#ClearSearch` (AccountsOverviewStatistics; `ListPageGateway.ClearSearch`)
- `#Back`, `#OpenAccount`, `#OpenContact`, `#OpenSavingsPlan`, `#OpenSecurity`, `#OpenAttachments`
  (`PostingsCardLinkedActionsPlaywrightTests`, `RibbonGroupOrderPlaywrightTests` — letzterer
  assertiert Gruppenreihenfolge via DOM!)
- `#Delete`, `#Attachments` (`ConfirmationDialogE2ETests`)
- `#BudgetReport`, `#PrevMonth` (`BudgetReportDemoDataE2ETests` — klickt auch per `querySelector`)
- `#Reload` (`AuthenticationFlowPlaywrightTests`, per `querySelector('#Reload')?.click()`)
- `#UpdateCheckNow`, `#Save` (`SetupUpdateGateway`)
- `#Settings` (`LoadingBarPlaywrightTests`)
- `#Import` + `#Import input[type=file]` (`HomePageGateway`, Import-Tests), `#Book` (Import)
- `#OpenPostingRules`, `#ReevaluatePostingTypes` (`AccountPostingRulesPlaywrightTests`)
- `#FiltersOpen`, `#SaveAs` (`ReportDashboardGateway` — nicht im Scope)

**Risiko bei Umsetzung:** Umbenennen/Entfernen von Action-IDs oder Gruppenverschiebungen ändern
diese Selektoren nicht (IDs bleiben stabil), aber `Hidden`/`Disabled`-Zustände und Sichtbarkeit
können `WaitForAsync(Visible)`-Assertions brechen — insb. `RibbonGroupOrderPlaywrightTests` und
die `…ToBeVisibleAsync()`-Assertions in `AccountsOverviewStatisticsPlaywrightTests` (`#New`,
`#ClearSearch` müssen sichtbar bleiben).

## 2. Screenshot-/E2E-Infrastruktur

- `scripts/screenshots/` existiert mit Vorlauf `out-iter5/`; Doku: `Docs/screenshot-generation.md`.
- `FinanceManager.Tests.E2E` startet die App selbst (Playwright); Auth-Gateway unter
  `FinanceManager.Tests.E2E/Helpers/AuthGateway.cs`.

## 3. Baseline-Läufe (vor Änderungen)

| Lauf | Befehl | cwd | Ergebnis | Artefakt |
| --- | --- | --- | --- | --- |
| Unit (bUnit+VM) | `dotnet test FinanceManager.Tests` (net10.0) | Repo-Root | **2511/2511 bestanden, 0 fehlgeschlagen, 0 übersprungen** (~48 s) | `inventory/test-results/test-results-unit-baseline.trx` |
| Integration | `dotnet test FinanceManager.Tests.Integration/FinanceManager.Tests.Integration.csproj -c Debug --results-directory …/inventory/test-results -- --report-xunit-trx --report-xunit-trx-filename test-results-integration-baseline.trx` | Repo-Root | **180/180 bestanden, 0 fehlgeschlagen, 0 übersprungen** (10m52s) | `inventory/test-results/test-results-integration-baseline.trx` |
| E2E (Playwright) | `dotnet test FinanceManager.Tests.E2E/FinanceManager.Tests.E2E.csproj -c Debug --results-directory …/inventory/test-results -- --report-xunit-trx --report-xunit-trx-filename test-results-e2e-baseline.trx` | Repo-Root | **zum Inventur-Zeitpunkt (19:20) noch laufend** — Prozess aktiv (`FinanceManager.Tests.E2E.exe` + `FinanceManager.Web.exe` + Playwright-Treiber); TRX wird bei Abschluss automatisch in `inventory/test-results/` geschrieben. Mind. 1 bereits **vorhandener** Fehlschlag beobachtet: `HomeMassImportPlaywrightTests.UploadIngCsvNewFormat_ViaUi_ShouldImportWithoutReviewDialog` (`href` `card/statement-drafts/{guid}` enthält kein `/card/statement-drafts/` — URL-Format-Assertion, ribbon-unabhängig, vorab als bekannter Bestandsfehler zu verbuchen) | `inventory/test-results/test-results-e2e-baseline.trx` (folgt bei Laufende) |

> **Nachholbedarf:** Finale E2E-Gesamtzahlen nach Abschluss hier ergänzen (vorhandene Fehler als
> Bestandsfehler dokumentieren, nicht diesem Vorhaben zuschreiben).

SDK: `dotnet --version` → 11.0.100; Ziel-Framework net10.0. Build-Warnung NU1510
(überflüssige `PackageReference`s in `FinanceManager.Web.csproj`) — vorhanden, unverändert.

## 4. Bekannte/vorhersehbare Folge-Testanforderungen

- bUnit-Tests für die neue Zielkonfiguration (Gruppennamen/-reihenfolge, `Small`/`Large`,
  `Hidden`/`Disabled`) analog `RibbonTests.cs` ergänzen/anpassen.
- `RibbonGroupOrderPlaywrightTests` (E2E) assertiert Reihenfolge — nach Umstellung prüfen.
- `EnumCoverageTests` bleibt unverändert gültig (keine neuen Enum-Member).
