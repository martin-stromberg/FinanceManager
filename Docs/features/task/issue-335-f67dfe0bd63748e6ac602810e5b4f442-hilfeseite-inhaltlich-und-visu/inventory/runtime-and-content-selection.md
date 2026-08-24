# Laufzeit und Inhaltsauswahl

## Routen

- `HelpHub.razor` ist unter `/help` registriert.
- `HelpPageView.razor` ist unter `/help/view/{*HelpPath}` registriert.
- Beide Seiten verwenden `HelpLayout.razor`, das derzeit nur einen `main`-Container um den Seiteninhalt legt.

## Hub-Auswahl

`HelpHub.razor` ermittelt den absoluten Quellpfad ueber `HelpDocumentPathResolver.GetHelpSourcePath`, baut mit `HelpSearchIndexBuilder.Build` den Index und filtert IDs, Titel und normalisierbare Pfade. Der Builder iteriert ueber alle Unterverzeichnisse von `Docs/help` und erzeugt genau einen Suchdatensatz pro Verzeichnis.

Die Karten verlinken auf `/help/view/{document.Id}`. Der Hub zeigt somit keine Auswahl einzelner Dateien, sondern eine Auswahl von Themen-IDs.

## Detail-Auswahl

`FindMarkdownFile` verwendet fuer einen einteiligen Pfad zuerst ein vorhandenes Themenverzeichnis und waehlt darin in dieser Reihenfolge: lokalisierter Index, lokalisierte Themen-Datei, `index.md`, Themen-Datei ohne Sprache und danach die erste nicht-englische Markdown-Datei.

Bei mehrteiligen Pfaden darf die letzte Route-Komponente einen beliebigen Markdown-Dateinamen referenzieren. Die Pfadnormalisierung verhindert Traversal und ungueltige Segmente, aber nicht die Auswahl eines fachlich-technischen Dokuments.

## Konsequenz fuer die Anforderung

Die Inhaltsgrenze muss vor oder innerhalb von `FindMarkdownFile` liegen. Hub, Suchindex und Detailseite muessen auf dieselbe Allowlist bzw. denselben redaktionellen Katalog zugreifen, damit keine abweichenden Primaerquellen entstehen.
