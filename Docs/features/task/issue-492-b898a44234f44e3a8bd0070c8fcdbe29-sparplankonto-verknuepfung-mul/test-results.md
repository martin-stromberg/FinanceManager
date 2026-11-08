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
