# Test-Ergebnisse

## Ergebnis

**Status:** Keine Fehler

## Fehlgeschlagene Tests

Keine.

## Zusammenfassung

| Test-Projekt | Gesamt | Bestanden | Fehlgeschlagen | Übersprungen |
|---|---|---|---|---|
| FinanceManager.Tests (Unit) | 677 | 677 | 0 | 0 |
| FinanceManager.Tests.Integration | 60 | 60 | 0 | 0 |
| FinanceManager.Tests.E2E | 17 | 17 | 0 | 0 |
| **Gesamt** | **754** | **754** | **0** | **0** |

## Testabdeckung

**Abdeckung:** 11,6 % gesamt (gemessen durch Unit-Test-Run; Integration-Projekt ohne Coverage-Collector)

> Die niedrige Gesamtabdeckung ergibt sich daraus, dass Infrastructure- und Web-Schichten kaum durch Unit-Tests abgedeckt werden — planmäßig über E2E- und Integrationstests.

| Paket | Abdeckung |
|---|---|
| FinanceManager.Infrastructure | 7,2 % |
| FinanceManager.Web | 23,4 % |
| FinanceManager.Shared | 30,4 % |
| FinanceManager.Domain | 66,0 % |
| FinanceManager.Application | 78,1 % |

### Dateien unter 80 % (Auswahl je Paket)

| Datei | Abdeckung |
|---|---|
| `FinanceManager.Application\BackgroundTaskRunner.cs` | 0 % |
| `FinanceManager.Domain\Users\User.TimeZone.cs` | 7 % |
| `FinanceManager.Domain\Users\User.Notifications.cs` | 14 % |
| `FinanceManager.Domain\Entity.cs` | 36 % |
| `FinanceManager.Domain\Users\User.cs` | 57 % |
| `FinanceManager.Domain\Budget\BudgetCategory.cs` | 61 % |
| `FinanceManager.Domain\Guards.cs` | 67 % |
| `FinanceManager.Domain\Budget\BudgetRule.cs` | 71 % |
| `FinanceManager.Domain\Budget\BudgetOverride.cs` | 73 % |
| `FinanceManager.Domain\Budget\BudgetPurpose.cs` | 73 % |
| `FinanceManager.Domain\Reports\ReportCacheEntry.cs` | 75 % |
| `FinanceManager.Infrastructure\Statements\Parsers\TemplateStatementFileParser.cs` | 6 % |
| `FinanceManager.Infrastructure\Statements\Parsers\ING_PDF_StatementFileParser.cs` | 16 % |
| `FinanceManager.Infrastructure\Statements\Parsers\BaseStatementFileParser.cs` | 21 % |
| `FinanceManager.Infrastructure\Statements\Files\PdfStatementFile.cs` | 38 % |
| `FinanceManager.Infrastructure\Statements\StatementDraftService.Mapping.cs` | 49 % |
| `FinanceManager.Infrastructure\Statements\Files\ING_PDF_StatementFile.cs` | 50 % |
| `FinanceManager.Infrastructure\Statements\Files\Barclays_PDF_StatementFile.cs` | 69 % |
| `FinanceManager.Infrastructure\Statements\Files\Wuestenrot_PDF_StatementFile.cs` | 69 % |
| `FinanceManager.Infrastructure\Statements\Files\TextStatementFile.cs` | 75 % |
| `FinanceManager.Shared\Dtos\Statements\StatementDraftDetailDtos.cs` | 10 % |
| `FinanceManager.Shared\Dtos\Reports\ReportAggregatesQueryRequest.cs` | 17 % |
| `FinanceManager.Shared\Extensions\DateExt.cs` | 19 % |
| `FinanceManager.Shared\Dtos\Accounts\AccountUpdateRequest.cs` | 20 % |
| `FinanceManager.Shared\Dtos\Statements\StatementDraftMassBookStatusDtos.cs` | 33 % |
| `FinanceManager.Shared\ApiClient.cs` | 60 % |
| `FinanceManager.Web\ViewModels\ViewModelBase.cs` | 15 % |
| `FinanceManager.Web\Infrastructure\ApiErrors\ApiErrorFactory.cs` | 22 % |
| `FinanceManager.Web\ViewModels\Budget\MonthlyBudgetKpiViewModel.cs` | 40 % |
| `FinanceManager.Web\ViewModels\Common\BaseCardViewModel.cs` | 54 % |
| `FinanceManager.Web\Controllers\BackgroundTasksController.cs` | 55 % |
| `FinanceManager.Web\ViewModels\Postings\Common\PostingsCardViewModel.cs` | 74 % |

## Fehlende Tests

Quelle: `Coverage-Daten`

Zahlreiche Dateien weisen 0 % Zeilenabdeckung durch Unit-Tests auf (überwiegend Infrastructure- und Web-Schicht). Exemplarische Auswahl:

- `FinanceManager.Application\BackgroundTaskRunner.cs` — 0 % Abdeckung
- `FinanceManager.Domain\Result.cs` — 0 % Abdeckung
- `FinanceManager.Domain\ValueObject.cs` — 0 % Abdeckung
- `FinanceManager.Domain\Users\User.AlphaVantage.cs` — 0 % Abdeckung
- `FinanceManager.Domain\Security\IpBlock.cs` — 0 % Abdeckung
- `FinanceManager.Infrastructure\Auth\DemoDataService.cs` — 0 % Abdeckung
- `FinanceManager.Infrastructure\Setup\SetupImportService.cs` — 0 % Abdeckung
