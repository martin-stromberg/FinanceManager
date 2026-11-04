# Projektplan – Planung: MCP-Integration für FinanceManager (Issue #470)

## Übersicht

**Projekttitel:** Konzeption eines benutzerbezogenen MCP-Servers (Model Context
Protocol) für FinanceManager.

**Gesamtziel:** Erarbeitung eines vollständigen Fach-, Sicherheits- und
Architekturkonzepts, mit dem Benutzer externe KI-Systeme (z. B. ChatGPT, Claude,
Copilot, lokale LLMs) sicher und kontrolliert auf ihre Finanzdaten zugreifen lassen
können. Lieferergebnis sind die zwölf in `requirement.md` geforderten Konzept-
dokumente (Markdown, deutsch) unter `Docs/planung/mcp-integration/` – keine
Implementierung. Das Konzept ist Grundlage für die spätere technische Umsetzung,
die Aufwandsschätzung und das Product Backlog.

**Betroffene Bereiche:**

- Domänenmodell (Konten, Buchungen, Kontoauszüge/-entwürfe, Kontakte, Sparpläne,
  Wertpapiere, Budgets, Reports/KPIs, Anhänge) – Grundlage für
  Datentypen-Klassifizierung und Rechteableitung.
- Sicherheit/Benutzerverwaltung (Identity, JWT, IP-Sperren, Rate Limiting,
  Feature-Flags) – Anknüpfungspunkte für Token-Auth, Notfallmaßnahmen.
- Kontoauszugs-Import (`IStatementDraftService`-Pipeline) – fachliche Basis des
  Kontoauszugs-Assistenten (F7).
- API-/UI-Schicht (BFF-REST-API, Blazor Server, `IApiClient`) – Anknüpfung für
  MCP-Verwaltungsoberflächen und Architekturkonzept.
- Persistenz (`AppDbContext`, SQLite, EF-Migrations, Backup-Konventionen) –
  Datenmodell-Erweiterungen.

## Grobe Vorgehensentscheidungen

1. **Ablageort und Sprache:** Alle Ergebnisdokumente werden deutschsprachige
   Markdown-Dateien unter `Docs/planung/mcp-integration/` abgelegt (Annahmen
   A1–A3 der Anforderung; konsistent zur bestehenden Docs-Konvention). Jeder
   Schritt liefert benannte Dateien in diesem Verzeichnis.
2. **Gruppierung der Artefakte:** Die zwölf Ergebnisartefakte werden in fünf
   fachlich lieferbare Schritte gebündelt, entlang der natürlichen
   Konzeptabhängigkeiten: Erst das fachliche Rechte-/Datenmodell (Grundlage für
   alles Weitere), dann der darauf aufsetzende Toolkatalog, dann die
   Querschnittskonzepte Sicherheit/Audit/Approval, dann die technische
   Dimension (UI, Architektur, Datenmodell), zuletzt die Planungsebene
   (Roadmap, Schätzung, Backlog), die alle Konzepte konsolidiert.
3. **Umgang mit dem Ist-Stand:**
   - `AiActionRequest` existiert **nicht** im Code → die Approval-Queue wird
     als neue persistente Entität konzipiert; als Muster dienen
     `StatementDraftBookingGuard` (persistenter Lock) und das
     `IBackgroundTaskManager`-Pattern (asynchrone benutzerbezogene Ausführung),
     wobei die In-Memory-Queue durch Persistenz ersetzt gedacht wird.
   - `AccountShare`/`AccountShareRole` (Read/Write/Admin) ist ein vorhandenes
     Freigabemodell und dient als Vorbild/Anknüpfung für das
     MCP-Berechtigungsmodell. Die belegte Vorgabe „MCP-Rechte dürfen
     Benutzerrechte nur einschränken, niemals erweitern" ist verbindliche
     Prämisse des Rechtemodells.
   - `Posting` hat **kein** `OwnerUserId`; `StatementImport`/`StatementEntry`
     nur indirekten Benutzerbezug → Rechte werden im Berechtigungsmodell über
     die referenzierten Aggregate (Account/Contact/SavingsPlan/Security)
     indirekt abgeleitet statt neue Owner-Felder auf Fachentitäten zu
     fordern.
   - „Kontotypen/Kontaktgruppen/Kategorien" der Anforderung werden auf die
     realen Gegebenheiten gemappt (`AccountType`-Enum mit zwei Werten,
     `ContactCategory`, `SecurityCategory`, `SavingsPlanCategory`,
     `AttachmentCategory`, `BudgetCategory`).
   - Die bestehende REST-API ist faktisch BFF für die Blazor-UI → der
     MCP-Server wird als **parallele Zugriffsschicht** auf denselben
     Service-Layer konzipiert (Tools als Adapter auf `I*Service`), nicht als
     zweiter fachlicher Pfad und nicht als generische CRUD-API.
