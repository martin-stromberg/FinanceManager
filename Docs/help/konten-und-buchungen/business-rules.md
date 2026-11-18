← [Zurück zur Übersicht](index.md)

# Konten und Buchungen — Business Rules

## Stornierung nur bei gültigem Zustand

**Beschreibung:** Eine Buchung darf nur storniert werden, wenn sie fachlich reversibel ist.

**Bedingungen:**
- Die Buchung existiert.
- Die Buchung gehört zum Benutzerkontext.
- Es bestehen keine fachlichen Sperrgründe.

**Verhalten:**
- Wenn alle Bedingungen erfüllt sind: Gegenbuchung wird erzeugt und verknüpft.
- Sonst: Es wird ein Validierungsfehler zurückgegeben.

**Umsetzung:** `PostingReversalService.CanReverseAsync` und `PostingReversalService.ReversePostingAsync`.

## Kontozugriff ist eigentümergebunden

**Beschreibung:** Konten- und Buchungszugriffe sind auf den jeweiligen Benutzer beschränkt.

**Bedingungen:**
- Authentifizierter Benutzerkontext muss vorhanden sein.

**Verhalten:**
- Wenn Eigentümer passt: Daten werden geliefert oder geändert.
- Sonst: Zugriff wird abgelehnt.

**Umsetzung:** `AccountsController` und `PostingsController` mit benutzerbezogenen Serviceabfragen.

## Sammelkonten verwalten mehrere IBANs

**Beschreibung:** Ein Sammelkonto kann mehrere verknüpfte IBANs für Unterkonten, Sparbriefe oder Sparpläne führen.

**Bedingungen:**
- Das Konto gehört dem angemeldeten Benutzer.
- Die verknüpfte IBAN ist für dieses Konto noch nicht vorhanden.

**Verhalten:**
- Wenn die IBAN neu ist: Sie wird dem Sammelkonto zugeordnet.
- Wenn die IBAN bereits existiert: Die Änderung wird abgelehnt.

**Umsetzung:** `AccountsController.GetLinkedIbansAsync`, `AccountsController.AddLinkedIbanAsync`, `AccountsController.RemoveLinkedIbanAsync` und `AccountService`.

## Kontierungsregeln bestimmen die Postenart

**Beschreibung:** Ein Bankkonto kann Kontierungsregeln (`AccountPostingRule`) besitzen, die Bankpostings automatisch eine Postenart (`BankPostingType`: `Inflow`, `Outflow`, `Interest`, `Tax`) zuweisen. Eine Regel ist genau „ein Textfeld enthält einen Suchtext" — es stehen die Felder `Subject` (Verwendungszweck) und `BookingDescription` (Buchungsbeschreibung) zur Auswahl.

**Bedingungen:**
- Das Konto gehört dem angemeldeten Benutzer (Besitzerprüfung über `Account.OwnerUserId`).
- `Pattern` ist Pflicht, wird getrimmt und ist auf 200 Zeichen begrenzt.
- `PostingType` muss ein definierter Wert ungleich `Unspecified` sein.
- `OrderIndex` bestimmt die Auswertungsreihenfolge; ohne Angabe wird eine neue Regel ans Ende gehängt (`max + 1`).

**Verhalten:**
- Die Regeln werden in `OrderIndex`-Reihenfolge (Tiebreak `Id`) ausgewertet; die **erste** zutreffende Regel gewinnt.
- Der Vergleich ist ein normalisierter Teilstring-Abgleich: Umlaute werden ersetzt (ä → ae, ß → ss), Klein-/Großschreibung und Leerzeichen werden ignoriert — auf Feldwert und Suchtext gleichermaßen.
- Trifft keine Regel zu, greift der Vorzeichen-Fallback: `Amount < 0` → `Outflow`, sonst `Inflow` (Betrag 0 = `Inflow`).
- Nicht-Bankpostings (Kontakt-, Sparplan- und Wertpapier-Gegenpostings) erhalten keine Postenart (`Unspecified`).

