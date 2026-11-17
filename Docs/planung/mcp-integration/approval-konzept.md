# Approval-Konzept – MCP-Integration für FinanceManager (Issue #470)

**Status:** Konzept (Planungsartefakt, keine Implementierung)
**Einordnung:** Artefakt 8 der Anforderung; spezifiziert den
Genehmigungsworkflow (F10) für kritische MCP-Aktionen. Die Liste
genehmigungspflichtiger Aktionen leitet sich aus der
Sicherheitsklassifizierung und der Übersichtstabelle von
[scope-und-toolkatalog.md](scope-und-toolkatalog.md) (Abschnitt 7) ab;
die Genehmigungsinstanz ist der „Kontext-Deckel" der Prüfstufe 3 aus
[berechtigungsmodell.md](berechtigungsmodell.md). Protokollierung aller
Entscheidungen: [audit-konzept.md](audit-konzept.md).

---

## 1. Zielbild und Grundsätze

Genehmigungspflichtige Aktionen eines MCP-Clients werden **nicht direkt**
ausgeführt, sondern als `AiActionRequest` persistiert und dem
Zugangsbesitzer zur Entscheidung vorgelegt. Erst nach expliziter
Freigabe (oder bei „dauerhaft vorab genehmigtem" Scope, siehe 3.4) wird
die Aktion ausgeführt – über denselben Service-Pfad, den auch die UI
nutzt.

**Verbindliche Grundsätze:**

1. **Der Benutzer entscheidet über Wirkung, nicht über Absicht.** Die
   Genehmigungsoberfläche zeigt eine Wirkungsvorschau aus dem
   Parameter-Snapshot (was genau verbucht/gelöscht/storniert wird) –
   nicht den vom Client formulierten Begleittext (Prompt-Injection-
   Schutz, `sicherheitskonzept.md` Abschnitt 5.3).
2. **Genehmigung ist zeitlich begrenzt und einmalig.** Eine Freigabe
   gilt für genau einen Request mit eingefrorenem Parameter-Snapshot;
   abgelaufene Anfragen verfallen, genehmigte werden genau einmal
   ausgeführt.
3. **Persistenz statt Speicher.** Requests überleben Prozess-Neustarts
   (Auto-Update, Service-Restart sind Normalbetrieb im
   Single-Instance-Deployment) – deshalb persistente Queue statt
   In-Memory-`IBackgroundTaskManager` (Abschnitt 5.1).
4. **„Vorab genehmigt" ist dokumentierte Genehmigung, kein Umgehen.**
   Der Genehmigungsmodus „dauerhaft vorab genehmigt" (Fachkonzept 5.1)
   führt die Aktion direkt aus, protokolliert aber dieselbe
   `AiActionRequest`-Spur – die Genehmigungsinstanz bleibt
   nachvollziehbar.
5. **Kein neuer fachlicher Pfad.** Die Ausführung ruft exakt die
   `I*Service`-Contracts des betreffenden Tools mit der `ownerUserId`
   des Besitzers; die Genehmigung verzögert die Ausführung, sie ändert
   sie nicht.

---

## 2. Katalog genehmigungspflichtiger Aktionen

Die Liste ergibt sich **mechanisch** aus der Spalte
„genehmigungspflichtig" der Übersichtstabelle (Toolkatalog, Abschnitt 7)
und der Zuordnungsregel „finale/irreversible Teilmengen sind
genehmigungspflichtig". Konsistenzbedingung: Jede Änderung der
Sicherheitsklassifizierung im Toolkatalog ändert diese Liste mit.

### 2.1 Immer genehmigungspflichtig (Klasse „genehmigungspflichtig")

| Tool/Aktion | Finale Wirkung | Scope |
|---|---|---|
| `fm_statement_draft_book` | Verbuchung von Entwürfen: erzeugt `Posting`, `StatementImport` + `StatementEntry`, aktualisiert `Account.CurrentBalance`, `PostingAggregate`-Rebuild, Budget-Impacts (`IBudgetImpactEvaluationService`) | (Kontoauszugsentwürfe, Buchung) |
| `fm_statement_draft_discard` | Entwurf/e verwerfen (physisch gelöscht) | (Kontoauszugsentwürfe, Löschung) |
| `fm_posting_reverse` | Storno: Gegenbuchung mit `ReversalForPostingId`/`ReversedByPostingId`, transaktional inkl. Gruppenbuchungen | (Buchungen, Löschung=Storno) |

### 2.2 Genehmigungspflichtige Teilmengen der Pflege-Tools (T22–T29)

Die Pflege-Tools sind Klasse „schreibvorbereitend", aber ihre
**finalen Teiloperationen** sind genehmigungspflichtig:

| Tool | Genehmigungspflichtige Teiloperation |
|---|---|
| `fm_contacts_manage` | Kontakt-**Merge** (`MergeAsync`, destruktiv) und Kontakt-**Löschen** |
| `fm_accounts_manage` | Konto-**Löschen** |
| `fm_savings_plans_manage` | **Archivierung** (`ArchiveAsync`/`ArchivedUtc` = Lösch-Äquivalent) |
| `fm_securities_manage` | **Archivierung** |
| `fm_budgets_manage` | Löschen von `BudgetPurpose`/`BudgetRule`/`BudgetOverride`/`BudgetCategory` |
| `fm_categories_manage` | Löschen der vier Kategorietypen |
| `fm_attachments_manage` | Löschen von Anhängen |
| `fm_reports_configuration_manage` | Löschen von `ReportFavorite`/`HomeKpi`/`PortfolioKpiConfiguration` |

### 2.3 Bedingt genehmigungspflichtig (Klasse „schreibvorbereitend")

Alle übrigen schreibvorbereitenden Aufrufe (Vervollständigung,
Änderung, Erstellung, Import) unterliegen dem **Genehmigungsmodus des
Zugangs** (Fachkonzept 5.1/6.1):

- **Stufe Sachbearbeiter (Standard):** je Aktion bzw. je Batch als
  `AiActionRequest` (Batch-Vervollständigung = ein Request pro
  `ApplyBatchEntryUpdatesAsync`-Aufruf); der Benutzer kann für
  vertrauenswürdige Zugänge auf Direktausführung umstellen.
- **Stufe Buchhalter (Standard):** Stammdaten-Änderungen direkt;
  Buchung/Löschung/Storno stets als Request (2.1/2.2).
- **„Dauerhaft vorab genehmigt" (pre-approved Scope):** Benutzer kann
  für vertrauenswürdige Zugänge auch finale Aktionen auf direkte
  Ausführung stellen. Auch dann wird ein `AiActionRequest`-Eintrag
  geschrieben – mit Status-Historie „Approved (vorab)" als
  dokumentierte Genehmigungsinstanz (Abschnitt 3.4).
- **Nie genehmigungspflichtig:** lesende und vorschlagende Tools sowie
  die Meta-Tools – sie haben keine schreibende Wirkung.

### 2.4 Freigabestufen-Konsistenz

Die Mindest-Freigabestufe je Tool (Toolkatalog, Abschnitt 5.3) bleibt
maßgeblich: `Buchung`/`Löschung` erfordern mindestens **Buchhalter**
bzw. einen entsprechenden „Individuell"-Scope. Die Genehmigung ist ein
**zusätzlicher** Deckel über dem Recht – sie ersetzt die
Scope-Prüfung nicht und kann sie nicht aufheben.

---

## 3. `AiActionRequest` – neue persistente Entität

`AiActionRequest` existiert **nicht** im Code (Inventory, Abschnitt 5)
und wird als neue benutzerbezogene Entität konzipiert. Fachliche
Feldfestlegung (Persistenzdetails: `datenmodell-erweiterungen.md`,
Schritt 4):

| Feld (fachlich) | Bedeutung |
|---|---|
| `Id` | Eindeutige Request-Id (`Guid`, `Entity`-Konvention) – wird dem Client synchron als Referenz zurückgegeben |
| `OwnerUserId` | Besitzer = Besitzer des anfordernden MCP-Zugangs |
| `McpAccessId` | Anfordernder Zugang (Zurechenbarkeit; bleibt auch nach Zugangslöschung protokollrelevant) |
| `ToolName` + `CatalogVersion` | Angefordertes Tool und Katalogversion zum Anforderungszeitpunkt |
| `ParameterSnapshot` | Eingefrorener Parameter-Satz des Aufrufs (serialisiert; Grundlage der Wirkungsvorschau und der Ausführung – **keine** Neuinterpretation nach Freigabe) |
| `ReasonText` | Optionaler Begleittext des Clients (untrusted content; in der UI als Daten markiert, nicht als Anweisung) |
| `Status` | Zustand gemäß Abschnitt 3.2 |
| `RequestedUtc` | Zeitpunkt der Anforderung |
| `ExpiresUtc` | Ablauf der Entscheidungsfrist (Abschnitt 3.3) |
| `DecidedUtc` / `DecidedByUserId` / `DecisionNote` | Entscheidung: wann, durch wen (immer der Besitzer), optionale Ablehnbegründung; bei vorab-genehmigtem Modus entsprechend markiert |
| `ExecutedUtc` | Ausführungszeitpunkt |
| `ExecutionResult` | Ergebnisreferenz (z. B. erzeugte `Posting`-Ids, `StatementImportId`, `BookingResult`-Kurzfassung) – Verknüpfungspfad ins Audit |
| `FailureReason` | Fehlerursache bei `Failed` (klassifiziert, s. 6.2) |
| `CorrelationId` | Verknüpfung Toolaufruf ↔ Request ↔ Audit-Einträge |
| `IdempotencyKey` | Aus Zugangs-Id + clientseitigem/serverseitigem Schlüssel abgeleiteter Duplikatschutz (Abschnitt 5.2) |

### 3.1 Grundsatz der Snapshot-Treue

Der `ParameterSnapshot` ist zum Zeitpunkt `RequestedUtc` eingefroren und
unveränderlich. Der Client kann die Parameter nicht nachträglich ändern
(Änderung = neuer Request); der Benutzer kann beim Genehmigen **nicht**
editieren, nur freigeben oder ablehnen (Ablehnung + Hinweis an den
Client → Client stellt ggf. neue Anfrage). Das verhindert
Bait-and-Switch-Angriffe, bei denen die genehmigte Aktion nachträglich
eine andere wäre.

### 3.2 Zustandsmodell

Fünf Kernzustände (Vorgabe der Anforderung) plus **ein dokumentierter
Zusatzzustand** `Expired`, weil „abgelaufen" fachlich weder
„vom Benutzer abgelehnt" noch „fehlgeschlagen" ist und die
Audit-Auswertbarkeit die Unterscheidung braucht:

```text
                 ┌──────────┐
                 │ Pending  │  (angelegt, Benachrichtigung an Besitzer)
                 └────┬─────┘
        ┌─────────────┼──────────────────┐
        │             │                  │
        ▼             ▼                  ▼
   ┌─────────┐   ┌──────────┐      ┌──────────┐
   │Approved │   │ Rejected │      │ Expired  │
   └────┬────┘   └──────────┘      └──────────┘
        │
        ├──── Ausführung erfolgreich ────► ┌──────────┐
        │                                  │ Executed │
        └──── Ausführung fehlgeschlagen ──►│ Failed   │
                                           └──────────┘
```

| Zustand | Semantik |
|---|---|
| **Pending** | Angelegt, Entscheidung offen; Ablauffrist läuft |
| **Approved** | Vom Besitzer freigegeben (oder vorab genehmigt); liegt in der persistenten Ausführungsqueue |
| **Rejected** | Vom Besitzer abgelehnt (inkl. Ablehnbegründung) oder systemseitig zurückgezogen (s. Übergänge) |
| **Executed** | Ausführung erfolgreich; `ExecutionResult` gefüllt (Terminal) |
| **Failed** | Ausführung versucht und fehlgeschlagen (Terminal; `FailureReason`) |
| **Expired** | Entscheidungsfrist überschritten ohne Entscheidung (Terminal) |

### 3.3 Erlaubte Übergänge (verbindlich)

| Von → Nach | Auslöser |
|---|---|
| Pending → Approved | Freigabe im Genehmigungscenter durch den Besitzer |
| Pending → Rejected | Ablehnung durch den Besitzer; **oder** systemseitig, wenn der Zugang deaktiviert/widerrufen wird oder das Rechteprofil das benötigte Scope nicht mehr enthält (Grund dokumentiert) |
| Pending → Expired | `ExpiresUtc` erreicht ohne Entscheidung (Ablaufjob) |
| Approved → Executed | Queue-Ausführung erfolgreich |
| Approved → Failed | Queue-Ausführung fehlgeschlagen (Service-Fehler, Vorbedingung nicht mehr erfüllt) |
| Approved → Rejected | Nur systemseitig vor Ausführungsbeginn, wenn ein harter Deckel greift (globaler Schreib-Stopp aktiviert, Zugang widerrufen) – mit dokumentiertem Grund; einmal Gestartetes läuft nicht in Rejected |
| (Terminalzustände) | Executed, Failed, Rejected, Expired – keine weiteren Übergänge |

**Verbotene Übergänge** (werden technisch ausgeschlossen, nicht nur
konventionell): Executed → irgendwas; Rejected → Approved (Ablehnung
ist final – neue Anfrage nötig); direkter Sprung Pending → Executed
ohne Approved.

**Zeitlimit/Ablauf:** Jeder Request trägt `ExpiresUtc`. Vorschlag:
Standard-Entscheidungsfrist 72 h, konfigurierbar je Zugang; ein
Hintergrundlauf (oder Auswertung bei Zugriff) markiert überfällige
Pending-Requests als `Expired`. Begründung: Unbegrenzt offene Requests
wären veraltete Wirkversprechen – die Datenlage (Entwurfszustand,
Stornierbarkeit) ändert sich.

### 3.4 Vorab-Genehmigung (pre-approved Scope)

Ist die Aktion im Genehmigungsmodus des Zugangs als „dauerhaft vorab
genehmigt" markiert, wird der `AiActionRequest` **unmittelbar als
Approved** angelegt (`DecisionNote` = „Vorab-Genehmigung
Zugangseinstellung", `DecidedByUserId` = Besitzer) und direkt in die
Ausführungsqueue gestellt. Der Client erhält wie üblich die Request-Id;
für ihn ist der Vorgang transparent, für das Audit identisch
protokolliert. Damit existiert **kein** Ausführungspfad an der
`AiActionRequest`-Spur vorbei.

---

## 4. Fachlicher Ablauf

```text
[MCP-Client]  Toolaufruf (z. B. fm_statement_draft_book)
      │  1. Authentifizierung + Scope-/Filter-/Deckel-Prüfung (Stufen 0–3)
      │     – Fehler → sofortige Ablehnung, kein Request
      ▼  2. Klasse genehmigungspflichtig / Genehmigungsmodus greift
[Server]      AiActionRequest anlegen (Pending, ParameterSnapshot,
      │       ExpiresUtc, CorrelationId)
      │  3. INotificationWriter.CreateForUserAsync (Typ EventDriven,
      │     Target Dashboard/Modal) → Besitzer: „Neue Genehmigungsanfrage"
      ▼
[Client]      synchron: { aiActionRequestId, status: Pending }
      │       Statusabfrage via fm_task_status
      ▼
[Benutzer]    Genehmigungscenter: Liste offener Requests → Detail:
      │       Wirkungsvorschau aus ParameterSnapshot
      │       (z. B. Buchungsplan: Entries, Beträge, Zielkonto)
      ├─ Freigeben → Approved (+ Benachrichtigung optional) → Queue
      └─ Ablehnen  → Rejected (+ Begründung) → Client sieht Rejected
      ▼
[Queue]       Persistente Ausführung (Abschnitt 5)
      │       Idempotenz-/Konkurrenzschutz (StatementDraftBookingGuard,
      │       CanReverseAsync, atomare Batches)
      ├─ Erfolg → Executed + ExecutionResult (z. B. Posting-Ids,
      │           StatementImportId, BookingResult)
      └─ Fehler → Failed + FailureReason
      ▼
[Client]      fm_task_status(aiActionRequestId) → Endzustand + Ergebnis
[Benutzer]    EventDriven-Benachrichtigung über Ausführungsergebnis
```

**Ergebnisrückmeldung an den Client:** Der anfordernde Toolaufruf ist
bewusst **asynchron**: Die synchrone Antwort enthält nur die
`AiActionRequestId`; Ergebnis und Endzustand liefert `fm_task_status`
(Meta-Scope `Ausführungsstatus`, nur eigene Requests). Ein MCP-Client,
der nicht poll-fähig ist, erfährt den Ausgang spätestens beim nächsten
Aufruf.

---

## 5. Ausführung nach Freigabe

### 5.1 Persistente Queue statt In-Memory-`IBackgroundTaskManager`

**Befund:** `IBackgroundTaskManager` (`Enqueue(type, userId, payload)`,
Status Queued/Running/Completed, Cancel, Progress) ist eine
**In-Memory-Queue** – ihr Inhalt überlebt keinen Prozess-Neustart.

**Festlegung:** Die `AiActionRequest`-Tabelle selbst **ist** die
persistente Queue. Begründung:

- Pending-/Approved-Requests müssen Neustarts überleben –
  Auto-Update (`msTools.Updater`) und Service-Restarts sind
  Normalbetrieb; genehmigte, aber nicht ausgeführte Aktionen dürfen
  nicht verloren gehen (fachlich: eine erteilte Freigabe ist
  verbindlich).
- Der Request-Datensatz trägt ohnehin alles Nötige (Tool, Snapshot,
  Besitzer, Zustand); ein zweiter Queue-Datenspeicher wäre redundant.
- Musterübernahme: Ein Hintergrund-Executor nach dem
  `BackgroundTaskRunner`/`IBackgroundTaskExecutor`-Muster pollt
  `Approved`-Requests und führt sie aus – derselbe Architekturgedanke
  wie `BookingTaskExecutor` für `book-all`, nur mit DB-gestützter statt
  In-Memory-Warteschlange. Für Sichtbarkeit/Fortschritt lange laufender
  Ausführungen (Massenverbuchung) kann der Executor weiterhin
  `IBackgroundTaskManager`-Tasks erzeugen – der `AiActionRequest`
  bleibt führender Datensatz, der Task nur Fortschrittsanzeige.
- Der persistente Lock `StatementDraftBookingGuard`
  (LockToken + ExpiresUtc) ist das Vorbild, wie Single-Instance-
  Konkurrenz auf DB-Ebene gelöst wird; Claim-/Abruflogik der Queue
  folgt demselben Muster (siehe 5.2).

### 5.2 Idempotenz und Doppelausführungsschutz

Finanzwirkungen dürfen **genau einmal** eintreten:

- **Zustands-Single-Shot:** Nur Übergang `Approved → Executed/Failed`
  ist erlaubt; ein Request, der bereits `Executed` ist, kann nicht
  erneut ausgeführt werden. Der Queue-Claim erfolgt atomar
  (Status-Update mit Bedingung `Status == Approved` – unter SQLite
  als geschützte Transaktion denkbar; Serialisierungsdetails sind
  Umsetzungssache, fachlich ist „höchstens ein Executor pro Request"
  verbindlich).
- **Fachliche Vorprüfung bei Ausführung:** Zwischen Freigabe und
  Ausführung kann sich die Datenlage geändert haben → der Executor
  wiederholt die fachlichen Guard-Prüfungen:
  `CanReverseAsync` vor Storno, `AlreadyBooked`-/Validierungsstatus vor
  Verbuchung, Existenz- und Besitzprüfung vor Lösch-/Archiv-
  Operationen. Nicht mehr erfüllte Vorbedingungen → `Failed` mit
  klassifiziertem Grund (z. B. „bereits verbucht", „bereits
  storniert", „Ziel nicht mehr vorhanden").
- **Bereichssperren:** Für Verbuchung greift derselbe
  `StatementDraftBookingGuard`-Konkurrenzschutz wie im UI-Pfad – ein
  Entwurf kann nicht gleichzeitig über UI und Approval-Queue gebucht
  werden.
- **Anforderungs-Deduplizierung:** `IdempotencyKey` verhindert, dass
  ein mehrfach abgeschickter Toolaufruf (Netzwerk-Retry, Client-Bug)
  mehrere gleiche Pending-Requests erzeugt; ein zweiter Aufruf mit
  gleichem Schlüssel liefert denselben Request zurück
  (Anwendung des `allowDuplicate`-Gedankens des
  `IBackgroundTaskManager` auf Request-Ebene).
- **Atomarität der Wirkung:** Batch-Aktionen bleiben ganz-oder-gar-
  nicht (`ApplyBatchEntryUpdatesAsync`-Semantik); Teilausführung ist
  kein zulässiger Endzustand.

---

## 6. Fehlerbehandlung, Timeout, Widerruf

### 6.1 Fehlerbilder je Phase

| Phase | Fehler | Ergebnis |
|---|---|---|
| Anforderung | Scope/Filter/Deckel verweigert | kein Request; sofortige Fehlerantwort (404-/Scope-Fehler-Semantik) |
| Anforderung | Duplikat (gleicher `IdempotencyKey`) | Rückgabe des bestehenden Requests |
| Anforderung | Pending-Limit des Zugangs erreicht (Rate-Limit-Dimension) | `429` – Schutz vor Approval-Spam |
| Entscheidung | Ablauf ohne Entscheidung | `Expired`; Client sieht Endzustand |
| Entscheidung | Zugang deaktiviert/widerrufen während Pending | `Rejected` (systemseitig, Grund „Zugang entzogen") |
| Entscheidung | Scope/Profil zwischenzeitlich eingeschränkt | `Rejected` (systemseitig, Grund „Recht entfallen") – Re-Prüfung der Stufen 0–3 erfolgt zusätzlich **bei der Ausführung** |
| Entscheidung | Globaler Kill-Switch / Schreib-Stopp | Requests werden nicht ausgeführt; bleiben Approved bis Ablauf oder werden mit dokumentiertem Grund verworfen (Konfigurationsverhalten; Zielbild: Approved bleibt stehen, Executor stoppt – bei Reaktivierung und nicht überschrittenem `ExpiresUtc` wird ausgeführt) |
| Ausführung | Fachliche Vorbedingung entfallen (z. B. Draft bereits verbucht) | `Failed` + `FailureReason` (klassifiziert) |
| Ausführung | Service-Fehler/Exception | `Failed` + `FailureReason`; Audit-Eintrag; **kein** automatischer Retry (fachliche Aktionen sind kein idempotentes Massengeschäft – Fehler werden dem Benutzer gemeldet, Neuanforderung ist bewusste Client-Aktion) |
| Ausführung | Timeout der Ausführung | `Failed` („Timeout"); fachliche Konsistenz sichert die Service-Transaktion, nicht der Request |

### 6.2 FailureReason-Klassifikation (für Client und Audit)

Stabil codierte Gründe statt Freitext (Konvention `Err_*`-Semantik):
`Err_AlreadyBooked`, `Err_AlreadyReversed`, `Err_TargetGone`,
`Err_RightsRevoked`, `Err_ValidationFailed`, `Err_ExecutionTimeout`,
`Err_WriteAccessStopped`, `Err_Internal`. Detailtexte sind
lokalisierte Anzeigetexte, der Code ist die verlässliche Bedingung.

### 6.3 Widerrufs-Szenarien

- **Zugang deaktiviert/widerrufen:** offene Pending-Requests →
  `Rejected`; Approved-aber-nicht-Ausgeführte → `Rejected`
  (systemseitig, vor Ausführungsbeginn). Begründung: Der Widerruf
  muss auch aufgeholte Wirkungen stoppen können.
- **Benutzer löscht Zugang:** Requests bleiben als Audit-Substrat
  erhalten (McpAccessId-Referenz bleibt lesbar, auch wenn der Zugang
  weg ist – Datenmodell entsprechend „Soft-Referenz").
- **Client „widerruft" Anforderung:** nicht vorgesehen – ein Client
  kann nicht eingelöste Versprechen zurückziehen; er kann höchstens
  keine weiteren stellen. Der Besitzer lehnt ab (fachlich
  konsistent: nur die Genehmigungsinstanz entscheidet).

---

## 7. Fachliche Anforderungen an das Genehmigungscenter (UI)

Detail-UI ist Inhalt des UI-Konzepts (Schritt 4); hier nur die
fachlichen Anforderungen:

1. **Eingangskorb:** Liste offener Pending-Requests des Benutzers
   (über alle seine Zugänge), sortiert nach `RequestedUtc`, mit
   Restlaufzeit (`ExpiresUtc`), Zugangsname, Tool, kompakter
   Wirkungsangabe (z. B. „Verbuchung: 14 Buchungen, Konto 'Giro',
   Summe −812,40 €").
2. **Wirkungsvorschau:** Detailansicht zeigt die konkreten,
   eingefrorenen Wirkungen aus dem `ParameterSnapshot` – je Aktionstyp
   die relevante fachliche Sicht:
   - Verbuchung → Buchungsplan (Entries, Beträge, Zuordnungen,
     `forceWarnings`-Hinweise, gesperrte Entries),
   - Storno → betroffene Buchungsgruppe (`GetRelatedPostingsAsync`-
     Entscheidungsvorlage),
   - Löschen/Archivieren/Merge → betroffene Datensätze mit
     Konsequenzhinweis (z. B. „Merge ist nicht rückgängig zu machen").
   `ReasonText` des Clients wird angezeigt, aber als unmarkierter
   Dritttext (untrusted content) gekennzeichnet.
3. **Entscheidungsaktionen:** Freigeben / Ablehnen (optional mit
   Begründung, die dem Client als Entscheidungsfeedback zurückgespielt
   wird); kritische Wirkungen laufen über das bestehende
   `IConfirmationService`-Bestätigungsmuster (Severity-Stufen).
4. **Batch-Bedienung:** Mehrfachauswahl für gleichartige Requests
   (z. B. mehrere Batch-Vervollständigungen desselben Zugangs) –
   jede Freigabe bleibt ein einzelner Zustandsübergang je Request.
5. **Historie:** Filterbare Ansicht entschiedener Requests
   (Approved/Executed/Failed/Rejected/Expired) mit Ergebnis und
   `ExecutionResult`-Referenz – Einstiegspunkt in die Audit-Ansicht.
6. **Benachrichtigungen:** `NotificationWriter` EventDriven an den
   Besitzer bei neuer Anfrage und bei Ausführungsergebnis;
   `Target`-Kategorie nach Dringlichkeit (Modal nur für
   besonders kritische Wirkungen – fachliche Festlegung:
   Standard Dashboard/HomePage, kein Modal-Spam bei Agenten-Workloads).

---

## 8. Konsistenzanforderungen an Folgeartefakte

- **Audit-Konzept:** protokolliert Anforderung, Entscheidung (wer,
  wann, Begründung), Ablauf und Ausführungsergebnis je Request –
  inklusive vorab-genehmigter Pfade; `CorrelationId` verknüpft die
  Ereigniskette.
- **Sicherheitskonzept:** Genehmigungsmodus und Schreib-Stopp als
  Stufe-3-Deckel; Pending-Limit als Rate-Limit-Dimension;
  `ReasonText`-Markierung als untrusted content.
- **Datenmodell (Schritt 4):** persistente `AiActionRequest`-Entität
  mit den Feldern aus Abschnitt 3 inkl. Indizes
  (`OwnerUserId`+`Status`, `ExpiresUtc`), `Entity`-Basiskonvention,
  Backup-Einbindung; die Request-Tabelle ist zugleich die Queue
  (kein separater Speicher).
- **Architekturkonzept (Schritt 4):** Queue-Executor nach
  `BackgroundTaskRunner`-Muster, Claim-/Atomaritätslogik unter
  SQLite, Einbindung der Guard-Prüfungen und des
  `StatementDraftBookingGuard`.
- **UI-Konzept (Schritt 4):** Genehmigungscenter gemäß Abschnitt 7;
  `IConfirmationService`-Muster; resx-Lokalisierung de/en.
