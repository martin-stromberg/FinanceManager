# Test-Ergebnisse

## Ergebnis

**Status:** Fehler vorhanden

## Fehlgeschlagene Tests

### FinanceManager.Tests.E2E — CollectionAccountImportPlaywrightTests

- **UploadCollectionAccountCsv_ShouldCreateMultipleDrafts** — Expected uploaded.FirstDraft not to be \<null\>.
- **UploadCollectionAccountCsv_ShouldAutoAssignAccountViaLinkedIban** — Expected uploaded.FirstDraft!.DetectedAccountId to be {57b38a9b-9456-438d-b50a-6dc5c5b6f595} because the draft IBAN matches a linked IBAN of the collection account, but found \<null\>.
- **BookCollectionAccountDraft_ShouldAutoAddUnknownIbanToLinkedList** — Expected draft.DetectedAccountId to be {7c884acd-31b9-4803-99e7-a02c08d5cf27}, but found \<null\>.

## Zusammenfassung

- Gesamt: 780
- Bestanden: 777
- Fehlgeschlagen: 3
- Übersprungen: 0

_Aufgeteilt auf drei Testprojekte: FinanceManager.Tests (698), FinanceManager.Tests.Integration.ApiClient (60), FinanceManager.Tests.E2E (22)._

## Testabdeckung

**Abdeckung:** 11,9 % gesamt (Coverage nur via FinanceManager.Tests; Infrastructure und Web sind strukturell nicht unit-getestet)

| Paket | Abdeckung |
|-------|-----------|
| `FinanceManager.Application` | 78,1 % |
| `FinanceManager.Domain` | 66,8 % |
| `FinanceManager.Shared` | 30,6 % |
| `FinanceManager.Web` | 24,1 % |
| `FinanceManager.Infrastructure` | 7,5 % |

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
