← [Zurück zur Übersicht](index.md)

# Anhänge — Beschreibung

## Zweck

Der Bereich verwaltet Dateien und URLs als Anhänge zu Fachobjekten (z. B. Kontakte, Konten, Sparpläne, Wertpapiere).

## Funktionsweise

Anhänge werden über `AttachmentsController` bereitgestellt. Neben Datei-Upload und Download sind Metadatenpflege, Kategorisierung und Symbolzuordnungen möglich. Symbolanhänge können als Bilddateien, einschließlich sicherer SVG-Dateien, hochgeladen und in der Oberfläche angezeigt werden.

## Beispiele

- Vertragsdokument als Anhang zu einem Sparplan.
- Symbolgrafik als spezieller Anhang für einen Kontakt oder ein Wertpapier.
- SVG-Datei als Kontaktsymbol, wenn sie keine unsicheren Inhalte enthält.
- Nachträgliche Zuordnung eines Anhangs zu einer Kategorie.

## Einschränkungen

- Ein Anhang benötigt Inhalt, URL oder eine Referenz auf einen anderen Anhang.
- Zugriff ist an Entität und Benutzerkontext gebunden.
- Unsichere SVG-Inhalte werden beim Upload serverseitig abgelehnt.
