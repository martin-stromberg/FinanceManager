# Architektur: Budget-Verwendungszweck-Pattern inkl. Regex

## Überblick

Das Feature erweitert `BudgetRule` um ein optionales Textmuster zur Buchungstext-Filterung.
Die Implementierung ist in Domain, API, Service-Layer, Reporting und Statement-BudgetImpact verankert.

## Datenmodell

- Entity: `FinanceManager.Domain.Budget.BudgetRule`
  - `PurposePattern : string?`
  - `PurposePatternIsRegex : bool`
- Setter/Validierung: `SetPurposePattern(string? pattern, bool isRegex)`

## Persistenz

- Migration:
  - `FinanceManager.Infrastructure/Migrations/20260604172812_202606041500_AddBudgetRulePurposePattern.cs`
- DB-Änderung:
  - `BudgetRules.PurposePattern` (`TEXT`, nullable, maxLength 500)
  - `BudgetRules.PurposePatternIsRegex` (`INTEGER`, default false)

## API-Schicht

- Controller:
  - `FinanceManager.Web/Controllers/BudgetRulesController.cs`
- DTOs:
  - `BudgetRuleCreateRequest`
  - `BudgetRuleUpdateRequest`
  - `BudgetRuleDto`
- Fehler-Mapping:
  - Domain-`ArgumentException` bei ungültiger Regex wird auf `ValidationProblem`/HTTP 400 gemappt.

## Validierungsdesign (Regex)

- Beim Speichern (`Create`/`Update`) wird Regex nur auf Compile-Validität geprüft:
  - `new Regex(trimmedPattern)`
- Kein semantischer Match-Test beim Speichern.
- Grundsatz: frühes syntaktisches Feedback, Matching-Verhalten bleibt zur Laufzeit im Fachkontext.

## Matching-Design

### Budget Report

- Komponente: `FinanceManager.Infrastructure/Budget/BudgetReportService.cs`
- Regel:
  - kein/leer Pattern => Match
  - `UseRegex=false` => `IndexOf(..., StringComparison.OrdinalIgnoreCase) >= 0`
  - `UseRegex=true` => `Regex.IsMatch(..., IgnoreCase | CultureInvariant, 200ms Timeout)`
  - `ArgumentException`/`RegexMatchTimeoutException` => kein Match

### Statement Draft / Budget Impact

- Komponente: `FinanceManager.Infrastructure/Statements/BudgetImpactEvaluationService.cs`
- Regel:
  - Input ist zusammengesetzter Text aus `subject` + `bookingDescription`
  - gleiche Matching-Strategie wie im Reporting
  - Regex-Fehler/Timeout werden abgefangen; Verarbeitung läuft weiter

## Cache-Invalidierung

- Komponente: `FinanceManager.Infrastructure/Budget/BudgetRuleService.cs`
- Bei Create/Update/Delete von Budget-Regeln wird Report-Cache zur Aktualisierung markiert.

## Testrelevante Architekturpunkte

- Validierung:
  - `BudgetCrudServicesTests`, `BudgetRulesControllerTests`
- Matching im Bericht:
  - `BudgetReportServiceRawDataTests`
  - `ApiClientBudgetReportUnbudgetedMirrorTests`
- Matching im Statement-BudgetImpact:
  - `BudgetImpactEvaluationServiceTests`
  - `ApiClientStatementDraftsTests`
