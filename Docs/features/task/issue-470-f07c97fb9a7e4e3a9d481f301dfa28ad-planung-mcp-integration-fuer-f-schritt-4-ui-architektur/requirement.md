# Anforderung – Schritt 4: UI-, Architektur- und Datenmodellkonzept

Quelle: `Docs/projects/task/issue-470-f07c97fb9a7e4e3a9d481f301dfa28ad-planung-mcp-integration-fuer-f/project-plan.md`, Abschnitt „### Schritt 4: UI-, Architektur- und Datenmodellkonzept" (Projekt: Planung MCP-Integration für FinanceManager, Issue #470). Fachliche Anforderungen: `requirement.md` des Projekts, F11/F12 + Artefakt 7.

## Schrittbeschreibung (aus project-plan.md, verbindlich)

Konzeption der technisch-realen Dimension: Benutzeroberflächen, Zielarchitektur
des MCP-Servers und die erforderlichen Persistenz-Erweiterungen. Lieferung
dreier deutschsprachiger Markdown-Konzeptdokumente unter
`Docs/planung/mcp-integration/` – keine Implementierung.

### `ui-konzept.md` (Artefakt 5, F11)

- Komponentenübersicht: Verwaltung der MCP-Zugänge (Liste, Erzeugen,
  Aktivieren/Deaktivieren, Ablaufdaten), Tokenverwaltung (Einmalanzeige,
  Rotation, Widerruf), Rechteverwaltung (Freigabestufen + individuelle
  Rechte), Filterverwaltung (optionale Kategorie-Einschränkungen),
  Audit-Ansicht (Nutzungs- und Aktivitätsprotokoll), Genehmigungscenter
  (Pending-Anfragen prüfen/freigeben/ablehnen).
- UX-Leitlinien: einfache Benutzerführung für Standardfälle (Freigabestufe
  wählen), Expertenmodus für individuelle Rechte/Filter, Verständlichkeit
  sicherheitsrelevanter Begriffe; `IConfirmationService`-Bestätigungsmuster
  für kritische Aktionen.
- Einordnung in die UI-Konventionen: Blazor Server, MVVM-ViewModels, die
  ausschließlich über `IApiClient` mit der eigenen API kommunizieren;
  notwendige neue API-Endpunkte fachlich skizziert; Lokalisierung de/en
  (resx); Benutzer- vs. Admin-Sichten (globale MCP-Schalter,
  Sicherheitsalarme).

### `architekturkonzept.md` (Artefakt 6, F12)

- Zielarchitektur: Position des MCP-Servers als parallele Zugriffsschicht
  zur bestehenden BFF-REST-API auf demselben Service-Layer (kein eigener
  fachlicher Pfad); Betriebsform im Single-Instance-Deployment
  (Vorschlag: integriert in `FinanceManager.Web` unter eigenem Route-Pfad
  `/mcp`), Transport via Streamable HTTP (Annahme A4) – behandelte
  Annahme 1 des Projektplans hier final entscheiden (Alternativen
  begründet).
- Sicherheitslayer: Token-Validierung, Auflösung Zugang→UserId
  (`ICurrentUserService`-Äquivalent), Scope-Prüfung je Toolaufruf,
  Rate-Limit-Einbindung, Notfall-Schalter (globales Flag, Schreib-Stopp).
- Tool-Registrierung und Ausführungsmodell: Adapter auf `I*Service`-
  Contracts, Einbindung der Approval-Queue für genehmigungspflichtige
  Tools, Logging-/Audit-Anbindung.
- Persistenzmodell-Übersicht: neue Entitäten in `AppDbContext`
  (DbSet-Konventionen, `OnModelCreating`, EF-Migrations), Umgang mit
  SQLite-Schreibserialisierung, Backup-Einbindung.
- Deployment-/Betriebssicht: HTTP-only-LAN vs. Reverse-Proxy-Betrieb,
  Auto-Update-Verträglichkeit, Konfiguration (`appsettings`-Flags).

### `datenmodell-erweiterungen.md` (Artefakt 7)

- Liste aller neuen Entitäten/Typen mit Feldern (fachliches Niveau):
  MCP-Zugang/Token (Hash, Ablauf, Widerruf, letzte Nutzung),
  Scope-/Rechtesatz je Zugang (Standard nach Freigabestufe + individuelle
  Overrides), optionale Filter-Zuordnungen, `AiActionRequest` (Zustände,
  Tool-Referenz, Parameter-Snapshot, Ergebnis), Audit-Log-Einträge, ggf.
  globale MCP-Sicherheitseinstellungen.
- Je Entität: Owner-Bezug (benutzerbezogen vs. global), Beziehungen zu
  Bestands-Entitäten, Einordnung in die `Entity`-Basisklassen-Konvention
  (`Id`, `CreatedUtc`, `ModifiedUtc`), Indizes/Uniqueness-Bedarf,
  Backup-Relevanz (`*BackupDto`-Konvention), Lösch-/Archivierungsverhalten.
- Migrationssicht: Erweiterung von `AppDbContext` via DbSet +
  `OnModelCreating`-Konfiguration + EF-Migration; Auswirkungen auf Backup/
  Restore; keine Änderung bestehender Fachentitäten außer begründeten
  Ausnahmen (kein `OwnerUserId` auf `Posting` – indirekte Ableitung
  bleibt).

## Akzeptanzkriterien (aus project-plan.md)

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

## Abhängigkeiten

Schritte 1–3 liegen vor und wurden verbindlich übernommen:
`fachkonzept.md`, `berechtigungsmodell.md`, `scope-und-toolkatalog.md`,
`sicherheitskonzept.md`, `approval-konzept.md`, `audit-konzept.md`.