4. **Constraints als verbindliche Rahmenbedingungen:** Self-hosted
   Single-Instance mit SQLite (Schreibserialisierung, kein verteiltes
   Locking), ausdrücklich unterstützter LAN-/HTTP-Betrieb ohne TLS
   (Token-Transport als reales Risiko im Sicherheitskonzept behandeln),
   Feature-Flag-Konvention via `appsettings` (Vorbild für globale
   MCP-Deaktivierung), Blazor-Server-/MVVM-Konventionen (UI spricht nur über
   `IApiClient` mit der eigenen API).
5. **Fachliche Prinzipien:** MCP-Tools bilden Geschäftsprozesse ab, keine
   generischen Datenbankoperationen; „Löschen" von Buchungen existiert fachlich
   nicht – Rücknahme = Storno (`PostingReversalService`). Nicht-Ziele (kein
   DB-Direktzugriff, keine Online-Banking-Anbindung, keine generischen
   CRUD-APIs, keine vollautonome Buchhaltung) sind in allen Dokumenten als
   Abgrenzung zu führen.
6. **Transport-Grundsatz:** Als MCP-Transport wird Streamable HTTP /
   Remote-MCP angenommen (Annahme A4); die konkrete Zielarchitektur ist
   Inhalt des Architekturkonzepts (Schritt 4), nicht dieses Plans.

## Entwicklungsschritte

### Schritt 1: Fachkonzept und Berechtigungsmodell

Konzeption der fachlichen Grundlage der MCP-Integration: Zielbild, Akteure und
Szenarien (Benutzer, externe KI-Systeme/MCP-Clients), Verwaltung benutzerbezogener
MCP-Zugänge sowie das vollständige Rechtemodell, auf dem alle späteren Konzepte
aufsetzen.

Das **Fachkonzept** (`fachkonzept.md`) enthält:

- Ziele, Hauptziele und Abgrenzung zu den expliziten Nicht-Zielen der Anforderung.
- Akteure und Rollen (datenbesitzender Benutzer, Admin, MCP-Client/KI-System) sowie
  übergreifende Anwendungsszenarien (Analyse, Assistenz, Kontoauszugs-Verarbeitung,
  genehmigungspflichtige Aktionen, Agentenszenarien).
- Fachliche Spezifikation der MCP-Zugänge (F1): mehrere Zugänge pro Benutzer,
  Aktivieren/Deaktivieren, Ablaufdaten, Tokenverwaltung (fachliche Sicht:
  Erzeugen, Anzeigen einmalig, Widerruf), Nutzungsprotokollierung.
- **Freigabestufen** (F5): standardisiertes Vertrauensmodell mit den Stufen
  Analyse, Assistent, Sachbearbeiter, Buchhalter, Individuell – je Stufe die
  erlaubten Aktionsarten und die fachliche Bedeutung.
- **Datentypen-Klassifizierung** (F3): vollständige Matrix aller 18
  Datentypen der Anforderung (Konten, Kontotypen, Kontoauszüge,
  Kontoauszugsentwürfe, Buchungen, Kontakte, Kontaktgruppen, Sparpläne,
  Sparplankategorien, Wertpapiere, Wertpapierkategorien, Budgets,
  Budgetkategorien, Budgetregeln, Reports, KPI-Konfigurationen, Anhänge,
  Anhangkategorien) gegen die Rechtearten Leserecht, Vorschlagsrecht,
  Vervollständigungsrecht, Änderungsrecht, Erstellungsrecht, Buchungsrecht,
  Löschrecht – jeweils mit Mapping auf die realen Entitäten/Enums des Repos
  (u. a. `AccountType`-Enum statt Kontotyp-Entität, `ContactCategory` =
  Kontaktgruppen, `StatementImport`/`StatementEntry` = Kontoauszüge,
  `StatementDraft(+Entry)` = Entwürfe, `BudgetPurpose`/`BudgetRule`/
  `BudgetCategory`, `ReportFavorite`/`HomeKpi`/`PortfolioKpiConfiguration`,
  `Attachment(+Category)`).
