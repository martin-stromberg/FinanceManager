← [Zurück zur Übersicht](index.md)

# Systemverwaltung und Setup — Installation und Konfiguration

## Voraussetzungen

- Laufende Anwendung mit Datenbankmigrationen.
- Konfigurierte Authentifizierung und lokalisierte Ressourcen.

## Installationsschritte

1. Anwendung starten und initiale Migration/Seeding ausführen.
2. Administratorbenutzer anlegen.
3. Setup-Bereiche über die Seite `setup` prüfen.

## Konfiguration

| Parameter | Typ | Standardwert | Beschreibung |
|-----------|-----|--------------|--------------|
| `ImportSplitMode` | Enum | `MonthlyOrFixed` | Strategie für Import-Splitting |
| `ImportMaxEntriesPerDraft` | int | `250` | Max. Entwurfszeilen pro Draft |
| `ImportMonthlySplitThreshold` | int? | `250` | Schwellwert für Monats-Split |
| `ImportMinEntriesPerDraft` | int | `8` | Min. Entwurfszeilen pro Draft |
| `MassImportDialogPolicy` | Enum | `OnMissingInformation` | Dialogverhalten im Massenimport |

## Überprüfung

- Login/Logout funktioniert.
- Benutzerprofil und Benachrichtigungseinstellungen sind speicherbar.
- Backup kann erstellt und Restore-Status abgefragt werden.
