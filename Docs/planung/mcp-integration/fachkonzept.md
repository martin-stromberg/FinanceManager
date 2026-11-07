# Fachkonzept – MCP-Integration für FinanceManager (Issue #470)

**Status:** Konzept (Planungsartefakt, keine Implementierung)
**Einordnung:** Artefakt 1 der Anforderung; Grundlage für den Scope- und Toolkatalog
(Schritt 2) sowie für Sicherheits-, Audit- und Approval-Konzept (Schritt 3).
Das zugehörige Rechtemodell im Detail ist in
[berechtigungsmodell.md](berechtigungsmodell.md) beschrieben.

---

## 1. Zielbild

Benutzer von FinanceManager sollen externe KI-Systeme (z. B. ChatGPT, Claude,
Copilot, lokale LLMs) über einen benutzerbezogenen MCP-Server (Model Context
Protocol) kontrolliert auf ihre Finanzdaten zugreifen lassen können. Der
MCP-Server ist dabei eine **parallele Zugriffsschicht** auf denselben
Service-Layer (`I*Service`-Contracts), den auch die bestehende BFF-REST-API für
die Blazor-UI nutzt – kein zweiter fachlicher Pfad, kein eigener Datenbestand.

Jeder Zugriff läuft über einen vom Benutzer explizit angelegten **MCP-Zugang**
(F1) mit einem eigenen Token, einer **Freigabestufe** (F5) und einem daraus
abgeleiteten bzw. individuell angepassten **Rechteprofil** (F2/F3), das optional
durch **Filter** (F4) auf Teilmengen der Daten eingeschränkt wird.

### 1.1 Hauptziele

- **Sichere Anbindung von LLMs über MCP:** Zugriff ausschließlich über
  MCP-Tools, die Geschäftsprozesse abbilden – niemals über generische
  Datenbankoperationen.
- **Benutzerbezogene Freigaben:** Jeder MCP-Zugang ist fest an einen Benutzer
  (`User`/`OwnerUserId`) gebunden; Mandantenfähigkeit entsteht allein durch
  das bestehende Besitzmodell.
- **Granulare Rechtevergabe:** Rechte werden als Datentyp × Rechteart
  (× optionaler Filter) vergeben, siehe F3 und `berechtigungsmodell.md`.
- **Nachvollziehbarkeit:** Jeder Token-Einsatz wird protokolliert
  (Nutzungsprotokollierung, F1; Detailtiefe ist Inhalt des Audit-Konzepts).
- **Unterstützung zukünftiger Agentenszenarien:** Das Rechtemodell ist so
  gestaltet, dass spätere, stärker autonome Workflows (z. B. wiederkehrende
  Kontoauszugs-Verarbeitung) ohne Modellbruch abgebildet werden können.
- **Schutz sensibler Finanzdaten:** Der verbindliche Grundsatz lautet –
  MCP-Rechte dürfen bestehende Benutzerrechte **nur einschränken, niemals
  erweitern** (belegte Vorgabe der Anforderung). Ein MCP-Zugang kann nie mehr
  sehen oder tun als sein Besitzer in der UI/API.

### 1.2 Abgrenzung – explizite Nicht-Ziele

Die folgenden Punkte sind verbindliche Nicht-Ziele der Anforderung und werden
in allen Konzeptdokumenten als Abgrenzung geführt:

- **Kein direkter Datenbankzugriff:** MCP-Clients erhalten keinen SQL- oder
  generischen Datenzugriff; der Zugriff erfolgt ausschließlich über fachliche
  Tools auf dem Service-Layer.
- **Kein Zugriff auf Online-Banking-Systeme:** Kontoauszüge gelangen
  ausschließlich über den vorhandenen Datei-Import (`IStatementDraftService`,
  Parser unter `Infrastructure/Statements/`) in die Anwendung. Es wird keine
  Bankanbindung (HBCI/FinTS/PSD2 o. ä.) konzipiert.
- **Keine generischen CRUD-APIs für KI-Systeme:** Tools bilden Geschäfts-
  prozesse ab (z. B. „Entwurf vervollständigen", „Verbuchung vorschlagen"),
  keine Entity-CRUD-Fläche.
- **Keine vollautonome Finanzbuchhaltung ohne Benutzerkontrolle:** Kritische
  Aktionen (Verbuchung, Storno, Stammdaten-Löschung) bleiben je nach
  Konfiguration genehmigungspflichtig (siehe Freigabestufen, Abschnitt 5, und
  das Approval-Konzept in Schritt 3).
- **Keine Implementierung:** Dieses Dokument ist Planungsartefakt.

### 1.3 Fachliche Invarianten, die das Konzept respektiert

- **Postings sind nicht direkt schreibbar.** Im bestehenden System werden
  `Posting`-Datensätze ausschließlich durch die Verbuchung von
  `StatementDraft`-Entwürfen erzeugt (`Book`/`Commit`/`book-all` über
  `IStatementDraftService`); die API bietet für Buchungen nur Lese- und
  Storno-Endpunkte. Das MCP-Konzept übernimmt diese Invariante: Es gibt kein
  „Posting schreiben"-Tool, „Erstellung" von Buchungen existiert fachlich
  nicht.
- **„Löschen" von Buchungen existiert nicht.** Rücknahme erfolgt ausschließlich
  als **Storno** über `PostingReversalService` (Gegenbuchung mit
  `ReversedByPostingId`/`ReversalForPostingId` statt physischer Löschung).
- **Archivieren statt Löschen** gilt bereits heute für `SavingsPlan`
  (`ArchivedUtc`) und `Security` (`ArchivedUtc`) – die fachliche „Löschung"
  dieser Datentypen ist als Archivierung zu verstehen.