- **Filterkonzept** (F4): optionale Einschränkung von Rechten auf Basis
  vorhandener Gruppierungsstrukturen (Kontotypen, Kontaktgruppen,
  Wertpapierkategorien, Sparplankategorien, Anhangkategorien); Standard =
  Rechte gelten für alle Datensätze eines Datentyps; Anknüpfung an das
  vorhandene CSV-Filtermuster von `ReportFavorite`.

Das **Berechtigungsmodell** (`berechtigungsmodell.md`) enthält:

- Scope-Modell und Aktionsmodell (F2): wie Scopes Datentyp × Rechteart
  (× optionaler Filter) kombinieren.
- Rechtevererbung: Standardrechte je Freigabestufe → individuelle
  Rechteanpassung je Zugang; Konfliktverhalten bei überlappenden
  Regeln (einschränkend vs. erweiternd, Vorrangregeln).
- Verbindlicher Grundsatz: MCP-Rechte dürfen bestehende Benutzerrechte nur
  einschränken, niemals erweitern – inkl. Behandlung von `AccountShare`-
  Fremdfreigaben (Read/Write/Admin) als Obergrenze.
- Rechteableitung für Entitäten ohne eigenen Owner: `Posting` (über
  `AccountId`/`ContactId`/`SavingsPlanId`/`SecurityId` je `Kind`),
  `StatementImport`/`StatementEntry` (über `AccountId`), `AccountLinkedIban`,
  `AliasName`, `SecurityPrice` – jeweils dokumentiert, über welches Aggregate
  die Eigentümerschaft aufgelöst wird.
- Semantik der Rechtearten im FinanceManager-Kontext (z. B. „Buchungsrecht" =
  Verbuchung von Entwürfen, nicht direktes Posting-Schreiben; „Löschrecht"
  für Buchungen = Storno, nie physisches Löschen).

**Liefert Artefakte:** 1 (Fachkonzept → `fachkonzept.md`), 3 (Berechtigungsmodell
→ `berechtigungsmodell.md`) – abgelegt unter `Docs/planung/mcp-integration/`.

**Akzeptanzkriterien:**

- Beide Dokumente liegen als deutschsprachige Markdown-Dateien unter
  `Docs/planung/mcp-integration/` vor.
- Die F3-Matrix enthält alle 18 Datentypen × 7 Rechtearten und mappt jeden
  Anforderungsbegriff auf die konkrete Entität/Enum des Repos (kein
  unaufgelöster Begriff).
- Das Berechtigungsmodell beschreibt Scope- und Aktionsmodell, Vererbung,
  Konfliktverhalten sowie Standard- und individuelle Rechte vollständig (F2)
  und operationalisiert den Grundsatz „nur einschränken, niemals erweitern"
  inklusive `AccountShare`-Bezug.
- Für jede Entität ohne eigenes `OwnerUserId` ist die Rechteableitung
  dokumentiert.
- Die fünf Freigabestufen sind je Stufe mit erlaubten Aktionen beschrieben (F5);
  das Filterkonzept (F4) und die MCP-Zugangsverwaltung (F1) sind vollständig
  spezifiziert.

**Abhängigkeiten:** Keine

**Betroffene fachliche Bereiche:** Domänenmodell gesamt (alle Fachbereiche),
Benutzer-/Rechtemodell, MCP-Zugangsverwaltung.

### Schritt 2: Scope- und Toolkatalog

Konzeption des fachlichen MCP-Toolkatalogs auf Basis des in Schritt 1 definierten
Rechte- und Scope-Modells (F6) sowie des KI-gestützten Kontoauszugs-Assistenten
(F7) als zentraler Workflow.

Der **Scope- und Toolkatalog** (`scope-und-toolkatalog.md`) enthält:

- Verbindliche Grundsätze: Tools bilden Geschäftsprozesse ab (keine generischen
  Datenbankoperationen, keine CRUD-APIs, kein DB-Direktzugriff – Nicht-Ziele
  explizit); jeder Tool ist einem Scope/einer Rechteart aus Schritt 1
  zugeordnet.
