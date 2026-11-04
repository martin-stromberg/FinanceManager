# Entwicklungsschritte – Projekt Umsetzung: MCP-Integration für FinanceManager

Branch: `task/mcp-integration`

| # | Titel | Abhängigkeiten | Branch | Status |
|---|-------|----------------|--------|--------|
| 1 | MCP-Fundament & Zugangsverwaltung (EP-01, #479) | Keine | `task/mcp-integration-schritt-1-fundament` | Fertig |
| 2 | Read-Only-Toolkatalog (EP-02, #480) | 1 | `task/mcp-integration-schritt-2-toolkatalog` | Fertig |
| 3 | Audit-Grundlage & Revisionssicherheit (EP-03, #481) | 1 | `task/mcp-integration-schritt-3-audit` | Fertig |
| 4 | Assistenz- & Vorschlags-Tools (EP-04, #482) | 2, 3 | `task/mcp-integration-schritt-4-assistenz` | Fertig |
| 5 | Individuelle Rechte & Filter (EP-05, #483) | 1 | `task/mcp-integration-schritt-5-rechte` | Fertig |
| 6 | Audit-Ansicht & Nutzungsprotokoll (EP-06, #484) | 3, 5 | `task/mcp-integration-schritt-6-audit-ui` | Fertig |
| 7 | Kontoauszugs-Assistent (EP-07, #485) | 2, 3 | `task/mcp-integration-schritt-7-kontoauszug` | Fertig |
| 8 | Stammdaten-Pflege (EP-08, #486) | 2, 3 | `task/mcp-integration-schritt-8-stammdaten` | Fertig |
| 9 | Genehmigungsworkflow & persistente Queue (EP-09, #487) | 1, 2, 3 | `task/mcp-integration-schritt-9-approval` | In Arbeit |
| 10 | Genehmigungscenter & finale Aktionen (EP-10, #488) | 8, 9 | `task/mcp-integration-schritt-10-center` | Offen |
| 11 | Agenten-Workflows & Betriebshärtung (EP-11, #489) | 9, 10 | `task/mcp-integration-schritt-11-agenten` | Offen |

Statuswerte: Offen | In Arbeit | Fertig | Blockiert

Wiederaufbau-Notiz (2026-11-04): Erster Clone wurde von der Anwendung
bereinigt; Neuaufbau in `_scratch/mcp-umsetzung`. Jeder Commit wird sofort
gepusht. Plan zuvor als `vollständig` geprüft (Hinweise eingearbeitet:
47 Features, Enum-Coverage S1, Dep 3 in S7).
