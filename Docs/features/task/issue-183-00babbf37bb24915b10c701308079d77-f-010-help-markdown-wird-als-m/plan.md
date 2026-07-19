# Umsetzungsplan: Help-Markdown sicher rendern

## Zielbild

Help-Inhalte werden an einer zentralen Stelle in erlaubtes HTML umgewandelt und
sanitisiert. `MarkupString` wird nur noch mit diesem nachweisbar bereinigten
HTML verwendet. Der Suchindex wird als Datenmodell verarbeitet und im Browser
ohne HTML- oder JavaScript-Interpolation in den DOM geschrieben. Help-Seiten
erhalten eine restriktive CSP. Build und Start der Anwendung stellen sicher,
dass nur vorgesehene Help-Artefakte ausgeliefert werden.

## Betroffene Bereiche

| Bereich | Dateien | Aenderung |
|---|---|---|
| Rendering/API | `FinanceManager.Web/Controllers/HelpController.cs` | Gemeinsamen Help-Renderer und validierte Suchindexausgabe verwenden; keine ungeprueften HTML-/JSON-Dateien ausliefern. |
| Rendering | `FinanceManager.Web/Components/Pages/HelpPageView.razor` | Regex-Konverter entfernen, sanitisiertes Renderergebnis ausgeben, Inline-`style` aus dem Razor-Markup entfernen. |
| Suche | `FinanceManager.Web/wwwroot/help/js/help-search.js` | `innerHTML` fuer Indexwerte und Inline-`onclick` entfernen; Elemente, Text und sichere URLs ueber DOM-APIs erzeugen. |
| CSP/Middleware | `FinanceManager.Web/ProgramExtensions.cs` | CSP fuer Help-Dokumente, Help-Hub und Help-API festlegen und an den betroffenen Responses setzen. |
| Styles | `FinanceManager.Web/wwwroot/help/css/help-page.css`, ggf. `FinanceManager.Web/Components/Pages/HelpHub.razor` | Inline-CSS entfernen bzw. in die statische Help-CSS verschieben, damit `style-src` ohne `unsafe-inline` auskommt. |
| Abhaengigkeiten | `FinanceManager.Web/FinanceManager.Web.csproj` | Bewaehrte Markdown- und HTML-Sanitization-Pakete mit festen Versionen aufnehmen; `Markdig` fuer Markdown und `Ganss.Xss` oder eine gleichwertige Whitelist-Sanitization verwenden. |
| Artefakte | `FinanceManager.Web/FinanceManager.Web.csproj`, Build-/Help-Dateien | Help-Eingaben als explizite Build-Artefakte behandeln und Integritaetspruefung/Manifest fuer die ausgelieferten Dateien vorsehen. |
| Tests | `FinanceManager.Tests/`, `FinanceManager.Tests.Integration/`, ggf. `FinanceManager.Tests.E2E/` | Unit-, API- und Browser-nahe Tests fuer Sanitization, Suche, CSP und Artefaktpruefung ergaenzen. |

## Umsetzungsschritte

### 1. Sicherheitskonzept und zentrale Rendering-Grenze

1. Einen `IHelpContentRenderer` mit konkreter Implementierung im Web-Projekt
   einfuehren und per DI registrieren.
2. Markdown mit `Markdig` rendern. HTML aus dem Markdown wird standardmaessig
   deaktiviert oder als Text behandelt. Erlaubt werden nur die bereits von der
   Help-Ansicht benoetigten Elemente: Ueberschriften, Absaetze, Listen,
   Blockquotes, `strong`, `em`, `code`, `pre`, Tabellen und Links.
3. Das Renderergebnis mit einer expliziten Whitelist sanitizen. Entfernen:
   `script`, Event-Handler, `style`, `iframe`, `object`, `embed`, Formulare,
   gefaehrliche Attribute sowie nicht erlaubte URL-Schemata. Links werden auf
   `http`, `https` und interne Help-Ziele begrenzt; externe Links erhalten
   `rel="noopener noreferrer"`.
4. Die Frontmatter-Entfernung als eigene, getestete Funktion vor dem Rendern
   beibehalten. Der Renderer liefert nur fertiges, sanitisiertes HTML zur
   Ausgabe.
