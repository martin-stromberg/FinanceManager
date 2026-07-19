# Tests und Test-Hilfsmethoden

## Testklassen

Bei der Suche in `FinanceManager.Tests`, `FinanceManager.Tests.Integration` und `FinanceManager.Tests.E2E` wurden keine Help-spezifischen Testklassen oder Testmethoden für `HelpController`, `HelpPageView`, `HelpPageManager`, Markdown-Ausgabe, Suchindex-Ausgabe oder CSP gefunden.

Die vorhandene `FinanceManager.Tests.Integration/TestWebApplicationFactory.cs` stellt eine allgemeine `WebApplicationFactory<Program>` für Integrations-Tests bereit. Sie konfiguriert unter anderem eine In-Memory-SQLite-Datenbank und deaktiviert Hintergrunddienste, enthält aber keine Help-spezifische Einrichtung oder Prüfung.

## Hilfsmethoden

Es wurden keine Help-spezifischen Test-Hilfsmethoden gefunden. Allgemeine E2E-Hilfen wie `BrowserApiHelper` und `AuthGateway` sind für andere API- beziehungsweise Navigationsszenarien vorhanden, werden aber in den gefundenen Dateien nicht für Help-Inhalte verwendet.