- **Owner-Prinzip:** Nahezu alle Fachentitäten tragen `OwnerUserId`; die
  Besitzprüfung erfolgt heute im Service (`ownerUserId`-Parameter, EF-Where),
  nicht über Global Query Filters. MCP-Tools müssen denselben Weg gehen und
  erhalten ihre `ownerUserId` aus dem Token→Benutzer-Mapping.
- **Indirekte Besitzverhältnisse:** `Posting` (kein `OwnerUserId`),
  `StatementImport`/`StatementEntry` (über `AccountId`),
  `StatementDraftEntry` (über `DraftId`), `AccountLinkedIban`, `AliasName`,
  `SecurityPrice` und `PostingAggregate` besitzen keinen direkten
  Benutzerbezug. Ihre Rechteableitung ist in `berechtigungsmodell.md`
  festgelegt.
- **Fremdfreigaben:** `AccountShare`/`AccountShareRole` (Read/Write/Admin)
  erlaubt Freigaben von Konten zwischen Benutzern. Diese Rolle bildet für
  MCP-Zugriffe des Teilnehmers die absolute **Obergrenze** (Abschnitt 5.4 und
  `berechtigungsmodell.md`).

---

## 2. Akteure und Rollen

| Akteur | Beschreibung | Rechte/Rolle im MCP-Kontext |
|---|---|---|
| **Datenbesitzender Benutzer** | Registrierter `User` (Identity), der seine Finanzdaten in FinanceManager pflegt | Legt MCP-Zugänge an, wählt Freigabestufe, individuelle Rechte und Filter; widerruft Zugänge; prüft und genehmigt anstehende Aktionen; sieht das Nutzungsprotokoll. Vollrechte auf seine Daten gemäß bestehendem Besitzmodell. |
| **Admin** | `User` mit `IsAdmin` bzw. Identity-Rolle „Admin" | Administriert Benutzer (bestehender `IUserAdminService`-Weg). Für MCP: Bedient globale Notfallschalter (globale MCP-Deaktivierung, Schreib-Stopp) und erhält Sicherheitsalarme (`Notification` Typ `SystemAlert`). **Wichtig:** Admin-Rechte erweitern nicht die fachliche Datensichtbarkeit – ein Admin-MCP-Zugang sieht nur die Daten des Admin-Benutzers; ein Fremdzugriff auf Benutzerdaten via MCP ist nicht vorgesehen. |
| **MCP-Client / KI-System** | Externes System (ChatGPT, Claude, Copilot, lokales LLM, ggf. später Agenten-Frameworks), das sich mit einem Personal Access Token am MCP-Server authentifiziert | Technisch kein eigener Benutzer. Wirkt stets **im Namen und unterhalb der Rechte** des Zugangsbesitzers, zusätzlich eingeschränkt durch Freigabestufe, Rechteprofil, Filter und ggf. AccountShare-Obergrenzen. Jede Aktion ist dem konkreten MCP-Zugang zurechenbar. |

**Rollenprinzip:** Es gibt genau eine Vertrauensgrenze – zwischen dem
FinanceManager-Server (vertrauenswürdig) und dem MCP-Client (prinzipiell
untrusted, einschließlich aller von ihm gelieferten und an ihn gelieferten
Inhalte; siehe Prompt-Injection-Behandlung im Sicherheitskonzept, Schritt 3).

---

## 3. Übergreifende Anwendungsszenarien

Die Szenarien ordnen die Anforderungen in typische Nutzungsbilder ein und
bestimmen, welche Rechtearten (Abschnitt 4) und Freigabestufen (Abschnitt 5)
jeweils benötigt werden.

### 3.1 Analyse und Reporting (lesend)

Der Benutzer lässt ein KI-System seine Finanzdaten auswerten: Umsatzanalysen,
Ausgaben-Kategorisierung, Budgetabfragen, Wertpapierbestand. Benötigt werden
ausschließlich **Leserechte** auf den betroffenen Datentypen (`Posting` über
`IPostingsQueryService`-Äquivalent, `Account`, `BudgetPurpose`/`BudgetRule`,
`Security`, `ReportFavorite`/`HomeKpi`). Typische Stufe: **Analyse**.

### 3.2 Assistenz und Vorschläge

Das KI-System macht fachliche Vorschläge, ohne Daten zu verändern: z. B.
Vorschläge für `BudgetRule.PurposePattern` (analog dem vorhandenen
Auto-Matching), neue `ContactCategory`-Zuordnungen, `AliasName.Pattern` für
besseres Kontakt-Matching beim Import. Benötigt **Vorschlagsrechte**;
Vorschläge werden dem Benutzer zur Übernahme angeboten. Typische Stufe:
**Assistent**.

### 3.3 Kontoauszugs-Verarbeitung (F7 – zentraler Workflow)

Der Benutzer importiert Kontoauszugs-Dateien wie bisher (Upload,
`MassImportOrchestrator`, Parser). Der MCP-Client analysiert anschließend die
entstandenen `StatementDraft`-/`StatementDraftEntry`-Daten, ergänzt fehlende
Zuordnungen (`ContactId`, `SavingsPlanId`, `SecurityId`,
`SecurityTransactionType` …), nutzt die vorhandene Dublettenerkennung
(`ClassifyAsync`, `AlreadyBooked`-Status, `StatementEntry.RawHash`) und
bereitet die Verbuchung vor. Benötigt **Lese-, Vorschlags- und
Vervollständigungsrechte** auf Kontoauszugsentwürfe und zugehörige Stammdaten.
Typische Stufe: **Sachbearbeiter**. Die eigentliche Verbuchung (Book) ist eine
höherwertige Aktion (Stufe **Buchhalter** bzw. genehmigungspflichtig).

