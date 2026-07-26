# EF-Core-Konfiguration und Persistenz

## Modellkonfiguration

`FinanceManager.Infrastructure/AppDbContext.cs:122` konfiguriert `User.MassImportDialogPolicy` als erforderliche `short`-Spalte und setzt `HasDefaultValue(OnMissingInformation)`. Ein `HasSentinel(...)` ist nicht konfiguriert.

Der Defaultwert markiert die Property fuer EF Core als datenbankgeneriert (`ValueGeneratedOnAdd`). Da der implizite Sentinel fuer den Enum `0` ist und `0` dem gueltigen Wert `AlwaysConfirm` entspricht, kann EF bei einem neuen User den Wert `AlwaysConfirm` als "nicht gesetzt" interpretieren und die Spalte beim Insert auslassen. Dann liefert die Datenbank `OnMissingInformation` zurueck. Das ist die konkrete Ursache der Modellvalidierungswarnung und kann einen explizit gewuenschten `AlwaysConfirm`-Wert bei Inserts verfalschen.

## Lese- und Schreibpfad

`FinanceManager.Web/Controllers/UserSettingsController.cs:248-258` liest die gespeicherte Policy direkt aus `User`. `:299-304` setzt sie ueber `SetMassImportDialogPolicy` und speichert mit `SaveChangesAsync`. Bestehende Werte `0` und `1` werden beim Lesen nicht umgedeutet; das Risiko betrifft vor allem neue Entities bzw. Inserts, bei denen `0` der EF-Sentinel ist.

## Bewertung

Die Anwendung, die Konstruktoren und die API-Defaulttests bezeichnen `OnMissingInformation` als fachlichen Standard. Der Datenbankdefault ist ebenfalls `1`. Der Datenbankdefault und der fachliche Default sind somit identisch. Gleichzeitig ist `AlwaysConfirm = 0` ein gueltiger, explizit persistierbarer Wert. Eine Korrektur muss deshalb die Unterscheidung zwischen "nicht gesetzt" und explizitem `AlwaysConfirm` herstellen, ohne gespeicherte Werte zu aktualisieren.

