# Planprüfung – Projektplan Umsetzung beider Design-Varianten (Issue #590)

Geprüft: `project-plan.md` (2 Entwicklungsschritte) gegen `requirement.md`,
`inventory.md`, `Docs/design/proposals/README.md` und `Docs/design/inventory/`.

## Ergebnis

**Status:** Projektplan lückenhaft

Der Plan ist in Substanz tragfähig (zwei unabhängige, exklusive Umsetzungs-Branches,
vollständige Varianten-Umsetzung, E2E + Doku je Schritt, PR gegen `staging` statt
Merge — fachlich korrekt begründet). Zwei Muss-Punkte aus der Anforderung sind
jedoch nicht ausdrücklich im Plan verankert (siehe unten), daher formale
Feststellung „lückenhaft" mit kleinen, konkret nachbesserbaren Punkten.

## Abgleich Anforderung ↔ Entwicklungsschritte

| # | Anforderung (requirement.md) | Abgedeckt durch | Abdeckung |
|---|------------------------------|-----------------|-----------|
| 1 | Zwei Branches, je ein PR gegen `staging` (Ziel, AK, F6) | Grobe Vorgehensentscheidungen („Pro Variante ein eigener Umsetzungs-Branch", „Schritt-Abschluss = PR gegen `staging`"), Schritt 1 + 2 (Push + PR durch Koordinator), `steps.md` (Branch-Namen) | ✅ |
| 2 | Beide Varianten vollständig: Shell, Aktionsleiste, alle Listen-/Karten-/Report-/Overlay-Ansichten (F3) | Schritt 1 (Sidebar, Ribbon, Tabellen/Karten, „komplette Oberfläche", verbindliche Vorlagen `proposals/variantA/` + `inventory/`); Schritt 2 (Icon-Rail, Top-Action-Bar, Karten, `proposals/variantB/`) | ✅ |
| 3 | Keine neuen Features, keine erfundenen Elemente (F1) | Beide Schritte explizit („Keine neuen Features, keine erfundenen Elemente"); Verweis auf `proposals/README.md` (entfernte erfundene Features) | ✅ |
| 4 | Funktionale Gleichwertigkeit, prüfbar mit produktivem Backup (F2, AK) | Schritt 1 explizit („volle Funktionalität mit produktivem Backup"); Schritt 2 über „dieselben … Modellvorgaben wie Schritt 1" mitvererbt | ✅ (Schritt 2 nur implizit) |
| 5 | E2E-Tests vollständig je Branch inkl. Abnahme-Screenshots (F4, AK) | Beide Schritte („E2E-Tests laufen vollständig inkl. Abnahme-Screenshots") | ✅ |
| 6 | Dokumentation je Branch angepasst (`Docs/help/`, README/Release Notes) (F5) | Beide Schritte („Dokumentation wird an das neue Design angepasst"); betroffene Bereiche nennen `Docs/help/`, `README.md`, `docs/RELEASE_NOTES.md` (Pfad existiert) | ✅ |
| 7 | PRs mit Closing-Bezug zu Issue #590 (F6) | Nur „PR gegen `staging`" genannt — das Closing-Keyword (`Closes #590`) für PR-Beschreibung/Commits ist nirgends gefordert | ⚠️ **teilweise** |
| 8 | Build + Unit-/Integrations-Tests grün (AK) | Schritte nennen nur E2E; Unit/Integration+Build nicht als Akzeptanzkriterium benannt — indirekt über „vollständigen lifecycle-Skill" abgedeckt | ⚠️ implizit |
| 9 | UI-only: kein Domain-/Application-/Infrastructure-Eingriff, keine Migrationen (Nicht-Ziel) | Übersicht („Keine Änderungen an Domain/Application/Infrastructure"), beide Schritte („UI-only") | ✅ |
| 10 | Kein Merge der Varianten untereinander / kein Merge nach `staging` durch Agenten (Nicht-Ziel) | Vorgehensentscheidungen begründen PR-statt-Merge; Genehmigung durch Anwender | ✅ |
| 11 | Basierung auf gemeinsamem Design-Basisbranch (Abhängigkeiten) | Vorgehensentscheidungen („basierend auf dem gemeinsamen Design-Branch = Projekt-Basisbranch"); deckt sich mit `steps.md` | ✅ |

## Abhängigkeitsprüfung

- Schritt 1: Abhängigkeiten „Keine" — korrekt (basieren beide auf dem
  Basisbranch-Snapshot, nicht aufeinander).
- Schritt 2: Abhängigkeiten „Keine" — korrekt. Der Text-Verweis „gleiche echten
  Aktionen je Ansicht wie in A … dieselben Inventar- und Modellvorgaben wie
  Schritt 1" ist eine Referenz auf die **gemeinsame Spezifikation**
  (`Docs/design/inventory/`, `proposals/README.md`), keine Ausführungsabhängigkeit
  von Schritt 1 → keine versteckte Sequenz.
- Zyklen: keine. Parallele Umsetzung in getrennten Worktrees ist konsistent mit
  „keine Abhängigkeiten" und der Exklusivität der Varianten.
- Workflow-Abweichung (PR statt Merge in den Basisbranch) ist im Plan und in
  `steps.md` dokumentiert und fachlich zwingend (dieselben UI-Dateien) — korrekt.

## Fehlende oder unvollständige Punkte

1. **Closing-Bezug zu Issue #590 fehlt (Muss, F6).** Die Anforderung verlangt PRs
   „mit Closing-Bezug zu Issue #590" (globale Regel: `Closes #590`/`Fixes #590`,
   `(Issue #590)` allein schließt nichts). Der Plan fordert nur „PR gegen
   `staging`" — weder in den Schritten noch in den Vorgehensentscheidungen ist
   das Closing-Keyword für PR-Beschreibung/Abschlusscommit benannt.
   *Nachbesserung:* In beiden Schritten bzw. den Vorgehensentscheidungen
   ergänzen, dass PR-Body und Abschlusscommit `Closes #590` enthalten.
2. **Akzeptanzkriterium „Build + Unit-/Integrationstests grün" nicht in den
   Schritten benannt.** Die Schritte fordern nur „E2E-Tests vollständig". Als
   eigenständige Lifecycle-Eingabe sollten die testbaren Akzeptanzkriterien
   vollständig sein: Build 0 Fehler, `FinanceManager.Tests` +
   `FinanceManager.Tests.Integration` grün, E2E ohne neue Fehlschläge gegen die
   dokumentierte Baseline (8 preexisting-Fehlschläge, `inventory/tests.md`).
   *Nachbesserung:* Akzeptanzkriterien-Zeile je Schritt ergänzen (oder einmalig
   in den Vorgehensentscheidungen als gemeinsames Akzeptanzkriterium).

## Hinweise

- Schritt 2 erbt fachliche Randbedingungen (produktives Backup, Startseiten-Modell
  `HomeKpiGrid`/`ReportFavorite`, „keine neuen Features") über den Verweis auf
  Schritt 1 — verständlich, aber für eine strikt eigenständige Lifecycle-Eingabe
  könnten die 2–3 Kernbedingungen direkt wiederholt werden.
- E2E-Abnahmekriterium könnte konkretisiert werden: „keine neuen Fehlschläge
  gegenüber den 8 Baseline-Fehlschlägen" (aus dem geprüften Entwurfsphasen-Plan
  `Docs/features/task-issue-590-.../plan.md`, Phase 4) statt nur „läuft
  vollständig" — sonst ist unklar, ob die preexisting-Fehlschläge als Blocker
  gelten.
- `docs/RELEASE_NOTES.md` existiert (Groß-/Kleinschreibung egal unter Windows,
  kanonisch `Docs/RELEASE_NOTES.md`).
- Kein weiterer Handlungsbedarf: keine erfundenen Features im Plan, Nicht-Ziele
  (kein Varianten-Merge, kein Domain-/Infra-Eingriff) respektiert, offene Punkte
  korrekt mit „Keine" beantwortet.
