# Planprüfung Schritt 3

- **Korrekte Datenquelle:** `Posting.PostingType` ist persistiert und wird beim Import/Buchen klassifiziert; kein Duplikat der Regel-Engine nötig.
- **Kompatibilität:** Das neue Favoritenfeld ist nullable, das Backup-Feld hat einen Default; alte Datenbanken und alte Backups bleiben lesbar.
- **Filtersemantik:** Bankpostenarten sind fachlich auf Bankposten begrenzt und werden nur ausgewertet, wenn `PostingKind.Bank` angefragt ist.
- **Aggregat vs. Drilldown:** Aggregation verwendet bei aktivem Typfilter Rohpostings, weil `PostingAggregates` kein `PostingType` enthält; `ReportPostingsQueryService` filtert direkt auf `Posting.PostingType`.
- **UI/Persistenz:** Temporärer und angewendeter ViewModel-Zustand, Filterzähler, Favoriten-DTO und MCP-Mapping sind durchgängig.
