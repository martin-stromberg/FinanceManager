# Anforderung – Schritt 5: Roadmap, Aufwandsschätzung und Product-Backlog

Quelle: `Docs/projects/task/issue-470-f07c97fb9a7e4e3a9d481f301dfa28ad-planung-mcp-integration-fuer-f/project-plan.md`, Abschnitt „### Schritt 5: Roadmap, Aufwandsschätzung und Product-Backlog" (Projekt: Planung MCP-Integration für FinanceManager, Issue #470). Fachliche Anforderungen: `requirement.md` des Projekts, F13 + Artefakte 10–12.

## Schrittbeschreibung (aus project-plan.md, verbindlich)

Konsolidierung aller Konzeptdokumente zu einer priorisierten
Umsetzungsplanung (F13). Lieferung deutschsprachiger Markdown-Dokumente
unter `Docs/planung/mcp-integration/` – keine Implementierung.

### `roadmap.md` (Artefakt 10, F13)

- Die fünf vorgegebenen Phasen: 1. Read-Only Analyse, 2. Assistenz- und
  Vorschlagsfunktionen, 3. Kontoauszugs-Assistent,
  4. Genehmigungspflichtige Aktionen, 5. Agentengestützte Workflows.
- Pro Phase: Funktionsumfang (welche Tools/Konzeptteile aus den
  Schritten 1–4 enthalten sind), Sicherheitsrisiken und deren
  Mitigation, technische Voraussetzungen (inkrementelle Ausbaustufen:
  welche Entitäten/UI-/Architekturteile je Phase nötig sind),
  Erfolgskriterien.
- Priorisierungsbegründung und Phasen-Abhängigkeiten.

### `aufwandsschaetzung.md` (Artefakt 11)

- Schätzung je Phase bzw. Epic mit transparenter Methodik
  (Personentage), begründeten Annahmen und Unsicherheiten;
  Aufschlüsselung nach Bereichen (Backend/Domain, Sicherheitslayer, UI,
  Migration, Test).

### `product-backlog.md` (Artefakt 12)

- Epics entlang der Phasen und Querschnitte, darunter priorisierte
  Features mit kurzer Beschreibung und Akzeptanzkriterien-Skizze;
  eindeutiger Bezug jedes Features zu Phase und Konzeptdokument; keine
  Implementierungsdetails.

### `README.md`

- Index aller Dokumente in `Docs/planung/mcp-integration/`
  (Übersicht/Navigation), Verlinkung aller zwölf Ergebnisdokumente.

## Akzeptanzkriterien (aus project-plan.md)

- Alle Dokumente liegen als deutschsprachige Markdown-Dateien unter
  `Docs/planung/mcp-integration/` vor.
- Die Roadmap enthält exakt die fünf vorgegebenen Phasen mit
  Funktionsumfang, Sicherheitsrisiken, technischen Voraussetzungen und
  Erfolgskriterien je Phase (F13).
- Die Aufwandsschätzung deckt alle Phasen/Epics ab und macht Methodik
  und Annahmen transparent.
- Das Product-Backlog enthält Epics und priorisierte Features mit Bezug
  zu Phase und Konzeptdokument; alle Konzeptinhalte aus Schritten 1–4
  sind im Backlog abbildbar (kein Konzept ohne Umsetzungsfeature).
- `README.md` verlinkt alle zwölf Ergebnisdokumente.

## Abhängigkeiten

Schritte 1–4 liegen vor und wurden verbindlich übernommen:
`fachkonzept.md`, `berechtigungsmodell.md`, `scope-und-toolkatalog.md`,
`sicherheitskonzept.md`, `approval-konzept.md`, `audit-konzept.md`,
`ui-konzept.md`, `architekturkonzept.md`,
`datenmodell-erweiterungen.md`.
