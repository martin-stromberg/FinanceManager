# Testergebnisse Schritt 3

- `dotnet build FinanceManager.sln --no-restore -v:m`: erfolgreich, 0 Fehler.
- Fokussierter Testlauf Reports/Favoriten/ViewModel: 86 Tests erfolgreich, 0 fehlgeschlagen.
- Backup/Favorite-Nachtest: 12 Tests erfolgreich, 0 fehlgeschlagen.
- Bekannte Warnungen: NU1510 sowie bestehende xUnit-Analysehinweise.

## Neue Abdeckung

- Aggregation filtert Bankposten nach Inflow/Outflow/Interest/Tax.
- Drilldown filtert Bankposten nach derselben Auswahl.
- Favoriten speichern `BankPostingTypesCsv` und liefern `BankPostingTypes` in Create/List/Get.
- Backup-DTO und Restore runden die CSV-Auswahl und akzeptieren alte Backups ohne das Feld.
