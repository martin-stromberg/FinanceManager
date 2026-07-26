# Policy und User

## MassImportDialogPolicy

`FinanceManager.Shared/Dtos/Statements/MassImportDtos.cs:8-17` definiert den Enum als `short`:

- `AlwaysConfirm = 0`: Dialog immer anzeigen.
- `OnMissingInformation = 1`: Dialog nur bei fehlenden Informationen anzeigen.

Damit ist `AlwaysConfirm` zugleich der CLR-Default eines nicht nullable Enums. Der fachliche Default ist im Code jedoch `OnMissingInformation`.

## User-Defaults

In `FinanceManager.Domain/Users/User.cs:22-94` setzen zwei Konstruktoren den Policywert explizit auf `OnMissingInformation`. Die beiden Konstruktoren `User(string username, bool isAdmin)` und `User(string username, string passwordHash, bool isAdmin)` setzen ihn nicht explizit; dort greift aber der Property-Initializer in `User.cs:143`, der ebenfalls `OnMissingInformation` setzt. Der private parameterlose ORM-Konstruktor setzt keinen Wert selbst.

Die Policy ist privat setzbar und wird ueber `SetMassImportDialogPolicy` (`User.cs:232-235`) geaendert. Die Setter-Methode validiert den Enum nicht; die API bindet den Requesttyp direkt und persistiert den uebergebenen Wert.

## Fachliche Nutzung

`FinanceManager.Infrastructure/Statements/MassImportOrchestrator.cs:336-344` behandelt `AlwaysConfirm` als Sonderfall; andere Werte werden in die Pruefung fehlender Informationen einbezogen. Ein versehentliches `0` bedeutet daher fachlich nicht den Standard, sondern `AlwaysConfirm`.

## User-Erzeugung

Die produktive Erstellung ueber ASP.NET Identity verwendet den Konstruktor `User(string username, bool isAdmin)` bzw. `User(string username)` und setzt anschliessend das Passwort ueber `UserManager`. Der E2E-Seeder (`FinanceManager.Tests.E2E/Helpers/TestUserSeeder.cs:29-37`) verwendet den Konstruktor mit Passwort-Hash und Admin-Flag. In beiden Faellen liefert der Initializer `OnMissingInformation`.