- Fachlicher Katalog der benötigten Tools, gruppiert nach Geschäftsprozess
  (z. B. Analyse/Reporting lesend, Stammdaten-Vorschläge, Entwurfs-
  Vervollständigung, Verbuchungsvorbereitung, Storno-Anforderung): je Tool
  Zweck, Scope-Zuordnung, benötigte Berechtigungen, Sicherheitsklassifizierung
  (lesend / vorschlagend / schreibvorbereitend / genehmigungspflichtig),
  erwartete Ein- und Ausgaben (orientiert an den `FinanceManager.Shared`-
  DTOs) sowie Nebenbedingungen (Idempotenz, Limits).
- **Kontoauszugs-Assistent** (F7): vollständige fachliche Spezifikation des
  Workflows – Nutzung ausschließlich der vorhandenen Importmechanismen (keine
  Bankanbindung), Analyse von `StatementDraft`-Entwürfen, Vervollständigung
  fehlender Informationen (Kontakt-, Sparplan-, Wertpapier-Zuordnung),
  Dublettenerkennung, Verbuchungsvorschläge und Plausibilitätsprüfungen;
  Mapping der Teilfunktionen auf die vorhandene `IStatementDraftService`-
  Oberfläche (Get*/Validate für Analyse, Set*/Save*/Batch für
  Vervollständigung, Classify/AlreadyBooked für Dubletten, Book für
  Verbuchungsvorschläge) – der MCP-Layer wird als dünner Adapter konzipiert.
- Versionierungsstrategie des Toolkatalogs: fachliche Regeln für
  Tool-Benennung, Änderungsklassen (kompatibel/brechend), Hinzufügen/
  Entfernen von Tools und Kommunikation gegenüber MCP-Clients.
- Übersichtstabelle: Tool → Scope/Rechteart → Sicherheitsklasse →
  Freigabestufen-Mindestniveau → genehmigungspflichtig (ja/nein) als Eingabe
  für das Approval-Konzept.

**Liefert Artefakte:** 4 (Scope- und Toolkatalog → `scope-und-toolkatalog.md`)
unter `Docs/planung/mcp-integration/`.

**Akzeptanzkriterien:**

- Das Dokument liegt als deutschsprachige Markdown-Datei unter
  `Docs/planung/mcp-integration/` vor.
- Jedes gelistete Tool ist einem Geschäftsprozess, einem Scope/einer
  Rechteart (Begriffe aus Schritt 1) und einer Sicherheitsklasse zugeordnet;
  Ein-/Ausgaben sind auf DTO-Niveau beschrieben.
- Der Kontoauszugs-Assistent (F7) ist vollständig beschrieben und jede
  Teilfunktion ist auf die vorhandene Import-/Entwurfs-Pipeline
  (`IStatementDraftService` bzw. `IMassImportOrchestrator`) abgebildet; eine
  direkte Bankanbindung ist explizit ausgeschlossen.
- Die Versionierungsstrategie ist dokumentiert.
- Kein Tool beschreibt generische CRUD- oder DB-Zugriffe; für Buchungen ist
  Storno als einziger Rücknahmeweg modelliert.

**Abhängigkeiten:** Schritt 1

**Betroffene fachliche Bereiche:** Kontoauszugs-Import/Entwürfe,
Buchungen/Storno, alle datenlesenden Fachbereiche (Analyse), MCP-Scope-Modell.

### Schritt 3: Sicherheits-, Audit- und Approval-Konzept

Konzeption der drei Schutz- und Kontrollquerschnitte der MCP-Integration.

Das **Sicherheitskonzept** (`sicherheitskonzept.md`) enthält (F8):

- **Authentifizierung:** Personal Access Tokens pro MCP-Zugang –
  Token-Generierung, Einweg-Hashing zur Speicherung (Vorbild
  `Pbkdf2IdentityPasswordHasher`; Abgrenzung zum reversiblen
  `IAlphaVantageSecretProtector`-Ansatz), Token-Lebensdauer/Ablaufdaten,
  Widerruf mit sofortiger serverseitiger Invalidierung (Vorbild
  `OnTokenValidated`-/Security-Stamp-Pipeline), Transportmechanismus
  (Authorization-Header).
- **Autorisierung:** Durchsetzung von Scopes, Aktionen und Filtern aus
  Schritt 1/2 inkl. Unterordnung unter die Benutzerrechte; Behandlung der
  indirekten Owner-Ableitung.
- **Prompt-Injection-Schutz:** Klassifizierung aller an MCP-Clients
  ausgelieferten Inhalte als untrusted content (Buchungstexte
  `Subject`/`RecipientName`, Kontaktbeschreibungen, Notizen, Anhänge inkl.
  `Attachment.Content` als byte[] in SQLite, importierte Dokumente) sowie
  fachliche Gegenmaßnahmen (Markierung/Isolation, Output-Konventionen,
  Restriktionen für schreibende Tools).
