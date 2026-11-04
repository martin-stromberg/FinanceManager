# Anforderung – Planung: MCP-Integration für FinanceManager

Quelle: Issue #470 (martin-stromberg/FinanceManager)
Art: Planungsaufgabe (Ergebnis sind Konzeptdokumente, kein Produktivcode)

## Ziel

Erarbeitung eines vollständigen Fach-, Sicherheits- und Architekturkonzepts zur
Integration eines benutzerbezogenen MCP-Servers (Model Context Protocol) in
FinanceManager. Das Konzept ist Grundlage für die spätere technische Umsetzung,
die Aufwandsschätzung und die Erstellung eines Product Backlogs.

## Ausgangssituation (belegte Vorgaben)

- FinanceManager verwaltet persönliche Finanzdaten: Konten, Kontoauszüge,
  Buchungen, Kontakte, Sparpläne, Wertpapiere, Budgets, Anhänge.
- Benutzer sollen künftig externe KI-Systeme (z. B. ChatGPT, Claude, Copilot,
  lokale LLMs) sicher und kontrolliert auf ihre Daten zugreifen lassen können.
- Der Zugriff erfolgt über einen MCP-Server.
- Unkontrollierte Schreibzugriffe durch KI-Systeme müssen verhindert werden;
  die Anwendung darf kein Sicherheitsrisiko werden.

## Hauptziele

- Sichere Anbindung von LLMs über MCP
- Benutzerbezogene Freigaben
- Granulare Rechtevergabe
- Nachvollziehbarkeit aller Aktionen
- Unterstützung zukünftiger Agentenszenarien
- Schutz sensibler Finanzdaten

## Explizite Nicht-Ziele

- Direkter Datenbankzugriff
- Direkter Zugriff auf Online-Banking-Systeme
- Generische CRUD-APIs für KI-Systeme
- Vollautonome Finanzbuchhaltung ohne Benutzerkontrolle
- Implementierung (reine Planungsaufgabe – Annahme, aus „Planungsaufgabe"
  und Ergebnisliste abgeleitet)

## Fachliche Anforderungen

### F1 – MCP-Zugänge
Verwaltung mehrerer MCP-Zugänge pro Benutzer: Aktivieren/Deaktivieren,
Ablaufdaten, Tokenverwaltung, Widerruf, Nutzungsprotokollierung.

### F2 – Berechtigungsmodell
Scope-Modell, Aktionsmodell, Rechtevererbung, Konfliktverhalten, Standard- und
individuelle Rechte. Grundsatz (belegte Vorgabe): MCP-Rechte dürfen bestehende
Benutzerrechte nur einschränken, niemals erweitern.

### F3 – Datentypen-Klassifizierung
Klassifizierung aller fachlichen Datentypen hinsichtlich: Leserecht,
Vorschlagsrecht, Vervollständigungsrecht, Änderungsrecht, Erstellungsrecht,
Buchungsrecht, Löschrecht. Datentypen: Konten, Kontotypen, Kontoauszüge,
Kontoauszugsentwürfe, Buchungen, Kontakte, Kontaktgruppen, Sparpläne,
Sparplankategorien, Wertpapiere, Wertpapierkategorien, Budgets,
Budgetkategorien, Budgetregeln, Reports, KPI-Konfigurationen, Anhänge,
Anhangkategorien.

### F4 – Filterkonzept
Optionale Einschränkungen auf Basis vorhandener Gruppierungsstrukturen:
Kontotypen, Kontaktgruppen, Wertpapierkategorien, Sparplankategorien,
Anhangkategorien. Filter sind optional; standardmäßig gelten Rechte für alle
Datensätze eines Datentyps.

### F5 – Freigabestufen
Standardisiertes Vertrauensmodell mit Stufen: Analyse, Assistent,
Sachbearbeiter, Buchhalter, Individuell. Pro Stufe Festlegung erlaubter
Aktionen.

