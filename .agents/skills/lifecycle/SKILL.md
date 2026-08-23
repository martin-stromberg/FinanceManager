---
name: lifecycle
description: Orchestriert die vollstaendige Bearbeitung einer Kundenanforderung in Codex: Anforderung uebersetzen, Bestandsaufnahme, Planung, Implementierung, Reviews, Tests, Dokumentation und Commit. Nutze diesen Skill, wenn der Nutzer eine neue oder begonnene Feature-Anforderung mit Lifecycle-Artefakten unter docs/features/{branchname}/ bearbeiten lassen will.
---

# Lifecycle

Dieser Skill koordiniert die vollstaendige Bearbeitung einer Kundenanforderung. Er ist ein Orchestrierungs-Skill: Der Hauptagent plant und implementiert fachlich nicht selbst, sondern delegiert die inhaltlichen Schritte an passende Subagenten oder fuehrt die im jeweiligen Schritt benannten Skills/Workflows aus, wenn Subagenten in der aktuellen Codex-Umgebung nicht verfuegbar sind.

## Wann verwenden

Nutze diesen Skill, wenn der Nutzer eine Anforderung mit Formulierungen wie diesen uebergibt:

- "Bearbeite diese Anforderung"
- "Fuehre den Lifecycle aus"
- "Setze diese Kundenanforderung komplett um"
- "Mach mit der begonnenen Anforderung weiter"
- "Setze den bestehenden Feature-Branch fort"

Wenn keine neue Anforderung genannt wird, pruefe vorhandene Artefakte unter `docs/features/{branchname}/` und setze am ersten offenen Schritt fort.

## Vorgehen

1. Lies die vollstaendige Ablaufbeschreibung in `lifecycle.md`.
2. Ermittle den aktuellen Branch und verweigere die Arbeit auf Hauptbranches.
3. Erstelle oder aktualisiere `docs/features/{branchname}/todo.md`.
4. Bestimme anhand vorhandener Artefakte den Einstiegspunkt.
5. Fuehre die Schritte aus `lifecycle.md` strikt in Reihenfolge aus.
6. Warte nach jeder Delegation auf den Abschluss, bevor du fortfaehrst.
7. Aktualisiere `todo.md` nach jedem abgeschlossenen Schritt.
8. Erstelle die vorgesehenen Commits nur, wenn es tatsaechlich passende Aenderungen gibt.

## Wichtige Regeln

- Arbeite nicht direkt auf `main`, `master`, `develop` oder `dev`.
- Der Hauptagent gibt keine fachlichen Zwischenantworten zur Loesung; er koordiniert den Ablauf.
- Inhaltliche Arbeit wird delegiert. Wenn keine Subagenten verfuegbar sind, fuehre die beschrieben Ablauf selbst aus und dokumentiere diese Abweichung kurz im Ergebnis.
- Bei offenen Punkten in `plan.md` muss der Nutzer eingebunden werden, bevor erneut geplant wird.
- Die Implementierungs- und Review-Schleife laeuft maximal drei Iterationen.
- Bei Abbruch oder ausbleibendem Fortschritt wird `continue.md` angelegt.
- Am Ende wird das Feature-Verzeichnis nur geloescht, wenn alle relevanten Todo-Eintraege erledigt sind.
- Bei UI-Features muessen konkrete E2E-Tests fuer den exakten Benutzerfluss im Plan verankert und erfolgreich ausgefuehrt werden, bevor der Commit erfolgt.
- Beim Code-Review ist sicherzustellen, dass jede `RaiseUiActionRequested`-Aktion in der zugehoerigen Blazor-Seite/-Komponente einen Handler besitzt.

## Detailanleitung

Folge der Datei [`lifecycle.md`](lifecycle.md).
