← [Zurück zur Übersicht](index.md)

# Kontoauszüge und Import — Fehlerbehebung

## Export wird mit „Export limit exceeded" abgelehnt

**Symptom:** Der Export der Entwurfszeilen endet mit HTTP 400 und der Meldung „Export limit exceeded".

**Ursache:** Der Entwurf enthält (nach den aktiven Filtern) mehr Einträge, als das serverseitige Zeilenlimit `Exports:MaxRows` (Standard 50.000) erlaubt.

**Lösung:**
1. Grenzen Sie den Export über die Such- und Datumsfilter der Eintragsliste ein.
2. Alternativ kann ein Administrator den Konfigurationsschlüssel `Exports:MaxRows` erhöhen.

## Export schlägt mit „Invalid format" fehl

**Symptom:** Der Export-Endpunkt `GET /api/statement-drafts/{draftId}/export` antwortet mit HTTP 400 „Invalid format".

**Ursache:** Der Query-Parameter `format` enthält einen anderen Wert als `csv` oder `xlsx`.

**Lösung:**
1. Den Parameter `format` auf `csv` oder `xlsx` setzen — oder weglassen, dann gilt `csv` als Standard.

> **Hinweis:** Der Fehler tritt nur bei direkten API-Aufrufen auf; die Ribbon-Aktionen „CSV exportieren" und „Excel exportieren" setzen stets gültige Formate.
