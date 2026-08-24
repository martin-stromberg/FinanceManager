# Help-Asset-Integritaet und Middleware

## Validator

`FinanceManager.Web/Services/Help/HelpAssetIntegrityValidator.cs` laedt `wwwroot/help/help-assets.sha256` relativ zu `IWebHostEnvironment.ContentRootPath`. Fuer jede Datei wird der relative Manifestschluessel normalisiert und der aktuelle SHA-256-Hash mit dem Manifestwert verglichen. Fehlende Manifestzeilen, fehlende Dateien und Hashabweichungen werden abgelehnt.

Der Validator ist als Singleton registriert (`FinanceManager.Web/ProgramExtensions.cs:154-155`) und cached das Manifest lazy. Aenderungen am Manifest waehrend eines Prozesses werden daher nicht neu eingelesen; die Tests erzeugen fuer Mutationen jeweils eine neue Factory bzw. einen neuen Validator.

## Inline-Middleware statt eigener Klasse

Eine Klasse `HelpSecurityMiddleware` existiert im aktuellen Quellbaum nicht. Die Funktion liegt in `ProgramExtensions.ConfigureMiddleware`:

- CSP fuer Help-UI, Help-Assets und Help-API wird bei Help-Pfaden gesetzt (`ProgramExtensions.cs:414-422`).
- Statische Dateien unter `/help` mit Dateiendung werden vor `UseStaticFiles` in eine physische Datei unter `app.Environment.WebRootPath` aufgeloest und validiert (`ProgramExtensions.cs:424-446`).
- Unbekannte, fehlende oder nicht im Manifest enthaltene Assets enden mit HTTP 404.

Die Controller-Endpunkte validieren ihre eigenen Dateien nochmals. Die Middleware schuetzt nur statische `/help/...`-Requests; `/api/help/...` wird nicht als statisches Asset behandelt.

## Risiko fuer diese Anforderung

Controller und Middleware muessen exakt denselben WebRoot und denselben relativen Manifestpfad verwenden. Der aktuelle Validator berechnet Schluessel relativ zum Content Root, waehrend die Middleware den Pfad relativ zum WebRoot bildet und anschliessend den Validator aufruft. Das funktioniert nur, wenn das Manifest unter `ContentRoot/wwwroot/help` liegt und die Build-Ausgabe diesen Pfad unveraendert beibehalt.