### 3.4 Genehmigungspflichtige Aktionen

Aktionen mit dauerhafter Wirkung – Verbuchung von Entwürfen, Storno von
Buchungen, Erstellung/Änderung/Löschung von Stammdaten – können je nach
Konfiguration eine explizite Benutzerfreigabe erfordern (Approval-Konzept,
Schritt 3; neue persistente Entität `AiActionRequest` mit Zuständen
Pending/Approved/Rejected/Executed/Failed). Der Benutzer wird über den
vorhandenen Benachrichtigungskanal (`NotificationWriter`, Typ `EventDriven`)
informiert und entscheidet im Genehmigungscenter.

### 3.5 Agentenszenarien (zukünftig)

Wiederkehrende, mehrstufige Aufgaben: z. B. „verarbeite jeden neuen
Kontoauszugs-Entwurf automatisch bis zur Buchungsreife und melde offene
Fälle". Das Rechtemodell trägt dies bereits: Der Agent nutzt denselben
MCP-Zugang mit identischem Scope-Modell; Schutz erfolgt über enge Filter,
Ablaufdaten, Rate Limits und die Genehmigungspflicht für finale Aktionen.
Vollautonomie ist ausdrücklich Nicht-Ziel – auch Agenten bleiben
genehmigungsgebunden für kritische Rechtearten.

---

## 4. Rechtearten (fachliches Aktionsmodell, Vorbereitung F2/F3)

Die Anforderung definiert sieben Rechtearten. Ihre **fachliche Bedeutung im
FinanceManager-Kontext** ist wie folgt festgelegt (technische
Scope-Kombinatorik in `berechtigungsmodell.md`):

| Rechteart | Fachliche Semantik | Typische Ausprägung im Code-Bezug |
|---|---|---|
| **Leserecht** | Datensätze des Datentyps einsehen (Listen, Details, Aggregates, Exporte) | `GET`-Äquivalente; z. B. `IStatementDraftService.Get*`, `IPostingsQueryService`, Report-Auswertungen |
| **Vorschlagsrecht** | KI erzeugt fachliche Vorschläge (Zuordnungen, Kategorisierungen, Muster, Analysen), die **nicht persistiert** werden, sondern dem Benutzer als Vorschlag zurückgegeben/angezeigt werden | Klassifizierungshinweise für `StatementDraftEntry`, `AliasName.Pattern`- und `BudgetRule.PurposePattern`-Vorschläge; kein Schreibzugriff |
| **Vervollständigungsrecht** | Fehlende Informationen **bereits vorhandener** Datensätze ergänzen – insbesondere die Vervollständigung von Kontoauszugs-Entwürfen | `IStatementDraftService.Set*`/`Save*`/`ApplyBatchEntryUpdatesAsync` (ContactId, SavingsPlanId, SecurityId, SecurityTransactionType, `SetAccountAsync`, `SetDescriptionAsync`), Stammdaten-Ergänzungen (z. B. `AliasName` anlegen, `Contact.CategoryId` setzen) |
| **Änderungsrecht** | Bestehende Datensätze inhaltlich ändern | `edit-core` am `StatementDraftEntry`, Stammdaten-Update-Methoden (`Account.Rename`, `SetIban`, …) |
| **Erstellungsrecht** | Neue Datensätze anlegen | `Contact`/`SavingsPlan`/`Security`/`BudgetPurpose`/`BudgetRule`/`ReportFavorite`/`Attachment` anlegen; `StatementDraft` per Upload/`CreateEmptyDraftAsync` erzeugen |
| **Buchungsrecht** | **Verbuchung von Kontoauszugs-Entwürfen** auslösen – die einzige Aktion, die `Posting`-Daten erzeugt | `IStatementDraftService.Book*`/`Commit`/`book-all` (erzeugt `Posting`, `StatementImport`/`StatementEntry`, aktualisiert `Account.CurrentBalance`, triggert `PostingAggregate`-Rebuild und `IBudgetImpactEvaluationService`). **Explizit kein** direktes Posting-Schreiben. |
| **Löschrecht** | Datensätze entfernen – datentypspezifische Semantik | Entwürfe verwerfen (`StatementDraft` löschen), Stammdaten löschen; für `SavingsPlan`/`Security` = Archivieren (`ArchivedUtc`); für `Posting` = **Storno** via `PostingReversalService` (nie physisches Löschen) |

**Ablese-Regel:** Die Rechtearten sind **gestuft nach Wirkung** geordnet
(Lesen → Vorschlag → Vervollständigung → Änderung/Erstellung → Buchung/Löschung).
Höherwertige Rechte setzen das Leserecht desselben Datentyps fachlich voraus;
das Modell vergibt sie jedoch explizit, nicht implizit (siehe
Konfliktverhalten im Berechtigungsmodell).

---

## 5. Freigabestufen (F5)

Die fünf Freigabestufen sind das **standardisierte Vertrauensmodell**: Der
Benutzer wählt bei der Anlage eines MCP-Zugangs eine Stufe; die Stufe
bestimmt die Standardrechte (Rechtevererbung, Details in
`berechtigungsmodell.md`). Nur die Stufe „Individuell" erlaubt Abweichungen.

### 5.1 Stufenübersicht und Aktionsmatrix

