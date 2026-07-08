# Test-Ergebnisse

## Ergebnis

**Status:** Keine Fehler

## Fehlgeschlagene Tests

_Keine._

## Zusammenfassung

- Gesamt: 757
- Bestanden: 757
- Fehlgeschlagen: 0
- Übersprungen: 0

_Aufschlüsselung nach Testprojekt:_

| Testprojekt | Tests | Zeit |
|---|---|---|
| FinanceManager.Tests | 680 | ~1 min |
| FinanceManager.Tests.Integration | 60 | ~26 s |
| FinanceManager.Tests.E2E | 17 | ~58 s |

## Testabdeckung

**Abdeckung:** 11,6 %

| Paket | Abdeckung |
|-------|-----------|
| FinanceManager.Infrastructure | 7,2 % |
| FinanceManager.Web | 23,4 % |
| FinanceManager.Shared | 30,4 % |
| FinanceManager.Domain | 66,0 % |
| FinanceManager.Application | 78,1 % |

## Fehlende Tests

Quelle: `Coverage-Daten`

446 Quelldateien haben 0 % Zeilenabdeckung. Aufschlüsselung nach Paket:

| Paket | Dateien ohne Abdeckung |
|-------|------------------------|
| FinanceManager.Infrastructure | 211 |
| FinanceManager.Web | 151 |
| FinanceManager.Shared | 72 |
| FinanceManager.Domain | 8 |
| FinanceManager.Application | 4 |

### FinanceManager.Application — Dateien mit 0 % Abdeckung

- `FinanceManager.Application\BackgroundTaskRunner.cs` — 0 % Abdeckung
- `FinanceManager.Application\Reports\IPostingExportService.cs` — 0 % Abdeckung
- `FinanceManager.Application\Securities\ReturnAnalysis\IReturnAnalysisService.cs` — 0 % Abdeckung
- `FinanceManager.Application\Statements\Dtos\BatchUpdateDtos.cs` — 0 % Abdeckung

### FinanceManager.Domain — Dateien mit 0 % Abdeckung

- `FinanceManager.Domain\Budget\BudgetCategory.cs` — 0 % Abdeckung
- `FinanceManager.Domain\Budget\BudgetOverride.cs` — 0 % Abdeckung
- `FinanceManager.Domain\Budget\BudgetPurpose.cs` — 0 % Abdeckung
- `FinanceManager.Domain\Budget\BudgetRule.cs` — 0 % Abdeckung
- `FinanceManager.Domain\Result.cs` — 0 % Abdeckung
- `FinanceManager.Domain\Security\IpBlock.cs` — 0 % Abdeckung
- `FinanceManager.Domain\Users\User.AlphaVantage.cs` — 0 % Abdeckung
- `FinanceManager.Domain\ValueObject.cs` — 0 % Abdeckung

_Die vollständige Liste der 446 Dateien ohne Abdeckung ist den Coverage-Reports unter `FinanceManager.Tests\TestResults\` zu entnehmen._
