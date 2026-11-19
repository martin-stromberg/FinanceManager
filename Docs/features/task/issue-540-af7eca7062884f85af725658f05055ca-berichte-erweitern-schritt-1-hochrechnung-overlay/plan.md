# Plan – Schritt 1

1. Posting-ID und Betrag durch Projection-Events, DTOs, Suppress-Requests und Suppression-Entity führen; Betrag beim Speichern übernehmen.
2. Suppression-Matching auf `PostingId` erweitern; Datensätze ohne Posting-ID behandeln weiterhin den alten `(Kind, EntityId, PriorYearDate)`-Schlüssel.
3. Unique-Index und EF-Migration für `PostingId`/`Amount` ergänzen; Backup-DTO kompatibel erweitern.
4. Suppressed-Liste mit Betrag liefern und im Overlay auf den geöffneten Entity-/Kind-Scope filtern.
5. Nach dem Einbeziehen Berichtsdaten neu laden und die Overlay-Erwartungsliste aus dem aktualisierten Punkt wieder aufbauen.
6. Posten-Overlay-Beschreibung aus Beschreibung/Betreff und Verwendungszweck zusammensetzen.
7. Regressionstests für gleiche Datumsposten, Kontextfilterung, Betrag, Sofortrefresh-Hilfelogik und Backup-Kompatibilität ergänzen.
