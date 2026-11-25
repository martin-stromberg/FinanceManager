# Planprüfung – Projektplan Umsetzung beider Design-Varianten (Issue #590)

Geprüft: `project-plan.md` (2 Entwicklungsschritte, nachgebesserte Fassung) gegen
`requirement.md`, `inventory.md`, `Docs/design/proposals/README.md` und
`Docs/design/inventory/`. Vorbefund siehe `project-plan-check.1.md`.

## Ergebnis

**Status:** Projektplan vollständig

Beide Muss-Lücken aus dem Vorbefund sind geschlossen: `Closes #590` ist in beiden
Schritten für PR-Body und Abschlusscommit benannt, und die Akzeptanzkriterien
(Build, Unit/Integration, E2E gegen Baseline `inventory/tests.md`,
Doku-Anpassung) sind je Schritt vollständig ausformuliert. Schritt 2 ist nun
eigenständig vollständig beschrieben.

## Abgleich Anforderung ↔ Entwicklungsschritte

| # | Anforderung (requirement.md) | Abgedeckt durch | Abdeckung |
|---|------------------------------|-----------------|-----------|
| 1 | Zwei Branches, je ein PR gegen `staging` (Ziel, AK, F6) | Vorgehensentscheidungen („Pro Variante ein eigener Umsetzungs-Branch", „Schritt-Abschluss = PR gegen `staging`"), Abschluss beider Schritte, `steps.md` (Branch-Namen) | ✅ |
| 2 | Beide Varianten vollständig: Shell, Aktionsleiste, alle Listen-/Karten-/Report-/Overlay-Ansichten (F3) | Schritt 1 (Sidebar, Ribbon, Tabellen/Karten, `proposals/variantA/` + `inventory/`); Schritt 2 (Icon-Rail, Top-Action-Bar, Karten, `proposals/variantB/` + `inventory/`) | ✅ |
| 3 | Keine neuen Features, keine erfundenen Elemente (F1) | Beide Schritte explizit; Verweis auf `proposals/README.md` (Umbauspezifikation inkl. entfernter erfundener Features) | ✅ |
| 4 | Funktionale Gleichwertigkeit, prüfbar mit produktivem Backup (F2, AK) | Beide Schritte explizit („volle Funktionalität mit produktivem Backup, kein Datenmodell-/Migrations-Eingriff") | ✅ |
| 5 | E2E-Tests vollständig je Branch inkl. Abnahme-Screenshots (F4, AK) | Akzeptanzkriterien beider Schritte: vollständiger E2E-Lauf, erzeugte + sichtgeprüfte Abnahme-Screenshots, Bewertung gegen Baseline `inventory/tests.md` | ✅ |
| 6 | Dokumentation je Branch angepasst (`Docs/help/`, README/Release Notes) (F5) | Akzeptanzkriterien beider Schritte + betroffene Bereiche (`Docs/help/`, `README.md`, `docs/RELEASE_NOTES.md`) | ✅ |
| 7 | PRs mit Closing-Bezug zu Issue #590 (F6) | Abschluss beider Schritte: „PR gegen `staging` mit Closing-Bezug (`Closes #590` im PR-Body, Verknüpfung im Abschlusscommit)" | ✅ |
| 8 | Build + Unit-/Integrations-Tests grün (AK) | Akzeptanzkriterien beider Schritte: „`dotnet build` fehlerfrei; Unit- und Integrationstests grün" | ✅ |
| 9 | UI-only: kein Domain-/Application-/Infrastructure-Eingriff, keine Migrationen (Nicht-Ziel) | Übersicht + beide Schritte („UI-only … kein Datenmodell-/Migrations-Eingriff") | ✅ |
| 10 | Kein Merge der Varianten untereinander / kein Merge nach `staging` durch Agenten (Nicht-Ziel) | Vorgehensentscheidungen begründen PR-statt-Merge; Genehmigung durch Anwender | ✅ |
| 11 | Basierung auf gemeinsamem Design-Basisbranch (Abhängigkeiten) | Vorgehensentscheidungen; deckt sich mit `steps.md` | ✅ |

## Abhängigkeitsprüfung

- Schritt 1 und Schritt 2: Abhängigkeiten jeweils „Keine" — korrekt; beide
  basieren auf demselben Basissnapshot des Projekt-Basisbranchs, nicht
  aufeinander.
- Der Text in Schritt 2 („gleiche echten Aktionen je Ansicht wie in A, aus dem
  Inventar") referenziert die gemeinsame Spezifikation
  (`Docs/design/inventory/`), keine Ausführungsabhängigkeit → keine versteckte
  Sequenz. Schritt 2 ist inhaltlich eigenständig vollständig (Vorlagen,
  Startseiten-Modell, Randbedingungen, Akzeptanzkriterien alle selbst benannt).
- Zyklen: keine. Parallele Umsetzung in getrennten Worktrees ist konsistent.
- Workflow-Abweichung (PR statt Merge in Basisbranch) ist dokumentiert und
  fachlich zwingend (dieselben UI-Dateien würden sich gegenseitig zerstören).

## Fehlende oder unvollständige Punkte

Keine — alle Muss-Anforderungen sind vollständig und testbar abgedeckt, die
Schritte sind als eigenständige Lifecycle-Eingaben tauglich (verbindliche
Vorlagen, Randbedingungen, Akzeptanzkriterien, Abschlussdefinition benannt).

## Hinweise

- Der Baseline-Verweis `inventory/tests.md` in den Akzeptanzkriterien löst sich
  auf `Docs/features/task-issue-590-c1454f4ac23246f98840e1a8c7175f1e-design-ueberarbeiten/inventory/tests.md`
  (Entwurfsphasen-Inventar, 8 dokumentierte preexisting-Fehlschläge) — nicht auf
  `Docs/design/inventory/` (enthält kein `tests.md`). Für den Lifecycle-Schritt
  ggf. den vollständigen Pfad angeben, damit der ausführende Agent die Baseline
  eindeutig findet.
- Bei PRs gegen `staging` schließt `Closes #590` das Issue erst bei Promotion
  nach `main` — gewünscht und konsistent mit dem Staging-Workflow.
- Hinweis aus Vorbefund obsolet: „Schritt 2 erbt Randbedingungen per Verweis" —
  Schritt 2 beschreibt jetzt alle Bedingungen selbst.
