# Übersetzte Anforderung: Postenart Zinsen (Issue #541)

## Fachliche Zusammenfassung

Bank-Postings (`Posting` mit `PostingKind.Bank`) sowie die ihnen vorgelagerten Kontoauszug-Entwurfszeilen (`StatementDraftEntry`) erhalten eine fachliche Buchungsart („Postenart") mit den Werten Eingang, Ausgang, Zins und Steuer. Die Postenart wird automatisch bestimmt: vorrangig über am Bankkonto (`Account`) hinterlegte Kontierungsregeln (Textfeld + enthaltener Text → Buchungsart), ansonsten über das Vorzeichen des Betrags (negativ = Ausgang, positiv = Eingang). Der Anwender kann die ermittelte Postenart in der Kontoauszugzeile vor dem Buchen korrigieren; beim Buchen wird sie auf den erzeugten Posten übertragen. Zusätzlich gibt es am Bankkonto eine Aktion, die die Postenart aller bereits gebuchten Posten des Kontos in einem Hintergrundprozess (bestehendes `IBackgroundTaskManager`/`IBackgroundTaskExecutor`-Muster) neu bewertet. Die Demodaten werden um Kontierungsregeln für „Zinsen" und „Kapitalertragssteuer" an den Demo-Bankkonten erweitert.

## Fachlicher Scope

- **Verarbeitungseinheit:** Die Postenart eines einzelnen Bankpostens — konkret eines `StatementDraftEntry` (Kontoauszugzeile vor dem Buchen) bzw. des daraus entstehenden `Posting` (`PostingKind.Bank`) eines `Account`. Die Neubewertung verarbeitet die Gesamtheit der gebuchten Bank-Postings genau eines Bankkontos je ausgelöstem Lauf.
- **Auslösende Ereignisse:**
  - Automatisch: Klassifizierung einer Kontoauszugzeile (`ClassifyInternalAsync` in `StatementDraftService`, u. a. beim Import, bei `ClassifyAsync`, bei `SetAccountAsync`) — Regeln vor Vorzeichenlogik.
  - Nutzeraktion: manuelle Auswahl/Änderung der Postenart in der Kontoauszugzeile vor dem Buchen.
  - Systemereignis beim Buchen: Übernahme der (ggf. manuell korrigierten) Postenart auf den gebuchten `Posting` in `BookAsync`.
  - Nutzeraktion am Bankkonto: Auslösen der Neubewertung des gebuchten Postenbestands als Hintergrundprozess (`IBackgroundTaskManager.Enqueue` mit neuem `BackgroundTaskType`).
  - Nutzeraktion: Anlegen/Bearbeiten/Entfernen von Kontierungsregeln am Bankkonto.
- **Erwartete Wirkung:**
  - `StatementDraftEntry` und `Posting` tragen eine persistierte Postenart; der gebuchte Bankposten zeigt die (regel- oder vorzeichenbasiert bzw. manuell gesetzte) Buchungsart.
  - In der Kontoauszugzeile ist die automatisch ermittelte Postenart sichtbar und vor dem Buchen änderbar.
  - Nach der Neubewertung tragen alle gebuchten Bank-Postings des Kontos die per aktueller Regel-/Vorzeichenlogik ermittelte Postenart; Fortschritt und Ergebnis sind wie bei anderen Hintergrundprozessen sichtbar (`BackgroundTaskInfo`, `BackgroundTaskStatusPanel`).
  - Am Bankkonto sind Kontierungsregeln verwaltbar; Demodaten-Bankkonten besitzen Regeln für „Zinsen" und „Kapitalertragssteuer".
- **Umgang mit fehlenden Objekten:**
  - Konto ohne Kontierungsregeln: Regelschritt entfällt, Vorzeichenlogik greift (Eingang/Ausgang). Kein Objekt wird erzeugt.
  - Mehrere zutreffende Regeln: Verhaltensfestlegung erforderlich (siehe Offene Fragen); Annahme: erste zutreffende Regel in definierter Reihenfolge gewinnt.
  - Regel trifft zu → Regel-Buchungsart (auch Zins/Steuer unabhängig vom Vorzeichen); keine Regel trifft zu → Vorzeichenbestimmung.
  - Bestehender Datenbestand ohne neue Felder: alte Backups und bestehende Postings ohne Postenart müssen ladbar bleiben (Default-Werte im Backup-DTO; Verhalten für Alt-Postings siehe Offene Fragen).
- **Scope-Ausschlüsse:**
  - Keine Auswertung der Postenart in Berichten, Budgetplanung, KPIs oder `PostingAggregate` — gefordert ist nur Erfassung, Speicherung, Anzeige und Neubewertung.
  - Keine Einzelbearbeitung der Postenart an bereits gebuchten Postings über die Posten-Ansicht; Änderung gebuchter Posten erfolgt ausschließlich über die Neubewertungsfunktion am Konto.
  - Keine Kontierungsregeln für Kontakte, Sparpläne oder Wertpapiere — nur Bankkonten; nur die Buchungsart des Bankpostens ist adressiert.
  - Keine komplexen Regelbedingungen (Regex, Betragsgrenzen, Datumsbereiche, Kombinationen) — eine Regel ist genau „ein Textfeld enthält einen Text".
  - Keine Änderung von Betrag, Kontaktzuordnung, `IsCostNeutral`, Sparplan-/Wertpapierzuordnung oder Buchungslogik — die Regel bestimmt ausschließlich die Postenart.
  - Keine automatische Aufteilung/Verrechnung von Steuerbeträgen (z. B. keine Erzeugung separater Steuerposten wie bei `SecurityPostingSubType.Tax`).

## Betroffene Klassen und Komponenten

### Datenmodell (FinanceManager.Domain)
- `Posting` (`FinanceManager.Domain/Postings/Posting.cs`): neue Eigenschaft für die Postenart (z. B. `PostingType`/`BankPostingType`, analog zum Muster `SecuritySubType`/`SavingsPlanSubType`), inkl. Set-Methode und Erweiterung von `PostingBackupDto` (neuer Parameter mit Default für alte Backups), `ToBackupDto()`, `AssignBackupDto()`.
- `StatementDraftEntry` (`FinanceManager.Domain/Statements/StatementDraft.cs`): neue Eigenschaft für die gewählte/vorgeschlagene Postenart inkl. Set-Methode und `StatementDraftEntryBackupDto`-Erweiterung mit Default-Wert.
- Neue Entität für Kontierungsregeln (z. B. `AccountPostingRule`, Namespace `FinanceManager.Domain.Accounts`, Muster `AccountLinkedIban`): `AccountId`, Auswahl des Textfelds (`Subject` = Verwendungszweck, `BookingDescription` = Buchungsbeschreibung), Suchtext, Ziel-Postenart; `OwnerUserId` über Besitzerpfad `Account` (AGENT.md: Abfragen über den Besitzerpfad filtern), Backup-DTO mit Defaults.
- Neue Enums: Postenart (Eingang/Ausgang/Zins/Steuer) und Regel-Textfeld (Verwendungszweck/Buchungsbeschreibung) — Ablage nach Projektkonvention unter `FinanceManager.Shared/Dtos/...` bzw. Domain; `PostingKind` ist eine andere Achse (Ziel-Domäne) und bleibt unverändert.

### Shared DTOs (FinanceManager.Shared)
- `StatementDraftEntryDto`, `PostingServiceDto`: Erweiterung um die Postenart.
- DTOs/Requests für Regelverwaltung und Postenart-Update (Muster `StatementDraftSaveEntryAllRequest`, `AccountLinkedIbanUpsertRequest`, Request für Setzen der Postenart am Entry).
- `BackgroundTaskType` (`FinanceManager.Shared/Dtos/Admin/BackgroundTaskType.cs`): neues Mitglied für die Postenart-Neubewertung.

### Logik / Services
- `StatementDraftService.Classification.cs` (`ClassifyInternalAsync`): Ermittlung der Postenart pro Entry — zuerst Regeln des `draft.DetectedAccountId`-Kontos, dann Vorzeichen-Fallback.
- `StatementDraftService` (`BookAsync`, `CreatePostings`-Abschnitte, `SaveEntryAllAsync`, `UpdateEntryCoreAsync`, `ApplyBatchEntryUpdatesAsync`): Übernahme/Setzen der Postenart; Übertrag auf den erzeugten `Posting` beim Buchen.
- `IStatementDraftService` + `StatementDraftService`: neuer bzw. erweiterter Anwendungsfall zum Setzen der Postenart eines Entries.
- `IAccountService`/`AccountService` bzw. eigener Service: CRUD der Kontierungsregeln pro Konto; Service-/Controller-Muster analog `AccountLinkedIban` (`AccountsController`).
- Neuer `IBackgroundTaskExecutor` in `FinanceManager.Web/Services` (Muster `RebuildAggregatesTaskExecutor`, `BookingTaskExecutor`): iteriert gebuchte Bank-Postings des Kontos und wendet Regeln/Vorzeichenlogik an; Registrierung in `ProgramExtensions.cs`; Enqueue-Auslöser (z. B. `AccountsController`-Endpunkt oder `BackgroundTasksController`).

### Persistenz / Infrastruktur
- `AppDbContext`: `DbSet` + EF-Konfiguration für die neue Regel-Entität; Spalten für die neue Postenart an `Postings` und `StatementDraftEntries`.
- EF-Migration per `dotnet ef migrations add` (niemals manuell, laut AGENT.md).
- `BackupService.BuildBackupDataAsync` (Export der Regeln als neue Sektion, nicht in `RequiredDataProperties`) und `SetupImportService` (Import mit `TryGetProperty`-Guard); neue Felder an `Posting`/`StatementDraftEntry` über deren Backup-DTOs mit Defaults.

### UI (FinanceManager.Web)
- Kontoauszugzeile (`CardPage`/`GenericCardPage` bzw. `QuickEditTable` für `StatementDraftEntry`): Auswahlfeld für die Postenart.
- `BankAccountCardViewModel`: Ribbon-Aktion für die Neubewertung (Gruppierung gemäß AGENT.md — „größere Aktion mit dem Datensatz", z. B. `Ribbon_Group_Actions`/`Ribbon_Group_Bookings`); Verwaltungsoberfläche für Kontierungsregeln (Muster `LinkedIbansPanel`).
- `PostingServiceDto`-Anzeige (Postenliste `AccountPostingsListViewModel`): Darstellung der Postenart am gebuchten Posten.
- `Resources/*.resx` (de + en) für alle neuen Labels; Icons aus `/icons/sprite.svg`; `MobileShortcut` auf der primären Aktion; `@rendermode InteractiveServer` auf neuen Seiten.

### Demodaten
- `DemoDataService` (`FinanceManager.Infrastructure/Demo/DemoDataService.cs`): Kontierungsregeln „Zinsen" und „Kapitalertragssteuer" an den Demo-Bankkonten (`giroAccount`, `primarySavingsAccount` etc.).

### Tests
- `EnumCoverageTests` (`FinanceManager.Tests/Common/EnumCoverageTests.cs`): neue Enum-Mitglieder ergänzen (Postenart, Regel-Textfeld, `BackgroundTaskType`-Mitglied).
- Unit-Tests: Regel-Matching, Vorzeichen-Fallback, manuelle Überschreibung, Übertrag beim Buchen, Neubewertungs-Executor.
- Backup/Restore-Tests (`FinanceManager.Tests/Infrastructure/*Backup*Tests.cs`): neue Felder/Sektion + Lesen alter Backups.
- Ggf. Integration/API-Tests für neue Endpunkte.

## Implementierungsansatz

- **Bewertungslogik zentral kapseln:** Eine gemeinsame Domänen-/Service-Logik „Postenart bestimmen" (Regeln zuerst, dann Vorzeichen), die sowohl bei `ClassifyInternalAsync` (Draft-Entries) als auch im Neubewertungs-Executor (gebuchte `Posting`s) verwendet wird. Da gebuchte Postings `Subject`/`Description` tragen, sind beide Regel-Textfelder auch nachträglich auswertbar.
- **Datenfluss:** `StatementDraftEntry` erhält die Postenart bei der Klassifizierung und bei manueller Auswahl (neuer Service-Endpunkt bzw. Erweiterung des Save-All-Pfads); `BookAsync` überträgt sie beim Erzeugen des `Posting` (Parameter-/Set-Methode, analog `SetIsPreliminary`, `SetSavingsPlanSubType`).
- **Regelverwaltung:** Eigene Child-Collection am `Account` im Stil von `AccountLinkedIban` (eigenes `DbSet`, eigener Controller-/Service-Pfad, eigenes Panel in der Konto-Karte). Besitzerprüfung über `Account.OwnerUserId`.
- **Hintergrundprozess:** Neues `BackgroundTaskType`-Mitglied + neuer `IBackgroundTaskExecutor` (Payload: `AccountId`); Ribbon-Aktion am Bankkonto queued den Task; Fortschritt über `context.ReportProgress`; Idempotenz entsteht über `Enqueue(..., allowDuplicate: false)`.
- **Backup-Vollständigkeit (AGENT.md):** Neue Postenart-Felder in die bestehenden `*BackupDto`-Records mit Default-Werten; Regel-Collection als optionale Backup-Sektion.
- **Enum-Pflege:** Neue Enum-Mitglieder in `EnumCoverageTests` ergänzen; UI-Texte in resx (de/en).

## Konfiguration

- Kontierungsregeln sind **pro Datensatz (Bankkonto/`Account`)** konfigurierbar — Verwaltung auf der Konto-Detailseite (Muster `LinkedIbansPanel`/`AccountLinkedIban`).
- Die Regel-Feldliste (Verwendungszweck/`Subject`, Buchungsbeschreibung/`BookingDescription`) ist als Enum modelliert und damit erweiterbar („ein verfügbares Textfeld").
- Keine globale Anwendungseinstellung gefordert.

## Offene Fragen

1. **Match-Semantik der Regeln:** Ist „enthält" case-insensitive und als Teilstring zu verstehen? Soll die Normalisierung analog zur bestehenden Klassifizierung erfolgen (`NormalizeUmlauts`, Groß-/Kleinschreibung, Whitespace)?
2. **Konflikte bei mehreren zutreffenden Regeln:** Gewinnt die erste Regel in einer Reihenfolge (dann bräuchte die Regel ein Sortierkriterium), die spezifischste, oder soll der Eintrag zur Prüfung markiert werden?
3. **Betrag = 0:** Welche Vorzeichen-Buchungsart gilt bei Beträgen von 0, wenn keine Regel greift (Annahme: Eingang, da nur negative Beträge als „Ausgang" definiert sind)?
4. **Gegenpostings:** Soll die Postenart auch auf die gruppierten Gegenpostings derselben `GroupId` (z. B. `PostingKind.Contact`) übertragen werden oder ausschließlich auf den Bankposten? (Annahme laut Anforderung: nur Bankposten.)
5. **Altbestand / Standardwert:** Welche Postenart erhalten bestehende Postings nach dem Update ohne Neubewertung — Default anhand des Vorzeichens oder „unbestimmt"/nullable?
6. **Umfang der Neubewertung:** Gilt sie auch für stornierte (`ReversedByPostingId`), Reversal- und vorläufige (`IsPreliminary`) Postings sowie für Split-Child-Postings, oder nur für reguläre gebuchte Bankposten?
7. **Verfügbare Textfelder:** Sind wirklich nur `Subject` (Verwendungszweck) und `BookingDescription` (Buchungsbeschreibung) wählbar, oder soll `RecipientName` (Empfänger) ebenfalls als regelbasiertes Feld angeboten werden?
8. **Überschreibschutz:** Soll eine manuell gesetzte Postenart am Entry durch eine spätere Re-Klassifizierung (`ClassifyAsync` läuft automatisch erneut, z. B. bei `SetAccountAsync`/Batch-Update) zurückgesetzt werden, oder muss die manuelle Wahl als fixiert markiert werden?
9. **Regeländerung am Konto:** Sollen offene (ungebuchte) Draft-Entries nach dem Speichern von Regeln automatisch neu bewertet werden, oder erst bei der nächsten regulären Klassifizierung?
