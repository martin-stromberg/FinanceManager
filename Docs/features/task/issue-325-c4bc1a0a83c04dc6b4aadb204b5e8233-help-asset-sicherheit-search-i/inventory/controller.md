# HelpController und Search Index

## Relevante Stellen

- `FinanceManager.Web/Controllers/HelpController.cs:144-180` implementiert `GET /api/help/search-index/{language}.json`.
- Die Sprache wird auf `de` oder `en` normalisiert (`HelpController.cs:369-373`).
- Der physische Pfad ist `IWebHostEnvironment.WebRootPath/help/{language}/search-index.json`.
- Wenn die Datei fehlt, erzeugt `GenerateSearchIndex` (`HelpController.cs:204-239`) aus `Docs/help` einen Index und liefert ihn direkt mit `200 OK`.
- Wenn die Datei vorhanden ist, wird sie zuerst ueber `IHelpAssetIntegrityValidator.IsTrustedHelpFile` geprueft und danach als JSON geparst.

## Aktuelles Verhalten

Der statische Pfad ist an die Manifestpruefung gebunden; der Missing-File-Zweig umgeht diese Grenze jedoch vollstaendig. Dadurch kann ein fehlendes oder nicht gebautes `search-index.json` trotz fehlendem Manifest als dynamischer `OK`-Response erscheinen. Ein vorhandener, manipulierter Index wird dagegen als `NotFound` behandelt, sofern der Validator den Hashvergleich ausfuehrt.

## Abhaengigkeiten

- `HelpDocumentPathResolver.GetHelpSourcePath` erwartet `Docs/help` relativ zum Content Root des Webprojekts (`HelpDocumentPathResolver.cs:15-18`).
- Die Suche nutzt die zentralen Sprachregeln des Controllers, waehrend `ProgramExtensions.BuildLocalizationOptions` die Kulturen nochmals als `de` und `en` definiert.
- Die DTO-Ausgabe wird aus dem JSON-Feld `documents` aufgebaut; ungueltige Eintraege werden verworfen, ein strukturell ungueltiges Dokument fuehrt zu `BadRequest`.

## Fuer die Umsetzung zu klaeren

- Ob der Fallback vollstaendig entfaellt und fehlende Assets immer `NotFound` liefern.
- Alternativ muesste der Build vor App- und Teststart deterministisch fuer jede unterstuetzte Sprache erzeugen und manifestieren; auch dann sollte kein ungeschuetzter Laufzeit-Fallback bestehen bleiben.
