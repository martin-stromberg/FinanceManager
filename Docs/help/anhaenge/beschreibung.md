← [Zurück zur Übersicht](index.md)

# Anhänge — Beschreibung

## Zweck

Der Bereich verwaltet Dateien und URLs als Anhänge zu Fachobjekten (z. B. Kontakte, Konten, Sparpläne, Wertpapiere).

## Funktionsweise

Anhänge werden über `AttachmentsController` bereitgestellt. Neben Datei-Upload und Download sind Metadatenpflege, Kategorisierung und Symbolzuordnungen möglich.

## Beispiele

- Vertragsdokument als Anhang zu einem Sparplan.
- Symbolgrafik als spezieller Anhang für ein Wertpapier.
- Nachträgliche Zuordnung eines Anhangs zu einer Kategorie.

## Einschränkungen

- Ein Anhang benötigt Inhalt, URL oder eine Referenz auf einen anderen Anhang.
- Zugriff ist an Entität und Benutzerkontext gebunden.
