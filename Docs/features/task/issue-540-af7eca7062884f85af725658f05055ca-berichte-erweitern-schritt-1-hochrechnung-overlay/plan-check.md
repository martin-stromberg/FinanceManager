# Planprüfung – Schritt 1

## Ergebnis

**Status:** Plan vollständig

## Prüfung
- Die postenbezogene ID behebt die Kollision gleich datierter Buchungen ohne den Betreff als Identität zu verwenden.
- Der alte Schlüssel bleibt als Fallback, damit vorhandene Unterdrückungen nicht verloren gehen.
- Die Overlay-Filterung nutzt den aktuell angezeigten Scope und serverseitig weiterhin Owner-/Favorite-Prüfung.
- Die Aktualisierung nach dem Einbeziehen basiert auf erneut geladenen Berichtsdaten und bleibt auch bei mehreren Kandidaten konsistent.
- Persistenz-, API-, UI-, Backup- und Testpfade sind abgedeckt.

## Risiken
- Bei alternder Schreibweise ohne Posting-ID können mehrere Kandidaten weiterhin gemeinsam unterdrückt werden; dies ist als bewusste Bestandskompatibilität dokumentiert.
- Für netzende Wertpapier-Dividenden muss die ausgewählte Quellposting-ID stabil sein; der Dividendenanker wird als Quellposten verwendet.