- **Notfallmaßnahmen:** globale MCP-Deaktivierung per Feature-Flag
  (`appsettings`-Konvention), Deaktivierung aller Schreibzugriffe
  (Lese-Fallback), Erkennung und Behandlung verdächtiger Aktivitäten
  (Anknüpfung `IpBlock`-Infrastruktur, Benachrichtigung via
  `NotificationWriter` SystemAlert).
- **Rate Limiting:** Limits pro MCP-Token/Zugang (Vorbild
  `RateLimitPolicies.AuthSensitive` – pro Token statt pro IP), Abstufungen
  je Sicherheitsklasse.
- **Transport-/Betriebsrisiken:** LAN-/HTTP-Betrieb ohne TLS als
  dokumentiertes Risiko; Empfehlungen (Reverse Proxy/TLS, ggf.
  Token-Bindung), Forwarded-Headers-Konfiguration.

Das **Audit-Konzept** (`audit-konzept.md`) enthält (F9):

- Was revisionssicher protokolliert wird: Toolaufrufe (wer/welcher Zugang,
  wann, welches Tool, Parameter, Ergebnisstatus), durchgeführte und
  abgelehnte Änderungen, Genehmigungsentscheidungen, Sicherheitsereignisse
  (Token-Erzeugung/-Widerruf, Notfallabschaltungen, verdächtige Aktivität).
- Detailtiefe und Datenschutzabwägung (persönliche Finanzdaten im Log),
  Aufbewahrungsdauer, Auswertbarkeit für den Benutzer (Audit-Ansicht fachlich)
  und den Admin.
- Nachvollziehbarkeit von KI-Entscheidungen: Verknüpfung Toolaufruf ↔
  AiActionRequest ↔ durchgeführter Änderung (z. B. erzeugte Postings,
  gebuchte Entwürfe).
- Abgrenzung zum vorhandenen technischen Logging (`RequestLoggingMiddleware`,
  `FileLoggerProvider`) – kein Audit-Trail, neuer persistenter Speicher nötig.

Das **Approval-Konzept** (`approval-konzept.md`) enthält (F10):

- Katalog genehmigungspflichtiger Aktionen, abgeleitet aus der
  Sicherheitsklassifizierung des Toolkatalogs (Schritt 2) und den
  Freigabestufen (Schritt 1).
- Neu konzipierte persistente Entität `AiActionRequest` (nicht im Code
  vorhanden) mit Zuständen Pending, Approved, Rejected, Executed, Failed –
  Zustandsdiagramm, erlaubte Übergänge, Zeitlimit/Ablauf von Anfragen.
- Fachlicher Ablauf: Anforderung durch KI-Tool → Benutzerbenachrichtigung
  (`NotificationWriter`, EventDriven) → Prüfung im Genehmigungscenter →
  Ausführung nach Freigabe → Ergebnisrückmeldung an den MCP-Client;
  Fehlerbehandlung (Fehlschlag, Timeout, Widerruf des Zugangs während
  Pending).
- Fachliche Anforderungen an Ausführungsschutz: Idempotenz/Schutz vor
  Doppelausführung (Vorbild `StatementDraftBookingGuard`), persistente Queue
  als Erweiterung des In-Memory-`IBackgroundTaskManager`-Patterns.
- UX-Prinzipien des Genehmigungscenters (fachlich; Detail-UI in Schritt 4).

**Liefert Artefakte:** 2 (Sicherheitskonzept → `sicherheitskonzept.md`),
8 (Approval-Konzept → `approval-konzept.md`), 9 (Audit-Konzept →
`audit-konzept.md`) unter `Docs/planung/mcp-integration/`.

**Akzeptanzkriterien:**

- Alle drei Dokumente liegen als deutschsprachige Markdown-Dateien unter
  `Docs/planung/mcp-integration/` vor.
- Das Sicherheitskonzept deckt Authentifizierung (PAT inkl. Hashing,
  Lebensdauer, Widerruf), Autorisierung, Prompt-Injection-Schutz,
  Notfallmaßnahmen und Rate Limiting vollständig ab (F8) und behandelt den
  HTTP-/LAN-Betrieb als explizites Risiko.
