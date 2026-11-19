# Plan Schritt 3

1. Filtervertrag `ReportAggregationFilters`, Request- und Favorite-DTO um `BankPostingTypes` erweitern.
2. `ReportFavorite` um `BankPostingTypesCsv`, Backup-DTO und kompatible Defaults erweitern; EF-Migration erzeugen.
3. Controller, Service, MCP-Mapping und ViewModel-Filterstatus durchreichen.
4. Bankseite im Filterdialog mit vier lokalisierten Optionen ergänzen.
5. Aggregation bei gesetztem Typfilter auf Rohpostings umschalten; Drilldown identisch filtern.
6. Fokussierte Tests für Aggregation, Postenliste, Favoriten-Persistenz und Backup-Roundtrip ergänzen.
