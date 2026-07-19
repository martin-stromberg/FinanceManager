# Detail: Logging und Token-Redigierung

## Relevante Dateien

- `FinanceManager.Web/Infrastructure/RequestLoggingMiddleware.cs`
- `FinanceManager.Web/ProgramExtensions.cs`
- `FinanceManager.Web/Infrastructure/Logging/FileLoggerProvider.cs`
- `FinanceManager.Web/appsettings.json`
- `FinanceManager.Web/appsettings.Production.json`

## Ist-Zustand

`RequestLoggingMiddleware.InvokeAsync` misst die Laufzeit und schreibt nach erfolgreicher Verarbeitung oder nach Exceptions einen Logeintrag.

Fundstellen:

- `RequestLoggingMiddleware.cs:50-57`: Erfolgspfad, Log-Level `Debug` oder `Warning`, Path-Parameter enthaelt `context.Request.Path + context.Request.QueryString`.
- `RequestLoggingMiddleware.cs:62-69`: Exception-Pfad, Log-Level `Warning`, Path-Parameter enthaelt ebenfalls `context.Request.Path + context.Request.QueryString`.
- `ProgramExtensions.cs:314`: Middleware wird sehr frueh in die Pipeline eingebunden.
- `ProgramExtensions.cs:49-58`: Console-Logging ist aktiv, File-Logging kann per Konfiguration zugeschaltet werden.
- `appsettings.Production.json:7`: Kategorie `FinanceManager.Web.Infrastructure.RequestLoggingMiddleware` ist in Production auf `Warning` gesetzt. Fehlerhafte Requests und Exceptions werden deshalb auch in Production relevant.

## Sicherheitsrelevanter Befund

Der anonyme Attachment-Download akzeptiert `token` als Query-Parameter. Da die Middleware den QueryString unveraendert in das Log-Template einsetzt, werden Werte aus Requests wie `/api/attachments/{id}/download?token=<wert>` im Klartext geloggt.

Die Anforderung verlangt Redigierung fuer alle Logpfade, die aktuell `Request.Path + Request.QueryString` verwenden. Das betrifft beide Fundstellen in der Middleware.

## Umsetzungshinweise

- Redigierung sollte vor dem Aufruf von `_logger.Log` beziehungsweise `_logger.LogWarning` erfolgen.
- Der Parametername `token` muss case-insensitive erkannt werden.
- Nicht-sensitive Query-Parameter sollten erhalten bleiben.
- Der Ersatzwert sollte konstant sein, z. B. `[REDACTED]`, damit keine Laenge oder Struktur des Tokens preisgegeben wird.
- ASP.NET-Core-Typen wie `QueryString`, `QueryHelpers.ParseQuery` oder `context.Request.Query` sind robuster als manuelles Splitten.
- Vorsicht: `context.Request.Query` kann Form/Query-Parsing ausloesen; fuer Query-Strings ist das akzeptabel, sollte aber in Tests mit URL-Encoding und mehrfachen Parametern abgedeckt werden.

## Testhinweise

Es wurden keine bestehenden Tests fuer `RequestLoggingMiddleware` gefunden. Sinnvoll ist eine neue Testklasse in `FinanceManager.Tests`, z. B. `Web/RequestLoggingMiddlewareTests.cs` oder `Infrastructure/RequestLoggingMiddlewareTests.cs`.

Testfaelle:

- `?token=secret` wird als `token=[REDACTED]` geloggt.
- `?Token=secret`, `?TOKEN=secret` und gemischte Schreibweisen werden redigiert.
- `?foo=bar&token=secret&page=1` behaelt `foo` und `page` bei.
- Exception-Pfad redigiert identisch.
- Ein Request ohne Query bleibt unveraendert.