- Das Audit-Konzept benennt alle zu protokollierenden Ereignisse, Detailtiefe,
  Aufbewahrung und Auswertbarkeit (F9) und grenzt sich vom vorhandenen
  technischen Logging ab.
- Das Approval-Konzept spezifiziert `AiActionRequest` mit den Zuständen
  Pending/Approved/Rejected/Executed/Failed, den Workflow inkl.
  Fehlerbehandlung und Doppelausführungsschutz (F10).
- Die Liste genehmigungspflichtiger Aktionen ist konsistent mit der
  Sicherheitsklassifizierung aus `scope-und-toolkatalog.md`.

**Abhängigkeiten:** Schritt 1, Schritt 2

**Betroffene fachliche Bereiche:** Sicherheit/Authentifizierung, Compliance/
Protokollierung, Genehmigungsworkflow, Benachrichtigungen.

### Schritt 4: UI-, Architektur- und Datenmodellkonzept

Konzeption der technisch-realen Dimension: Benutzeroberflächen, Zielarchitektur
des MCP-Servers und die erforderlichen Persistenz-Erweiterungen.

Das **UI-Konzept** (`ui-konzept.md`) enthält (F11):

- Komponentenübersicht: Verwaltung der MCP-Zugänge (Liste, Erzeugen,
  Aktivieren/Deaktivieren, Ablaufdaten), Tokenverwaltung (Einmalanzeige,
  Rotation, Widerruf), Rechteverwaltung (Freigabestufen + individuelle
  Rechte), Filterverwaltung (optionale Kategorie-Einschränkungen),
  Audit-Ansicht (Nutzungs- und Aktivitätsprotokoll), Genehmigungscenter
  (Pending-Anfragen prüfen/freigeben/ablehnen).
- UX-Leitlinien: einfache Benutzerführung für Standardfälle (Freigabestufe
  wählen), Expertenmodus für individuelle Rechte/Filter, Verständlichkeit
  sicherheitsrelevanter Begriffe; Verwendung des
  `IConfirmationService`-Bestätigungsmusters für kritische Aktionen.
- Einordnung in die UI-Konventionen: Blazor Server, MVVM-ViewModels, die
  ausschließlich über `IApiClient` mit der eigenen API kommunizieren;
  notwendige neue API-Endpunkte fachlich skizziert; Lokalisierung de/en
  (resx); Benutzer- vs. Admin-Sichten (z. B. globale MCP-Schalter,
  Sicherheitsalarme).

Das **Architekturkonzept** (`architekturkonzept.md`) enthält (F12):

- Zielarchitektur: Position des MCP-Servers als parallele Zugriffsschicht zur
  bestehenden BFF-REST-API auf demselben Service-Layer (kein eigener
  fachlicher Pfad); Betriebsform im Rahmen des Single-Instance-Deployments
  (Vorschlag: integriert in `FinanceManager.Web` unter eigenem Route-Pfad),
  Transport via Streamable HTTP (Annahme A4).
- Sicherheitslayer: Token-Validierung, Auflösung Zugang→UserId
  (`ICurrentUserService`-Äquivalent), Scope-Prüfung je Toolaufruf,
  Rate-Limit-Einbindung, Notfall-Schalter (globales Flag, Schreib-Stopp).
- Tool-Registrierung und Ausführungsmodell: Adapter auf `I*Service`-
  Contracts, Einbindung der Approval-Queue für genehmigungspflichtige Tools,
  Logging-/Audit-Anbindung.
- Persistenzmodell-Übersicht: neue Entitäten in `AppDbContext`
  (DbSet-Konventionen, `OnModelCreating`, EF-Migrations), Umgang mit
  SQLite-Schreibserialisierung, Backup-Einbindung.
- Deployment-/Betriebssicht: HTTP-only-LAN vs. Reverse-Proxy-Betrieb,
  Auto-Update-Verträglichkeit, Konfiguration (`appsettings`-Flags).

Das Konzept **Datenmodell-Erweiterungen** (`datenmodell-erweiterungen.md`)
enthält (Artefakt 7):

- Liste aller neuen Entitäten/Typen mit Feldern (fachliches Niveau, kein Code):
  MCP-Zugang/Token (Hash, Ablauf, Widerruf, letzte Nutzung), Scope-/Rechtesatz
  je Zugang (Standard nach Freigabestufe + individuelle Overrides), optionale
  Filter-Zuordnungen, `AiActionRequest` (Zustände, Tool-Referenz, Parameter-
  Snapshot, Ergebnis), Audit-Log-Einträge, ggf. globale
  MCP-Sicherheitseinstellungen.