Legende: ● = erlaubt (Standardrecht der Stufe), ◐ = erlaubt, aber
genehmigungspflichtig (Benutzerfreigabe je Aktion), ○ = nicht erlaubt.
„Genehmigungspflichtig" bedeutet: Ausführung erst nach expliziter Freigabe im
Genehmigungscenter (Approval-Konzept, `AiActionRequest`).

| Rechteart | Analyse | Assistent | Sachbearbeiter | Buchhalter | Individuell |
|---|---|---|---|---|---|
| Lesen | ● | ● | ● | ● | konfigurierbar |
| Vorschlag | ○ | ● | ● | ● | konfigurierbar |
| Vervollständigung | ○ | ○ | ◐¹ | ●/◐² | konfigurierbar |
| Änderung | ○ | ○ | ◐¹ | ●/◐² | konfigurierbar |
| Erstellung | ○ | ○ | ◐¹ | ●/◐² | konfigurierbar |
| Buchung | ○ | ○ | ○ | ◐ | konfigurierbar |
| Löschung | ○ | ○ | ○ | ◐ | konfigurierbar |

¹ **Sachbearbeiter:** Vervollständigung auf Kontoauszugsentwürfe
(`StatementDraft`/`StatementDraftEntry`) und Stammdaten-Ergänzungen sind
erlaubt, werden aber je Aktion bzw. je Batch dem Benutzer zur Genehmigung
vorgelegt (Standard-Konfiguration; der Benutzer kann die
Genehmigungspflicht für vertrauenswürdige Zugänge auf Dauer-Freigabe
umstellen). Änderung/Erstellung sind auf Entwürfe und vervollständigende
Stammdaten begrenzt – keine Buchung, kein Löschen.

² **Buchhalter:** Erstellung/Änderung/Vervollständigung sind erlaubt. Ob sie
direkt oder genehmigungspflichtig ausgeführt werden, ist eine
Zugangs-Einstellung (Standard: Stammdaten-Änderungen direkt, Buchung/Storno/
Löschung stets genehmigungspflichtig). **Buchung und Löschung sind auch auf
Stufe Buchhalter nie „still"** – sie erfordern mindestens die einmalige
Genehmigung, können aber für vertrauenswürdige Agenten-Zugänge auf
„dauerhaft vorab genehmigt" (pre-approved Scope) gestellt werden; die
Genehmigungsinstanz bleibt dabei dokumentiert.

### 5.2 Fachliche Bedeutung der Stufen

- **Analyse:** Reines Auswertungsprofil. Geeignet für Reporting- und
  Frage-Antwort-Integrationen. Keinerlei Schreibwirkung – auch keine
  Vorschläge im System (Vorschläge des Clients sind reine Antwortinhalte).
- **Assistent:** Analyse plus die Möglichkeit, Vorschläge strukturiert in der
  Anwendung anzuzeigen (Vorschlagsrecht), z. B. Kategorisierungs- und
  Regelvorschläge. Keine Persistenz von Änderungen.
- **Sachbearbeiter:** Der Arbeitsprofil für den Kontoauszugs-Assistenten:
  Entwürfe analysieren, vervollständigen, für die Buchung vorbereiten – ohne
  selbst buchen oder löschen zu dürfen. Entspricht fachlich einem Mitarbeiter,
  der Belege vorbereitet, aber nicht freigibt.
- **Buchhalter:** Volles Prozessrecht inklusive Verbuchung und Storno –
  gebunden an die Genehmigungsregeln. Entspricht dem Benutzer selbst, aber
  niemals darüber hinaus (Grundsatz „nur einschränken").
- **Individuell:** Frei zusammengesetztes Rechteprofil je Datentyp × Rechteart
  (× Filter) innerhalb der Obergrenzen. Dient Spezialfällen, z. B. „darf nur
  Sparpläne lesen und Budgetregeln vorschlagen".

### 5.3 Stufenwahl als fachliche Entscheidung

Die Stufe ist **Eigenschaft des MCP-Zugangs**, nicht des Benutzers. Ein
Benutzer kann mehrere Zugänge mit unterschiedlichen Stufen betreiben (z. B.
„ChatGPT-Analyse" auf Stufe Analyse, „Lokales LLM für Kontoauszüge" auf Stufe
Sachbearbeiter). Downgrade/Upgrade eines bestehenden Zugangs ist eine
bewusste Benutzeraktion und wird protokolliert (Nutzungsprotokoll/Audit).

### 5.4 Obergrenzen unabhängig von der Stufe

- **Benutzerrechte als Decke:** Ein MCP-Zugang vererbt nie Rechte, die der
  Besitzer nicht hat. Fehlt dem Besitzer z. B. der Zugriff auf ein fremdes
  Konto, kann kein Zugang es öffnen.
- **AccountShare-Rolle als Decke:** Für per `AccountShare` freigegebene
  Konten des Besitzers gilt die Share-Rolle als Maximalniveau:
  `Read` → nur Leserechte auf kontoabhängige Daten (`Posting` mit
  `AccountId` des geteilten Kontos, `StatementImport`/`StatementEntry`,
  `AccountLinkedIban`); `Write` → zusätzlich Vervollständigung/Änderung/
  Erstellung im kontoabhängigen Kontext sowie Verbuchung; `Admin` →
  zusätzlich Lösch-/Stornorechte im kontoabhängigen Kontext. Details in
  `berechtigungsmodell.md`, Abschnitt AccountShare-Obergrenze.
- **Admin ist kein Datentürsteher:** Ein Admin-MCP-Zugang gewährt keinen
  Zugriff auf Daten anderer Benutzer.

---

## 6. Fachliche Spezifikation der MCP-Zugänge (F1)

Ein **MCP-Zugang** ist ein benutzerbezogenes Berechtigungsobjekt mit eigenem
Token. Fachlich entspricht er einem „API-Benutzerkonto im Namen des
Benutzers". Es existiert noch keine solche Entität im Code (Inventory,
Abschnitt 5) – die Datenmodell-Erweiterung ist Inhalt von Schritt 4; hier wird
die fachliche Spezifikation festgelegt.

