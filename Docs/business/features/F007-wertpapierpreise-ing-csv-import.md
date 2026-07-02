# F007 – Wertpapierpreise ING-CSV-Import

## Einleitung

Mit dieser Funktion importieren Sie Kursdaten aus einer ING-CSV direkt in ein Wertpapier.  
Sie sparen Zeit bei der Pflege Ihrer Kursverläufe.  
Der Import erkennt automatisch neue, geänderte und bereits passende Tageskurse.  
So bleibt Ihre Auswertung verlässlich und aktuell.
Die Aktion liegt auf der **Kursseite** eines Wertpapiers (nicht auf der allgemeinen Wertpapier-Detailseite).

## Wer nutzt es?

Diese Funktion nutzen Fachanwender in der Portfolio-Pflege.  
Typisch sind Mitarbeitende, die Kursdaten regelmäßig aus Bankdokumenten übernehmen.

## Schritt-für-Schritt-Anleitung

1. Sie öffnen die **Kursseite** eines bestehenden Wertpapiers.
2. Sie klicken in der Aktionsleiste auf **Import Prices**.
3. Sie wählen Ihre ING-CSV-Datei aus.
4. Sie starten den Import mit **Importieren**.
5. Sie warten auf die Ergebnisanzeige.
6. Sie prüfen die Zähler für neue, geänderte und unveränderte Tage.
7. Sie prüfen bei Bedarf die angezeigten Zeilenhinweise.

## So werden Ihre Zeilen behandelt

- **Neue Preise:** Für einen noch fehlenden Tag wird ein neuer Tageskurs angelegt.  
- **Aktualisierte Preise:** Für einen vorhandenen Tag wird der Kurs ersetzt, wenn der Betrag abweicht.  
- **Unveränderte Preise:** Für einen vorhandenen Tag mit gleichem Betrag bleibt alles unverändert.  
- **Übersprungene Zeilen:** Ungültige Zeilen werden nicht übernommen und klar gemeldet.

## Erwartete Ergebnisse

Nach dem Import sehen Sie eine klare Zusammenfassung direkt im Bildschirm.  
Sie erkennen sofort, wie viele Tage neu, geändert oder unverändert sind.  
Fehlerhafte Zeilen sehen Sie einzeln mit Hinweis zur betroffenen Zeile.

## Benutzerhinweise bei Problemen

- Leere Datei: Sie erhalten einen Hinweis, dass keine gültige Datei vorliegt.  
- Falsches Dateiformat: Sie erhalten einen Hinweis auf ein nicht unterstütztes Format.  
- Keine gültigen Zeilen: Sie erhalten einen Hinweis, dass keine importierbaren Kurszeilen gefunden wurden.  
- Falsches Wertpapier oder kein Zugriff: Das System zeigt an, dass das Wertpapier nicht gefunden wurde.

## Beispiel

Sie importieren eine ING-Datei mit 120 Zeilen für ein Wertpapier.  
Das Ergebnis zeigt 80 neue Tage, 15 geänderte Tage und 20 unveränderte Tage.  
5 Zeilen werden übersprungen, weil dort Werte fehlen.  
Sie prüfen die Hinweise und korrigieren nur diese 5 Zeilen.

## Was passiert im Hintergrund?

Die Anwendung liest jede Zeile aus Ihrer Datei und ordnet sie einem Kalendertag zu.  
Danach vergleicht sie den Tag mit vorhandenen Kursen des gewählten Wertpapiers.  
Nur wirklich neue oder geänderte Werte werden übernommen.

## Häufige Fragen (FAQ)

**F: Muss ich vor jedem Import alte Kurse löschen?**  
A: Nein. Die Funktion ergänzt und korrigiert bestehende Tageskurse automatisch.

**F: Was passiert bei einem erneuten Import derselben Datei?**  
A: Bereits gleiche Werte bleiben unverändert. Es entstehen keine doppelten Tage.

**F: Werden fehlerhafte Zeilen trotzdem teilweise übernommen?**  
A: Nein. Fehlerhafte Zeilen werden übersprungen und als Hinweis gelistet.

**F: Sehe ich nach dem Import sofort das Ergebnis?**  
A: Ja. Die Zusammenfassung erscheint direkt nach Abschluss des Imports.

## Verwandte Funktionen

- [F006 – Wertpapier-Verwaltung](./F006-wertpapier-verwaltung.md)
- [F007 – Wertpapierpreise (Übersicht)](./F007-wertpapierpreise.md)
- [Anforderungen: Wertpapierkurse ING-CSV-Import](../../requirements/wertpapierkurse-ing-requirements.md)
- [Architektur-Blueprint: Wertpapierkurse ING-CSV-Import](../../architecture/architecture-blueprint-wertpapierkurse-ing.md)
- [Testplan: Wertpapierkurse ING](../../tests/wertpapierkurse-ing-testplan.md)
