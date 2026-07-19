# Code-Review: Help-Markdown sicher rendern

## Status

Befunde vorhanden

## Befunde

1. **Mittel - Sanitization-Abhaengigkeit bleibt verwundbar bzw. uneinheitlich aufgeloest:** Das Web-Projekt bindet `HtmlSanitizer` als zentrale Sicherheitsgrenze ein (`FinanceManager.Web/FinanceManager.Web.csproj:42`). `dotnet list FinanceManager.Web/FinanceManager.Web.csproj package --vulnerable --include-transitive` meldet fuer genau dieses Projekt weiterhin `AngleSharp 0.17.1` mit moderater Sicherheitsanfaelligkeit `GHSA-pgww-w46g-26qg`; `dotnet build` meldet dieselbe `NU1902`-Warnung. Zusaetzlich laufen die Unit-Tests mit `NU1608`, weil dort fuer `HtmlSanitizer 9.0.892` andere `AngleSharp`-/`AngleSharp.Css`-Versionen aufgeloest werden als von der Paketabhaengigkeit gefordert. Damit ist ausgerechnet der neue Sanitizer-Pfad nicht auf einer kompatiblen, sicher bewerteten Dependency-Basis nachgewiesen. Vor Abschluss von F-010 sollte entweder auf eine kompatible, nicht verwundbare Sanitizer-/Parser-Kombination aktualisiert oder die konkrete Advisory-Auswirkung fuer die genutzten Markdown-/Legacy-HTML-Pfade dokumentiert akzeptiert werden.

## Geschlossene Befunde aus `review-code.2.md`

1. **Reale interne Help-Links:** geschlossen. `HelpContentRenderer` schreibt relative `.md`-Links nun relativ zum aktuellen Dokumentpfad in `/help/view/...` um (`FinanceManager.Web/Services/Help/HelpContentRenderer.cs:99-115`, `:198-300`). Reale Top-Level-, Abschnitts- und Ruecklinks aus `Docs/help` sind durch Renderer-Tests abgedeckt (`FinanceManager.Tests/Web/Help/HelpContentRendererTests.cs:57`, `:70`, `:83`) und der HTTP-Pfad `/help/view/budgetplanung` wird gegen reale relative Links getestet (`FinanceManager.Tests.Integration/HelpSecurityMiddlewareTests.cs:59`).
2. **Help-CSP gegen globale Blazor-Ausgabe:** im aktuellen Code geschlossen. Help-Routen erhalten den CSP-Header (`FinanceManager.Web/ProgramExtensions.cs:361-369`), und `App.razor` rendert auf `/help` und `/help/...` weder `<ImportMap />` noch `_framework/blazor.web.js` (`FinanceManager.Web/Components/App.razor:4-8`, `:69-88`). Die Integrationstests pruefen Help-HTML ohne Inline-Skripte und ohne Blazor-/ImportMap-Ausgabe (`FinanceManager.Tests.Integration/HelpSecurityMiddlewareTests.cs:22`, `:40`).

## Weitere Beobachtungen

- Die statische Help-Asset-Auslieferung wird vor `UseStaticFiles()` gegen das Manifest geprueft und unbekannte Assets werden mit `404` blockiert (`FinanceManager.Web/ProgramExtensions.cs:371-390`).
- Das Manifest wird beim Build aus den vorgesehenen Help-CSS-/JS-/JSON-/HTML-Dateien sowie `Docs/help/**/*.md` erzeugt (`FinanceManager.Web/FinanceManager.Web.csproj:80-91`).
- Suche und Suchindex wurden gegen DOM-XSS gestaerkt: `help-search.js` nutzt DOM-APIs statt `innerHTML`, und Controller-Tests decken ungueltige Suchindex-Dokumente sowie manipulierte JSON-Dateien mit echtem Validator ab (`FinanceManager.Tests/Controllers/HelpControllerSecurityTests.cs:136`, `:185`, `:217`, `:231`).

## Fehlende Tests

- Kein echter Browser-/E2E-Test laedt `/help` und `/help/view/...` unter der CSP und prueft Konsole/CSP-Verletzungen, Suche, interne Links, externe Links, manipulierte Indexwerte sowie deutsche und englische Oberflaeche.
- Die HTTP-Integritaetsabdeckung ist verbessert, aber noch nicht vollstaendig: manipulierte CSS- und JS-Dateien sowie ein fehlendes Manifest werden nicht fuer alle relevanten statischen Help-Requests ueber echte HTTP-Antworten nachgewiesen.
- Die Unit-Tests laufen wegen der `NU1608`-Aufloesung nicht nachweislich mit derselben `AngleSharp`-Version wie das Web-Projekt; das reduziert die Aussagekraft der Sanitizer-Regressionstests bis zur Bereinigung der Paketaufloesung.

## Verifikation

- `dotnet test FinanceManager.Tests\FinanceManager.Tests.csproj --no-restore --filter "FullyQualifiedName~HelpContentRendererTests|FullyQualifiedName~HelpControllerSecurityTests|FullyQualifiedName~HelpAssetIntegrityValidatorTests"`: 25 Tests bestanden; Warnungen `NU1608`, `NU1902`, `NU1903`.
- `dotnet test FinanceManager.Tests.Integration\FinanceManager.Tests.Integration.csproj --no-restore --filter "FullyQualifiedName~HelpSecurityMiddlewareTests"`: 11 Tests bestanden; Warnungen `NU1902`, `NU1903`.
- `dotnet build FinanceManager.Web\FinanceManager.Web.csproj --no-restore`: erfolgreich, 0 Fehler, 8 Warnungen; darunter `NU1902` fuer `AngleSharp 0.17.1` und `NU1903` fuer `SQLitePCLRaw.lib.e_sqlite3 2.1.11`.
- `dotnet list FinanceManager.Web\FinanceManager.Web.csproj package --vulnerable --include-transitive`: meldet `AngleSharp 0.17.1` mit moderater und `SQLitePCLRaw.lib.e_sqlite3 2.1.11` mit hoher Sicherheitsanfaelligkeit.
