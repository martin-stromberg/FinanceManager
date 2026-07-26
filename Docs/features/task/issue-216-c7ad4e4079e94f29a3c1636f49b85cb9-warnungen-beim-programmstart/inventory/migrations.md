# Migrationen und gespeicherte Daten

## Einfuehrungsmigration

`FinanceManager.Infrastructure/Migrations/20260703061917_202607030850_AddMassImportDialogPolicy.cs:8-22` fuegt `AspNetUsers.MassImportDialogPolicy` als nicht nullable `INTEGER` hinzu und setzt `defaultValue: (short)1`. Damit erhalten bestehende Benutzer bei der Schemaerweiterung `OnMissingInformation`.

Die zugehoerige Designer-Datei und `AppDbContextModelSnapshot.cs` modellieren die Spalte als `ValueGeneratedOnAdd`, `INTEGER` und Default `(short)1`.

## Historie

Die Migration ist die einzige Migration, die `MassImportDialogPolicy` hinzufuegt. Die nachfolgenden Migrationen bis `20260718203753_AddBudgetPurposeValuationType` sowie `20260719090000_ProtectAlphaVantageApiKeys` enthalten keine weitere Aenderung an dieser Spalte. Es gibt daher keine historische Umstellung zwischen `0` und `1`.

## Auswirkungen einer Korrektur

Eine reine EF-Modellkorrektur am Sentinel bzw. an der Value-Generation benoetigt nach der Bestandsaufnahme keine Datenmigration: Bestehende Zeilen bleiben unveraendert, und der Datenbankdefault `1` kann fachlich bestehen bleiben. Eine neue Migration waere nur erforderlich, wenn der SQL-Default oder die Spaltendefinition selbst geaendert wird.

