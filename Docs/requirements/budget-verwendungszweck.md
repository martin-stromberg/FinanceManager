# Feature-Anforderungen: Budget-Verwendungszweck-Pattern inkl. Regex

## Ziel

Budget-Regeln können optional über den Buchungstext (Verwendungszweck/Vertragsnummer) eingeschränkt werden.

## Umgesetzter Funktionsumfang (Ist)

- Neue Felder an Budget-Regeln:
  - `PurposePattern` (optional, max. 500 Zeichen)
  - `UseRegex` (optional, Standard `false`)
- Ohne Pattern: bisheriges Verhalten (kein zusätzlicher Textfilter).
- Mit Pattern:
  - `UseRegex=false`: case-insensitive Contains-Matching.
  - `UseRegex=true`: Regex-Matching.

## Validierungsregeln

- `PurposePattern` wird getrimmt.
- Leeres/Whitespace-Pattern wird als nicht gesetzt behandelt.
- Maximallänge: 500 Zeichen.
- Bei `UseRegex=true` wird **nur syntaktische Regex-Validität** geprüft (Compile-Check).
- Ungültige Regex liefert `ValidationProblem` (HTTP 400) auf `PurposePattern`.

## API/DTO-Änderungen

- `BudgetRuleCreateRequest` erweitert um `PurposePattern`, `UseRegex`.
- `BudgetRuleUpdateRequest` erweitert um `PurposePattern`, `UseRegex`.
- `BudgetRuleDto` erweitert um `PurposePattern`, `UseRegex`.
- Endpunkte unverändert:
  - `POST /api/budget/rules`
  - `PUT /api/budget/rules/{id}`

## UI-Auswirkung

- In Create/Edit Budget-Regel ist ein optionales Pattern-Feld vorhanden.
- Regex-Nutzung ist explizit über Schalter/Checkbox steuerbar.
- Bei ungültiger Regex wird Speichern blockiert und ein Validierungsfehler angezeigt.

## Auswirkungen in Berichten und Buchung

- **Budgetbericht** berücksichtigt `PurposePattern` bei Zuordnung zu Budgetzwecken.
- **Kontoauszug/Buchung (Statement Drafts)** berücksichtigt `PurposePattern` in der BudgetImpact-Berechnung.

## Datenbank/Migration

- Migration:
  - `20260604172812_202606041500_AddBudgetRulePurposePattern`
- Schema:
  - `BudgetRules.PurposePattern` (`TEXT`, nullable, max 500)
  - `BudgetRules.PurposePatternIsRegex` (`INTEGER`, default `false`)

## Testabdeckung (Phase 3)

- Unit:
  - `FinanceManager.Tests/Budget/BudgetCrudServicesTests.cs`
  - `FinanceManager.Tests/Infrastructure/Budget/BudgetReportServiceRawDataTests.cs`
  - `FinanceManager.Tests/Statements/BudgetImpactEvaluationServiceTests.cs`
- Controller:
  - `FinanceManager.Tests/Controllers/BudgetRulesControllerTests.cs`
- Integration:
  - `FinanceManager.Tests.Integration/ApiClient/ApiClientBudgetReportUnbudgetedMirrorTests.cs`
  - `FinanceManager.Tests.Integration/ApiClient/ApiClientStatementDraftsTests.cs`

## Nicht zum Feature gehörende bekannte Testfailures

- `SecurityPriceErrorRecoveryTests` (2x)
- `ReturnAnalysisServiceTests` (IRR-Formatierung, 1x)
- `ApiClientAuthTests` (isAdmin-Erwartung, 1x)

Diese Failures sind fachfremd und nicht durch das Budget-Pattern-Feature verursacht.