### 6.1 Eigenschaften eines MCP-Zugangs

| Eigenschaft | Fachliche Festlegung |
|---|---|
| **Besitzer** | Genau ein `User` (`OwnerUserId`-Bezug). Keine gemeinsamen Zugänge, keine Admin-Quersicht auf fremde Tokeninhalte. |
| **Name/Bezeichnung** | Vom Benutzer frei gewählt (z. B. „ChatGPT Desktop"), eindeutig je Benutzer. |
| **Freigabestufe** | Eine der fünf Stufen (Abschnitt 5); bei „Individuell" zusätzlich explizites Rechteprofil. |
| **Rechteprofil** | Effektive Menge der Scopes (Datentyp × Rechteart × Filter); bei Standardstufen aus der Stufe abgeleitet. |
| **Filter** | Optionale Einschränkungen je Datentyp (Abschnitt 7). Standard: keine Filter = alle Datensätze des Benutzers. |
| **Status** | `Aktiv` / `Deaktiviert`. Deaktivierte Zugänge verweigern jede Authentifizierung, bleiben aber erhalten (inkl. Protokoll) und können reaktiviert werden. |
| **Ablaufdatum** | Optionales Datum (`ExpiresUtc` o. ä.); nach Ablauf verhält sich der Zugang wie deaktiviert. Dient der Absicherung temporärer Freigaben. |
| **Token** | Personal Access Token (PAT); Lebenszyklus in 6.3. Genau ein aktives Token je Zugang (Rotation ersetzt das alte). |
| **Genehmigungsmodus** | Zugangs-Einstellung, ob schreibende Aktionen je Aktion genehmigt werden oder als dauerhaft vorab genehmigt gelten (siehe 5.1). |
| **Protokollbezug** | Alle Nutzungs- und Verwaltungsereignisse werden dem Zugang zugeordnet (6.5). |

### 6.2 Anlage und Verwaltung

- **Mehrere Zugänge pro Benutzer** sind vorgesehen und explizit gewünscht
  (Trennung nach Client/Zweck, damit Widerruf und Rechte granular bleiben).
- **Anlagefluss (fachlich):** Benutzer legt Zugang an → wählt Name,
  Freigabestufe, optional Filter und Ablaufdatum → System erzeugt Token →
  **einmalige Anzeige** des Tokens im Klartext → danach nur noch Hash-
  Vergleich möglich.
- **Aktivieren/Deaktivieren:** sofort wirksame Umschaltung; ein deaktivierter
  Token wird serverseitig sofort abgelehnt (analog dem bestehenden
  `OnTokenValidated`-Prinzip: Statusprüfung bei jeder Anfrage, kein
  „Restlebensdauer-Fenster").
- **Löschen eines Zugangs:** fachlich = Widerruf plus Entfernung der
  Konfiguration; das Nutzungsprotokoll bleibt revisionsfest erhalten
  (Aufbewahrung: Audit-Konzept).

### 6.3 Token-Lifecycle (fachliche Sicht)

1. **Erzeugen:** Bei Zugangsanlage oder expliziter **Rotation**. Format:
   kryptografisch zufälliges Secret mit Präfix (z. B. `fm_mcp_…`), damit es in
   Logs/Quelltext erkennbar ist.
2. **Einmalige Anzeige:** Der Klartext-Token wird dem Benutzer **genau einmal**
   nach Erzeugung gezeigt (Copy-and-store-Prinzip, vergleichbar gängiger
   PAT-Praxis). Danach ist der Klartext im System nicht mehr abrufbar.
3. **Speicherung:** Nur als **Einweg-Hash** in der Datenbank (Vorbild
   `Pbkdf2IdentityPasswordHasher`; ausdrücklich **nicht** reversibel wie der
   `IAlphaVantageSecretProtector`-Ansatz, da ein MCP-Token nie im Klartext
   benötigt wird). Authentifizierung = Hash-Vergleich.
4. **Verwendung:** Der Client sendet den Token pro Anfrage
   (Authorization-Header; Transportdetails: Sicherheits-/Architekturkonzept).
   Bei jeder Verwendung wird `LastUsedUtc` (Nutzungsprotokoll) aktualisiert.
5. **Ablauf:** Erreichen des Ablaufdatums deaktiviert den Zugang faktisch;
   der Benutzer wird über `Notification` (EventDriven) vor/ bei Ablauf
   informiert.
6. **Rotation:** Erzeugt ein neues Token, **invalidiert das alte sofort**
   (kein Übergangs-Doppelbetrieb; fachliche Begründung: Finanzdaten, ein
   kompromittiertes Token muss sofort tot sein).
7. **Widerruf:** Sofortige serverseitige Invalidierung durch den Benutzer –
   der Token wird ab sofort abgelehnt. Vorbild: die bestehende Security-
   Stamp-Prüfung in der JWT-Pipeline, die Benutzer-Tokens bei Änderung
   sofort ungültig macht.

### 6.4 Verlust/Kompromittierung (fachlich)

- Token verloren → neuer Token (Rotation); der alte ist ohnehin nicht
  wiederherstellbar (Hash).
- Token kompromittiert (Verdacht) → Widerruf bzw. Deaktivierung; das
  Nutzungsprotokoll zeigt, welche Aktionen unter dem Token gelaufen sind
  (Anschluss Audit-Konzept).
- Gesamtverdacht → globaler Schalter „alle Schreibzugriffe stoppen" bzw.
  „MCP global deaktivieren" (Feature-Flag-Konvention via `appsettings`,
  Vorbild `BackgroundTasks:Enabled`; Detail: Sicherheitskonzept).

### 6.5 Nutzungsprotokollierung (fachlich)

Je MCP-Zugang wird mindestens protokolliert:

- **Nutzungsdaten:** letzte Verwendung (`LastUsedUtc`), Anzahl Aufrufe,
  zuletzt verwendete Tools, Fehlschläge (z. B. abgelehnte Scopes) –
  als Übersicht für den Benutzer in der Zugangsverwaltung.
- **Ereignisse:** Erzeugung, Rotation, Widerruf, Aktivierung/Deaktivierung,
  Stufen-/Rechte-/Filter-Änderungen, Genehmigungsentscheidungen – als
  revisionssicheres Ereignisprotokoll (Spezifikation der Detailtiefe und
  Aufbewahrung: `audit-konzept.md`, Schritt 3).

Die fachliche Anforderung: Der Benutzer kann je Zugang beantworten – *„Welches
KI-System hat wann was mit meinen Daten getan oder versucht?"*

---

## 7. Datentypen-Klassifizierung (F3)

### 7.1 Begriffs-Mapping (Anforderung → Code)

Die 19 Datentypen werden verbindlich auf die realen
Entitäten/Enums des Repos gemappt (Quelle: `inventory.md` und
`inventory/entitaeten.md`; Nr. 19 `Administration` wurde mit den
Admin-Tools T34–T38 ergänzt – installationsbezogen, kein Fachdatentyp):

| # | Datentyp (Anforderung) | Reale Entität / Enum | Hinweis |
|---|---|---|---|
| 1 | Konten | `Account` (+ `AccountLinkedIban`) | `AccountType`: Giro/Savings |
| 2 | Kontotypen | `AccountType`-**Enum** auf `Account.Type` | **keine eigene Entität** – nur 2 Werte, nicht änderbar |
| 3 | Kontoauszüge | `StatementImport` + `StatementEntry` | entstehen bei Commit/Verbuchung; Owner indirekt über `AccountId` |
| 4 | Kontoauszugsentwürfe | `StatementDraft` + `StatementDraftEntry` | zentraler F7-Workflow |
| 5 | Buchungen | `Posting` (+ `PostingAggregate`) | **kein `OwnerUserId`**; schreibgeschützt, Erzeugung nur via Verbuchung, Rücknahme = Storno |
| 6 | Kontakte | `Contact` (+ `AliasName`) | `ContactType`-Enum |
| 7 | Kontaktgruppen | `ContactCategory` | „Gruppe" = Kategorie-Entität |
| 8 | Sparpläne | `SavingsPlan` | Löschen = Archivieren (`ArchivedUtc`) |
| 9 | Sparplankategorien | `SavingsPlanCategory` | |
| 10 | Wertpapiere | `Security` (+ `SecurityPrice`) | Löschen = Archivieren |
| 11 | Wertpapierkategorien | `SecurityCategory` | |
| 12 | Budgets | `BudgetPurpose` (+ `BudgetOverride`) | „Budget" = Zweck inkl. Überschreibungen |
| 13 | Budgetkategorien | `BudgetCategory` | |
| 14 | Budgetregeln | `BudgetRule` | inkl. `PurposePattern`-Auto-Matching |
| 15 | Reports | `ReportFavorite` (+ `ReportCacheEntry`) | persistierte Report-Konfigurationen inkl. CSV-Filter |
| 16 | KPI-Konfigurationen | `HomeKpi`, `PortfolioKpiConfiguration` | |
| 17 | Anhänge | `Attachment` | `Content` als `byte[]` **in der DB**; polymorph via `EntityKind`/`EntityId` |
| 18 | Anhangkategorien | `AttachmentCategory` | |
| 19 | Administration | kein Entitäts-Typ: Installationsscope (Updates via `IUpdateOrchestrator`, Backups via `IBackupService`, `Notification` des Besitzers, Admin-Settings via `IMcpSettingsService`/`IWellKnownSettingsService`) | zusätzlich Admin-Gate auf die Besitzerrolle (`User.IsAdmin`) |

### 7.2 Klassifizierungs-Matrix (19 Datentypen × 7 Rechtearten)

Legende: **✓** = Rechteart fachlich sinnvoll/vergebbar · **—** = nicht
anwendbar (kein fachlicher Schreibpfad / Invariante) · **◑** = nur indirekt
vergebbar (über verknüpfte Entität/Prozess).

| Datentyp (→ Entität) | Lesen | Vorschlag | Vervollst. | Änderung | Erstellung | Buchung | Löschung |
|---|---|---|---|---|---|---|---|
| Konten (`Account`, `AccountLinkedIban`) | ✓ | ✓¹ | ✓ | ✓ | ✓ | — | ✓ |
| Kontotypen (`AccountType`-Enum) | ✓ | — | — | — | — | — | — |
| Kontoauszüge (`StatementImport`, `StatementEntry`) | ✓ | — | — | — | ◑² | — | —³ |
| Kontoauszugsentwürfe (`StatementDraft(+Entry)`) | ✓ | ✓ | ✓ | ✓ | ✓⁴ | ✓ | ✓⁵ |
| Buchungen (`Posting`, `PostingAggregate`) | ✓ | — | — | — | —⁶ | —⁶ | ✓⁷ |
| Kontakte (`Contact`, `AliasName`) | ✓ | ✓ | ✓ | ✓ | ✓ | — | ✓ |
| Kontaktgruppen (`ContactCategory`) | ✓ | ✓ | — | ✓ | ✓ | — | ✓ |
| Sparpläne (`SavingsPlan`) | ✓ | ✓ | ✓ | ✓ | ✓ | — | ✓⁸ |
| Sparplankategorien (`SavingsPlanCategory`) | ✓ | — | — | ✓ | ✓ | — | ✓ |
| Wertpapiere (`Security`, `SecurityPrice`) | ✓ | ✓ | ✓ | ✓ | ✓ | — | ✓⁸ |
| Wertpapierkategorien (`SecurityCategory`) | ✓ | — | — | ✓ | ✓ | — | ✓ |
| Budgets (`BudgetPurpose`, `BudgetOverride`) | ✓ | ✓ | ✓ | ✓ | ✓ | — | ✓ |
| Budgetkategorien (`BudgetCategory`) | ✓ | — | — | ✓ | ✓ | — | ✓ |
| Budgetregeln (`BudgetRule`) | ✓ | ✓⁹ | ✓ | ✓ | ✓ | — | ✓ |
| Reports (`ReportFavorite`, `ReportCacheEntry`) | ✓ | ✓ | — | ✓ | ✓ | — | ✓ |
| KPI-Konfigurationen (`HomeKpi`, `PortfolioKpiConfiguration`) | ✓ | ✓ | — | ✓ | ✓ | — | ✓ |
| Anhänge (`Attachment`) | ✓¹⁰ | — | ✓ | ✓¹¹ | ✓ | — | ✓ |
| Anhangkategorien (`AttachmentCategory`) | ✓ | — | — | ✓ | ✓ | — | ✓ |
| Administration (Installationsscope) | ✓ | — | — | ✓¹² | ✓¹³ | — | — |

**Fußnoten:**
1. Vorschlag z. B. IBAN-/Bankkontakt-Ergänzung, `DetectedAccountId`-
   Korrekturvorschläge aus der Entwurfsanalyse.
2. `StatementImport`/`StatementEntry` entstehen ausschließlich als Ergebnis
   der Verbuchung eines Entwurfs (`Commit`/Book) – kein eigenständiger
   Erstellungspfad; das „Erstellungsrecht" ist faktisch im Buchungsrecht der
   Entwürfe enthalten.
3. Importierte Kontoauszüge sind revisionssichere Belege; ein MCP-
   Löschrecht wird nicht vergeben. (Etwaige Admin-/Datenhaltungs-Aspekte
   sind kein MCP-Anliegen.)
4. Erzeugung via Upload-Endpunkt (`POST /upload`, `mass-import`) oder
   `CreateEmptyDraftAsync` – Datei-Uploads bleiben wie bisher der einzige
   Eingang (kein Bankabruf).
5. „Löschen" = Entwurf verwerfen (vorhandene Delete-Semantik der
   Draft-Verwaltung).