5. Den bisherigen Regex-Konverter aus `HelpPageView.razor` entfernen und das
   Renderergebnis als `MarkupString` ausgeben. Die Sicherheitsinvariante wird
   im Code dokumentiert: kein anderer Eingang darf in diese Ausgabe gelangen.

### 2. API und statische Help-Ausgabe absichern

1. `GetMarkdown` auf den zentralen Renderer umstellen oder alternativ ein
   eindeutig dokumentiertes Ausgabeformat vereinbaren. Keine Dateiinhalte
   direkt als HTML zurueckgeben.
2. Den Legacy-Endpunkt `GetHelpPage` ebenfalls durch denselben HTML-Sanitizer
   fuehren. Damit ist der nicht mehr primaer verwendete HTML-Ausgabepfad nicht
   von der Schutzmassnahme ausgenommen.
3. Fuer `GetSearchIndex` ein begrenztes DTO fuer Dokumente mit `id`, `title`,
   `excerpt` und `keywords` einfuehren. JSON mit `System.Text.Json` einlesen,
   Pflichtfelder und erlaubte Zeichen/Laengen validieren und nur das DTO wieder
   serialisieren. Fehlerhafte oder ungueltige Eintraege werden verworfen oder
   mit einer kontrollierten Fehlerantwort behandelt; keine Rohdatei wird
   durchgereicht.
4. Pfad- und Sprachvalidierung zentralisieren, damit alle drei Endpunkte die
   gleiche erlaubte Eingabemenge verwenden. Fehlermeldungen enthalten keine
   vom Request oder von Help-Dateien uebernommenen HTML-Fragmente.

### 3. Suchindex ohne DOM-XSS verwenden

1. `help-search.js` so umbauen, dass statische Meldungen mit `textContent`
   gesetzt werden.
2. Trefferkarten mit `document.createElement`, `textContent`,
   `setAttribute`/DOM-Eigenschaften und `addEventListener` erstellen.
3. `id` vor der Navigation gegen das erwartete Feature-ID-Format validieren;
   die URL wird ausschliesslich aus dem validierten Wert zusammengesetzt.
4. `innerHTML` vollstaendig aus indexabhaengigen Ausgabepfaden entfernen.
   Statische, unveraenderliche Container koennen bei Bedarf mit DOM-Methoden
   aufgebaut werden, sodass die Suche keine Inline-Handler benoetigt.

### 4. CSP und statische Dateien

1. In `ConfigureMiddleware` eine Help-spezifische CSP-Middleware oder einen
   Response-Header-Handler vor statischer Help-Auslieferung und Razor-Routing
   integrieren. Als Ausgangspunkt verwenden: `default-src 'self';
   script-src 'self'; style-src 'self'; img-src 'self' data:; connect-src
   'self' ws: wss:; object-src 'none'; base-uri 'self'; frame-ancestors 'self';
   form-action 'self'`.
2. Pruefen, ob Interactive Server, Authentifizierung und Entwicklungsmodus
   weitere Quellen benoetigen. Jede Ausnahme muss auf den konkreten Help-
   Workflow begrenzt und begruendet werden; `unsafe-inline` fuer Skripte wird
   nicht eingefuehrt.
3. Das Inline-`style` in `HelpPageView.razor` in
   `wwwroot/help/css/help-page.css` verschieben. Falls die CSP fuer den Help-
   Hub gilt, auch dort Inline-Skripte/Styles entfernen oder als externe Assets
   ausliefern.
4. Fuer statische Legacy-HTML-Dateien denselben Header setzen. Inline-Skripte
   bleiben dadurch blockiert; unsichere Legacy-Seiten werden bevorzugt entfernt
   oder auf die sichere Rendereroute umgestellt.

### 5. Build-Artefakte und Integritaet

1. Die erlaubten Help-Quellen und Ausgabeverzeichnisse im `.csproj` explizit
   festlegen. Unkontrolliertes Kopieren beliebiger Dateien unter `wwwroot/help`
   vermeiden.
2. Im Build einen SHA-256-Manifest fuer die vorgesehenen Markdown-, JSON-,
   CSS- und JS-Help-Artefakte erzeugen. Das Manifest wird als Build-Output
   behandelt und nicht aus einer zur Laufzeit veraenderbaren Quelle gelesen.
