# Bestandsaufnahme: Postenart Zinsen (Issue #541)

Analysiert wurden das Datenmodell, die Klassifizierungs-/Buchungslogik, die Hintergrundtask-Infrastruktur, Backup/Restore, UI-ViewModels und Tests — bezogen auf die Anforderung einer fachlichen Postenart (Eingang/Ausgang/Zins/Steuer) für Bankposten und Kontoauszugszeilen inkl. kontoabhängiger Kontierungsregeln und Hintergrund-Neubewertung.

## Zusammenfassung

- **Feature existiert noch nicht:** Weder `Posting` noch `StatementDraftEntry` tragen eine Postenart; es gibt keinen Enum für Buchungsart oder Regel-Textfeld, keine Regel-Entität (`AccountPostingRule` o. ä.), kein `DbSet`, keine UI-Auswahl, keine Neubewertungs-Task.
- **Kernpfad vorhanden:** `ClassifyInternalAsync` (`StatementDraftService.Classification.cs:119`) lädt bereits das `DetectedAccountId`-Konto und ist der natürliche Einschubpunkt für Regeln + Vorzeichen-Fallback; `BookCoreAsync` (`StatementDraftService.cs:2350`, lokale Funktion `CreateBankAndContactPostingAsync` :2421) erzeugt den `PostingKind.Bank`-Posten — dort würde die Postenart übertragen.
- **Etablierte Muster für alle Neubauten vorhanden:**
  - Child-Entität am Konto: `AccountLinkedIban` (eigenes `DbSet`, Cascade-Delete, Besitzerprüfung über `Account.OwnerUserId`, `Get/Add/Remove`-Service-Trio, `{id}/linked-ibans`-Endpunkte im `AccountsController`, `LinkedIbansPanel`-Komponente).
  - Hintergrundprozess: `BackgroundTaskType` + `IBackgroundTaskExecutor` (Payload-Parsing-Muster in `BookingTaskExecutor.ParseOptions`), Enqueue via `IBackgroundTaskManager.Enqueue(type, userId, payload, allowDuplicate)`; Registrierung in `ProgramExtensions.cs:164–171`.
  - Backup: `*BackupDto`-Records mit Default-Endparametern (`PostingBackupDto`, `AccountBackupDto`), optionale Sektionen in `BuildBackupDataAsync` + `TryGetProperty`-Guard im `SetupImportService` (Vorbild `AccountLinkedIbans` :471–500).
  - SubType-Fachmuster am `Posting`: `SecuritySubType`/`SavingsPlanSubType` inkl. `Set…SubType`-Methoden und DTO-Defaults in `PostingServiceDto`.
  - UI: Enum-Lookup `lookupType: "Enum:<EnumName>"` in `CardField` (Vorbild `SecurityTransactionType`, `StatementDraftEntryCardViewModel.cs:146`); Ribbon-Gruppenreihenfolge im `BankAccountCardViewModel` bereits AGENT.md-konform (Navigation → Manage → Bookings → Linked).
- **EnumCoverage-Falle:** `BackgroundTaskType_Values` (`EnumCoverageTests.cs:154`) assertiert alle 7 Mitglieder — ein neues Mitglied ohne Test-Ergänzung lässt den Test fehlschlagen. `PostingKind`/`SecurityPostingSubType`/`StatementDraftEntryStatus` sind aktuell **nicht** coverage-getestet; neue Enums (Postenart, Regel-Textfeld) müssen laut AGENT.md ergänzt werden.
- **Demodaten:** `DemoDataService` legt vier Bankkonten an (`giroAccount`, `primarySavingsAccount`, `secondarySavingsAccount`, `bausparAccount`, :435–476) und erzeugt bereits Zins-Einträge („Zinsgutschrift" :951, „Jahresendzinsen 1 %" :960–965) — keine Regeln vorhanden.
- **Test-Ausgangszustand:** Alle drei Suiten grün — Unit 2326/2326 (Exit 0), Integration 172/172 (Exit 0), E2E 163/163 (Exit 0). **Keine nachgewiesenen preexisting Fehler.** Details und TRX-Nachweise: [Tests](inventory/tests.md).

## Details

- [Datenmodell](inventory/models.md)
- [Enums](inventory/enums.md)
- [Interfaces](inventory/interfaces.md)
- [Logik / Services / UI](inventory/logic.md)
- [Tests](inventory/tests.md)