- Je Entität: Owner-Bezug (benutzerbezogen vs. global), Beziehungen zu
  Bestands-Entitäten, Einordnung in die `Entity`-Basisklassen-Konvention
  (`Id`, `CreatedUtc`, `ModifiedUtc`), Indizes/Uniqueness-Bedarf,
  Backup-Relevanz (`*BackupDto`-Konvention), Lösch-/Archivierungsverhalten.
- Migrationssicht: Erweiterung von `AppDbContext` via DbSet +
  `OnModelCreating`-Konfiguration + EF-Migration; Auswirkungen auf Backup/
  Restore; keine Änderung bestehender Fachentitäten außer begründeten
  Ausnahmen (z. B. kein `OwnerUserId` auf `Posting` – indirekte Ableitung
  bleibt).

**Liefert Artefakte:** 5 (UI-Konzept → `ui-konzept.md`), 6 (Architekturkonzept
→ `architekturkonzept.md`), 7 (Datenmodell-Erweiterungen →
`datenmodell-erweiterungen.md`) unter `Docs/planung/mcp-integration/`.

**Akzeptanzkriterien:**

- Alle drei Dokumente liegen als deutschsprachige Markdown-Dateien unter
  `Docs/planung/mcp-integration/` vor.
- Das UI-Konzept deckt alle sechs geforderten Komponenten (Zugänge, Token,
  Rechte, Filter, Audit, Genehmigungscenter) ab und berücksichtigt einfache
  Benutzerführung, Expertenmodus und die Blazor/MVVM/`IApiClient`-Konvention.
- Das Architekturkonzept beschreibt MCP-Server-Struktur, Service-Layer-
  Anbindung, Sicherheitslayer, Tool-Registrierung, Scope-Prüfung, Logging
  und Persistenzmodell (F12) konsistent zu Schritten 1–3 und zu den
  Single-Instance-/SQLite-/LAN-Constraints.
- Das Datenmodell-Dokument enthält alle neuen Entitäten inkl. Owner-Bezügen,
  Beziehungen, Konventionen (DbSet/OnModelCreating/Migration/Backup) und
  erfordert keine Änderung der Owner-Ableitung bestehender Fachentitäten.

**Abhängigkeiten:** Schritt 1, Schritt 2, Schritt 3

**Betroffene fachliche Bereiche:** Benutzeroberfläche (alle neuen
Verwaltungsbereiche), Systemarchitektur (Web-Host, Sicherheitslayer),
Persistenz/Datenmodell.

### Schritt 5: Roadmap, Aufwandsschätzung und Product-Backlog

Konsolidierung aller Konzeptdokumente zu einer priorisierten Umsetzungsplanung
(F13).

Die **priorisierte Umsetzungsroadmap** (`roadmap.md`) enthält:

- Die fünf vorgegebenen Phasen: 1. Read-Only Analyse, 2. Assistenz- und
  Vorschlagsfunktionen, 3. Kontoauszugs-Assistent, 4. Genehmigungspflichtige
  Aktionen, 5. Agentengestützte Workflows.
- Pro Phase: Funktionsumfang (welche Tools/Konzeptteile aus den Schritten
  1–4 enthalten sind), Sicherheitsrisiken und deren Mitigation,
  technische Voraussetzungen (inkrementelle Ausbaustufen: welche
  Entitäten/UI-/Architekturteile je Phase nötig sind), Erfolgskriterien.
- Priorisierungsbegründung und Phasen-Abhängigkeiten.

Die **Aufwandsschätzung** (`aufwandsschaetzung.md`) enthält:

- Schätzung je Phase bzw. Epic mit transparenter Methodik (z. B.
  T-Shirt-Größen/Personentage), begründeten Annahmen und Unsicherheiten;
  Aufschlüsselung nach Bereichen (Backend/Domain, Sicherheitslayer, UI,
  Migration, Test).

Das **Product-Backlog** (`product-backlog.md`) enthält:

- Epics entlang der Phasen und Querschnitte (z. B. MCP-Zugangsverwaltung,
  Berechtigungs-/Scope-Engine, Tool-Layer, Kontoauszugs-Assistent,
  Approval-Workflow, Audit, Admin-/Notfallfunktionen), darunter priorisierte
  Features mit kurzer Beschreibung und Akzeptanzkriterien-Skizze;
  eindeutiger Bezug jedes Features zu Phase und Konzeptdokument; keine
  Implementierungsdetails.