3. Vor der Auslieferung oder beim Start die vorhandenen Dateien gegen das
   Manifest pruefen. Bei Abweichungen keine betroffene Help-Datei ausliefern
   und einen Security-Logeintrag schreiben; der genaue Fail-Closed-Umfang wird
   durch Tests festgelegt, damit die Anwendung nicht stillschweigend unsichere
   Inhalte aktiviert.
4. Dokumentieren, dass Help-Dateien nur im vorgesehenen Build-Prozess geaendert
   werden duerfen und dass eine Laufzeit-Aenderung einen neuen Build mit neuem
   Manifest erfordert.

## Tests und Nachweise

1. Renderer-Unit-Tests fuer erlaubtes Markdown, HTML-Tags, `script`,
   Event-Handler, `javascript:`-Links, Data-URLs, verschachtelte Payloads,
   Codebloecke, Tabellen und externe Links.
2. Controller-Integrationstests fuer `GetMarkdown`, `GetHelpPage` und
   `GetSearchIndex`: kein ausfuehrbarer Inhalt, korrektes Content-Type,
   ungueltiger Suchindex wird abgewiesen und Frontmatter bleibt entfernt.
3. JavaScript-/E2E-Tests mit manipulierten `title`, `excerpt`, `id` und
   Fehlermeldungen: sichtbarer Text bleibt Text, es gibt keine Inline-Handler
   und keine Navigation auf fremde oder JavaScript-URLs.
4. Middleware-/Integrationstest fuer CSP auf `/help`, `/help/view/...`,
   `/help/js/...` und den Help-API-Routen. Erwartete Direktiven sowie das
   Fehlen von `script-src 'unsafe-inline'` pruefen.
5. Build-/Integritaetstest fuer Manifest-Erzeugung, unveraenderte Artefakte und
   manipulierte Dateien. Der Test muss nachweisen, dass manipulierte Quellen
   nicht als gueltige Help-Ausgabe akzeptiert werden.
6. Bestehende Web-, Integrations- und E2E-Testprojekte ausfuehren. Manuell im
   Browser pruefen, dass erlaubte Help-Formatierung, Suche, interne Links,
   externe Links und beide Sprachen weiterhin funktionieren.

## Akzeptanzkriterien-Mapping

| Akzeptanzkriterium | Nachweis |
|---|---|
| HTML/Script aus Markdown wird nicht ausgefuehrt | Renderer- und API-Tests plus CSP-/E2E-Test |
| JavaScript-Payloads bleiben wirkungslos | Sanitizer-Tests, DOM-Tests und CSP |
| Manipulierter Suchindex erzeugt kein XSS | DTO-Validierung und DOM-basierte Suche |
| Erlaubte Markdown-Formatierung bleibt erhalten | Renderer-Regressionstests und Browserpruefung |
| Alle Help-Ausgabepfade sind geschuetzt | Markdown-, Legacy-HTML-, Suchindex- und statische Asset-Tests |
| CSP eingefuehrt und nachvollziehbar | Middleware-Test und Dokumentation der Direktiven |
| Build-Artefakte gegen Manipulation geschuetzt | Manifest-/Integritaetstest und Build-Konfiguration |

## Risiken und Entscheidungen

- Die genaue Paketversion von `Markdig` und der Sanitization-Bibliothek wird
  beim Implementieren gegen das aktuelle .NET-10-Target und die vorhandenen
  Paketquellen verifiziert. Die Whitelist bleibt fachlich explizit und darf
  nicht von Bibliotheksdefaults allein abhaengen.
- Eine globale CSP kann andere Seiten beeinflussen. Der Plan begrenzt sie
  deshalb auf Help-Ausgabepfade; falls das Framework den Header nur auf der
  gesamten Dokumentantwort sinnvoll setzen kann, ist die Auswirkung vor der
  Umsetzung zu pruefen und zu dokumentieren.
- Legacy-HTML kann durch CSP oder Sanitization Darstellung verlieren. Das ist
  akzeptabel, sofern der sichere Markdown-Pfad die gewuenschte Help-
  Formatierung liefert; die Tests muessen den produktiv benoetigten Umfang
  bestaetigen.

## Offene Punkte

Keine. Die Paketwahl, die konkrete CSP-Einbindung und die Manifest-Erzeugung
sind im Implementierungsschritt anhand der vorhandenen Paketquellen und des
laufenden Help-Workflows zu verifizieren; sie blockieren die Planung nicht.