### F6 – MCP-Toolkonzept
Fachlicher Toolkatalog: erforderliche Tools, Scope-Zuordnung,
Sicherheitsklassifizierung, benötigte Berechtigungen, erwartete Ein-/Ausgaben,
Versionierungsstrategie. Grundsatz: Tools bilden Geschäftsprozesse ab, keine
generischen Datenbankoperationen.

### F7 – Kontoauszugs-Assistent
KI-gestützter Workflow für importierte Kontoauszüge: keine direkte
Bankanbindung, Nutzung bestehender Importmechanismen, Analyse von Entwürfen,
Vervollständigung fehlender Informationen, Dublettenerkennung,
Verbuchungsvorschläge, Plausibilitätsprüfungen.

### F8 – Sicherheitskonzept
Vollständiges Sicherheitsmodell:
- Authentifizierung: Personal Access Tokens, Token-Hashing, Token-Lebensdauer,
  Widerruf.
- Autorisierung: Scopes, Aktionen, Filter, Benutzerrechte.
- Prompt-Injection-Schutz für untrusted content (Buchungstexte, Kontakte,
  Notizen, Anhänge, importierte Dokumente).
- Notfallmaßnahmen: globale MCP-Deaktivierung, Deaktivierung aller
  Schreibzugriffe, Erkennung verdächtiger Aktivitäten.
- Rate Limiting.

### F9 – Audit- und Compliance-Konzept
Revisionssichere Protokollierung: was wird protokolliert, Detailtiefe,
Aufbewahrungsdauer, Auswertbarkeit; Speicherung von Toolaufrufen und
Änderungsdaten; Nachvollziehbarkeit von KI-Entscheidungen.

### F10 – Genehmigungsworkflow
Approval-Workflow für kritische Aktionen: genehmigungspflichtige Aktionen,
Zustände, Benutzeroberfläche, Ausführung nach Freigabe, Fehlerbehandlung.
Mögliche Grundlage `AiActionRequest` mit Zuständen Pending, Approved,
Rejected, Executed, Failed.

### F11 – Benutzeroberfläche
UI-Komponenten: Verwaltung MCP-Zugänge, Tokenverwaltung, Rechteverwaltung,
Filterverwaltung, Audit-Ansicht, Genehmigungscenter. Berücksichtigung:
einfache Benutzerführung, Expertenmodus, Verständlichkeit.

### F12 – Technische Architektur
Zielarchitektur: MCP-Server-Struktur, Service-Layer, Sicherheitslayer,
Tool-Registrierung, Scope-Prüfung, Logging, Persistenzmodell.

### F13 – Roadmap
Umsetzungsroadmap mit Aufwandsschätzung. Phasen (Vorgabe):
1. Read-Only Analyse
2. Assistenz- und Vorschlagsfunktionen
3. Kontoauszugs-Assistent
4. Genehmigungspflichtige Aktionen
5. Agentengestützte Workflows
Pro Phase: Funktionsumfang, Sicherheitsrisiken, technische Voraussetzungen,
Erfolgskriterien.

## Geforderte Ergebnisartefakte (Akzeptanzkriterien)

1. Fachkonzept
2. Sicherheitskonzept
3. Berechtigungsmodell
4. Scope- und Toolkatalog
5. UI-Konzept
6. Architekturkonzept
7. Datenmodell-Erweiterungen
8. Approval-Konzept
9. Audit-Konzept
10. Priorisierte Umsetzungsroadmap
11. Aufwandsschätzung
12. Product-Backlog mit Epics und Features

## Annahmen (nicht belegt, zu dokumentieren)

- A1: Ablageort der Ergebnisdokumente: `Docs/planung/mcp-integration/`
  (deutschsprachige Docs-Konvention des Repos).
- A2: Dokumentationssprache ist Deutsch (Repo- und Issue-Sprache).
- A3: Keine Codeänderung; Markdown-Dokumente sind das Lieferergebnis.
- A4: Als transportables Format für den MCP-Server wird Streamable HTTP /
  Remote-MCP angenommen (externe KI-Systeme) – Details im Architekturkonzept.

## Offene Fragen

- Keine blockierenden. Detailentscheidungen werden im Projektplan als
  begründete Vorschläge geführt.
