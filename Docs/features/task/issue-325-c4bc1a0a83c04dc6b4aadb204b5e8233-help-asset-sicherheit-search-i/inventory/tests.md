# Tests und TestWebApplicationFactory

## Unit- und Integrationstests

- `FinanceManager.Tests/Controllers/HelpControllerSecurityTests.cs:126-144` erwartet derzeit explizit den dynamischen `OK`-Fallback bei fehlendem statischem Index.
- `HelpControllerSecurityTests.cs:235-264` prueft, dass ein manifestierter, danach manipulierter Index als `NotFound` endet.
- Weitere Controller-Tests pruefen JSON-Struktur, das Entfernen ungueltiger Dokumente sowie Sprach- und Inhaltsvalidierung.
- `FinanceManager.Tests/Web/Help/HelpAssetIntegrityValidatorTests.cs:20-119` deckt fehlendes Manifest, nicht gelistete Dateien, Hashabweichungen, Rehashing und die Abdeckung aller gelieferten Help-Assets ab.
- `FinanceManager.Tests.Integration/HelpSecurityMiddlewareTests.cs:108-206` prueft fehlendes Manifest sowie Manipulationen an CSS, JavaScript und `de/search-index.json` ueber echte HTTP-Requests.

## TestWebApplicationFactory

`FinanceManager.Tests.Integration/TestWebApplicationFactory.cs` erweitert `WebApplicationFactory<Program>`, setzt die Umgebung auf `Development`, deaktiviert Hintergrunddienste und konfiguriert eine isolierte SQLite-Datenbank. Es gibt keine explizite `UseContentRoot`- oder `UseWebRoot`-Konfiguration. Die HTTP-Tests greifen fuer Dateimutationen dagegen ueber `AppContext.BaseDirectory/../../../../FinanceManager.Web/wwwroot/help` direkt auf das Quellverzeichnis zu.

Damit ist die Testannahme implizit: Die laufende Factory muss das Webprojekt beziehungsweise dessen statische Asset-Ausgabe verwenden, die von diesen Pfaden aus erreichbar ist. Eine abweichende Output-Kopie oder ein anderer Content Root kann dazu fuehren, dass Mutation, Middleware und Controller verschiedene physische Dateien sehen.

## Testluecken und erwartete Anpassungen

- Der bestehende Missing-Index-Test muss an den gewaehlten Vertrag angepasst werden: `NotFound` bei fehlendem Asset oder Build-Voraussetzung ohne Runtime-Fallback.
- Es fehlen explizite Regressionen fuer fehlende `search-index.json` in beiden unterstuetzten Sprachen ueber Controller und HTTP-Pipeline.
- Fuer die Buildregel sollte ein Test sicherstellen, dass beide Search-Index-Dateien erzeugt, im Manifest gelistet und mit korrektem Hash ausgeliefert werden.
- Die Factory sollte ihren effektiven Content Root/WebRoot explizit an die getestete Asset-Quelle binden, damit Manipulationstests nicht vom aktuellen Buildlayout abhaengen.

