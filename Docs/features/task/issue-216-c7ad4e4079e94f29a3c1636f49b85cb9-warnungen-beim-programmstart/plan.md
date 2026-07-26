# Umsetzungsplan: EF-Warnung bei MassImportDialogPolicy

## Ziel

Die EF-Core-Modellvalidierungswarnung fuer `User.MassImportDialogPolicy` soll mit einer minimalen, fachlich unveraenderten Modellkonfiguration behoben werden. Der Standard `OnMissingInformation` bleibt erhalten; ein explizit gesetztes `AlwaysConfirm` muss bei neuen Benutzern korrekt als `0` persistiert werden. Bestehende gespeicherte Werte duerfen nicht veraendert werden.

## Befund und Entscheidung

- `MassImportDialogPolicy.AlwaysConfirm` hat den Enum-Wert `0` und ist damit aktuell implizit der EF-Sentinel.
- `MassImportDialogPolicy.OnMissingInformation` hat den Wert `1` und ist sowohl der fachliche Standard der `User`-Konstruktoren als auch der SQL-Default der Migration.
- Durch `HasDefaultValue(...)` wird die Property als `ValueGeneratedOnAdd` behandelt. Ohne expliziten Sentinel kann EF den gueltigen Wert `AlwaysConfirm` beim Insert als "nicht gesetzt" behandeln und die Spalte auslassen.
- Die bestehende SQL-Spalte ist nicht nullable und hat bereits den fachlich richtigen Default `1`. Die Einfuehrungsmigration hat bestehende Zeilen mit `1` initialisiert; spaetere Migrationen deuten keine Werte um.

Die Umsetzung verwendet daher in `FinanceManager.Infrastructure/AppDbContext.cs` direkt an der bestehenden Property-Konfiguration:

```csharp
.HasDefaultValue(MassImportDialogPolicy.OnMissingInformation)
.HasSentinel(MassImportDialogPolicy.OnMissingInformation)
```

`HasSentinel(...)` ist hier besser als das Entfernen des Datenbankdefaults: Es beseitigt die Warnung mit der dafuer vorgesehenen EF-Core-Konfiguration, bewahrt den bestehenden SQL-Default und verwendet keinen ungueltigen Enum-Wert als kuenstlichen Sentinel. Bei `OnMissingInformation` darf die Datenbank den identischen Default liefern; bei `AlwaysConfirm` ist der Wert vom Sentinel verschieden und wird explizit geschrieben.

Das Entfernen von `HasDefaultValue(...)` waere zwar ebenfalls eine moegliche Warnungsbehebung, wuerde aber die Datenbankgenerierung und potenziell die Schema-Defaultdefinition veraendern. Diese groessere Aenderung ist fuer die Anforderung nicht notwendig.

## Umsetzungsschritte

1. Die `User.MassImportDialogPolicy`-Konfiguration in `AppDbContext.OnModelCreating` um `HasSentinel(MassImportDialogPolicy.OnMissingInformation)` erweitern. Conversion zu `short`, Nicht-Nullable-Konfiguration und bestehender DB-Default bleiben unveraendert.
2. Einen gezielten EF-Modelltest ergaenzen, der fuer die Property den Sentinel `OnMissingInformation`, `ValueGeneratedOnAdd`, den `short`-Store-Typ und den Defaultwert `1` prueft. Der Test soll ausserdem die relevante Modellvalidierungswarnung nicht mehr ausloesen.
3. Einen relationalen SQLite-Regressionstest fuer einen neuen `User` mit explizitem `AlwaysConfirm` ergaenzen: speichern, Kontext leeren bzw. neu aufbauen und erneut laden; erwartet wird `AlwaysConfirm` (`0`).
4. Den bestehenden Default-/Persistenztest fuer `OnMissingInformation` beibehalten bzw. gezielt ergaenzen, damit der Standardwert fuer neue Benutzer unveraendert bleibt.
5. Einen Persistenztest fuer bereits gespeicherte Werte vorsehen, der sowohl `AlwaysConfirm` (`0`) als auch `OnMissingInformation` (`1`) nach dem Reload unveraendert bestaetigt. Keine Datenbereinigung und keine Aenderung an Domain-, API- oder Controller-Logik.
6. Nach der Modellanpassung den EF-Migrationsstatus bzw. einen Modellvergleich pruefen. Erwartung: keine anwendbare Schemaaenderung; insbesondere keine Aenderung an `AspNetUsers.MassImportDialogPolicy`, deren Nullable-/Typdefinition oder SQL-Default.

## Migration und Datenbestand

Es ist keine neue Migration einzuplanen. `HasSentinel(...)` aendert nur das EF-Verhalten beim Erkennen eines nicht gesetzten Werts; die Datenbankstruktur und der Default `1` bleiben identisch. Vor Abschluss ist mit dem im Repository verwendeten EF-Core-10-Tooling zu verifizieren, dass keine ausstehende Schemaaenderung gemeldet wird. Falls das Tool wider Erwarten eine Migration mit ausschliesslich leerem `Up`/`Down` erzeugt, wird sie nicht uebernommen; die Ursache waere zu pruefen, ohne bestehende Migrationen oder den Snapshot manuell zu veraendern.

## Verifikation

- Betroffene Infrastructure- und Testprojekte bauen erfolgreich.
- Der gezielte Modelltest bestaetigt die Sentinel-/Default-Metadaten und das Ausbleiben der Warnung.
- Der SQLite-Insert-/Reload-Test bestaetigt die Persistierung von `AlwaysConfirm`.
- Der Defaulttest bestaetigt `OnMissingInformation` fuer neue Benutzer.
- Bestehende gespeicherte Werte `0` und `1` werden unveraendert gelesen.
- Der EF-Migrationscheck bestaetigt, dass keine Migration erforderlich ist.

## Offene Punkte

Keine. Die fachliche Standardentscheidung und der Migrationsbedarf sind durch Inventar, Entity-Konfiguration, Migrationen und bestehende Tests geklaert.