6. `Posting` ist schreibgeschützt: Erzeugung erfolgt ausschließlich als
   Ergebnis des Buchungsrechts auf Entwürfe; „Buchung" auf `Posting` selbst
   ist nicht definiert.
7. Löschrecht auf Buchungen = **Storno** (`PostingReversalService`,
   `POST /api/postings/{id}/reverse`): erzeugt Gegenbuchung
   (`ReversalForPostingId`/`ReversedByPostingId`), nie physisches Löschen.
8. Fachliche Löschung = **Archivieren** (`ArchivedUtc`), konsistent zum
   bestehenden UI-Verhalten.
9. Besonders relevant: Vorschläge für `PurposePattern`/
   `PurposePatternIsRegex` (Auto-Matching) sowie Purpose-/Kategorie-
   Zuordnungen – existierendes Deterministik-Muster wird durch KI-Vorschläge
   ergänzt.
10. Lesen von Anhängen beinhaltet `Attachment.Content` (`byte[]` in SQLite):
    Binär-/Textinhalte gelten als **untrusted content**
    (Prompt-Injection-Risiko; Behandlung im Sicherheitskonzept). Recht
    deckt Metadaten **und** Inhalt ab; Feinsteuerung erfolgt ggf. über Filter.
11. Änderung = Zuordnung/`CategoryId`/`FileName`-Metadaten; Inhalt
    (`Content`) ist nach Upload unveränderlich (Sha256-Prüfsumme).