- Zusätzlich wird ein `README.md`-Index aller Dokumente in
  `Docs/planung/mcp-integration/` erstellt (Übersicht/Navigation).

**Liefert Artefakte:** 10 (Priorisierte Umsetzungsroadmap → `roadmap.md`),
11 (Aufwandsschätzung → `aufwandsschaetzung.md`), 12 (Product-Backlog →
`product-backlog.md`) sowie `README.md` unter
`Docs/planung/mcp-integration/`.

**Akzeptanzkriterien:**

- Alle Dokumente liegen als deutschsprachige Markdown-Dateien unter
  `Docs/planung/mcp-integration/` vor.
- Die Roadmap enthält exakt die fünf vorgegebenen Phasen mit Funktionsumfang,
  Sicherheitsrisiken, technischen Voraussetzungen und Erfolgskriterien je
  Phase (F13).
- Die Aufwandsschätzung deckt alle Phasen/Epics ab und macht Methodik und
  Annahmen transparent.
- Das Product-Backlog enthält Epics und priorisierte Features mit Bezug zu
  Phase und Konzeptdokument; alle Konzeptinhalte aus Schritten 1–4 sind im
  Backlog abbildbar (kein Konzept ohne Umsetzungsfeature).
- `README.md` verlinkt alle zwölf Ergebnisdokumente.

**Abhängigkeiten:** Schritt 1, Schritt 2, Schritt 3, Schritt 4

**Betroffene fachliche Bereiche:** Projektplanung/Product-Management,
übergreifend alle Konzeptbereiche.

## Übersichtstabelle

| # | Titel | Abhängigkeiten | Betroffene Bereiche |
|---|---|---|---|
| 1 | Fachkonzept und Berechtigungsmodell | Keine | Domänenmodell gesamt, Benutzer-/Rechtemodell, MCP-Zugangsverwaltung |
| 2 | Scope- und Toolkatalog | 1 | Kontoauszugs-Import/Entwürfe, Buchungen/Storno, Datenanalyse, Scope-Modell |
| 3 | Sicherheits-, Audit- und Approval-Konzept | 1, 2 | Sicherheit/Authentifizierung, Compliance/Protokollierung, Genehmigungsworkflow, Benachrichtigungen |
| 4 | UI-, Architektur- und Datenmodellkonzept | 1, 2, 3 | Benutzeroberfläche, Systemarchitektur, Persistenz/Datenmodell |
| 5 | Roadmap, Aufwandsschätzung und Product-Backlog | 1, 2, 3, 4 | Projektplanung/Product-Management, übergreifend |

## Offene Punkte

1. **Betriebsform des MCP-Servers:** In-process als eigener Endpoint in
   `FinanceManager.Web` vs. separater Prozess/Dienst. **Vorschlag:**
   In-process-Integration unter eigenem Route-Pfad (z. B. `/mcp`) – passt zu
   Single-Instance-Deployment, SQLite, Auto-Update und dem BFF-Befund; ein
   separater Prozess brächte zweiten DB-Zugriff und Verteilungsprobleme ohne
   erkennbaren Mehrwert. Endgültige Festlegung erfolgt im Architekturkonzept
   (Schritt 4), der Alternativen mit Begründung dokumentiert.
2. **Umgang mit HTTP-only-LAN-Betrieb:** Token-Transport im Klartext ist ein
   reales Risiko (kein TLS-Zwang im Ist-Stand). **Vorschlag:** Das
   Sicherheitskonzept empfiehlt Reverse-Proxy/TLS als Zielbetrieb, definiert
   HTTP-Betrieb aber als unterstützten Fallback mit dokumentierten Risiken
   und Zusatzmaßnahmen (kurze Token-Lebensdauer, IP-Rate-Limits, ggf.
   Hinweis-Pflicht in der UI) – eine TLS-Pflicht würde das dokumentierte
   LAN-Szenario brechen.

**Behandlung (2026-11-04):** Beide Punkte sind Konzeptentscheidungen, keine
Scope-/Priorisierungsfragen. Es wird mit den genannten Vorschlägen als
verbindliche Annahmen weitergearbeitet; die jeweiligen Konzeptdokumente
(Schritte 3 und 4) begründen die Entscheidung inklusive Alternativen. Damit
gelten beide Punkte als behandelt.
