# Übersetzte Anforderungsbeschreibung: Backup/Restore – Datenverlust durch fehlende Import- und DTO-Abdeckung (Issue #496)

## Fachliche Zusammenfassung

Der Benutzer-Backup-/Restore-Zyklus wird gemäß dem AGENT.md-Prinzip „Jedes persistierte Feld muss im Benutzer-Backup enthalten sein, und alte Backups müssen weiterhin einlesbar bleiben" vervollständigt. `BackupService.BuildBackupDataAsync` exportiert aktuell Sektionen, die `SetupImportService.ImportVersion3` nicht wiederherstellt, obwohl `AppDbContext.ClearUserDataAsync` sie löscht – jeder Restore vernichtet dadurch die Kontoauszugs-Import-Historie (`StatementImports`/`StatementEntries`), alle `Notifications` und alle `AccountShares` unwiederbringlich. Zusätzlich fehlen `AccountLinkedIbans` (Unter-IBANs von Sammelkonten) und `PortfolioKpiConfigurations` vollständig im Backup, und die Backup-DTOs von `BudgetRule` und `Security` decken nicht alle persistierten Felder ab.

## Betroffene Klassen und Komponenten

### Logikklassen / Services

- `FinanceManager.Infrastructure/Backups/BackupService.cs`
  - `BuildBackupDataAsync` (Z. 568 ff.): Export der neuen Sektionen `AccountLinkedIbans` und `PortfolioKpiConfigurations` ergänzen.
  - `RequiredDataProperties` (Z. 26–52): bleibt unverändert – neue Sektionen werden per AGENT.md-Konvention als optionale Sektionen geführt (Vorbild `SavingsPlanAccountLinks`, Z. 793).
- `FinanceManager.Infrastructure/Setup/SetupImportService.cs`
  - `ImportVersion3` (Z. 196 ff.): neue Importblöcke mit `TryGetProperty`-Guard für `StatementImports`, `StatementEntries`, `Notifications`, `AccountShares`, `AccountLinkedIbans` und `PortfolioKpiConfigurations`.
  - `BudgetRules`-Importblock (Z. 530–572): neue DTO-Felder an den `BudgetRule`-Konstruktor durchreichen.
  - `ImportProgress.Total` (Z. 205): Schrittzahl für die neuen Importblöcke erhöhen.
  - Hinweis: Die MCP-Sektionen (`McpAccesses`, `McpAccessScopes`, `McpAuditEntries`, `McpAuditEntryChanges`, `AiActionRequests`) sind bewusst nicht importiert und nicht betroffen – sie werden von `ClearUserDataAsync` nicht gelöscht (Kommentar Z. 1232–1236).
- `FinanceManager.Infrastructure/AppDbContext.cs`
  - `ClearUserDataAsync` (Z. 970 ff.): `BudgetRules`-Löschung (Z. 1134–1144) berücksichtigt aktuell nur `BudgetPurposeId`-gebundene Regeln; Regeln mit ausschließlich `BudgetCategoryId` überleben den Clear und werden beim Import dupliziert.

### Datenmodellklassen / DTO-Erweiterungen

- `FinanceManager.Domain/Budget/BudgetRule.cs`
  - `BudgetRuleBackupDto` (Z. 223–232): um `PurposePattern` (`string?`, Default `null`) und `PurposePatternIsRegex` (`bool`, Default `false`) erweitern – beide persistiert (Z. 77, 82), gehen aktuell beim Restore verloren.
  - `ToBackupDto()` (Z. 238) und `AssignBackupDto()` (Z. 245) entsprechend anpassen.
- `FinanceManager.Domain/Securities/Security.cs`
  - `SecurityBackupDto` (Z. 271): um `HasPriceError` (Z. 105), `PriceErrorMessage` (Z. 111), `PriceErrorSinceUtc` (Z. 117), `GoldenCrossNotifiedPhase` (Z. 130) und `TrendReversalNotifiedStatus` (Z. 146) mit Default-Werten erweitern.
  - `ToBackupDto()` (Z. 277) und `AssignBackupDto()` (Z. 285) entsprechend anpassen.
- `FinanceManager.Domain/Accounts/AccountLinkedIban.cs`
  - `AccountLinkedIbanBackupDto` existiert bereits (Z. 42); `ToBackupDto()`/`AssignBackupDto()` fehlen und sind ggf. zu ergänzen. Die Entität besitzt kein `OwnerUserId` – die Benutzerzuordnung erfolgt über `AccountId`.
