# Umsetzungsplan: Optimierung der Schnelleingabe

## Ziel

Im Kontoauszug-Schnellbearbeitungsmodus soll `Strg + Pfeil hoch` bzw. `Strg + Pfeil runter` den Fokus zeilenübergreifend in derselben Feldspalte verschieben. Die Tastenkombinationen gelten ausschließlich für die Eingabefelder von `QuickEditTable`.

## Technischer Ansatz

1. `QuickEditTable.razor` erweitert den bestehenden `OnKeyDown`-Handler um die Erkennung von `Ctrl` mit `ArrowUp` bzw. `ArrowDown`. Die Prüfung erfolgt vor der bestehenden F8-Verarbeitung; F8 und `Strg + F8` bleiben unverändert.
2. Für ein gültiges Ziel wird die aktuelle Position in `StatementDraftEntriesListViewModel.VisibleQuickEditItems` gesucht und um eine Zeile in die angeforderte Richtung verschoben. Damit werden ausgeblendete bzw. zum Löschen markierte Zeilen sowie nicht editierbare Statuszeilen entsprechend der bestehenden Schnellbearbeitungsreihenfolge behandelt.
3. Die Feldspalte wird über den bestehenden Feldnamen und `GetElementId` beibehalten. Das Ziel erhält damit die passende ID (`qe_booking_...`, `qe_valuta_...`, `qe_amount_...`, `qe_description_...`, `qe_recipient_...` oder `qe_subject_...`).
4. Nur bei einer vorhandenen Nachbarzeile wird eine dedizierte JS-Interop-Fokusaktion für die Ziel-ID ausgelöst. Am Anfang, am Ende oder bei einer ungültigen Ausgangsposition beendet der Handler die Verarbeitung ohne DOM-Aufruf, State-Änderung oder Fokusverlust.
5. Die bestehende Initialfokuslogik und die übrigen Eingabe-, Validierungs- und Übernahmehandler werden nicht verändert.

## Betroffene Dateien

- `FinanceManager.Web/Shared/QuickEdit/QuickEditTable.razor`: Tastenkombination, Nachbarzeile und Fokusziel.
- `FinanceManager.Tests.E2E/Tests/StatementDrafts/StatementDraftQuickEditValueTakeoverE2ETests.cs`: E2E-Abdeckung des Schnellbearbeitungsflusses und der Fokusnavigation.
- Eine Änderung an ViewModel, Backend, DTOs oder Persistenz ist nicht vorgesehen; `VisibleQuickEditItems` wird als bestehende Quelle der sichtbaren Reihenfolge verwendet.

## Konkrete E2E-Tests

Die Tests öffnen einen vorbereiteten Kontoauszug über `?quickEdit=true`, warten auf die gerenderten Schnellbearbeitungsfelder und lösen das Keyboard-Event am konkreten Input aus. Der Fokus wird anschließend über `document.activeElement.id` geprüft.

1. `QuickEdit_CtrlArrowUp_ShouldFocusSameFieldInPreviousVisibleRow`: Bei mindestens drei sichtbaren Schnellbearbeitungszeilen `qe_amount_<mittlereZeile>` fokussieren, `Ctrl + ArrowUp` auslösen und erwarten, dass `document.activeElement.id` exakt `qe_amount_<vorherigeZeile>` entspricht.
2. `QuickEdit_CtrlArrowDown_ShouldFocusSameFieldInNextVisibleRow`: Von `qe_subject_<mittlereZeile>` `Ctrl + ArrowDown` auslösen und exakt `qe_subject_<naechsteZeile>` erwarten. Damit wird die Feldspaltentreue für eine andere Spalte und die Gegenrichtung geprüft.
3. `QuickEdit_CtrlArrowUpAtFirstRow_ShouldKeepFocus`: Das erste sichtbare Feld fokussieren, `Ctrl + ArrowUp` auslösen und sicherstellen, dass die aktive ID unverändert bleibt.
4. `QuickEdit_CtrlArrowDownAtLastRow_ShouldKeepFocus`: Das letzte sichtbare Feld fokussieren, `Ctrl + ArrowDown` auslösen und sicherstellen, dass die aktive ID unverändert bleibt.
5. `QuickEdit_RegularInputAndF8_ShouldRemainUnaffected`: Eine normale Eingabe sowie die bestehenden F8- und `Ctrl + F8`-Szenarien ausführen und sicherstellen, dass Werte weiterhin unverändert korrekt verarbeitet werden.
6. `CtrlArrowNavigation_OutsideQuickEdit_ShouldNotChangeFocus`: Einen Kontoauszug ohne Schnellbearbeitungsmodus öffnen, ein vorhandenes Eingabefeld fokussieren, `Ctrl + ArrowUp` bzw. `Ctrl + ArrowDown` auslösen und bestätigen, dass keine Quick-Edit-Navigation stattfindet.

Falls der Testaufbau ausgeblendete oder nicht editierbare Einträge erzeugen kann, wird zusätzlich geprüft, dass die Navigation den nächsten Eintrag aus `VisibleQuickEditItems` fokussiert und kein Status-/Aktionsfeld als Ziel verwendet.

## Abnahmekriterien für die Implementierung

- Beide Richtungen verschieben den Fokus um genau eine sichtbare Schnellbearbeitungszeile.
- Die Feldspalte bleibt beim Wechsel unverändert.
- Listenanfang und Listenende sind sichere No-ops; der aktuelle Fokus bleibt gültig.
- Die Funktion wirkt ausschließlich im aktiven Schnellbearbeitungsmodus.
- F8, `Strg + F8`, normale Eingaben, Validierung und bestehende Initialfokusaktionen bleiben funktionsfähig.
- Die oben beschriebenen E2E-Tests laufen erfolgreich durch.

## Verifikation

Nach der Implementierung werden die fokussierten E2E-Tests der Statement-Draft-Suite ausgeführt. Zusätzlich werden Build bzw. bestehende automatisierte Tests des betroffenen Web- und E2E-Projekts ausgeführt, soweit die lokale Testumgebung dies unterstützt. Testergebnisse werden in `test-results.md` dokumentiert.

## Offene Punkte

Keine.
