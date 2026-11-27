# Code-Review

## Ergebnis

**Status:** Befunde vorhanden

## Fix-Verifikation (review-code.md, Runde 3)

Beide Befunde aus Runde 2 wurden nachgeprüft:

1. **`CardPage.razor` `?back=`-Open-Redirect — strukturell behoben, aber nicht vollständig**
   (siehe Befund unten). Alle `Back`-Verwendungen in der Datei wurden gegen den Diff geprüft:
   - `case "Back"` (Z. 133–150): Payload-URL-Zweig (`StartsWith("/")`/`"http"`) prüft jetzt
     `IsSameOriginUri(payload)` (Z. 138); `Back`-Zweig prüft `IsSameOriginUri(Back)` (Z. 146).
   - `case "Deleted"` (Z. 316–321) und `case "Saved"` (Z. 325) validieren `Back` ebenfalls;
     die Saved-Fallback-Kette (`new Uri(Back!)` → catch → `BuildCardPath`, Z. 326–344) ist
     identisch zum Altverhalten — relative `Back`-Werte werfen weiterhin in den catch-Zweig,
     kein Verhaltensbruch.
   - `SetBackNavigation` (Z. 404) sanitized: `IsSameOriginUri(Back) ? Back : null`.
   - Keine weitere `?back=`-Senke auf `CardPage.razor` übersehen: `Prev`/`Next` (Z. 210/218),
     `OpenBankContact`/`OpenAccountDetails` (Z. 240/253) und `NewRule` (Z. 368–369) propagieren
     `Back` lediglich escaped als `?back=`-Parameter in selbst gebaute relative URLs — die
     Zielseite validiert erneut (defense-in-depth konsistent). Die übrigen
     `NavigateTo(payload)`-Fälle (`OpenPostings`, `OpenStatementDraft`, `OpenPrices`,
     `OpenPerformance`, `OpenCategory`) werden aus VM-Payloads gespeist, nicht aus `?back=`.
   - `GenericCardPage.razor` hat **keine** `?back=`-Senke: kein `SupplyParameterFromQuery`
     (Kind-Komponente, keine Route); `OpenCreateForLookup` (Z. 697–699) produziert `?back=`
     nur aus `Nav.Uri` (same-origin, escaped). `ListPage.razor`/`BudgetReport.razor` sind
     an beiden Stellen bereits validiert (Runde 2 bestätigt, unverändert korrekt).
2. **`NavigationManagerExtensionsTests` — umgesetzt, aber ein Case zementiert den falschen
   Erwartungswert** (siehe Befund). 21 Cases (10 Accept + 11 Reject) inkl. `\\evil`,
   `/\evil`, Userinfo, Scheme-Großschreibung, `https:host`, `//evil`, `javascript:`,
   `data:`, `null`/leer/Whitespace. Lokal ausgeführt: 21/21 grün
   (`dotnet test FinanceManager.Tests --filter ~NavigationManagerExtensionsTests`).

## Befunde

### NavigationManagerExtensions.cs / NavigationManagerExtensionsTests.cs

