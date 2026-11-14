# Planung: MCP-Integration für FinanceManager (Issue #470) – Dokumentenindex

**Status:** Konzeptdokumente vollständig (Planungsartefakte, keine
Implementierung)

Dieser Ordner enthält den vollständigen Konzeptsatz zur Integration
eines benutzerbezogenen MCP-Servers (Model Context Protocol) in
FinanceManager. Benutzer sollen externe KI-Systeme (ChatGPT, Claude,
Copilot, lokale LLMs) über Personal Access Tokens, granulare
Freigabestufen und einen Genehmigungsworkflow sicher und
nachvollziehbar auf ihre Finanzdaten zugreifen lassen können.

Die zwölf geforderten Ergebnisartefakte der Anforderung
(`Docs/projects/task/issue-470-…/requirement.md`, F1–F13) liegen
vollständig vor. Zentrale Festlegungen im Überblick:

- **Architektur:** in-process in `FinanceManager.Web` unter `/mcp`,
  Transport Streamable HTTP, dünne Tool-Adapter auf denselben
  `I*Service`-Contracts wie die BFF-API – kein eigener fachlicher Pfad.
- **Sicherheit:** PAT `fm_mcp_*` (pbkdf2-Hash, Einmalanzeige), Scope-
  Tupel (Datentyp × Rechteart × Filter), Prüfstufen 0–3,
  `AccountShare`-Deckel, Rate-Limits, Notfallschalter `Mcp:Enabled` /
  `Mcp:WriteAccess:Enabled`.
- **Wirkungskontrolle:** 33 Tools T01–T33 in vier Sicherheitsklassen;
  finale Wirkungen nur über `AiActionRequest` (Genehmigungscenter);
  revisionssichere Audit-Kette mit Hash-Verkettung.
- **Nicht-Ziele:** kein DB-Direktzugriff, keine Bankanbindung, keine
  generischen CRUD-APIs, keine Vollautonomie.

## Artefakte (Nummerierung = Anforderung)

|| # | Artefakt | Datei | Kerninhalt |
|---|---|---|---|---|
|| 1 | Fachkonzept | [fachkonzept.md](fachkonzept.md) | Zielbild, Akteure, Szenarien, Rechtearten, Freigabestufen, MCP-Zugänge (F1), Datentypen-Matrix (F3), Filter (F4) |
|| 2 | Sicherheitskonzept | [sicherheitskonzept.md](sicherheitskonzept.md) | PAT-Auth, Hashing, Prüfstufen-Durchsetzung, Prompt-Injection-Schutz, Notfallmaßnahmen, Rate Limiting, HTTP-LAN-Risiko (F8) |
|| 3 | Berechtigungsmodell | [berechtigungsmodell.md](berechtigungsmodell.md) | Scope-/Aktionsmodell, Prüfstufen 0–3, Vererbung, Konfliktverhalten, „nur einschränken", Rechteableitung (F2) |
|| 4 | Scope- und Toolkatalog | [scope-und-toolkatalog.md](scope-und-toolkatalog.md) | 33 Tools T01–T33, 4 Sicherheitsklassen, Kontoauszugs-Assistent (F7), Versionierungsstrategie (F6) |
|| 5 | UI-Konzept | [ui-konzept.md](ui-konzept.md) | Zugangs-/Token-/Rechte-/Filterverwaltung, Audit-Ansicht, Genehmigungscenter, API-Skizze, UX-Leitlinien (F11) |
|| 6 | Architekturkonzept | [architekturkonzept.md](architekturkonzept.md) | In-process `/mcp`, Streamable HTTP, 10-Stufen-Pipeline, Registry/Dispatcher/Evaluator/Gate/Executor, Betrieb (F12) |
|| 7 | Datenmodell-Erweiterungen | [datenmodell-erweiterungen.md](datenmodell-erweiterungen.md) | `McpAccess`, `McpAccessScope`, `AiActionRequest`, `McpAuditEntry(+Change)`, Enums, Indizes, Migration, Retention, Backup |
|| 8 | Approval-Konzept | [approval-konzept.md](approval-konzept.md) | `AiActionRequest` (6 Zustände), genehmigungspflichtige Aktionen, persistente Queue, Idempotenz, Fehlerbehandlung (F10) |
|| 9 | Audit-Konzept | [audit-konzept.md](audit-konzept.md) | 5 Ereigniskategorien, Detailtiefe, Retention 12/36 Monate, Hash-Verkettung, Auswertbarkeit (F9) |
|| 10 | Priorisierte Umsetzungsroadmap | [roadmap.md](roadmap.md) | 5 Phasen (Read-Only → Assistenz → Kontoauszugs-Assistent → Genehmigungen → Agenten) mit Umfang, Risiken, Voraussetzungen, Erfolgskriterien (F13) |
|| 11 | Aufwandsschätzung | [aufwandsschaetzung.md](aufwandsschaetzung.md) | Baustein- und Phasenschätzung in Personentagen, Methodik, Annahmen, Risikozuschläge |
|| 12 | Product-Backlog | [product-backlog.md](product-backlog.md) | 11 Epics nach Phasen, priorisierte Features mit AK-Ansatz, Schätzung und Tool-/Doku-Verweisen |

## Empfohlene Lesereihenfolge

- **Fachlich/Produkt:** 1 → 4 → 8 → 10 → 12
  (Zielbild und Rechte → Was kann die KI → Wie wird entschieden →
  Wann kommt was → Umsetzungs-Features)
- **Sicherheitsreview:** 3 → 2 → 9 → 8 → 7
  (Rechtemodell → Durchsetzung → Nachweis → Genehmigung → Persistenz)
- **Technisch/Architektur:** 6 → 7 → 4 → 5 → 11
  (Zielarchitektur → Datenmodell → Vertragsfläche → Oberflächen → Aufwand)
- **Management/Planung:** 10 → 11 → 12 (direkt)

## Herkunft

Erarbeitet im Projekt „Planung: MCP-Integration für FinanceManager"
(Issue #470); Projektplan und Anforderung unter
`Docs/projects/task/issue-470-f07c97fb9a7e4e3a9d481f301dfa28ad-planung-mcp-integration-fuer-f/`.
Die Dokumente bilden die Grundlage für die spätere technische Umsetzung;
Katalog- und Konzeptversionierung regeln Änderungen (`katalog-v<n>`).
