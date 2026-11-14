# README-Prüfung (Schritt 12b) – Issue #498

**Ergebnis: Keine Änderung an `README.md` erforderlich.**

## Begründung

Der Change ist eine rein interne Sortierkorrektur: In drei Card-ViewModels
(`SavingsPlanCardViewModel`, `BankAccountCardViewModel`, `PostingsCardViewModel`)
wurde der `UiRibbonTab` „Verknüpfte Informationen" (`Ribbon_Group_Linked`) ans Ende
der Tab-Liste verschoben. Es wurden **keine** neuen Funktionen, Aktionen, Labels,
Icons, API-Endpunkte, Konfigurationsparameter, Entitäten oder Setup-Schritte eingeführt.

## Geprüfte README-Abschnitte

| Abschnitt | Prüfung | Ergebnis |
|-----------|---------|----------|
| Badges | `.NET-10.0`-Badge deckt sich mit `<TargetFramework>net10.0</TargetFramework>`; verlinkte Workflows `pr-staging-ci.yml` und `release.yml` existieren unter `.github/workflows/`; `LICENSE` vorhanden | aktuell |
| Kurzbeschreibung / Features | README beschreibt Features auf grober Ebene und enthält keinerlei Aussagen zur Ribbon-Gruppenreihenfolge (`grep -i ribbon README.md` → keine Treffer) | nicht betroffen |
| Installation / Usage / Konfiguration | Keine Setup- oder Konfigurationsänderung im Change | nicht betroffen |
| Projektstruktur / Tech-Stack | Keine neuen Projekte oder Frameworks | nicht betroffen |
| API-Dokumentation | Keine Endpunkt-Änderung | nicht betroffen |
| Tests | Keine neue Testinfrastruktur — nur zusätzliche Assertions in bestehenden Testklassen plus ein neuer Playwright-Test | nicht betroffen |
| Changelog | `changes.log` wurde bereits im Change ergänzt (Eintrag „2026-11-09 — Ribbon-Gruppierung …"); README verlinkt unverändert korrekt auf `CHANGELOG.md` | aktuell |
| Weitere Verweise | Alle in „Weitere Informationen" verlinkten Dateien existieren (`Docs/development.md`, `Docs/help/index.md`, `CI-CD.md`, `CONTRIBUTING.md`, `Docs/known-issues.md`, `CHANGELOG.md`, `LICENSE`) | aktuell |

## Abgrenzung

Die sichtbare Gruppenreihenfolge und die zugrunde liegende Konvention werden
anwender- bzw. entwicklerseitig bereits an den richtigen Stellen dokumentiert:

- `Docs/help/benutzeroberflaeche/beschreibung.md` und `ablauf-technisch.md`
  (im gleichen Branch aktualisiert — Anwenderdokumentation)
- `AGENT.md`, Abschnitt „Gruppierung des Ribbon-Menüs" (im gleichen Branch ergänzt —
  Meta-Konvention „Navigation-Gruppe an Position 0")
- `changes.log` (Eintrag zum Change)

Die README bleibt damit unverändert; das Artefakt dieses Schritts ist diese
Prüfdokumentation statt einer README-Änderung.
