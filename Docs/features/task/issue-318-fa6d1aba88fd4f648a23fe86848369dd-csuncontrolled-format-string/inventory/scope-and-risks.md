# Detail: Abgrenzung, Risiken und Änderungsumfang

## Im Umfang

- Ersetzen der unkontrollierten Formatstring-Verwendung in `ReportCacheService.BuildKey`.
- Beibehalten des bisherigen Cache-Key-Formats und der kulturinvarianten Datumsdarstellung.
- Fokussierte Tests für Struktur, Determinismus und relevante Eingabeunterschiede.

## Nicht im Umfang

- Keine Änderung an MVC-Action-Parametern oder Request-Dtos.
- Keine Änderung an Cache-Lebensdauer, Refresh-Triggern, JSON-Parametern oder EF-Core-Modell.
- Keine allgemeine Bereinigung anderer `string.Format`-Aufrufe.
- Keine Änderung an Präfixfilterung oder Benutzerisolierung über `OwnerUserId`.

## Risiken

- Eine unbeabsichtigte Änderung der Enum-Darstellung könnte bestehende Cacheeinträge unauffindbar machen. Deshalb muss `BookingDate`/`ValutaDate` als Textbestandteil erhalten bleiben.
- Eine kulturabhängige Datumsformatierung würde die Deterministik zwischen Laufzeitumgebungen verletzen. Die Ausgabe muss explizit `yyyyMMdd` und invariant bleiben.
- Eine Änderung der Trennzeichen oder Reihenfolge würde bestehende Cache-Schlüssel brechen, obwohl die Datenbank unverändert bleibt.

## Rückwärtskompatibilität

Bei unveränderter Schlüsselstruktur sind keine Migration und keine Bereinigung bestehender `ReportCacheEntry`-Datensätze erforderlich. Cacheeinträge bleiben über dieselben Schlüssel erreichbar.
