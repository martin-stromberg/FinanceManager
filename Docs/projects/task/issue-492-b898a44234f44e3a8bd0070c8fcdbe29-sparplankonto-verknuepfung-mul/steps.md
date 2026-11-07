# Entwicklungsschritte – Projekt Sparplan↔Konto-Verknüpfung

Branch: `task/issue-492-b898a44234f44e3a8bd0070c8fcdbe29-sparplankonto-verknuepfung-mul`

| # | Titel | Abhängigkeiten | Branch | Status |
|---|-------|----------------|--------|--------|
| 1 | Persistenz und fachliche Verwaltung der Sparplan↔Konto-Verknüpfung | Keine | `task/issue-492-b898a44234f44e3a8bd0070c8fcdbe29-sparplankonto-verknuepfung-mul-schritt-1-persistenz` | Fertig |
| 2 | Konto-basierte Ist-Betrachtung in der Plananalyse | 1 | `task/issue-492-b898a44234f44e3a8bd0070c8fcdbe29-sparplankonto-verknuepfung-mul-schritt-2-analyse` | Offen |
| 3 | REST-API und ApiClient für die Kontoverknüpfung | 1, 2 | `task/issue-492-b898a44234f44e3a8bd0070c8fcdbe29-sparplankonto-verknuepfung-mul-schritt-3-api` | Offen |
| 4 | „informal"-Kennzeichnung in der Postings-Ausgabe (API und MCP) | 2, 3 | `task/issue-492-b898a44234f44e3a8bd0070c8fcdbe29-sparplankonto-verknuepfung-mul-schritt-4-informal` | Offen |
| 5 | Istmeldung – Korrekturbuchung ohne Kontobewegung | 1, 3 | `task/issue-492-b898a44234f44e3a8bd0070c8fcdbe29-sparplankonto-verknuepfung-mul-schritt-5-istmeldung` | Offen |
| 6 | MCP-Schreiboperationen für Verknüpfung und Istmeldung | 3, 5 | `task/issue-492-b898a44234f44e3a8bd0070c8fcdbe29-sparplankonto-verknuepfung-mul-schritt-6-mcp` | Offen |
| 7 | Web-UI – Kontoverknüpfung verwalten und Kennzeichnungen anzeigen | 3, 4, 5 | `task/issue-492-b898a44234f44e3a8bd0070c8fcdbe29-sparplankonto-verknuepfung-mul-schritt-7-ui` | Offen |
| 8 | Web-UI – Aktion „Istmeldung" | 5, 7 | `task/issue-492-b898a44234f44e3a8bd0070c8fcdbe29-sparplankonto-verknuepfung-mul-schritt-8-istmeldung-ui` | Offen |
| 9 | Backup und Restore der neuen Daten | 1, 5 | `task/issue-492-b898a44234f44e3a8bd0070c8fcdbe29-sparplankonto-verknuepfung-mul-schritt-9-backup` | Offen |
| 10 | Regressionsabsicherung der Budgetauswertung | 2, 4, 5 | `task/issue-492-b898a44234f44e3a8bd0070c8fcdbe29-sparplankonto-verknuepfung-mul-schritt-10-budget` | Offen |
