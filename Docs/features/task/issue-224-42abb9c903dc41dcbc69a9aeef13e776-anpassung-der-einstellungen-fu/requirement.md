# Strukturierte Anforderung

## Metadaten

- Aufgaben-ID: `42abb9c9-03dc-41dc-bc69-a9aeef13e776`
- Branch: `task/issue-224-42abb9c903dc41dcbc69a9aeef13e776-anpassung-der-einstellungen-fu`
- Erstellt: `2026-07-27`
- Titel: Anpassung der Einstellungen fuer Programmupdates

## Ziel

Die Einstellungsseite fuer Programmupdates soll an die uebliche Funktionsweise der Anwendungseinstellungen angeglichen und um ueberfluessige, fuer Anwender verwirrende Konfigurationsfelder bereinigt werden.

## Funktionale Anforderungen

### Entfernen ueberfluessiger Einstellungsfelder

Die folgenden Einstellungsfelder sollen aus der Oberflaeche entfernt werden:

- Exe-Pfad
- Repository-Owner
- Repository-Name
- Manifest-Asset
- Arbeitsverzeichnis
- Health-Timeout in Sekunden

### Festwerte fuer entfernte Einstellungen

Fuer die entfernten Einstellungen gelten intern die folgenden festen Werte:

- Repository-Owner: `martin-stromberg`
- Repository-Name: `FinanceManager`
- Manifest-Asset: `update.json`
- Arbeitsverzeichnis: `updates`

Der Exe-Pfad soll nicht mehr als Anwender-Einstellung angeboten werden.

Der Health-Timeout soll nicht mehr angeboten werden, da der Mechanismus nach aktuellem Stand nicht wirksam ist und bei nicht erreichbarem Server der Blazor-Standardmechanismus greift.

### Uebersetzung des Updatepruefungsstatus

Der Statustext fuer das Ergebnis der Updatepruefung soll in der Benutzeroberflaeche uebersetzt angezeigt werden.

### Speichern von Einstellungen

Wenn in den verbleibenden Einstellungsfeldern Aenderungen vorgenommen werden, sollen diese ueber den vorhandenen Aktionsbutton `Speichern` im Ribbon-Menue gespeichert werden.

Der Button `Einstellungen speichern` im Update-Register soll entfernt werden.

### Integration der Update-Aktionen in das Ribbon-Menue

Die folgenden Aktionen sollen aus dem Update-Register in das Ribbon-Menue integriert werden:

- Jetzt pruefen
- Update installieren
- Update-Lock zuruecksetzen

### Autocomplete fuer Servicenamen

Das Einstellungsfeld fuer den Servicenamen soll eine Autocomplete-Funktion erhalten.

Die Autocomplete-Vorschlaege sollen aus den Diensten des aktuellen Systems gelesen werden.

Die Ermittlung der Dienste muss plattformspezifisch erfolgen:

- Windows: Windows-Dienste auslesen.
- Linux: Linux-Systemdienste auslesen.

## Nicht-funktionale Anforderungen

- Die Update-Einstellungen sollen sich konsistent zu den uebrigen Einstellungsseiten der Anwendung verhalten.
- Entfernte Einstellungen duerfen Anwendern nicht mehr als editierbare Felder angezeigt werden.
- Die verbleibenden Update-Aktionen sollen im Ribbon-Menue auffindbar und bedienbar sein.
- Die Autocomplete-Funktion fuer Servicenamen soll ohne Plattformfehler auf Windows und Linux funktionieren.

## Akzeptanzkriterien

- Die Felder `Exe-Pfad`, `Repository-Owner`, `Repository-Name`, `Manifest-Asset`, `Arbeitsverzeichnis` und `Health-Timeout in Sekunden` sind auf der Update-Einstellungsseite nicht mehr sichtbar.
- Die intern benoetigten Werte fuer Repository-Owner, Repository-Name, Manifest-Asset und Arbeitsverzeichnis werden weiterhin mit den festgelegten Standardwerten verwendet.
- Der Statustext der Updatepruefung erscheint uebersetzt.
- Aenderungen an verbleibenden Update-Einstellungen werden ueber den Ribbon-Button `Speichern` gespeichert.
- Der Button `Einstellungen speichern` ist im Update-Register nicht mehr vorhanden.
- Die Aktionen `Jetzt pruefen`, `Update installieren` und `Update-Lock zuruecksetzen` sind im Ribbon-Menue verfuegbar.
- Das Feld fuer den Servicenamen bietet Autocomplete-Vorschlaege aus den Systemdiensten an.
- Die Dienstermittlung funktioniert unter Windows und Linux mit jeweils passender plattformspezifischer Logik.

## Offene Punkte

- Keine.