12. Änderung auf Administration = admin-editable Effekte (Update-
    Installation/Planung, Benachrichtigungs-Dismissal, Admin-Settings) –
    final/effective Operationen laufen genehmigungspflichtig über
    `AiActionRequest`; zusätzlich gilt das Admin-Gate auf die
    Besitzerrolle.
13. Erstellung auf Administration = Backup-Snapshot anstoßen
    (`IBackupService.CreateAsync`, genehmigungspflichtig); ein
    Lösch-/Buchungspfad existiert nicht (Wiederherstellung ist bewusst
    nicht exponiert).

### 7.3 Konsequenzen der Matrix

- **Buchungsrecht existiert genau einmal:** nur auf Kontoauszugsentwürfen.
  Es ist die einzige Rechteart, die neue Buchungsdaten (`Posting`,
  `StatementImport`, `StatementEntry`) und Seiteneffekte
  (`Account.CurrentBalance`, `PostingAggregate`-Rebuild, Budget-Impacts via
  `IBudgetImpactEvaluationService`) erzeugt – deshalb höchste
  Schutzstufe (genehmigungspflichtig, Buchhalter-Minimum).
- **Drei Datentypen sind rein lesend oder strukturell eingeschränkt:**
  Kontotypen (Enum), Kontoauszüge (Belege), Buchungen (Invariante). Das
  verankert die Nicht-Ziele „keine generischen CRUD-APIs" bereits im
  Rechtemodell.
