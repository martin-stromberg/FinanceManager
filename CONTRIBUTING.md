# Contributing

Kurz und knapp: Bitte halte dich an die Projekt-Richtlinien, damit �nderungen konsistent und wartbar bleiben.

## API Fehlerbehandlung & Lokalisierung (Standard)

Dieses Projekt verwendet zwei eindeutig getrennte Patterns f�r Fehler, die vom Web API an `ApiClient` und anschlie�end an die UI propagiert werden.

### Pattern 1: Framework-Validation (ModelState / DataAnnotations)

- In Controllern gilt:
  - `if (!ModelState.IsValid) { return ValidationProblem(ModelState); }`
- Die Response ist i.d.R. `ValidationProblemDetails` (RFC-Style) mit einem `errors` Objekt.
- Der Client (siehe `ApiClient`) aggregiert diese `errors` best-effort (aktuell �ber `SetRFCStyleError(...)`) zu einer anzeigbaren Fehlermeldung.

### Pattern 2: Eigene Fehler (Origin + Code + Message)

F�r alle nicht-Framework-Fehler, die dem Anwender angezeigt werden sollen, liefert die API eine standardisierte Fehlerantwort mit:

- `origin`: API-Bereich/Endpoint (z.B. `API_BudgetRule`)
- `code`: stabiler, maschinenlesbarer Fehlercode
- `message`: lokalisierte Meldung in der Sprache des Anwenders

#### Accept-Language

Der Client muss die akzeptierten Sprachen an die API mitsenden (HTTP Header `Accept-Language`).
Die API nutzt Request Localization, damit `IStringLocalizer` die richtige Sprache liefert.

#### Code-Schema

Die Codes m�ssen konsistent, stabil und resx-tauglich sein.

**Formale Eingabefehler (HTTP 400)**

- `ArgumentException` ? `Err_Invalid_{ParamName}`
- `ArgumentOutOfRangeException` ? `Err_OutOfRange_{ParamName}`

`ParamName` muss das Property/Argument benennen, das unzul�ssig ist.

**Domain-Validierung / unzul�ssiger Zielzustand (typisch HTTP 409)**

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

#### Lokalisierungsschl�ssel

Die `message` wird serverseitig �ber `IStringLocalizer` aufgel�st.

Lookup-Key:

- `{origin}_{code}`

Beispiele:

- `API_BudgetRule_Err_Invalid_BudgetCategoryId`
- `API_BudgetRule_Err_Conflict_CategoryAndPurposeRules`

Fallback:

- Wenn kein Ressourceneintrag gefunden wird, wird als `message` die Original-Message der Exception zur�ckgegeben.

#### HTTP Status Codes

- `400 BadRequest`: formale Eingabefehler
- `404 NotFound`: Entity nicht gefunden
- `409 Conflict`: Domain-Regel verletzt / unzul�ssiger Zielzustand
- `403 Forbidden`: Aktion nicht erlaubt
- `500 InternalServerError`: unerwarteter Fehler

## Ressourcen / Lokalisation (resx)
- Platzierung: Alle `.resx`-Dateien geh�ren unter das `Resources`-Verzeichnis des betroffenen Projekts und zwar in Unterordnern, die dem Namespace der konsumierenden Klasse/Komponente entsprechen.
  - Beispiel: Die Komponente `Components.Pages.StatementDraftDetail` im Projekt `FinanceManager.Web` bekommt ihre Ressourcen unter
    `FinanceManager.Web/Resources/Components/Pages/StatementDraftDetail.resx` und die Kulturvariante `FinanceManager.Web/Resources/Components/Pages/StatementDraftDetail.de.resx`.
- Dateinamen:
  - Standardkultur: `{TypeName}.resx` (z. B. `StatementDraftDetail.resx`)
  - Kulturvarianten: `{TypeName}.{culture}.resx` (z. B. `StatementDraftDetail.de.resx`)
- Benennung der Schl�ssel: sprechend und einheitlich, z. B. `Ribbon_AccountDetails`.
- Konsumieren in Code (Blazor/Services): Verwende `IStringLocalizer<T>` mit demselben Typ `T`, f�r den die Ressource gedacht ist. Beispiel:
  ```csharp
  public class StatementDraftDetail // oder razor component class
  {
      private readonly IStringLocalizer<Components.Pages.StatementDraftDetail> _L;
      public StatementDraftDetail(IStringLocalizer<Components.Pages.StatementDraftDetail> localizer) => _L = localizer;
  }
  ```
- Projektkonfiguration: Stelle sicher, dass `Program.cs`/Startup `services.AddLocalization(options => options.ResourcesPath = "Resources");` setzt.

## Branch-Workflow (staging / main)
- PRs werden gegen `staging` erstellt, nicht gegen `main`. `staging` ist der Integrations- und Qualitätssicherungsbranch, `main` ist der ausschließliche Release-Branch.
- Hotfixes gehen ebenfalls über `staging` (kein Direct-Push oder PR direkt gegen `main`).
- Nach erfolgreichem Lauf von [`staging-ci.yml`](.github/workflows/staging-ci.yml) ("Pre-Release") auf `staging` erstellt der Workflow [`staging-to-main-promotion.yml`](.github/workflows/staging-to-main-promotion.yml) automatisch einen Draft-PR von `staging` nach `main`. Dieser PR benötigt einen manuellen Review und Merge durch einen Maintainer.
- Versionsbumps (Semantic Release) erfolgen ausschließlich beim Merge zu `main`, nicht auf `staging`.
- Für alle PRs (gegen `staging` wie gegen `main`) ist mindestens ein Approval erforderlich; die Branch-Protection-Rules werden über die GitHub-Repository-Einstellungen konfiguriert.

## Pull Requests
- Pr�fe vor dem Erstellen eines PRs, dass keine neuen `*.resx`-Dateien an unerwarteten Orten liegen. Nutze die bestehende Namespace-/Ordner-Struktur.
- Beschreibe im PR-Text, welche Ressourcen hinzugef�gt oder ge�ndert wurden und f�r welche Komponenten/Typen sie gedacht sind.

## CI / Checks (Empfehlung)
- F�ge wenn m�glich einen CI-Check hinzu, der sicherstellt, dass neue `resx`-Dateien unter `Resources/` liegen und dass der Pfad dem Namespace-Pattern entspricht (z. B. `Resources/**/<Namespace-as-folders>/**.resx`). Wir akzeptieren gern Hilfestellung f�r eine passende GitHub Action.

