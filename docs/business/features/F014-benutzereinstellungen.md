# F014 – Benutzereinstellungen

## Einleitung

In den Benutzereinstellungen passen Sie wichtige Regeln für Ihren Arbeitsalltag an.  
Sie steuern hier auch den Bestätigungsdialog beim Massenimport auf der Startseite.  
Damit entscheiden Sie, wie viel Kontrolle vor der Verarbeitung nötig ist.  
Die Einstellungen gelten für Ihr Benutzerkonto.

## Wer nutzt es?

Alle Nutzer der Anwendung nutzen diesen Bereich.  
Teamleitungen legen oft eine klare Dialog-Regel für das Tagesgeschäft fest.  
Neue Mitarbeitende nutzen die Vorgabe als sichere Führung.

## Schritt-für-Schritt-Anleitung

1. Sie klicken im **Menü** auf **Setup** oder **Einstellungen**.
2. Sie öffnen den Bereich **Import-Aufteilung**.
3. Sie wählen bei **Bestätigungsdialog für Massenimport** Ihre Regel.
4. Sie wählen **Immer bestätigen**, wenn immer ein Prüf-Dialog erscheinen soll.
5. Sie wählen **Nur bei fehlenden Angaben**, wenn der Dialog nur bei Unklarheiten erscheint.
6. Sie klicken auf **Speichern**.
7. Sie starten später einen Import auf der **Startseite** und prüfen das Verhalten.
8. Sie beachten: Der Einzelimport auf der Wertpapier-**Kursseite** bleibt davon unberührt.

## Beispiel

Ihr Team importiert täglich viele Dateien.  
Sie möchten jede Datei vorher sehen und bewusst freigeben.  
Sie wählen in **Import-Aufteilung** die Regel **Immer bestätigen**.  
Ab dann erscheint vor jedem Startseiten-Massenimport der Prüf-Dialog.

## Was passiert im Hintergrund?

Die Anwendung speichert Ihre Regel im Benutzerkonto.  
Beim Startseiten-Massenimport prüft sie erst alle Dateien.  
Dann entscheidet sie nach Ihrer Regel, ob ein Dialog erscheint.  
Bei Kursdateien prüft sie die Wertpapier-Zuordnung vor dem finalen Start erneut.

## Häufige Fragen (FAQ)

**F: Wo stelle ich den Bestätigungsdialog ein?**  
A: In **Setup** > **Import-Aufteilung** bei **Bestätigungsdialog für Massenimport**.

**F: Wann erscheint der Dialog bei „Nur bei fehlenden Angaben“?**  
A: Wenn wichtige Angaben fehlen, zum Beispiel eine klare Wertpapier-Zuordnung.

**F: Wirkt die Regel auch auf den Import auf der Kursseite?**  
A: Nein. Der Einzelimport auf der Kursseite bleibt ein eigener Ablauf.

**F: Was passiert bei gemischten Dateien im Massenimport?**  
A: Sie sehen je Datei Typ, Importweg, Zuordnung und Ausschluss-Option im Dialog.

**F: Gibt es dafür automatische Prüfungen?**  
A: Ja. Es gibt Prüfungen einzelner Bausteine und des gesamten Zusammenspiels.

## Verwandte Funktionen

- [F007 – Wertpapierpreise (ING-CSV-Import)](./F007-wertpapierpreise-ing-csv-import.md)
- [F004 – Kontoauszug-Import](./F004-kontoauszug-import.md)
- [F013 – Benachrichtigungen](./F013-benachrichtigungen.md)
- [F015 – Datensicherung](./F015-datensicherung.md)
