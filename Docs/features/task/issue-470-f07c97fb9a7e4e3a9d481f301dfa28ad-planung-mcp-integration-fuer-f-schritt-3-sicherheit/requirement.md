# Anforderung – Schritt 3: Sicherheits-, Audit- und Approval-Konzept

Quelle: `Docs/projects/task/issue-470-f07c97fb9a7e4e3a9d481f301dfa28ad-planung-mcp-integration-fuer-f/project-plan.md`, Abschnitt „### Schritt 3: Sicherheits-, Audit- und Approval-Konzept" (Projekt: Planung MCP-Integration für FinanceManager, Issue #470). Fachliche Anforderungen: `requirement.md` des Projekts, F8/F9/F10.

## Schrittbeschreibung (aus project-plan.md, verbindlich)

Konzeption der drei Schutz- und Kontrollquerschnitte der MCP-Integration.
Lieferung dreier deutschsprachiger Markdown-Konzeptdokumente unter
`Docs/planung/mcp-integration/` – keine Implementierung.

### `sicherheitskonzept.md` (Artefakt 2, F8)

- Authentifizierung: Personal Access Tokens je MCP-Zugang; Generierung,
  Einweg-Hashing zur Speicherung (Vorbild `Pbkdf2IdentityPasswordHasher`;
  Abgrenzung zum reversiblen `IAlphaVantageSecretProtector`/`dp:v1:`-Ansatz),
  Token-Lebensdauer/Ablaufdaten, Widerruf mit sofortiger serverseitiger
  Invalidierung (Vorbild `OnTokenValidated`/Security-Stamp-Pipeline),
  Transport per Authorization-Header.
- Autorisierung: Durchsetzung von Scopes, Aktionen und Filtern aus
  Schritt 1/2 inkl. Unterordnung unter die Benutzerrechte; Behandlung der
  indirekten Owner-Ableitung.
- Prompt-Injection-Schutz: alle an MCP-Clients ausgelieferten Inhalte als
  untrusted content (Buchungstexte `Subject`/`RecipientName`,
  Kontaktbeschreibungen, Notizen, `Attachment.Content` als byte[] in SQLite,
  importierte Dokumente); fachliche Gegenmaßnahmen (Markierung/Isolation,
  Output-Konventionen, Restriktionen für schreibende Tools).
- Notfallmaßnahmen: globale MCP-Deaktivierung per Feature-Flag
  (`appsettings`-Konvention), Deaktivierung aller Schreibzugriffe
  (Lese-Fallback), Erkennung/Behandlung verdächtiger Aktivitäten
  (`IpBlock`-Infrastruktur, `NotificationWriter` SystemAlert).
- Rate Limiting: Limits pro MCP-Token/Zugang (Vorbild
  `RateLimitPolicies.AuthSensitive` – pro Token statt pro IP), Abstufungen
  je Sicherheitsklasse.
- Transport-/Betriebsrisiken: LAN-/HTTP-Betrieb ohne TLS als dokumentiertes
  Risiko; Empfehlungen (Reverse Proxy/TLS, ggf. Token-Bindung),
  Forwarded-Headers-Konfiguration (Annahme 2 der „Offenen Punkte":
  TLS = Zielbetrieb, HTTP = dokumentierter Fallback mit Zusatzmaßnahmen).

### `approval-konzept.md` (Artefakt 8, F10)

- Katalog genehmigungspflichtiger Aktionen, abgeleitet aus der
  Sicherheitsklassifizierung des Toolkatalogs (Schritt 2) und den
  Freigabestufen (Schritt 1).
- Neu konzipierte persistente Entität `AiActionRequest` (nicht im Code
  vorhanden) mit Zuständen Pending, Approved, Rejected, Executed, Failed –
  Zustandsdiagramm, erlaubte Übergänge, Zeitlimit/Ablauf.
- Fachlicher Ablauf: Anforderung durch KI-Tool → Benutzerbenachrichtigung
  (`NotificationWriter`, EventDriven) → Prüfung im Genehmigungscenter →
  Ausführung nach Freigabe → Ergebnisrückmeldung an den MCP-Client;
  Fehlerbehandlung (Fehlschlag, Timeout, Widerruf während Pending).
- Ausführungsschutz: Idempotenz/Doppelausführungsschutz (Vorbild
  `StatementDraftBookingGuard`), persistente Queue als Erweiterung des
  In-Memory-`IBackgroundTaskManager`-Patterns.
- UX-Prinzipien des Genehmigungscenters (fachlich; Detail-UI in Schritt 4).

### `audit-konzept.md` (Artefakt 9, F9)

- Revisionssichere Protokollierung: Toolaufrufe (wer/welcher Zugang, wann,
  welches Tool, Parameter, Ergebnisstatus), durchgeführte und abgelehnte
  Änderungen, Genehmigungsentscheidungen, Sicherheitsereignisse
  (Token-Erzeugung/-Widerruf, Notfallabschaltungen, verdächtige Aktivität).
- Detailtiefe und Datenschutzabwägung (persönliche Finanzdaten im Log),
  Aufbewahrungsdauer, Auswertbarkeit für Benutzer und Admin.
- Nachvollziehbarkeit von KI-Entscheidungen: Verknüpfung Toolaufruf ↔
  AiActionRequest ↔ durchgeführte Änderung.
- Abgrenzung zum vorhandenen technischen Logging
  (`RequestLoggingMiddleware`, `FileLoggerProvider`) – neuer persistenter
  Speicher; WORM-/Integritätsaspekte auf SQLite-Niveau.

## Akzeptanzkriterien (aus project-plan.md)

- Alle drei Dokumente liegen als deutschsprachige Markdown-Dateien unter
  `Docs/planung/mcp-integration/` vor.
- Das Sicherheitskonzept deckt Authentifizierung (PAT inkl. Hashing,
  Lebensdauer, Widerruf), Autorisierung, Prompt-Injection-Schutz,
  Notfallmaßnahmen und Rate Limiting vollständig ab (F8) und behandelt den
  HTTP-/LAN-Betrieb als explizites Risiko.
- Das Audit-Konzept benennt alle zu protokollierenden Ereignisse,
  Detailtiefe, Aufbewahrung und Auswertbarkeit (F9) und grenzt sich vom
  vorhandenen technischen Logging ab.
- Das Approval-Konzept spezifiziert `AiActionRequest` mit den Zuständen
  Pending/Approved/Rejected/Executed/Failed, den Workflow inkl.
  Fehlerbehandlung und Doppelausführungsschutz (F10).
- Die Liste genehmigungspflichtiger Aktionen ist konsistent mit der
  Sicherheitsklassifizierung aus `scope-und-toolkatalog.md`.

## Abhängigkeiten

Schritt 1 (`fachkonzept.md`, `berechtigungsmodell.md`) und Schritt 2
(`scope-und-toolkatalog.md`) – beide liegen vor und wurden verbindlich
übernommen.