- **Fehlende Validierung von Eingaben (mittel)** — `IsSameOriginUri`
  (`NavigationManagerExtensions.cs:180-196`) validiert mit **.NET-`Uri`-Semantik**
  (`nav.ToAbsoluteUri`), die eigentliche Navigation löst aber der **Browser per
  WHATWG-URL-Parser** auf: `NavigateTo` reicht den Raw-String an
  `Blazor._internal.navigationManager.navigateTo`; der Client prüft `new URL(url,
  document.baseURI)` und setzt sonst `location.href = url` (verifiziert am ausgelieferten
  `blazor.web.js`). Dadurch gibt es eine Umgehungsklasse von Eingaben, die .NET **relativ**
  auflöst (→ akzeptiert), der Browser aber **absolut/schemabasiert** normalisiert.
  Empirisch gegen die reale Assembly verifiziert (`nav.ToAbsoluteUri`/`IsSameOriginUri`,
  net10.0):

  | Eingabe (`?back=`) | `IsSameOriginUri` | Browser-Auflösung |
  |---|---|---|
  | `https:evil.test` | **True** → `https://app.test/evil.test` | `https://evil.test/` → **Open Redirect** |
  | `https:/evil.test`, `https:\evil.test`, `https\t:evil.test` | **True** (relativ) | `https://evil.test/` → **Open Redirect** |
  | `ht\ttps://evil.test` | **True** → `https://app.test/ht\ttps://…` | WHATWG entfernt TAB → `https://evil.test` → **Open Redirect** |
  | `java\tscript:alert(1)` | **True** → `https://app.test/java\tscript:…` | WHATWG entfernt TAB → `javascript:alert(1)`; `location.href` führt aus → **XSS** |

  Kontraste (korrekt behandelt): `\\evil` → `file://evil/` (abgelehnt), `//evil.test` →
  fremd (abgelehnt), `https://app.test@evil.test` → Authority `evil.test` (abgelehnt),
  `javascript:`/`data:`/`vbscript:` → absolut, Scheme-Mismatch (abgelehnt), führende
  Leerzeichen vor `https://evil.test` → getrimmt, fremd (abgelehnt).

  Ausnutzung: `?back=https:evil.test` (bzw. `java%09script:…`) an einem der drei
  `?back=`Consumer (`CardPage`, `ListPage`, `BudgetReport`) — bei `Deleted`/`Saved` erfolgt
  die Navigation automatisch nach Speichern/Löschen. Der Runde-2-Fixzweck ist damit nicht
  ganz erreicht; der Test in `NavigationManagerExtensionsTests.cs:32`
  (`[InlineData("https:evil.test")]` in der **Accept**-Theorie inkl. Kommentar „resolves
  against the base URI") schreibt den Bypass sogar als gewollt fest.

  Empfehlung: `IsSameOriginUri` zusätzlich absichern — Eingaben mit C0-Kontrollzeichen
  (`char.IsControl`, insb. `\t`/`\r`/`\n`) ablehnen und Schema-Präfixe ohne `//`
  (`^[A-Za-z][A-Za-z0-9+.-]*:` gefolgt von nicht-`//`) ablehnen; alternativ striktere
  Whitelist (root-relativ `^/(?!/)` **oder** String beginnt mit `nav.BaseUri` — Achtung:
  legitime Produzenten liefern auch schemafreie relative Pfade wie
  `card/budget/rules/new?prefill=…`, z. B. `CardPage.razor:368-369`, daher darf die
  Whitelist nicht nur führende `/` verlangen). Tests: `https:evil.test` in die
  Reject-Theorie verschieben und `https:/evil.test`, `java\tscript:alert(1)`,
  `ht\ttps://evil.test` als Reject-Cases ergänzen.

## Nebenbeobachtung (Altbestand, nicht regressionsrelevant)

- `BudgetRuleCardViewModel` setzt `SetBackNavigation($"card/budget/purposes/{id}")`
  (Z. 100/104, ohne führenden `/`) und emittiert `RaiseUiActionRequested("Back", InitBack)`
  (Z. 340/377). Der Payload fällt in `CardPage.razor` `case "Back"` durch den
  `StartsWith`-/`IsSameOriginUri`-/GUID-Filter in den Einzelteil-Fallback
  (`parts.Length == 1`, Z. 174-178) → `NavigateTo($"list/{Kind}/card/budget/purposes/{id}")`
  — eine URL ohne Route-Match. Verhalten ist identisch zu vor dem Fix (der neue Guard ändert
  den Pfad nicht), also kein Regressionsbefund — verdient aber ggf. ein Folge-Issue
  (Payload müsste `card,…`-Syntax oder führenden `/` nutzen).

## Geprüfte Dateien

- `FinanceManager.Web/Components/Pages/CardPage.razor` (vollständig, alle `Back`/`NavigateTo`-Stellen)
- `FinanceManager.Web/Extensions/NavigationManagerExtensions.cs` (`IsSameOriginUri` Z. 180–196)
- `FinanceManager.Tests/Web/NavigationManagerExtensionsTests.cs` (21 Cases)
- `FinanceManager.Web/Components/Pages/ListPage.razor` (Z. 156, 217–219 — unverändert korrekt)
- `FinanceManager.Web/Components/Pages/BudgetReport.razor` (Z. 34, 71–74 — unverändert korrekt)
- `FinanceManager.Web/Components/Pages/GenericCardPage.razor` (vollständig, kein `?back=`-Consumer)
- `FinanceManager.Web/ViewModels/Budget/BudgetRuleCardViewModel.cs` (`InitBack`-Pfad)
- `FinanceManager.Web/ViewModels/Common/BaseViewModel.cs` (`SetBackNavigation`/`InitBack` Z. 109–120, `RequestOpenLinkedCard` Z. 356–379 — Produzent, `Nav.Uri`)

Verifikationsartefakte: `dotnet test` (21/21 grün) und ein Scratch-`NavigationManager`-
Harness (net10.0) für die `ToAbsoluteUri`-Auflösungstabelle oben.
