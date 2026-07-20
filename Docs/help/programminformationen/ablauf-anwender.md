← [Zurück zur Übersicht](index.md)

# Programminformationen — Ablauf für Anwender

## Voraussetzungen

- Sie sind erfolgreich in der Anwendung angemeldet.
- Die Anwendung hat eine gültige `release-metadata.json`-Datei.

## Schritt-für-Schritt-Anleitung

### 1. Öffnen Sie die Anwendung

Starten Sie die Anwendung und melden Sie sich mit Ihren Anmeldedaten an.

### 2. Suchen Sie die Versionsnummer

Nach erfolgreicher Anmeldung finden Sie die Versionsnummer im **Menü-Fußbereich** (rechts oben neben dem Logout-Button). Die Versionsnummer wird auf einer separaten Zeile angezeigt.

> **Hinweis:** Die Versionsnummer ist nur für authentifizierte Benutzer sichtbar. Wenn Sie nicht angemeldet sind, sehen Sie stattdessen den Login-Link.

### 3. Überprüfen Sie die Version

Lesen Sie die angezeigte Versionsnummer, z. B. `1.2.3`. Dies ist die aktuelle Version der Anwendung, die Sie gerade nutzen.

### Alternativ: Fallback bei fehlender Version

Wenn die Versionsinformation nicht ermittelbar ist (z. B. weil die `release-metadata.json`-Datei fehlt), wird der Text `Version unbekannt` angezeigt.

## Ergebnis

Sie haben die aktuelle Versionsnummer der Anwendung überprüft und können diese bei Bedarf (z. B. zur Fehlerberichterstattung oder zur Überprüfung der verfügbaren Funktionen) verwenden.

## Barrierefreiheit

Die Versionsnummer wird im HTML als einfacher Text mit dem CSS-Klassennamen `login-status` gerendert und ist vollständig für Bildschirmleser zugänglich. Es gibt keine speziellen Tastaturkürzel für diese Funktionalität.