**Umsetzung:** `PostingTypeEvaluator.Evaluate` (zentrale, statische Bewertungslogik inkl. `NormalizeForRuleMatch`/`NormalizeUmlauts`), Regel-CRUD in `AccountService.GetPostingRulesAsync`/`UpsertPostingRuleAsync`/`RemovePostingRuleAsync` hinter `AccountsController`-Endpunkten `GET/POST/DELETE {id}/posting-rules`.

## Postenarten-Neubewertung läuft als Hintergrundtask

**Beschreibung:** Die Ribbon-Aktion „Postenarten neu bewerten" am Bankkonto bewertet alle gebuchten Bankpostings des Kontos mit dem aktuellen Regel-/Vorzeichenstand neu.

**Bedingungen:**
- Das Konto existiert und gehört dem angemeldeten Benutzer (sonst `Err_NotFound`, im Task Abbruch mit `PTR_AccountNotFound`).
- Es darf nicht bereits ein gleicher Task für das Konto laufen (`allowDuplicate: false`).

**Verhalten:**
- `POST /api/accounts/{id}/posting-types/reclassify` queued `BackgroundTaskType.ReevaluatePostingTypes` mit Payload `{"accountId":"…"}` → `202 Accepted` mit `BackgroundTaskInfo`.
- Der Executor verarbeitet alle `PostingKind.Bank`-Postings des Kontos seitenweise (500er-Batches), setzt `posting.SetPostingType(PostingTypeEvaluator.Evaluate(...))` und meldet Fortschritt via `ReportProgress`; Abbruch über `CancellationToken`.
- Stornierte, Reversal- und vorläufige (`IsPreliminary`) Bankpostings werden ohne Sonderbehandlung mitbewertet; Gegenpostings anderer `PostingKind`s bleiben unberührt.
- Läuft der Task parallel zu neuen Buchungen, gewinnt die zuletzt geschriebene Postenart (idempotent, jederzeit wiederholbar).

**Umsetzung:** `AccountsController.EnqueuePostingTypeReevaluationAsync` (injizierter `IBackgroundTaskManager`), `PostingTypeReevaluationTaskExecutor` (`IBackgroundTaskExecutor`, `IServiceScopeFactory`-Scope, `ParseAccountId`-Payload-Parsing, PageSize 500); sichtbar über `BackgroundTaskStatusPanel` mit `BankAccountCardViewModel.VisibleBackgroundTaskTypes = { ReevaluatePostingTypes }`.

## Buchungen benötigen mindestens eine Zielreferenz

**Beschreibung:** Eine Buchung (`Posting`) trägt selbst kein `OwnerUserId` — der Besitzerbezug ergibt sich indirekt über die referenzierten Aggregate (`Account`, `Contact`, `SavingsPlan`, `Security`, alle mit `OwnerUserId`). Damit kein verwaistes Posting ohne erreichbaren Besitzerpfad entstehen kann, muss mindestens eine der vier Zielreferenzen gesetzt sein.

**Bedingungen:**
- Bei der Erzeugung eines `Posting` über den Konstruktor.
- Bei jedem `INSERT`/`UPDATE` auf der `Postings`-Tabelle.

**Verhalten:**
- `new Posting(...)` mit vier `null`-Referenzen: `ArgumentException` (Fail-fast, alle Überladungen delegieren auf den Hauptkonstruktor).
- `INSERT`/`UPDATE` mit vier `NULL`-Referenzspalten: Ablehnung durch den Check-Constraint `CK_Postings_HasReference` — fängt auch den Guard-umgehenden `AssignBackupDto`-Pfad beim `SaveChanges` ab.
- Beim Backup-Restore werden Postings übersprungen, deren remappte Referenzen allesamt `null` sind (`LogWarning`), statt einen Waisendatensatz anzulegen.
- Mehrere gesetzte Referenzen sind zulässig — die Invariante lautet „mindestens eine", nicht „genau eine".

**Umsetzung:** `Posting`-Hauptkonstruktor (Guard), `AppDbContext` (`HasCheckConstraint("CK_Postings_HasReference", ...)`, EF-Migration `AddPostingReferenceCheckConstraint`), `SetupImportService.ImportVersion3` (Skip-Regel im Posting-Import).
