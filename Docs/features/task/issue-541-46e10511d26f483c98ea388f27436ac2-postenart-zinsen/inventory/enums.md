# Enums — Bestandsaufnahme (Issue #541 „Postenart Zinsen")

Es existiert **kein** Enum für die Postenart (Eingang/Ausgang/Zins/Steuer) und **kein** Enum für das Regel-Textfeld (Verwendungszweck/Buchungsbeschreibung). Folgende bestehende Enums sind für die Anforderung relevant:

## `PostingKind`
Datei: `FinanceManager.Shared/Dtos/Postings/PostingKind.cs`

| Wert | Bedeutung |
|------|-----------|
| `Bank = 0` | Bankkonto-Posting — die Zielmenge der Postenart |
| `Contact = 1` | Kontakt-Posting (Gegenposten derselben `GroupId`) |
| `SavingsPlan = 2` | Sparplan-Posting |
| `Security = 3` | Wertpapier-Posting |

## `SecurityPostingSubType`
Datei: `FinanceManager.Shared/Dtos/Securities/SecurityPostingSubType.cs`

| Wert | Bedeutung |
|------|-----------|
| `Buy = 0` | Kauf |
| `Sell = 1` | Verkauf |
| `Dividend = 2` | Dividende |
| `Fee = 3` | Gebühr |
| `Tax = 4` | Steuer (nur Wertpapierkontext — keine eigenen Steuerposten für Bank gefordert) |

Fachmuster für eine optionale `SubType`-Eigenschaft an `Posting` (`Posting.SecuritySubType`).

## `SavingsPlanPostingSubType`
Datei: `FinanceManager.Shared/Dtos/SavingsPlans/SavingsPlanPostingSubType.cs`

| Wert | Bedeutung |
|------|-----------|
| `ActualReport = 0` | Istmeldungs-Korrekturposten |

Zweites SubType-Muster; DTO-Default `null` in `PostingBackupDto` und `PostingServiceDto`.

## `BackgroundTaskType`
Datei: `FinanceManager.Shared/Dtos/Admin/BackgroundTaskType.cs`

| Wert | Bedeutung |
|------|-----------|
| `ClassifyAllDrafts` | Alle offenen Drafts klassifizieren (`ClassificationTaskExecutor`) |
| `BookAllDrafts` | Alle offenen Drafts buchen (`BookingTaskExecutor`) |
| `BackupRestore` | Backup-Wiederherstellung (`BackupRestoreTaskExecutor`) |
| `SecurityPricesBackfill` | Kurs-Historie nachladen (`SecurityPricesBackfillExecutor`) |
| `RebuildAggregates` | Aggregat-Neuaufbau (`RebuildAggregatesTaskExecutor`) |
| `RefreshBudgetReportCache` | Budget-Report-Cache aktualisieren (`ReportCacheRefreshTaskExecutor`) |
| `CreateDemoData` | Demodaten erzeugen (`DemoDataTaskExecutor`) |

Wird in `EnumCoverageTests.BackgroundTaskType_Values` (`FinanceManager.Tests/Common/EnumCoverageTests.cs:154`) vollständig assertiert — ein neues Mitglied muss dort ergänzt werden.

## `StatementDraftEntryStatus`
Datei: `FinanceManager.Shared/Dtos/Statements/StatementDraftEntryStatus.cs`

| Wert | Bedeutung |
|------|-----------|
| `Open = 0` | Offen, zu klassifizieren |
| `Announced = 1` | Vorgemerkt |
| `Accounted = 2` | Kontakt zugeordnet |
| `AlreadyBooked = 3` | Duplikat / bereits gebucht |

## `StatementDraftStatus`
Datei: `FinanceManager.Shared/Dtos/Statements/StatementDraftStatus.cs`

| Wert | Bedeutung |
|------|-----------|
| `Draft = 0` | Editierbarer Entwurf |
| `Committed = 1` | Gebucht |
| `Expired = 2` | Verfallen |

## `AccountType`
Datei: `FinanceManager.Shared/Dtos/Accounts/AccountType.cs`

| Wert | Bedeutung |
|------|-----------|
| `Giro = 0` | Girokonto |
| `Savings = 1` | Sparkonto |
| `CreditCard = 2` | Kreditkarten-Sammelkonto (Split-Draft-Ausnahme) |

## `SavingsPlanExpectation`
Datei: `FinanceManager.Shared/Dtos/Accounts/SavingsPlanExpectation.cs`

| Wert | Bedeutung |
|------|-----------|
| `None = 0` | Keine Sparplan-Erwartung |
| `Optional = 1` | Optional |
| `Required = 2` | Erforderlich |

## `SecurityTransactionType`
Datei: `FinanceManager.Shared/Dtos/Securities/SecurityTransactionType.cs`

| Wert | Bedeutung |
|------|-----------|
| `Buy = 0` | Kauf |
| `Sell = 1` | Verkauf |
| `Dividend = 2` | Dividende |

## `BackgroundTaskStatus`
Datei: `FinanceManager.Shared/Dtos/Admin/BackgroundTaskStatus.cs`

| Wert | Bedeutung |
|------|-----------|
| `Queued` / `Running` / `Completed` / `Failed` / `Cancelled` | Task-Lebenszyklus für `BackgroundTaskInfo`/`BackgroundTaskStatusPanel` |

## EnumCoverage-Status (relevant für AGENT.md-Regel)

`FinanceManager.Tests/Common/EnumCoverageTests.cs` assertiert die Memberliste von 45 Enums. Davon betroffen/aktuell **nicht** abgedeckt:

- **Abgedeckt:** `BackgroundTaskType` (:154), `BackgroundTaskStatus` (:142), `StatementDraftStatus` (:251), `ImportFormat`, `MassImportFileType`, `MassImportDecisionSource`, `StatementEntryStatus` (Domain).
- **Nicht abgedeckt** (kein Coverage-Test vorhanden): `PostingKind`, `SecurityPostingSubType`, `SavingsPlanPostingSubType`, `StatementDraftEntryStatus`, `AccountType`, `SavingsPlanExpectation`, `SecurityTransactionType`. Neue Enums (Postenart, Regel-Textfeld) müssen laut AGENT.md in `EnumCoverageTests` ergänzt werden.
