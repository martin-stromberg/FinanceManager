# Contributing

Kurz und knapp: Bitte halte dich an die Projekt-Richtlinien, damit Änderungen konsistent und wartbar bleiben.

## API Fehlerbehandlung & Lokalisierung (Standard)

Dieses Projekt verwendet zwei eindeutig getrennte Patterns für Fehler, die vom Web API an `ApiClient` und anschließend an die UI propagiert werden.

### Pattern 1: Framework-Validation (ModelState / DataAnnotations)

- In Controllern gilt:
  - `if (!ModelState.IsValid) { return ValidationProblem(ModelState); }`
- Die Response ist i.d.R. `ValidationProblemDetails` (RFC-Style) mit einem `errors` Objekt.
- Der Client (siehe `ApiClient`) aggregiert diese `errors` best-effort (aktuell über `SetRFCStyleError(...)`) zu einer anzeigbaren Fehlermeldung.

### Pattern 2: Eigene Fehler (Origin + Code + Message)

Für alle nicht-Framework-Fehler, die dem Anwender angezeigt werden sollen, liefert die API eine standardisierte Fehlerantwort mit:

- `origin`: API-Bereich/Endpoint (z.B. `API_BudgetRule`)
- `code`: stabiler, maschinenlesbarer Fehlercode
- `message`: lokalisierte Meldung in der Sprache des Anwenders

#### Accept-Language

Der Client muss die akzeptierten Sprachen an die API mitsenden (HTTP Header `Accept-Language`).
Die API nutzt Request Localization, damit `IStringLocalizer` die richtige Sprache liefert.

#### Code-Schema

Die Codes müssen konsistent, stabil und resx-tauglich sein.

**Formale Eingabefehler (HTTP 400)**

- `ArgumentException` ? `Err_Invalid_{ParamName}`
- `ArgumentOutOfRangeException` ? `Err_OutOfRange_{ParamName}`

`ParamName` muss das Property/Argument benennen, das unzulässig ist.

**Domain-Validierung / unzulässiger Zielzustand (typisch HTTP 409)**

- `DomainValidationException` ? z.B.
  - `Err_Conflict_{DomainRule}` oder
  - `Err_InvalidState_{DomainRule}`

`DomainRule` ist PascalCase und beschreibt stabil die verletzte Regel.

**Not Found (HTTP 404)**

- `Err_NotFound_{Entity}`

**Not Allowed (HTTP 403)**

- `Err_NotAllowed_{Action}`

**Unexpected (HTTP 500)**

- `Err_Unexpected`

#### Lokalisierungsschlüssel

Die `message` wird serverseitig über `IStringLocalizer` aufgelöst.

Lookup-Key:

- `{origin}_{code}`

Beispiele:

- `API_BudgetRule_Err_Invalid_BudgetCategoryId`
- `API_BudgetRule_Err_Conflict_CategoryAndPurposeRules`

Fallback:

- Wenn kein Ressourceneintrag gefunden wird, wird als `message` die Original-Message der Exception zurückgegeben.

#### HTTP Status Codes

- `400 BadRequest`: formale Eingabefehler
- `404 NotFound`: Entity nicht gefunden
- `409 Conflict`: Domain-Regel verletzt / unzulässiger Zielzustand
- `403 Forbidden`: Aktion nicht erlaubt
- `500 InternalServerError`: unerwarteter Fehler

## Ressourcen / Lokalisation (resx)
- Platzierung: Alle `.resx`-Dateien gehören unter das `Resources`-Verzeichnis des betroffenen Projekts und zwar in Unterordnern, die dem Namespace der konsumierenden Klasse/Komponente entsprechen.
  - Beispiel: Die Komponente `Components.Pages.StatementDraftDetail` im Projekt `FinanceManager.Web` bekommt ihre Ressourcen unter
    `FinanceManager.Web/Resources/Components/Pages/StatementDraftDetail.resx` und die Kulturvariante `FinanceManager.Web/Resources/Components/Pages/StatementDraftDetail.de.resx`.
- Dateinamen:
  - Standardkultur: `{TypeName}.resx` (z. B. `StatementDraftDetail.resx`)
  - Kulturvarianten: `{TypeName}.{culture}.resx` (z. B. `StatementDraftDetail.de.resx`)
- Benennung der Schlüssel: sprechend und einheitlich, z. B. `Ribbon_AccountDetails`.
- Konsumieren in Code (Blazor/Services): Verwende `IStringLocalizer<T>` mit demselben Typ `T`, für den die Ressource gedacht ist. Beispiel:
  ```csharp
  public class StatementDraftDetail // oder razor component class
  {
      private readonly IStringLocalizer<Components.Pages.StatementDraftDetail> _L;
      public StatementDraftDetail(IStringLocalizer<Components.Pages.StatementDraftDetail> localizer) => _L = localizer;
  }
  ```
- Projektkonfiguration: Stelle sicher, dass `Program.cs`/Startup `services.AddLocalization(options => options.ResourcesPath = "Resources");` setzt.

## Pull Requests
- Prüfe vor dem Erstellen eines PRs, dass keine neuen `*.resx`-Dateien an unerwarteten Orten liegen. Nutze die bestehende Namespace-/Ordner-Struktur.
- Beschreibe im PR-Text, welche Ressourcen hinzugefügt oder geändert wurden und für welche Komponenten/Typen sie gedacht sind.

## CI / Checks (Empfehlung)
- Füge wenn möglich einen CI-Check hinzu, der sicherstellt, dass neue `resx`-Dateien unter `Resources/` liegen und dass der Pfad dem Namespace-Pattern entspricht (z. B. `Resources/**/<Namespace-as-folders>/**.resx`). Wir akzeptieren gern Hilfestellung für eine passende GitHub Action.

