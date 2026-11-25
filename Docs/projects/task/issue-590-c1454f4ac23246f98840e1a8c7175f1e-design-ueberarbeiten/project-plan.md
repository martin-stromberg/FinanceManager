# Projektplan – Umsetzung beider Design-Varianten (Issue #590)

## Übersicht

**Projekttitel:** Design-Umsetzung Variante A und Variante B als alternative PRs

**Gesamtziel:** Die beiden freigegebenen UI-Entwürfe unter
`Docs/design/proposals/` werden als zwei vollständige, funktional
gleichwertige, sich gegenseitig ausschließende Implementierungen der
FinanceManager-Oberfläche umgesetzt. Ergebnis: zwei Branches, je ein Pull
Request gegen `staging`; der Anwender genehmigt einen davon.

**Betroffene Bereiche:** `FinanceManager.Web` (Layout, Pages, Styles, resx),
`FinanceManager.Tests.E2E`, `Docs/help/`, `README.md`, `docs/RELEASE_NOTES.md`.
Keine Änderungen an Domain/Application/Infrastructure.

## Grobe Vorgehensentscheidungen

- **Pro Variante ein eigener Umsetzungs-Branch**, basierend auf dem
  Projekt-Basisbranch (Design-Branch mit Inventar + Entwürfen als verbindlicher
  Spezifikation). Die Varianten werden nicht gemergt — sie sind Alternativen.
- **Jeder Schritt durchläuft den vollständigen lifecycle-Skill** (Anforderung,
  Bestandsaufnahme, Planung, Implementierung, Reviews, Tests, Dokumentation,
  Abschlusscommit) auf seinem Branch.
- **Schritt-Abschluss = PR gegen `staging`** statt Merge in den Basisbranch:
  Ein Merge würde die alternative Variante zerstören (dieselben UI-Dateien).
  Abweichung vom Standard-Workflow, fachlich begründet durch die Aufgabe
  („für jeden Branch einen PR gegen staging").
- **Parallele Umsetzung in getrennten Git-Worktrees**, da beide Schritte
  unabhängige, auf demselben Basissnapshot basierende Alternativen sind.
- **E2E + Doku sind Bestandteil jedes Schritts** (Lifecycle-Schritte 8b/9),
  keine separaten Projekt-Schritte.

## Entwicklungsschritte

### Schritt 1: Design-Variante A umsetzen („Modernisierte Weiterentwicklung")

Die komplette FinanceManager-Oberfläche wird im Stil der freigegebenen
Variante A umgesetzt: breite Sidebar mit Labels und fixer 13er-Navigation
(Start, Konten, Kontakte, Sparpläne, Wertpapiere, Kontoauszüge, Budget,
Berichte, Benutzerverwaltung, KI-Zugänge, Einstellungen, Hilfe, Abmelden),
einheitliches FM-Logo, klassische Ribbon-Aktionsleiste mit den pro Ansicht im
Inventar definierten echten Gruppen und Aktionen, ruhige Datentabellen und
Karten. Dark-Theme `#0b0e13`/`#10131a`, Indigo-Akzent `#6d8dff`,
Inter-Typografie. Verbindliche Vorlage: `Docs/design/proposals/variantA/`
(HTML + PNG je Ansicht), `Docs/design/proposals/README.md` (Umbauspezifikation)
und `Docs/design/inventory/` (echte Strukturen/Aktionen). Startseite mit
Mitteilungen, konfigurierbaren KPI-Kacheln („Kachel hinzufügen") und
Berichts-Favoriten nach echtem Modell (`HomeKpiGrid`, `ReportFavorite`).
Keine neuen Features, keine erfundenen Elemente; UI-only, volle Funktionalität
mit produktivem Backup (kein Datenmodell-/Migrations-Eingriff).
Akzeptanzkriterien: `dotnet build` fehlerfrei; Unit- und Integrationstests
grün; E2E-Tests vollständig ausgeführt inkl. erzeugter und sichtgeprüfter
Abnahme-Screenshots — bewertet gegen die dokumentierte E2E-Baseline
(`Docs/features/task-issue-590-c1454f4ac23246f98840e1a8c7175f1e-design-ueberarbeiten/inventory/tests.md`, bekannte preexisting-Fehlschläge); Dokumentation
(`Docs/help/`, README/Release Notes) an das neue Design angepasst.
Abschluss: Push des Schritt-Branches; der Projekt-Koordinator erstellt den PR
gegen `staging` mit Closing-Bezug (`Closes #590` im PR-Body, Verknüpfung im
Abschlusscommit).

### Schritt 2: Design-Variante B umsetzen („Vollständig neu gedacht")

Die komplette FinanceManager-Oberfläche wird im Stil der freigegebenen
Variante B umgesetzt: schlanke Icon-Rail mit identischer 13er-Navigation,
kontextuelle Top-Action-Bar (gleiche echten Aktionen je Ansicht wie in A,
aus dem Inventar), kartenbasierte Inhalte auf geschichtetem Dark-Background,
Glas-Flächen, Teal/Violett-Akzente (`#2dd4bf`/`#8b5cf6`), einheitliches
FM-Logo. Verbindliche Vorlage: `Docs/design/proposals/variantB/` (HTML + PNG
je Ansicht), `Docs/design/proposals/README.md` (Umbauspezifikation) und
`Docs/design/inventory/` (echte Strukturen/Aktionen). Startseite mit
Mitteilungen, konfigurierbaren KPI-Kacheln („Kachel hinzufügen") und
Berichts-Favoriten nach echtem Modell (`HomeKpiGrid`, `ReportFavorite`).
Keine neuen Features, keine erfundenen Elemente; UI-only, volle Funktionalität
mit produktivem Backup (kein Datenmodell-/Migrations-Eingriff).
Akzeptanzkriterien: `dotnet build` fehlerfrei; Unit- und Integrationstests
grün; E2E-Tests vollständig ausgeführt inkl. erzeugter und sichtgeprüfter
Abnahme-Screenshots — bewertet gegen die dokumentierte E2E-Baseline
(`Docs/features/task-issue-590-c1454f4ac23246f98840e1a8c7175f1e-design-ueberarbeiten/inventory/tests.md`, bekannte preexisting-Fehlschläge); Dokumentation
(`Docs/help/`, README/Release Notes) an das neue Design angepasst.
Abschluss: Push des Schritt-Branches; der Projekt-Koordinator erstellt den PR
gegen `staging` mit Closing-Bezug (`Closes #590` im PR-Body, Verknüpfung im
Abschlusscommit).

## Übersichtstabelle

| # | Titel | Abhängigkeiten | Betroffene Bereiche |
|---|-------|----------------|---------------------|
| 1 | Design-Variante A umsetzen | Keine | Web-UI, E2E, Doku |
| 2 | Design-Variante B umsetzen | Keine | Web-UI, E2E, Doku |

## Offene Punkte

Keine.
