# Test-Ergebnisse

## Ergebnis

**Status:** Keine Fehler

## Fehlgeschlagene Tests

_Keine._

## Zusammenfassung

- Gesamt: 763
- Bestanden: 763
- Fehlgeschlagen: 0
- Übersprungen: 0

_Aufgeteilt auf drei Testprojekte: FinanceManager.Tests (687), FinanceManager.Tests.Integration (60), FinanceManager.Tests.E2E (16)._

## Testabdeckung

**Abdeckung:** 11,4 % gesamt (Coverage nur via FinanceManager.Tests; Infrastructure und Web sind strukturell nicht unit-getestet)

| Paket | Abdeckung |
|-------|-----------|
| `FinanceManager.Application` | 78,1 % |
| `FinanceManager.Domain` | 66,8 % |
| `FinanceManager.Shared` | 30,3 % |
| `FinanceManager.Web` | 22,6 % |
| `FinanceManager.Infrastructure` | 7,2 % |

## Fehlende Tests

Quelle: `Coverage-Daten`

Dateien mit 0 % Abdeckung (Auswahl relevanter Quelldateien, ohne generierte/Konfigurations-Dateien):

- `FinanceManager.Application\BackgroundTaskRunner.cs` — 0 % Abdeckung
- `FinanceManager.Application\Statements\Dtos\BatchUpdateDtos.cs` — 0 % Abdeckung
- `FinanceManager.Application\Reports\IPostingExportService.cs` — 0 % Abdeckung
- `FinanceManager.Domain\Result.cs` — 0 % Abdeckung
- `FinanceManager.Domain\ValueObject.cs` — 0 % Abdeckung
- `FinanceManager.Domain\Users\User.AlphaVantage.cs` — 0 % Abdeckung
- `FinanceManager.Domain\Accounts\AccountLinkedIban.cs` — 0 % Abdeckung
- `FinanceManager.Infrastructure\Statements\StatementDraftService.BatchUpdate.cs` — 0 % Abdeckung (teilweise; eine Klasse bei 46,8 %)
- `FinanceManager.Infrastructure\Statements\Parsers\Sparkasse_PDF_StatementFileParser.cs` — 0 % Abdeckung
- `FinanceManager.Infrastructure\Statements\Files\Sparkasse_PDF_StatementFile.cs` — 0 % Abdeckung
- `FinanceManager.Infrastructure\Savings\SavingsPlanCategoryService.cs` — 0 % Abdeckung
- `FinanceManager.Infrastructure\Securities\SecurityPriceService.cs` — 0 % Abdeckung
- `FinanceManager.Infrastructure\Securities\SecurityReportService.cs` — 0 % Abdeckung
- `FinanceManager.Infrastructure\Setup\AutoInitializationService.cs` — 0 % Abdeckung
- `FinanceManager.Infrastructure\Auth\DemoDataService.cs` — 0 % Abdeckung
