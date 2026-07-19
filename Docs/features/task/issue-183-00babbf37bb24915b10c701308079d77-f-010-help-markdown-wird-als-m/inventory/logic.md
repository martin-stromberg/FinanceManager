# Logik und Ausgabewege

## `HelpController`
Datei: `FinanceManager.Web/Controllers/HelpController.cs`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---|---|---|
| `HelpController(IWebHostEnvironment, ILogger<HelpController>)` | public | Übernimmt Webhost-Umgebung und Logger. |
| `GetHelpPage(string, string)` | public | Liest eine statische HTML-Datei aus `wwwroot/help/{language}/{featureId}.html` und gibt sie als `text/html` zurück. Prüft nur leere Parameter und einfache Pfadzeichen. |
| `GetMarkdown(string, string)` | public | Sucht Markdown-Dateien unter `../docs/business/features`, wählt abhängig von Sprache eine Datei, entfernt YAML-Frontmatter und gibt den verbleibenden Inhalt als `text/plain` zurück. |
| `GetSearchIndex(string)` | public | Liest `wwwroot/help/{language}/search-index.json` und gibt den Dateiinhalt als `application/json` zurück. |

Die Methoden sind über die Route `api/help` erreichbar. `GetMarkdown` wird von `HelpPageView` aufgerufen; `GetSearchIndex` wird von `help-search.js` per `fetch` aufgerufen. Der Controller verändert Help-Inhalte außer der Frontmatter-Entfernung nicht.

## `HelpPageView`
Datei: `FinanceManager.Web/Components/Pages/HelpPageView.razor`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---|---|---|
| `OnInitializedAsync()` | protected override | Bestimmt die Sprache, lädt Markdown über `/api/help/markdown/{language}/{FeatureId.ToLower()}` und setzt Lade-/Fehlerstatus. |
| `ConvertMarkdownToHtml(string)` | private | Konvertiert Überschriften, Hervorhebung, Code, interne/externe Links, Absätze, Listen und Blockquotes per Regex in HTML. |
| `GoBack()` | private | Navigiert zurück nach `/help`. |

Das Ergebnis von `ConvertMarkdownToHtml(markdown)` wird in der Razor-Datei als `MarkupString` ausgegeben. Die Regex-Konvertierung escaped den Eingabetext nicht und enthält keine erkennbare HTML-Sanitization oder Whitelist-Prüfung. Externe Links werden mit `target="_blank"` erzeugt; interne Help-Links werden auf `/help/view/{featureId}` umgeschrieben.

## `HelpPageManager`
Datei: `FinanceManager.Web/wwwroot/help/js/help-search.js`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---|---|---|
| `constructor()` | public | Initialisiert Suchindex, Sprache und Featureliste und startet `init()`. |
| `detectLanguage()` | public | Ermittelt Sprache aus `data-culture`, `html.lang` oder Browsersprache. |
| `init()` | public | Lädt den Suchindex, registriert Suchereignisse und zeigt alle Features an. |
| `showError(message)` | public | Schreibt eine Fehlerdarstellung mit interpolierter Meldung in `featureList.innerHTML`. |
| `loadSearchIndex()` | public | Lädt `/api/help/search-index/{language}.json` und übernimmt `data.documents` als Featureliste. |
| `setupSearch()` | public | Registriert Enter-, Klick- und Input-Handler für die Suche. |
| `showAutoComplete(query)` | public | Ermittelt maximal drei Treffer; die UI-Implementierung ist noch ein TODO. |
| `performSearch(query)` | public | Filtert Treffer und schreibt Ergebnis- oder Leerzustand in `searchResults.innerHTML`. |
| `searchFeatures(query)` | public | Sucht case-insensitiv in `title`, `excerpt` und `keywords`. |
| `displayAllFeatures()` | public | Zeigt alle Features bzw. einen Leerzustand über `innerHTML` an. |
| `renderResults(features)` | public | Erzeugt HTML-Karten aus `id`, `title` und `excerpt`, inklusive interpoliertem Inline-`onclick`. |
| `openFeature(language, featureId)` | public | Navigiert zu `/help/view/{featureId.toLowerCase()}`. |

`renderResults` verwendet Indexwerte direkt in HTML-Text, Attributen und JavaScript-Kontext. Für `showError` und die statischen Leerzustände wird ebenfalls `innerHTML` verwendet.

## `ProgramExtensions.ConfigureMiddleware`
Datei: `FinanceManager.Web/ProgramExtensions.cs`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---|---|---|
| `ConfigureMiddleware(WebApplication)` | public static | Registriert Request-/IP-Middleware, statische Dateien, Authentifizierung, Autorisierung, statische Assets, Razor Components und Controller-Routen. |

`app.UseStaticFiles()` wird vor den Razor- und Controller-Routen aufgerufen und liefert damit auch statische Help-Dateien aus. Im geprüften Middleware-Aufbau wird kein `Content-Security-Policy`-Header gesetzt.

