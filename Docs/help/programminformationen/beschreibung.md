← [Zurück zur Übersicht](index.md)

# Programminformationen — Beschreibung

## Zweck

Die Anwendung zeigt die aktuelle Versionsnummer in der Benutzeroberfläche an. Dies ermöglicht dem Anwender, schnell zu überprüfen, welche Programmversion aktuell in Verwendung ist. Diese Information ist besonders hilfreich bei der Fehlerberichterstattung oder zur Kontrolle, ob eine bestimmte Version mit einer erforderlichen Funktionalität verfügbar ist.

## Funktionsweise

Die Versionsnummer wird im Menü-Fußbereich angezeigt — rechts neben dem Logout-Button im oberen Bereich der Anwendung. Die Versionsinformation wird beim Laden der Anwendung bereitgestellt und wird nicht automatisch aktualisiert, wenn der Benutzer noch angemeldet ist (ein Neustart oder Refresh der Seite ist erforderlich, um eine neue Versionsinformation zu laden).

Die Versionsnummer wird aus der `release-metadata.json`-Datei gelesen, die beim Deployment der Anwendung erzeugt wird. Die Formatierung erfolgt ohne Präfix, z. B. `1.2.3`.

## Beispiele

**Szenario 1: Version verfügbar**
- Der Anwender öffnet die Anwendung und meldet sich an.
- Im Menü-Fußbereich sieht er die Versionsnummer, z. B. `1.2.3`.

**Szenario 2: Version nicht ermittelbar**
- Die `release-metadata.json`-Datei existiert nicht oder ist beschädigt.
- Im Menü-Fußbereich sieht der Anwender den Platzhaltertext `Version unbekannt`.

## Einschränkungen

- Die Versionsnummer wird nur angezeigt, wenn der Benutzer authentifiziert ist. Nicht angemeldete Benutzer sehen nur den Login-Link.
- Die Versionsnummer wird beim Laden der Seite gelesen und wird nicht in Echtzeit aktualisiert. Wenn das Programm aktualisiert wird, während der Benutzer angemeldet ist, zeigt die Anwendung die alte Version an, bis der Browser die Seite neu lädt oder der Benutzer sich ab- und wieder anmeldet.
- Ist die `release-metadata.json`-Datei nicht vorhanden oder leer, wird der Fallback-Text `Version unbekannt` angezeigt.
