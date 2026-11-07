# Abnahmeprüfung – Entwicklungsschritt 9

## Ergebnis

**Status:** Anforderung vollständig erfüllt

## Abweichungen

Keine.

## Hinweise

- **Prüfumfang:** `git diff c11a6267..HEAD` (Schritt 9 ist in Commit `67cf45ac` enthalten) gegen Schritt 9 in `project-plan.md` (Zeilen 166–177) sowie `requirement.md` (Konfiguration: Backup/Restore; AK 10). Code-Review plus Testlauf.

- **AK 1 – Export→Import erhält Links mit ID-Remapping und Istmeldung-Marker:** Export: `BackupService.BuildBackupDataAsync` sammelt `SavingsPlanAccountLinks` owner-scoped (`FinanceManager.Infrastructure/Backups/BackupService.cs:619–624`) und hängt sie als `SavingsPlanAccountLinks`-Sektion an die Payload (Zeile 793). Import: `SetupImportService.cs:445–465` deserialisiert die optionale Sektion **nach** Plan- und Konto-Import (`savingsMap`/`accountMap` bereits gefüllt, Zeilen 411/439) und legt `new SavingsPlanAccountLink(mappedPlan, mappedAccount, userId)` an; Links ohne überlebende Eltern werden übersprungen statt zu fehlschlagen. Marker: `PostingBackupDto` führt `SavingsPlanSubType` mit Default `null` (`FinanceManager.Domain/Postings/Posting.cs` — `ToBackupDto`/`AssignBackupDto` angepasst); Import setzt `entity.SetSavingsPlanSubType(...)` (`SetupImportService.cs:630`). Istmeldung-Postings werden automatisch exportiert, da die Posting-Einsammlung `SavingsPlanId`-Referenzen umfasst (`BackupService.cs:645–650`). Die `AccountLinkedIban`-Lücke (BackupDto ohne Export) ist vermieden.

- **AK 2 – alte Backups gültig, Payload-Version unverändert:** `SavingsPlanAccountLinks` ist **nicht** in `RequiredDataProperties` (Whitelist `BackupService.cs:26–52` unverändert); der Import liest die Sektion per `TryGetProperty` optional. `PostingBackupDto.SavingsPlanSubType` hat Default `null` → alte NDJSON-Zeilen ohne das Feld deserialisieren. Payload `Version = 3` und `AllowedBackupVersions` unverändert (Zeile 119). Getestet: `ImportAsync_PayloadWithoutLinkSection_StillWorks` (Backup ohne Sektion importiert fehlerfrei, 0 Links).

- **Testnachweis:** `dotnet test FinanceManager.Tests` (gefiltert) → `SavingsPlanLinkBackupRestoreTests` **3/3 bestanden**: `CreateAsync_Backup_ContainsLinksAndActualReportMarker` (NDJSON enthält Sektion + Marker-Feld), `ImportAsync_RestoresLinksWithRemappedIdsAndMarker` (Link zeigt auf neue Plan-/Konto-IDs, Posting behält `ActualReport`), `ImportAsync_PayloadWithoutLinkSection_StillWorks`.

- **Fortschrittszählung:** `Total` im Import-Fortschritt von 29 auf 30 Schritte erhöht und eigener Schritt „Savings Plan Account Links" eingefügt (`SetupImportService.cs:205`, 446–465) — konsistent mit der UI-Fortschrittsanzeige.

- **Randnotiz:** Links werden beim Import mit `userId` des importierenden Benutzers angelegt (nicht `dto.OwnerUserId`) — korrekt, da Backups single-owner sind und `OwnerUserId` im DTO nur Dokumentationszweck hat.