- **Kategorien sind voll verwaltbar**, aber ohne Vervollständigungs-
  semantik (eine Kategorie hat keine „fehlenden Felder", die ein Assistent
  ergänzt).

---

## 8. Filterkonzept (F4)

### 8.1 Zweck und Grundsatz

Filter sind **optionale Einschränkungen** innerhalb eines erteilten Rechts:
Sie begrenzen, **auf welche Datensätze** eines Datentyps ein Recht wirkt –
nie, ob das Recht besteht. Grundsatz der Anforderung: *„Standard = Rechte
gelten für alle Datensätze eines Datentyps."* Kein Filter bedeutet also
unbeschränkten Zugriff auf die Datensätze des Besitzers (im Rahmen des
Rechts und der Obergrenzen).

### 8.2 Filterdimensionen (vorhandene Gruppierungsstrukturen)

| Filterdimension | Basis im Code | Wirkt auf Datentyp |
|---|---|---|
| Kontotypen | `Account.Type` (`AccountType`: Giro=0, Savings=1) | Konten; indirekt kontoabhängige Daten (`Posting`, `StatementImport`/`StatementEntry`, Entwürfe) |
| Kontaktgruppen | `ContactCategory` (`Contact.CategoryId`) | Kontakte; indirekt `Posting` mit `ContactId` |
| Wertpapierkategorien | `SecurityCategory` (`Security.CategoryId`) | Wertpapiere; indirekt `Posting`/`SecurityPrice` mit `SecurityId` |
| Sparplankategorien | `SavingsPlanCategory` (`SavingsPlan.CategoryId`) | Sparpläne; indirekt `Posting` mit `SavingsPlanId` |
| Anhangkategorien | `AttachmentCategory` (`Attachment.CategoryId`) | Anhänge |

### 8.3 Ausprägung und Semantik

- **Modell:** Je (Zugang, Datentyp, Filterdimension) eine Liste zulässiger
  Werte (Kategorien-Ids bzw. `AccountType`-Werte). Mehrere Filterdimensionen
  auf denselben Datentyp werden **UND**-verknüpft – jede gesetzte Dimension
  schränkt weiter ein (konsistent zum Grundsatz „einschränkend gewinnt").
- **Speicherung (Vorschlag):** CSV-Id-Listen nach dem **bestehenden
  `ReportFavorite`-Muster** (`AccountIdsCsv`, `ContactCategoryIdsCsv`,
  `SecurityCategoryIdsCsv`, `SavingsPlanCategoryIdsCsv` …). Das bewährte
  CSV-Muster wird für Scope-Filter übernommen; kein neues Relationsschema
  nötig.
- **Leere Liste vs. kein Filter:** „kein Filter gesetzt" (Feld leer) =
  unbeschränkt. Eine explizit leere Auswahl, die *nichts* zulässt, wird über
  die Rechtsebene abgebildet (Recht gar nicht vergeben) – Filter drücken
  immer positive Einschränkung aus.
- **Datensätze ohne Kategorie:** Bei gesetztem Kategoriefilter gelten
  Datensätze mit `CategoryId == null` als **nicht erfasst** (sichere
  Interpretation: Unklassifiziertes fällt nicht in einen Whitelist-Filter).
  Fachliche Ausnahme, falls gewünscht: ein Pseudo-Wert „ohne Kategorie"
  kann in die Filterliste aufgenommen werden – Entscheidung offen an
  Schritt 2/4, Dokumentation dieser Konvention hier verbindlich als
  „ausschließend".
- **Indirekte Wirkung:** Filter auf Kontotyp/Kategorien wirken sich auf
  abhängige Entitäten über deren Rechteableitung aus (z. B. Kontotyp-Filter
  „nur Giro" lässt `Posting` mit `Kind=Bank` nur für Giro-Konten zu;
  siehe `berechtigungsmodell.md`, Abschnitt Rechteableitung).
- **Typische Anwendungsfälle:** „KI darf nur Sparkonto-Umsätze analysieren"
  (Kontotyp-Filter), „nur Wertpapiere der Kategorie 'ETF'" (Kategoriefilter),
  „Anhänge nur aus Kategorie 'Verträge'".

---

## 9. Abhängigkeiten und Folgeschritte

- **Schritt 2 (Scope- und Toolkatalog):** bildet jeden MCP-Tool auf die hier
  definierten Rechtearten/Datentypen/Freigabestufen ab; der Kontoauszugs-
  Assistent (F7) nutzt ausschließlich `IStatementDraftService`-Funktionen.
- **Schritt 3 (Sicherheits-/Audit-/Approval-Konzept):** konkretisiert
  Token-Hashing und Transport (F8), die persistente `AiActionRequest`-Queue
  (F10) und das revisionssichere Protokoll (F9) zu den hier festgelegten
  Nutzungs-/Ereignisdaten.
- **Schritt 4 (UI/Architektur/Datenmodell):** konkretisiert die neuen
  Entitäten (`McpAccess`, Token-Hash, Scope-/Filter-Tabellen nach
  `ReportFavorite`-CSV-Muster) in `AppDbContext` und die Verwaltungs-UI.
- Dieses Dokument setzt bewusst **keine** technischen Festlegungen zum
  MCP-Transport (Streamable HTTP, Annahme A4) – diese gehören ins
  Architekturkonzept.
