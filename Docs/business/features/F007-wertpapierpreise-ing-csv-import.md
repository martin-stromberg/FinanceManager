# F007 – Wertpapierpreise (ING-CSV-Import)

## Einleitung

Mit dieser Funktion importieren Sie ING-Kursdateien schnell und sicher.  
Sie nutzen dafür zwei Wege mit klaren Rollen.  
Auf der **Kursseite** importieren Sie eine Datei direkt in ein Wertpapier.  
Auf der **Startseite** verarbeiten Sie viele Dateien in einem Durchlauf.  
Bei Bedarf zeigt die Anwendung vor dem Start einen Prüf-Dialog.

## Wer nutzt es?

Diese Funktion nutzen Fachanwender in der Wertpapierpflege.  
Sie arbeiten oft mit mehreren Dateien aus Bankportalen.  
Neue Mitarbeitende nutzen den Dialog als sichere Kontrolle vor dem Import.

## Schritt-für-Schritt-Anleitung

### A) Einzelimport auf der Kursseite eines Wertpapiers

1. Sie öffnen ein Wertpapier und wechseln zur **Kursseite**.
2. Sie klicken auf **Import Prices**.
3. Sie wählen eine ING-Datei.
4. Sie starten den Import mit **Importieren**.
5. Sie prüfen das Ergebnis mit neuen, geänderten und unveränderten Kurstagen.

### B) Massenimport auf der Startseite

1. Sie öffnen die **Startseite** und starten den Import über **Datei importieren**.
2. Sie wählen mehrere Dateien in einem Schritt.
3. Die Anwendung prüft zuerst alle Dateien.
4. Falls nötig, sehen Sie den Dialog **Massenimport prüfen** vor dem Start.
5. Sie prüfen je Datei **Dateityp** und **Importservice**.
6. Bei Kursdateien wählen Sie im Feld **Wertpapier** die richtige Zuordnung.
7. Sie setzen **Ausschließen**, wenn eine Datei nicht verarbeitet werden soll.
8. Sie starten mit **Bestätigen** nur die freigegebenen Dateien.
9. Sie prüfen danach den Status je Datei, zum Beispiel importiert, übersprungen oder fehlgeschlagen.

## Beispiel

Sie laden vier Dateien auf der Startseite hoch.  
Zwei Dateien sind Kontoauszüge, zwei enthalten Kurse.  
Eine Kursdatei hat keine klare Zuordnung zu einem Wertpapier.  
Der Prüf-Dialog erscheint und fordert Ihre Auswahl im Feld **Wertpapier**.  
Sie schließen eine falsche Datei aus und bestätigen den Rest.  
Drei Dateien laufen durch, eine bleibt bewusst unberührt.

## Was passiert im Hintergrund?

Die Anwendung analysiert zuerst alle ausgewählten Dateien.  
Danach startet sie erst nach Ihrer Freigabe, wenn ein Dialog nötig ist.  
Vor dem finalen Kursimport prüft sie die Wertpapier-Zuordnung erneut.  
So verhindert sie falsche Zuordnungen bei zwischenzeitlichen Änderungen.  
Jede Datei erhält einen klaren Abschlussstatus.

## Hinweis zur Qualität

Für diese Funktion gibt es automatische Prüfungen auf Baustein-Ebene.  
Zusätzliche Prüfungen testen den Ablauf über mehrere Bereiche gemeinsam.

## Häufige Fragen (FAQ)

**F: Worin liegt der Unterschied zwischen Kursseite und Startseite?**  
A: Die Kursseite importiert eine Datei in ein bestimmtes Wertpapier. Die Startseite verarbeitet viele Dateien zusammen.

**F: Wann erscheint der Dialog vor dem Massenimport?**  
A: Das hängt von Ihrer Regel in **Setup** > **Import-Aufteilung** ab.

**F: Kann ich einzelne Dateien im Dialog ausschließen?**  
A: Ja. Setzen Sie je Datei das Feld **Ausschließen**.

**F: Wird eine Wertpapier-Zuordnung nur einmal geprüft?**  
A: Nein. Die Anwendung prüft sie vor dem endgültigen Import erneut.

**F: Was passiert bei Teilfehlern im Massenimport?**  
A: Erfolgreiche Dateien bleiben importiert. Fehlerhafte Dateien zeigen einen klaren Status und Hinweis.

## Verwandte Funktionen

- [F006 – Wertpapier-Verwaltung](./F006-wertpapier-verwaltung.md)
- [F014 – Benutzereinstellungen](./F014-benutzereinstellungen.md)
- [F004 – Kontoauszug-Import](./F004-kontoauszug-import.md)
