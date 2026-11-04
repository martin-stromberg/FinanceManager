# Anforderung – Schritt 1: Fachkonzept und Berechtigungsmodell

Quelle: `Docs/projects/task/issue-470-f07c97fb9a7e4e3a9d481f301dfa28ad-planung-mcp-integration-fuer-f/project-plan.md`, Abschnitt „### Schritt 1: Fachkonzept und Berechtigungsmodell" (Projekt: Planung MCP-Integration für FinanceManager, Issue #470).

## Schrittbeschreibung (aus project-plan.md, verbindlich)

Konzeption der fachlichen Grundlage der MCP-Integration: Zielbild, Akteure und
Szenarien (Benutzer, externe KI-Systeme/MCP-Clients), Verwaltung benutzerbezogener
MCP-Zugänge sowie das vollständige Rechtemodell, auf dem alle späteren Konzepte
aufsetzen.

### Fachkonzept (`Docs/planung/mcp-integration/fachkonzept.md`) enthält

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
  Datentypen der Anforderung gegen die Rechtearten Leserecht, Vorschlagsrecht,
  Vervollständigungsrecht, Änderungsrecht, Erstellungsrecht, Buchungsrecht,
  Löschrecht – jeweils mit Mapping auf die realen Entitäten/Enums des Repos.
- **Filterkonzept** (F4): optionale Einschränkung von Rechten auf Basis
  vorhandener Gruppierungsstrukturen; Standard = Rechte gelten für alle
  Datensätze eines Datentyps; Anknüpfung an das CSV-Filtermuster von
  `ReportFavorite`.

### Berechtigungsmodell (`Docs/planung/mcp-integration/berechtigungsmodell.md`) enthält

- Scope-Modell und Aktionsmodell (F2): Datentyp × Rechteart (× optionaler Filter).
- Rechtevererbung: Standardrechte je Freigabestufe → individuelle
  Rechteanpassung je Zugang; Konfliktverhalten bei überlappenden Regeln.
- Verbindlicher Grundsatz: MCP-Rechte dürfen bestehende Benutzerrechte nur
  einschränken, niemals erweitern – inkl. `AccountShare`-Fremdfreigaben
  (Read/Write/Admin) als Obergrenze.
- Rechteableitung für Entitäten ohne eigenen Owner: `Posting`,
  `StatementImport`/`StatementEntry`, `AccountLinkedIban`, `AliasName`,
  `SecurityPrice`.
- Semantik der Rechtearten im FinanceManager-Kontext („Buchungsrecht" =
  Verbuchung von Entwürfen; „Löschrecht" für Buchungen = Storno).

## Akzeptanzkriterien

- Beide Dokumente liegen als deutschsprachige Markdown-Dateien unter
  `Docs/planung/mcp-integration/` vor.
- Die F3-Matrix enthält alle 18 Datentypen × 7 Rechtearten und mappt jeden
  Anforderungsbegriff auf die konkrete Entität/Enum des Repos.
- Das Berechtigungsmodell beschreibt Scope- und Aktionsmodell, Vererbung,
  Konfliktverhalten sowie Standard- und individuelle Rechte vollständig (F2)
  und operationalisiert „nur einschränken, niemals erweitern" inkl.
  `AccountShare`-Bezug.
- Für jede Entität ohne eigenes `OwnerUserId` ist die Rechteableitung
  dokumentiert.
- Die fünf Freigabestufen sind je Stufe mit erlaubten Aktionen beschrieben (F5);
  Filterkonzept (F4) und MCP-Zugangsverwaltung (F1) sind vollständig spezifiziert.

## Rahmen

- Reine Dokumentationslieferung (Planungsaufgabe), keine Codeänderung.
- Alle Code-Referenzen müssen der Inventory des Projektverzeichnisses
  (`inventory.md`, `inventory/entitaeten.md`) entsprechen.