- `FinanceManager.Domain/Portfolio/PortfolioKpiConfiguration.cs`
  - Neuer `PortfolioKpiConfigurationBackupDto` sowie `ToBackupDto()`/`AssignBackupDto()` zu erstellen (beides existiert noch nicht). Benutzerbezogen über `OwnerUserId`.
- Bereits vollständig vorhandene DTO-Infrastruktur (keine Domain-Änderung nötig):
  - `StatementImport.StatementImportBackupDto` (`FinanceManager.Domain/Statements/StatementImport.cs`, Z. 72)
  - `StatementEntry.StatementEntryBackupDto` (`FinanceManager.Domain/Statements/StatementEntry.cs`, Z. 142)
  - `Notification.NotificationBackupDto` (`FinanceManager.Domain/Notifications/Notification.cs`, Z. 91)
  - `AccountShare.AccountShareBackupDto` (`FinanceManager.Domain/Accounts/AccountShare.cs`, Z. 57)

### Interfaces / Enums

- Keine neuen Interfaces oder Enums erforderlich. Bereits vorhandene Enum-Typen im Backup-Kontext: `ImportFormat`, `StatementEntryStatus`, `NotificationType`, `NotificationTarget`, `AccountShareRole`, `GoldenCrossPhase`, `TrendStatus`.

### UI-Komponenten / Controller

- Keine Änderungen – Restore-Auslösung über `BackupsController`/`BackupService.ApplyAsync` bleibt unverändert.

### Tests

- `FinanceManager.Tests/Infrastructure/BackupServiceFullExportTests.cs` (Export-Abdeckungs-Guard, erweitern).
- `FinanceManager.Tests/Infrastructure/SetupImportServiceTests.cs` und `BackupServiceTests.cs` (erweitern).
- `FinanceManager.Tests/Infrastructure/SavingsPlanLinkBackupRestoreTests.cs` dient als Muster für neue Round-Trip-Tests (Export → Clear → Import → Vergleich mit ID-Remapping) für alle neuen und geänderten Sektionen.

## Implementierungsansatz

1. **Import der vier fehlenden Sektionen in `ImportVersion3`** – jede als `TryGetProperty`-Guard-Block (fehlende Sektion = leere Liste), mit ID-Remapping über die bestehenden Maps:
   - `StatementImports` nach dem `Accounts`-Block: `AccountId` via `accountMap` remappen, neue `statementImportMap` aufbauen.
   - `StatementEntries` nach `StatementImports`, `Contacts`, `SavingsPlans` und `Postings`: `StatementImportId` via `statementImportMap`, `ContactId` via `contactMap`, `SavingsPlanId` via `savingsMap` remappen; `SecurityTransactionId` ggf. via `postingMap` (Ziel der Referenz in der Bestandsaufnahme verifizieren).
   - `Notifications`: `OwnerUserId` auf `userId` setzen (`AssignBackupDto` übernimmt ihn nicht).
   - `AccountShares` nach dem `Accounts`-Block: `AccountId` via `accountMap` remappen; Import nur sinnvoll, wenn das referenzierte Konto importiert wurde (Sonderfall Shares auf Fremdkonten – siehe Offene Fragen).
2. **Neue optionale Backup-Sektionen** nach dem `SavingsPlanAccountLinks`-Muster (Export in `BuildBackupDataAsync`, Guard-Import, nicht in `RequiredDataProperties`):
   - `AccountLinkedIbans`: Export über die `AccountId`s der Benutzerkonten (kein `OwnerUserId` am Entity); Import nach dem `Accounts`-Block mit `accountMap`-Remapping, Einträge ohne gemapptes Konto überspringen. `Account.LinkedIbans` werden aktuell per DB-Cascade (`DeleteBehavior.Cascade`, `AppDbContext` Z. 463–466) mit dem Konto gelöscht.
   - `PortfolioKpiConfigurations`: Export über `OwnerUserId == userId`; eine Zeile pro Benutzer – Import als Ersetzen/Aktualisieren der Bestandskonfiguration statt Insert (kein `ClearUserDataAsync`-Eintrag, damit alte Backups ohne diese Sektion die vorhandene Konfiguration nicht verlieren – Annahme, siehe Offene Fragen).
