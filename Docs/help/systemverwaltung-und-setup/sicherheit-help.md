# Systemverwaltung und Setup - Help-Dokumentation und Sicherheit

## Zweck

Die Help-Oberflaeche rendert Markdown-Dokumente zentral und stellt sie ueber
die Route `/help/view/...` bereit. Help-Inhalte werden dabei als nicht
vertrauenswuerdige Eingabe behandelt. Die Ausgabe darf deshalb nur ueber den
zentralen Renderer erfolgen.

## Inhaltserstellung

Help-Quellen liegen unter `Docs/help/` und werden im vorgesehenen Build-Prozess
als statische Artefakte erfasst. Frontmatter wird vor dem Markdown-Rendering
entfernt. Markdown wird mit einer eingeschraenkten Pipeline verarbeitet;
eingebettetes HTML ist deaktiviert.

Unterstuetzt werden insbesondere Ueberschriften, Absaetze, Listen,
Blockquotes, Hervorhebungen, Code, Tabellen und Links. Rohes HTML, Skripte,
Event-Handler, Inline-Styles, Formulare und eingebettete Inhalte sind kein
unterstuetztes Help-Format.

## Links in Help-Dokumenten

Relative Links auf Markdown-Dokumente werden ausgehend vom aktuellen Dokument
aufgeloest und sicher auf `/help/view/...` abgebildet. Dabei werden nur gueltige
Help-Pfadsegmente und sichere Fragment-Bezeichner akzeptiert. Pfade ausserhalb
des Help-Bereichs, absolute Nicht-HTTP-Ziele und ungueltige Markdown-Ziele
werden nicht als interne Help-Route uebernommen.

HTTP- und HTTPS-Links koennen als externe Ziele verwendet werden. Externe
Links erhalten `target="_blank"` sowie `rel="noopener noreferrer"`. Fuer
JavaScript-, Daten- und andere nicht erlaubte URL-Schemata wird kein Linkziel
ausgegeben.

## Rendering und API

`HelpContentRenderer` bildet die zentrale Sicherheitsgrenze fuer Markdown und
Legacy-HTML. Das Ergebnis wird explizit ueber eine HTML-Whitelist sanitiziert,
bevor es als `MarkupString` in der Help-Ansicht ausgegeben wird. Die Markdown-
und Legacy-API verwenden dieselbe Sanitization. Dateipfade, Sprache und
Suchindex-Eintraege werden vor der Ausgabe validiert; Rohdateien werden nicht
als Suchindex- oder HTML-Antwort durchgereicht.

Die Suche verarbeitet den validierten Suchindex als Datenmodell. Trefferkarten,
Texte und Fehlermeldungen werden mit DOM-Methoden und `textContent` erzeugt.
Indexwerte werden nicht als HTML oder Inline-JavaScript in den DOM interpoliert.
Die Navigation akzeptiert nur validierte Help-Feature-IDs.

## Content Security Policy

Help-Seiten, Help-Assets und Help-API-Antworten erhalten eine restriktive
Content Security Policy:

```text
default-src 'self'; script-src 'self'; style-src 'self'; img-src 'self' data:;
connect-src 'self' ws: wss:; object-src 'none'; base-uri 'self';
frame-ancestors 'self'; form-action 'self'
```

Damit werden Inline-Skripte und Inline-Styles nicht als Help-Bestandteil
freigegeben. Help-JavaScript und Help-CSS werden als externe, statische Assets
ausgeliefert. Legacy-HTML unter dem Help-Pfad unterliegt derselben
Schutzgrenze.

## Build und Integritaet

Der Web-Build nimmt nur die vorgesehenen Help-CSS-, Help-JavaScript-, JSON-,
Legacy-HTML- und `Docs/help/**/*.md`-Dateien in die Help-Artefakte auf. Vor dem
Build wird fuer diese Dateien ein SHA-256-Manifest unter
`wwwroot/help/help-assets.sha256` erzeugt.

Zur Laufzeit werden Help-Dateien gegen dieses Manifest geprueft. Nicht
aufgelistete Dateien oder Hash-Abweichungen gelten als nicht vertrauenswuerdig
und werden nicht ausgeliefert. Eine Aenderung an Help-Inhalten erfordert daher
einen neuen Build mit einem aktualisierten Manifest.

## Betriebshinweise

- Neue Help-Seiten als Markdown unter `Docs/help/` anlegen und im passenden
  `index.md` verlinken.
- Keine vertraulichen Daten, Skripte oder HTML-Umgehungen in Help-Quellen
  hinterlegen.
- Relative interne Verweise mit dem Ziel einer `.md`-Datei schreiben; die
  Anwendung uebernimmt die Abbildung auf die Help-Route.
- Nach Aenderungen Build und Help-Sicherheits- sowie Integritaetstests
  ausfuehren.

← [Zurueck zu Systemverwaltung und Setup](index.md)
