# PathBase-Infrastruktur und Navigationslogik

## `ProgramExtensions` — Middleware- und Service-Setup

Datei: `FinanceManager.Web/ProgramExtensions.cs`

| Methode / Stelle | Sichtbarkeit | Kurzbeschreibung |
|------------------|-------------|------------------|
| `ConfigureMiddleware` (Zeilen 626–638) | `public static` | Liest `app.Configuration["PathBase"]`; bei gesetztem Wert `app.UsePathBase(pathBase)` — als erste Middleware vor Forwarded-Headers, Logging, Rate-Limiting etc., damit Routing den gestrippen Pfad sieht. Kein `PathBase`-Eintrag in `appsettings*.json` (opt-in). |
| `ConfigureMiddleware` (Zeilen 648–682) | `public static` | Forwarded-Headers nur `XForwardedFor`/`XForwardedProto`, `ForwardLimit = 1`, Trust nur über `ReverseProxy:KnownProxies`/`KnownNetworks`. `X-Forwarded-Prefix` wird **nicht** ausgewertet. |
| `ConfigureMiddleware` (Zeile 808) | `public static` | Registriert `AuthenticationRedirectMiddleware` im Exception-Handler-Scope. |
| `RegisterAppServices` (Zeilen 225–236) | `public static` | HttpClient `Api`: `BaseAddress` = `{Scheme}://{Host}{Request.PathBase.Value}/` (Zeile 233) — PathBase-fähig. |
| `BuildLocalizationOptions` | `public static` | Request-Localization; nicht PathBase-relevant. |

## `AuthenticationRedirectMiddleware`

Datei: `FinanceManager.Web/Infrastructure/Auth/AuthenticationRedirectMiddleware.cs`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `InvokeAsync(HttpContext)` | `public` | Fängt `ApiAuthenticationRequiredException` aus dem Prerender-Pfad ab und sendet `302` nach `{Request.PathBase}/login?returnUrl={aktuelle Route}` (Zeilen 53–55) — PathBase explizit vorangestellt. |
| `CanRedirect(HttpContext)` | `private static` | Kein Redirect bei gestarteter Response, non-GET, öffentlichen Pfaden (`AuthReturnUrl.IsPublicPath`) sowie `/api`, `/_blazor`, `/_framework`. |

Abonnierte Events: keine. Publizierte Events: keine.

## `AuthReturnUrl`

Datei: `FinanceManager.Web/Infrastructure/Auth/AuthReturnUrl.cs` (static class)

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `FromNavigationUri` | `baseUri`, `currentUri` | `string?` | Leitet validierte relative Return-URL aus einer Navigations-URI ab (same-origin erforderlich). |
| `Validate` | `returnUrl` | `string?` | Akzeptiert nur interne, nicht öffentliche Pfade; lehnt absolute/protokoll-relative URLs, Steuerzeichen, Doppel-Encoding ab. Liefert Wert **mit** führendem Slash. |
| `BuildLoginUrl` | `returnUrl` | `string` | Erzeugt `login` bzw. `login?returnUrl={kodiert}` — **relativ ohne führenden Slash** (XML-Doc verweist explizit auf PathBase-Auflösung durch `NavigateTo`). |
| `IsPublicPath` | `path` | `bool` | Allowlist: `/login`, `/register`, `/help*`, `/api/auth*`, `/error*`. |

Wird verwendet von `AuthenticationRedirectMiddleware` und `AuthRedirect.razor`.

## `AuthRedirect.razor`

Datei: `FinanceManager.Web/Components/AuthRedirect.razor`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `OnInitialized` | `protected override` | Abonniert `Nav.LocationChanged` und `ApiClient.AuthenticationRequired`. |
| `CheckRedirectAsync(uri)` | `private` | Prüft öffentliche Routen via `AuthReturnUrl.IsPublicPath`; bei unauthentifiziertem Zugriff Redirect zu `register` (kein User vorhanden) oder Login mit `returnUrl`. Navigation relativ: `Nav.NavigateTo("register")` (Z72), `BuildLoginUrl(...)` (Z126). |
| `GetPath(uri)` | `private` | App-relativer Pfad via `Nav.ToBaseRelativePath` (Zeilen 86–100) — Kommentar dokumentiert explizit PathBase-Korrektheit. Vergleichsziel `"/login"`/`"/register"` in Z62 ist ein interner Vergleichswert, kein Navigationsziel. |
| `RedirectToLoginAsync` | `private` | Baut validierten `returnUrl` und navigiert relativ via `Nav.NavigateTo(AuthReturnUrl.BuildLoginUrl(...), forceLoad: true)`. |

