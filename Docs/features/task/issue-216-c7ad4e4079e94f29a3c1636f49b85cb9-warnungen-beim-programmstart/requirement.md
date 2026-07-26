# Anforderung: Warnung zur datenbankgenerierten MassImportDialogPolicy beseitigen

## Zusammenfassung
Beim Programmstart wird von Entity Framework Core eine Modellvalidierungswarnung zur Eigenschaft `User.MassImportDialogPolicy` ausgegeben. Es soll geprüft werden, ob die Konfiguration ein fachliches oder technisches Fehlverhalten verursachen kann. Falls ein Risiko besteht oder die Konfiguration die Warnung unnötig auslöst, soll die Ursache behoben werden, ohne die gewünschte Standard- und Persistierungslogik der Eigenschaft zu verändern.

## Auslöser und Akteure
- **Auslöser:** Anwendungstart und die dabei ausgeführte Validierung des Entity-Framework-Core-Modells.
- **Akteure:** Anwendung, Entity Framework Core und die Persistenzschicht; indirekt Benutzer, deren Einstellungen über die Entität `User` gespeichert werden.

## Beschreibung
Die Konfiguration der Eigenschaft `MassImportDialogPolicy` auf der Entität `User` verwendet einen datenbankgenerierten Standardwert, definiert jedoch keinen Sentinel-Wert. Da `AlwaysConfirm` der CLR-Standardwert des verwendeten Typs ist, wird der Datenbankstandardwert bei Inserts mit diesem Wert immer angewendet.

Die bestehende Modellkonfiguration und die fachliche Bedeutung der Richtlinienwerte sind zu untersuchen. Dabei ist zu klären, ob der Datenbankstandardwert bei Inserts tatsächlich immer verwendet werden soll oder nur dann, wenn kein Wert durch die Anwendung gesetzt wurde. Ein daraus entstehendes Fehlverhalten ist zu beheben. Die Modellkonfiguration soll anschließend keine entsprechende Entity-Framework-Core-Warnung mehr beim Programmstart erzeugen.

## Eingaben und Ausgaben
- **Eingaben:** Bestehende EF-Core-Modellkonfiguration der Entität `User`, der Typ und die möglichen Werte von `MassImportDialogPolicy`, der Datenbankstandardwert sowie vorhandene gespeicherte Benutzerdaten und Migrationen.
- **Ausgaben/Ergebnisse:** Korrigierte Modellkonfiguration und gegebenenfalls erforderliche Migration oder Datenanpassung; Inserts und Ladevorgänge von `User` verwenden die fachlich korrekten Werte; die genannte Modellvalidierungswarnung tritt beim Programmstart nicht mehr auf.

## Fehlerbehandlung
Falls die Untersuchung ein Risiko für bestehende Benutzereinstellungen, Inserts oder Migrationen feststellt, muss die Korrektur diese Fälle berücksichtigen und darf gespeicherte Einstellungen nicht unbeabsichtigt überschreiben. Können Datenbankstandardwert und Anwendungswert nicht eindeutig miteinander vereinbart werden, ist die fachlich erforderliche Entscheidung vor der Umsetzung zu klären.

## Abgrenzung
Nicht Teil dieser Anforderung sind Änderungen an anderen Modellvalidierungswarnungen, eine allgemeine Überarbeitung der Benutzereinstellungen oder fachliche Änderungen an den verfügbaren Mass-Import-Dialogrichtlinien. Änderungen an Migrationen oder bestehenden Daten sind nur Bestandteil, sofern sie für die korrekte und warnungsfreie Behebung dieser konkreten Konfiguration erforderlich sind.

## Akzeptanzkriterien
- [ ] Die Ursache der Warnung zur Eigenschaft `User.MassImportDialogPolicy` ist im bestehenden Code und in der Datenbankkonfiguration nachvollzogen.
- [ ] Es ist bewertet und dokumentiert, ob die bisherige Konfiguration bei Inserts zu einem fachlich falschen Wert führen kann.
- [ ] Eine erforderliche Korrektur stellt sicher, dass der Datenbankstandardwert nur unter den fachlich vorgesehenen Bedingungen verwendet wird.
- [ ] Bestehende gespeicherte Benutzereinstellungen werden durch die Korrektur nicht unbeabsichtigt verändert.
- [ ] Die Anwendung startet ohne die genannte EF-Core-Modellvalidierungswarnung.
- [ ] Relevante Tests für Modellkonfiguration sowie Insert- und Persistierungsverhalten sind erfolgreich.

## Offene Punkte
- [ ] Soll `AlwaysConfirm` als fachlicher Standardwert der Anwendung gelten, oder soll in bestimmten Fällen der Datenbankstandardwert greifen?
- [ ] Ist der konfigurierte Datenbankstandardwert identisch mit `AlwaysConfirm`, und existieren bereits Migrationen oder Daten, die bei einer Änderung berücksichtigt werden müssen?