3. **DTO-Erweiterungen mit Defaults** (AGENT.md-Regel: neue DTO-Parameter erhalten immer Default-Werte, damit Deserialisierung alter Backups nicht fehlschlägt):
   - `BudgetRuleBackupDto`: `string? PurposePattern = null`, `bool PurposePatternIsRegex = false`; beim Import über die vorhandenen Konstruktorparameter `purposePattern`/`purposePatternIsRegex` bzw. `SetPurposePattern` in `AssignBackupDto` wiederherstellen.
   - `SecurityBackupDto`: `HasPriceError = false`, `PriceErrorMessage = null`, `PriceErrorSinceUtc = null`, `GoldenCrossNotifiedPhase = GoldenCrossPhase.Far`, `TrendReversalNotifiedStatus = TrendStatus.Intact`. Wiederherstellung über `SetGoldenCrossNotifiedPhase`/`SetTrendReversalNotifiedStatus`; `PriceErrorSinceUtc` muss in `AssignBackupDto` direkt gesetzt werden, da `SetPriceError` den Zeitstempel auf `UtcNow` stempelt.
4. **`ClearUserDataAsync`-Korrektur**: `BudgetRules`-Filter auf alle Regeln mit `OwnerUserId == userId` erweitern (sowohl `BudgetPurposeId`- als auch `BudgetCategoryId`-gebundene), damit der Clear vollständig ist und beim Re-Import keine Duplikate entstehen.
5. **Abwärtskompatibilität**: `AllowedBackupVersions` bleibt `[3]` – neue Sektionen sind optional, neue DTO-Felder haben Defaults; alte Version-3-Backups bleiben einlesbar.
6. **Round-Trip-Tests**: Export → `ClearUserDataAsync` → Import → Feld-/Mengenvergleich für `StatementImports`, `StatementEntries`, `Notifications`, `AccountShares`, `AccountLinkedIbans`, `PortfolioKpiConfigurations`, `BudgetRule` (inkl. Pattern) und `Security` (inkl. Fehler-/Notified-Felder) sowie ein Duplikations-Test für kategoriegebundene `BudgetRules`.

## Konfiguration

Keine neue Konfiguration erforderlich. Die bestehenden Grenzwerte in `BackupSecurityOptions` (`Backups:Security`) und `AllowedBackupVersions = [3]` bleiben unverändert. Das Feature ist nicht konfigurierbar – die Backup-Vollständigkeit gilt projektweit als invariante Pflicht (AGENT.md).

## Offene Fragen

1. **`AccountShares` auf Fremdkonten:** Der Export erfasst auch Shares, bei denen der Sichernde selbst Share-Empfänger ist (`s.UserId == userId`) und deren `AccountId` auf Konten anderer Benutzer zeigt. Diese Konten stehen nicht im Backup – soll beim Restore die Original-`AccountId` unverändert übernommen werden (auf derselben Instanz ggf. gültig) oder sollen solche Shares übersprungen werden?
2. **`AccountShares.UserId`-Zielnutzer:** Verweist auf den Empfänger-Benutzer. Ist dieser auf dem Zielsystem nicht vorhanden (z. B. Neuinstallation/andere Instanz), entsteht eine verwaiste Freigabe – überspringen oder trotzdem importieren?
3. **`PortfolioKpiConfigurations` – Restore-Semantik:** Annahme: Sektion optional, vorhandene Konfiguration bleibt bei alten Backups erhalten und wird bei vorhandener Sektion ersetzt (Upsert statt Clear). Ist dieses Verhalten gewünscht, oder soll die Tabelle analog zu `HomeKpis` in `ClearUserDataAsync` gelöscht werden (mit dem Risiko des Verlusts bei alten Backups)?
4. **`StatementEntry.RawHash`-Unique-Index:** `RawHash` ist global eindeutig indiziert (`AppDbContext` Z. 503). Beim Import ohne `replaceExisting` können Kollisionen mit verbliebenen Einträgen auftreten – soll der Import Duplikate tolerierend überspringen oder ist der Restore-Pfad ausschließlich `replaceExisting: true` relevant?
5. **`StatementEntry.SecurityTransactionId`:** Das Feld wird nur gelesen (`StatementImportQueryService`), nirgends geschrieben – vermutlich historische `Posting`-Referenz. In der Bestandsaufnahme klären, ob ein Remapping via `postingMap` nötig ist oder das Feld unverändert durchgereicht werden kann.
6. **Fortschrittsanzeige:** `ImportProgress.Total` (aktuell 30) muss um die neuen Importblöcke erhöht werden – prüfen, ob die fest kodierte Zahl konsistent angepasst oder dynamisch ermittelt werden soll.