Abonnierte Events: `Nav.LocationChanged`, `ApiClient.AuthenticationRequired`.

## `NavigationManagerExtensions`

Datei: `FinanceManager.Web/Extensions/NavigationManagerExtensions.cs`

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `NavigateToModelCard` | `nav`, `result` | `bool` | Navigiert zu `card/…`-Routen — alle erzeugten Ziele relativ. |
| `NavigateToModelCreate` | `nav`, `kind`, `subKind` | `bool` | `card/{kind}[/{subKind}]/{Guid.Empty}` — relativ; normalisiert führende Slashes aus `kind` heraus. |
| `NavigateToModelCategories` | `nav`, `kind` | `bool` | `list/{kind}/categories` — relativ. |
| `IsSameOriginUri` | `nav`, `uri` | `bool` | Validiert `?back=`-/Navigationsziele gegen Scheme+Authority der `BaseUri`. **Akzeptiert root-absolute Pfade** (`/list/accounts` → `true`, Zeilen 186–211): ein solches `?back=`-Ziel würde unter PathBase das Präfix verlieren — in der Anforderung als offener Punkt mit niedrigem Risiko markiert, da `back`-Werte intern aus `Nav.Uri` erzeugt werden. |
| `IsSafeNavigationCandidate` | `value` | `bool` (private) | WHATWG-Sonderfälle (Steuerzeichen, Scheme-Präfixe ohne `//`). |

Verwendungen von `IsSameOriginUri`: `ListPage.razor` (Z156, Z217), `CardPage.razor` (Z138, Z146, Z319, Z325, Z404), `BudgetReport.razor` (Z34, Z71).

## `?back=`-Erzeugung (interne Quellen)

| Datei / Stelle | Erzeugter Wert |
|----------------|----------------|
| `ListPage.razor` Z212 | `Nav.Uri` (absolute URI inkl. PathBase) |
| `CardPage.razor` Z105, Z369 | `Nav.Uri`/`currentUrl` via `ToBaseRelativePath` |
| `BaseViewModel.RequestOpenLinkedCard` (ViewModels/Common/BaseViewModel.cs Z356–379) | `nav.Uri` als `?back=` |
| `HomeNotifications.razor` Z105–117 | `Nav.Uri` |
| `ValidationResultPanel.razor` Z80–103 | `ToBaseRelativePath(NavigationManager.Uri)`; Hinweis: `parts[1]` aus `PRELIMINARY_POSTINGS_WILL_BE_REVERSED`-Nachricht beginnt mit `/` (Z86–88) |

## Client-Skript-Basisauflösung

`FinanceManager.Web/wwwroot/js/financeManager.js` (Zeilen 140–153): `getBasePath()` liest das `<base href>`-Element und normalisiert `window.location.pathname` durch Entfernen des Basis-Pfads — PathBase-fähig. Alle `fetch`-Aufrufe in `wwwroot` sind relativ (siehe `komponenten.md`).

## Weitere PathBase-Nutzung

`FinanceManager.Web/Mcp/Tools/FmAdminBackupsTool.cs` Zeile 390: baut absolute URLs als `{Scheme}://{Host}{Request.PathBase}{path}` — PathBase-fähig.

## `AGENT.md`

Datei: `AGENT.md` (Repo-Wurzel, 35 Zeilen)

Vorhandene Abschnitte: „Backup-Vollständigkeit bei neuen Datenbankfeldern", „Benutzerbezug der Datensätze", „Gruppierung des Ribbon-Menüs", „Weitere projektspezifische Regeln". **Keine Regel zu root-absoluten internen Pfaden/PathBase vorhanden.**
