# Anforderung – Schritt 2: Scope- und Toolkatalog

Quelle: `Docs/projects/task/issue-470-f07c97fb9a7e4e3a9d481f301dfa28ad-planung-mcp-integration-fuer-f/project-plan.md`, Abschnitt „### Schritt 2: Scope- und Toolkatalog" (Projekt: Planung MCP-Integration für FinanceManager, Issue #470).

## Schrittbeschreibung (aus project-plan.md, verbindlich)

Konzeption des fachlichen MCP-Toolkatalogs auf Basis des in Schritt 1
definierten Rechte- und Scope-Modells (F6) sowie des KI-gestützten
Kontoauszugs-Assistenten (F7) als zentraler Workflow.

### Lieferdokument `Docs/planung/mcp-integration/scope-und-toolkatalog.md` enthält

- Verbindliche Grundsätze: Tools bilden Geschäftsprozesse ab (keine
  generischen Datenbankoperationen, keine CRUD-APIs, kein DB-Direktzugriff –
  Nicht-Ziele explizit); jeder Tool ist einem Scope/einer Rechteart aus
  Schritt 1 zugeordnet.
- Fachlicher Katalog der benötigten Tools, gruppiert nach Geschäftsprozess:
  je Tool Zweck, Scope-Zuordnung, benötigte Berechtigungen,
  Sicherheitsklassifizierung (lesend / vorschlagend / schreibvorbereitend /
  genehmigungspflichtig), erwartete Ein- und Ausgaben (orientiert an den
  `FinanceManager.Shared`-DTOs) sowie Nebenbedingungen (Idempotenz, Limits).
- **Kontoauszugs-Assistent** (F7): vollständige fachliche Spezifikation des
  Workflows – Nutzung ausschließlich der vorhandenen Importmechanismen
  (keine Bankanbindung), Analyse von `StatementDraft`-Entwürfen,
  Vervollständigung fehlender Informationen (Kontakt-, Sparplan-,
  Wertpapier-Zuordnung), Dublettenerkennung, Verbuchungsvorschläge und
  Plausibilitätsprüfungen; Mapping der Teilfunktionen auf die vorhandene
  `IStatementDraftService`-Oberfläche (Get*/Validate für Analyse,
  Set*/Save*/Batch für Vervollständigung, Classify/AlreadyBooked für
  Dubletten, Book für Verbuchungsvorschläge) – der MCP-Layer wird als
  dünner Adapter konzipiert.
- Versionierungsstrategie des Toolkatalogs: fachliche Regeln für
  Tool-Benennung, Änderungsklassen (kompatibel/brechend), Hinzufügen/
  Entfernen von Tools und Kommunikation gegenüber MCP-Clients.
- Übersichtstabelle: Tool → Scope/Rechteart → Sicherheitsklasse →
  Freigabestufen-Mindestniveau → genehmigungspflichtig (ja/nein) als
  Eingabe für das Approval-Konzept.

## Akzeptanzkriterien

- Das Dokument liegt als deutschsprachige Markdown-Datei unter
  `Docs/planung/mcp-integration/` vor.
- Jedes gelistete Tool ist einem Geschäftsprozess, einem Scope/einer
  Rechteart (Begriffe aus Schritt 1) und einer Sicherheitsklasse zugeordnet;
  Ein-/Ausgaben sind auf DTO-Niveau beschrieben.
- Der Kontoauszugs-Assistent (F7) ist vollständig beschrieben und jede
  Teilfunktion ist auf die vorhandene Import-/Entwurfs-Pipeline
  (`IStatementDraftService` bzw. `IMassImportOrchestrator`) abgebildet;
  eine direkte Bankanbindung ist explizit ausgeschlossen.
- Die Versionierungsstrategie ist dokumentiert.
- Kein Tool beschreibt generische CRUD- oder DB-Zugriffe; für Buchungen
  ist Storno als einziger Rücknahmeweg modelliert.

## Rahmen

- Reine Dokumentationslieferung (Planungsaufgabe), keine Codeänderung.
- Verbindliche Kanten aus Schritt 1: „Buchung" nur auf Entwürfen;
  Buchungs-Löschung = Storno; `StatementImport`/`StatementEntry` nicht
  löschbar; `SavingsPlan`/`Security`-Löschung = Archivieren.
- Alle Code-Referenzen müssen der Inventory des Projektverzeichnisses
  (`inventory.md`, `inventory/entitaeten.md`) bzw. dem tatsächlichen
  Code-Stand entsprechen.
