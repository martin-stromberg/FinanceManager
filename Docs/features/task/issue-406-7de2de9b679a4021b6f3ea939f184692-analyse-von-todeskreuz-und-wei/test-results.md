# Test-Ergebnisse

## Ergebnis

**Status:** Fehler vorhanden (ausschließlich preexisting, nicht feature-bezogen)

## Fehlgeschlagene Tests

### FinanceManager.Tests.E2E (Playwright)

- **StatementDraftQuickEditValueTakeoverE2ETests.QuickEdit_Blur_ShouldSendKeepaliveAndKeepLocalInputValue** — Expected response.Status to be 204, but found 401 (preexisting, Baseline-Nachweis: `inventory/test-results/baseline-e2e.trx`)
- **BudgetReportDemoDataE2ETests.DemoDataBudgetReport_PreviousMonth_ShouldContainAllExpectedRows** — Timeout: `#create-demo-data` nicht sichtbar (preexisting, Baseline-Nachweis: baseline-e2e.trx)
- **UpdateSetupPlaywrightTests.Admin_TriggersCheck** — Timeout auf `.setup-update-tab` (flaky/Umgebung; nicht feature-bezogen, in der Baseline mit anderem Fehlerbild ebenfalls instabil)

## E2E-Abdeckung

| Szenario | Test / Testklasse | Ergebnis |
|----------|-------------------|----------|
| Karte mit bärischen Kursen zeigt Widget mit Signalen | `SecurityTrendReversalE2ETests.CardPage_BearishPrices_ShowsTrendReversalWidgetWithSignals` | Bestanden |
| Karte mit Aufwärtstrend zeigt kein Widget | `SecurityTrendReversalE2ETests.CardPage_BullishPrices_HidesTrendReversalWidget` | Bestanden |
| Setup-Checkbox für Umkehrsignal-Benachrichtigungen umschalten/speichern | `SecurityTrendReversalE2ETests.SetupNotificationsTab_TrendReversalCheckbox_PersistsToggle` | Bestanden |

## Zusammenfassung

- Gesamt: 1692
- Bestanden: 1689
- Fehlgeschlagen: 3 (alle preexisting bzw. umgebungsbedingt, siehe oben)
- Übersprungen: 0

Details: Unit `FinanceManager.Tests` 1448/1448 (Baseline 1397 → +51 neue Tests); Integration `FinanceManager.Tests.Integration` 134/134; E2E `FinanceManager.Tests.E2E` 110 gesamt / 107 bestanden / 3 fehlgeschlagen.

Befehle (Repo-Root, vorausgehender `dotnet build FinanceManager.sln -c Debug` mit 0 Fehlern):
- `dotnet test FinanceManager.Tests/FinanceManager.Tests.csproj --no-build -c Debug -- --filter-not-trait "Category=OsInterface"`
- `dotnet test FinanceManager.Tests.Integration/FinanceManager.Tests.Integration.csproj --no-build -c Debug`
- `dotnet test FinanceManager.Tests.E2E/FinanceManager.Tests.E2E.csproj --no-build -c Debug`

## Testabdeckung

**Abdeckung:** Nicht messbar (Lauf ohne `--coverage`; CI misst in `staging-ci.yml` separat)

## Fehlende Tests

Quelle: `Dateinamen-Konvention`

- `FinanceManager.Web/Services/TrendReversalLocalizer.cs` — Keine Testdatei gefunden (indirekt über `TrendReversalServiceTests` + gemocktem `ITrendReversalLocalizer` abgedeckt; `GoldenCrossLocalizer` besitzt ebenfalls keine eigenen Tests)
- `FinanceManager.Web/Components/Shared/TrendReversalWidget.razor` — Keine Testdatei gefunden (via `SecurityTrendReversalE2ETests` abgedeckt; `GoldenCrossWidget` hat ebenfalls keine Komponententests)
- `FinanceManager.Infrastructure/Migrations/20261018121308_AddTrendReversal.cs` — Keine Testdatei gefunden (EF-generiert, Konvention)
