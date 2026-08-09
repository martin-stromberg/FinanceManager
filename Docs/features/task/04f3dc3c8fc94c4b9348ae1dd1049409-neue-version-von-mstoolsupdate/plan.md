# Umsetzungsplan: Neue Version von msTools.Updater v0.3.0

## Übersicht

Die Anwendung verwendet bereits vollständig msTools.Updater v0.3.0 für die Update-Verwaltung. Die veraltete Version v0.2.0, die temporär während der Migrationsphase zu Testzwecken eingecheckt wurde, soll aus dem Repository entfernt werden, um die Codebasis zu bereinigen und Verwirrung über die unterstützte Version zu vermeiden. Die Aufgabe umfasst das Löschen des Verzeichnisses `external/msTools.Updater/v0.2.0/`, Validierung fehlender Abhängigkeiten in Build-Skripten und einen abschließenden Commit.

## Designentscheidungen

Keine — folgt bestehenden Mustern. Die Anforderung ist ein Cleanup-Vorgang ohne Architekturentscheidungen. Das Löschen von veralteten, nicht mehr verwendeten Abhängigkeiten folgt der Konvention einer sauberen Codebasis.

## Programmabläufe

Keine neuen oder geänderten Programmabläufe erforderlich. Die Anwendung funktioniert nach Abschluss dieser Aufgabe identisch wie vorher — v0.3.0 ist bereits vollständig integriert und in Verwendung.

## Neue Klassen

Keine.

## Änderungen an bestehenden Klassen

Keine. Alle Update-Service-Klassen verwenden bereits v0.3.0; eine Anpassung des Codes ist nicht erforderlich.

## Datenbankmigrationen

Keine.

## Validierungsregeln

Keine.

## Konfigurationsänderungen

Keine. Die Referenz auf v0.3.0 ist bereits in `FinanceManager.Web.csproj` gesetzt; das Löschen von v0.2.0 hat keine Auswirkung auf die Konfiguration.

## Seiteneffekte und Risiken

Keine bekannten Seiteneffekte. Da v0.2.0 bereits vollständig aus dem Code-Repository entfernt wurde (grep-validiert: keine Quellcode-Abhängigkeiten), entfällt das Risiko von Exceptions oder Laufzeitfehlern durch das Löschen des Verzeichnisses.

## Umsetzungsreihenfolge

1. **Vorbedingung validieren: Versteckte Abhängigkeiten prüfen**
   - Voraussetzungen: Keine.
   - Beschreibung: Grep-Suche nach allen Verweisen auf `v0.2.0` in Build-Skripten (`*.ps1`, `*.sh`, `Dockerfile*`, `.github/workflows`), `.csproj`-Dateien und Konfigurationsdateien durchführen. Ziel: Sicherstellen, dass v0.2.0 wirklich nicht außerhalb des `external/`-Verzeichnisses referenziert wird.

2. **Verzeichnis `external/msTools.Updater/v0.2.0/` aus Git entfernen**
   - Voraussetzungen: Schritt 1 abgeschlossen, keine versteckten Abhängigkeiten gefunden.
   - Beschreibung: Das Verzeichnis `external/msTools.Updater/v0.2.0/` (einschließlich aller Assemblies, Abhängigkeiten, README und anderen Dateien) aus dem Repository löschen. Dies geschieht via `git rm -r external/msTools.Updater/v0.2.0/`.

3. **Dokumentation aktualisieren (optional)**
   - Voraussetzungen: Schritt 2 abgeschlossen.
   - Beschreibung: 
     - Falls das `external/msTools.Updater/v0.3.0/README.md` existiert: Kein zusätzlicher Vermerk nötig — das README bleibt unverändert.
     - Falls das `CHANGELOG.md` einen Eintrag zur Integration von v0.2.0 enthält: Optional einen Eintrag hinzufügen wie „Removed obsolete msTools.Updater v0.2.0 after successful migration to v0.3.0."

4. **Prozess für zukünftige Versionsupgrades dokumentieren**
   - Voraussetzungen: Schritt 3 abgeschlossen.
   - Beschreibung: Erstelle oder aktualisiere eine Dokumentation (z. B. `docs/maintenance/dependency-upgrade-process.md` oder einen Abschnitt in einem bestehenden Maintenance-Dokument), die beschreibt:
     - Wie neue Versionen des Updaters (z. B. v0.4.0) bereitgestellt werden (Platzierung im `external/`-Verzeichnis, Assembly-Referenzen aktualisieren).
     - Wie alte Versionen nach einer erfolgreichen Migration gelöscht werden (Ähnlich wie in den Schritten 1–2 dieser Aufgabe).
     - Diese Dokumentation dient als Vorlage für zukünftige Upgrades und verhindert Ad-hoc-Entscheidungen.

5. **Git-Commit erstellen**
   - Voraussetzungen: Schritte 1–4 abgeschlossen.
   - Beschreibung: Alle Änderungen in einem Git-Commit mit aussagekräftiger Nachricht committen. Beispiel: `fix: Remove obsolete msTools.Updater v0.2.0 after successful migration`. Nachricht sollte kurz erklären, dass v0.3.0 bereits vollständig in Verwendung ist und die Cleanup durchgeführt wird.

## Tests

### Neue Tests

Keine.

### Betroffene bestehende Tests

Keine. Alle Update-Tests verwenden bereits v0.3.0 und sind vom Löschen von v0.2.0 nicht betroffen.

### E2E-Tests (Pflicht)

Keine. Das Löschen von v0.2.0 ist ein reines Cleanup und ändert keinen sichtbaren Anwendungsverhalt. Bestehende E2E-Tests für Update-Funktionalität (z. B. `UpdateSetupPlaywrightTests`) sind nicht betroffen, da sie v0.3.0 verwenden.

## Offene Punkte

Keine.
