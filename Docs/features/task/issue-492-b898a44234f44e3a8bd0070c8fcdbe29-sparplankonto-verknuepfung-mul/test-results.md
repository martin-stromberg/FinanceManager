# Testergebnisse

**Status:** Keine Fehler

## Baseline (vor Umsetzung)

| Lauf | Ergebnis |
|------|----------|
| `dotnet build FinanceManager.sln` | 0 Fehler |

## Umsetzungsläufe

| Lauf | Befehl | Ergebnis |
|------|--------|----------|
| Unit (gefiltert) | `dotnet test FinanceManager.Tests --filter "FullyQualifiedName~Savings\|Report\|SavingsPlanCardLink\|ReportDashboardViewModel"` | 328/328 bestanden |
| Unit (vollständig) | `dotnet test FinanceManager.Tests --no-build` | 2110/2110 bestanden |
| Integration | `dotnet test FinanceManager.Tests.Integration --filter "ApiClientSavingsPlanLinksTests\|ApiClientDemoDataTests"` | 7 Tests; 1 initiales Fehlschlagen (`ShouldSkipCurrentMonthPostings`: erwartete 3 Drafts, jetzt 4 wegen Bausparkonto) → Erwartung korrigiert, Re-Run bestanden |
| E2E | Nicht eingerichtet (kein Blazor-E2E im Repo; Abdeckung via VM-/Integrationstests, siehe plan.md) | – |

## Neue Tests

- `SavingsPlanAccountLinkServiceTests_SavingsOnly` (3 Fälle: Giro-, Kreditkarten-Ablehnung, Savings-Annahme)
- `SavingsPlanServiceOwnAmountTests` (Eigenanteil verknüpft/nicht verknüpft)
- `ReportAggregationServiceSavingsPlanSourceTests` (PlanPostings-/LinkedAccountPostings-Quelle, unverknüpfter Plan, Fremd-Owner)
- `SavingsPlanCardLinkTests` erweitert (Nicht-Savings-Filterung, OpenLinkedAccount-Aktion)
- `ApiClientDemoDataTests` erweitert (Bauspar-Konto/Plan/Link/Buchungen/Zinsen/Budget/Favorit, Salden-Konsistenz)

## Betroffene bestehende Tests

- `ApiClientDemoDataTests.ShouldSkipCurrentMonthPostings`: Draft-Erwartung 3→4 (neues Bausparkonto) — begründete Anpassung.

## Fortsetzungslauf „Weitere Rückmeldungen" (2026-11-08)

|| Lauf | Befehl | Ergebnis |
||------|--------|----------|
|| Unit (gefiltert) | `dotnet test FinanceManager.Tests --filter "SavingsPlanCardLinkTests|SavingsPlanAccountLinkServiceTests|McpReadOnlyToolsTests|McpMasterDataWriteToolsTests"` | 128/128 bestanden |
|| Integration | `dotnet test FinanceManager.Tests.Integration --filter "McpSavingsPlanLinkTests"` | 2/2 bestanden (echte `/mcp`-Aufrufe auf realem Demo-Datenbestand) |
|| Integration | `dotnet test FinanceManager.Tests.Integration --filter "ApiClientSavingsPlanLinksTests|McpEndpointTests"` | 10/10 bestanden |
|| Format | `dotnet format FinanceManager.sln --verify-no-changes --severity error` | keine Abweichungen |
|| Static Analysis | `dotnet build FinanceManager.sln -p:TreatWarningsAsErrors=true` (CI-Exklusionsliste) | 0 Fehler |

## Neue Tests (Fortsetzung)

- `SavingsPlanCardLinkTests.RemoveAccountLinkAsync_ConfirmedViaService_CallsApi` / `RemoveAccountLinkAsync_Cancelled_DoesNotCallApi` (Sicherheitsabfrage via `IConfirmationService`)
- `SavingsPlanAccountLinkServiceTests_Queries.GetLinkedAccountIdsByPlan_MapsOnlyOwnedLinkedPlans` (Bulk-Lesesicht)
- `McpSavingsPlanLinkTests.Analysis_LinkedPlan_ReportsLinkedAccountsAndAccountBasedBalance` / `Manage_AddAndRemoveAccountLink_WorksOverMcp` (echter `/mcp`-Host, PAT, Demo-Daten „Bausparkonto“+„Bausparen“)
