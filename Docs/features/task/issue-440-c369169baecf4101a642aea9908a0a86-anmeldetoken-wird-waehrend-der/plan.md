# Umsetzungsplan: Anmeldetoken bleibt während aktiver Nutzung gültig

## Übersicht

Die erste Startseiten-API nach echtem Formularlogin und spätere Circuit-API-Aufrufe müssen authentifiziert bleiben. Navigation und Quick-Edit-Eingaben verlängern die Browsersitzung; ohne Bedienung läuft sie aus und erst die nächste geschützte Aktion führt zum Login. Vor Produktivänderungen wird der Originalfehler mit echten Browseraktionen reproduziert, auch wenn der erste Home-Aufruf bereits erfolgreich ist.

Basis ist `staging`. Dieser Plan ersetzt die vorige Fassung und behandelt T1–T6/P1–P3 aus `plan-check.md`. Keine Code-/Teständerungen in dieser Planungsphase.

## Designentscheidungen

| Komponente / Bereich | Gewählter Ansatz | Begründung |
|---|---|---|
| Circuit-Start | Neuer scoped `CircuitAuthState`: validierten JWT im Prerender-Request übernehmen und Snapshot mit `[PersistentState]`/`RegisterPersistentService<CircuitAuthState>(RenderMode.InteractiveServer)` vor interaktiver Komponenteninitialisierung wiederherstellen. | .NET 10 ist vorhanden; Server-Persistenz schützt Transfer mit Data Protection. Keine JS-Initialisierungsbarriere, kein globaler Token-Cache, kein zufälliger Circuit-HttpContext. |
| API-Scope | `AuthenticatedHttpClientHandler` im aufrufenden Scope außerhalb der gepoolten Factory-Pipeline erstellen; innerer Transport aus `IHttpMessageHandlerFactory.CreateHandler("Api")`. Scoped `HttpClient` und `IApiClient` teilen diesen Client. | Aktuelles `.AddHttpMessageHandler` erzeugt einen anderen Provider-Scope. `UseCookies=false` und Transportpooling bleiben erhalten. |
| Erneuerung | Browsercookie-Requests (Seitennavigation/Keepalive) erneuern; serverseitige Bearer-API-Aufrufe rotieren nicht unabhängig. Browser-Keepalive setzt HttpOnly-Cookie und liefert einen Data-Protection-geschützten Snapshot an JS, das ihn über `AuthSessionBridge` in den Circuit zurückgibt. | Set-Cookie einer Server-API erreicht den Browser nicht. Ein Erneuerungsweg hält Browser und Circuit konsistent. |
| UI-Integration | `AuthSessionBridge` als eigene Interactive-Server-Komponente im nicht-Help-Bereich von `App` neben `AuthRedirect`. Listenerregistrierung dorthin verlegen; Layout-Navigation nutzt dieselbe Pingpipeline. | App/Routes/Layout sind derzeit statisch, einzelne Seiten interaktiv; Registrierung darf nicht allein vom Layout abhängen. Kein Umbau aller Render-Modi oder Verstecken von Inhalt. |
| Aktivität | Vorhandene Pointer-/Tastatur-/Fokus-/Input-Ereignisse, Navigation und Blur; Quick-Edit-input nicht mehr ausschließen. 60/5 Sekunden Drosselung und Single-flight erhalten. | Laufende Eingabe/Paste zählt ohne Blur; keine periodischen Pings und keine neue Timeout-Konfiguration. |
| API-Beobachtung | Separater E2E-Testhost mit echter Produktstartkonfiguration und zusätzlichem `IHttpMessageHandlerBuilderFilter`/Beobachtungshandler für `Api`; testspezifische JSONL-Datei. | Browser-Network-Listener sehen Circuit-Serverrequests nicht. Keine Produktivtelemetrie allein für Tests. |
| Zeitfixture | Eigener Testserver, DB, Ports, Browserkontext mit `Jwt:LifetimeMinutes=2`; reale Serverzeit, unveränderter zehn Sekunden ClockSkew; Nachweis nach `exp + 12 s`. | 120 Sekunden reichen für normale 60-Sekunden-Pings bei Eingabe alle fünf Sekunden; gemeinsame 30-Minuten-Fixture bleibt unverändert. |

Frameworkgrundlage: [ASP.NET Core 10 prerendered state persistence](https://learn.microsoft.com/en-us/aspnet/core/blazor/state-management/prerendered-state-persistence?view=aspnetcore-10.0). Dort sind scoped Service-Persistenz vor interaktiver Nutzung und Data-Protection-Transfer für Interactive Server beschrieben; die Verwendung für den Projektsnapshot ist diese Designentscheidung.

## Programmabläufe

### Tokenübergabe und erster API-Aufruf (P1)

1. `Login.SubmitAsync`/`fmAuthLogin` bleiben der Formularpfad. Nach gesetztem Cookie lädt die UI Home vollständig.
2. `JwtRefreshMiddleware` validiert den eingegangenen JWT mit `JwtTokenValidationParametersFactory` und initialisiert `CircuitAuthState` vor Endpunkten. Refresh verwendet ausschließlich den validierten JWT-Principal samt `security_stamp`, niemals den Identity-Principal aus `HttpContext.User`.
3. Bei Browsernavigation im Erneuerungsfenster werden Cookie und Request-State gemeinsam aktualisiert. Snapshot enthält Token, Ablauf, Nutzer-ID und API-Basisadresse nach bestehender Konfiguration/Request-Origin; keine HttpContext-/Service-/Principal-Objekte.
4. Framework-Persistenz stellt diesen State im Circuit vor Komponenteninitialisierung wieder her, ausschließlich im Server-Render-Modus. State markiert explizit Requestinitialisierung bzw. Circuitwiederherstellung. Keine Tokenklartexte in HTML/Browserstorage.
5. `JwtCookieAuthTokenProvider.GetAccessTokenAsync` liest State, validiert und liefert noch gültige Tokens auch innerhalb des Erneuerungsfensters. Er erneuert nicht und schreibt keine Circuit-HTTP-Response. Fehlender/abgelaufener Token bleibt fehlend.
6. Der scoped Auth-Handler setzt diesen Bearer; der gepoolte Transport enthält keinen Nutzerzustand. Ungültige Authentifizierung erreicht den echten API-401-/`AuthenticationRequired`-/`AuthRedirect`-Pfad. Keine synthetischen erfolgreichen Antworten oder automatischen Re-Logins.
7. `CurrentUserService` nutzt in echten Controllerrequests weiter den Request-Principal, im ausdrücklich wiederhergestellten Circuit den validierten State. `Home.OnInitializedAsync` liest Nutzer-ID über `ICurrentUserService`, nicht direkt über `httpContextAccessor`. Dadurch bleiben Notifications/Sidebar regulär initialisiert.

### Browserrefresh und Rückführung

1. `AuthSessionBridge` registriert nach erstem Render eine `DotNetObjectReference` in JS. Registrierung selbst pingt nicht; nur tatsächlich während Registrierung aufgetretene Aktivität darf einmal vorgemerkt werden.
2. Gedrosselter Browserfetch validiert/erneuert den JWT im vorhandenen Fenster `max(5 Minuten, halbe Laufzeit)` über `JwtRefreshService`. Bearer-Serverrequests rotieren nicht zusätzlich.
3. Keepalive liefert 204 und einen geschützten Snapshot des validierten/erneuerten Tokens, auch wenn keine Rotation nötig war. Cookie wird bei Rotation gesetzt, Antwort ist `no-store`. JS benötigt keinen rohen Tokenheader; bestehende Verbraucher vor Entfernen von `X-Auth-Token` abgleichen.
4. `AuthSessionBridge.AcceptSnapshotAsync` prüft Data-Protection-Zweck, JWT/Lebensdauer, Nutzeridentität und monotonen Ablaufstand; übernimmt keinen älteren/fremden Snapshot. `keepaliveRequest` umfasst auch diesen Callback, damit Erfolg erst nach Circuit-Synchronisierung gilt.
5. Keine `StateHasChanged`-/Reload-/Fokusaktion für Tokenwechsel. Layout-Navigation wartet den vorhandenen Ping vor Zielwechsel ab, soweit interaktiv; globale JS-Listener/Bridge sichern statische Layoutpfade, Full-Page-Navigation initialisiert aus Cookie neu.
6. Pingfehler verlängern nichts und navigieren nicht. Invalidation/Logout verhindert verspätete Wiederaktivierung; mehrere Tabs behalten getrennte States und synchronisieren bei ihrer nächsten echten Aktivität den Browsercookie. Kein Nutzer-globaler Cache.

### Erst-Reproduktion und serverseitige Beobachtung (T1/T3)

1. Eigener Testnutzer mit `TestUserSeeder.EnsureUserAsync` direkt in isolierter SQLite-Datenbank, vor Formularlogin. Keine API-Anmeldung, kein `TestAuthCookieHelper`, kein künstliches Cookie.
2. Vor Öffnen/Submit Beobachter, Browser-Keepalive-/Redirectlogger und Sichtbarkeitsprobe starten. `LoginThroughUiAsync` bedient `#login-user`, `#login-pass`, Submit; Hilfe um Warten auf den echten Loginresponse und tatsächliches Ziel ergänzen (`body` allein genügt nicht).
3. Original-JWT nur aus `Set-Cookie` der tatsächlichen Loginantwort lesen; `exp`/Fingerprint speichern. Spätere Cookies nur lesen, nie ersetzen; keine Tokens/Passwörter im Report.
4. Testhost protokolliert Start-/Endsequenz, UTC, Methode, Pfad, Status, Tokenfingerprint, dekodierte Nutzer-ID/`exp`. Exklusive Hostinstanz/Testlauf-ID ordnet auch Requests ohne Bearer zu; Metadaten-Decoding ist keine Authentscheidung. Keine parallelen Szenarien in diesem Host.
5. Erster `GET /api/notifications` nach Login muss existieren und 200 liefern. Nach Hydration über Sidebar `a[href='/list/accounts']` und `a[href='/']` navigieren und jeweils neue `GET /api/accounts/statistics` bzw. `GET /api/notifications` erwarten. Damit ersetzt erfolgreicher Prerender keinen Circuitnachweis.
6. Erfolg erwarten, nicht absichtlich 401. Ist erster Home-Aufruf bereits grün, vor Produktivänderungen zwingend Navigation über Ablauf und beide QuickEdit-Varianten durchführen. Fachliche Assertion-Fehler bei API-Auth, Redirect, fehlender Erneuerung oder verlorenen Eingaben sind Rotnachweise; Setup-/TLS-/Selektor-/Beobachtungsfehler sind es nicht.
7. Commit, Konfiguration, UTC-Zeitlinie, originaler Ablauf, API-Sequenzen und Trace dokumentieren. Wenn alle Originalabläufe ohne Fehler laufen, keine hypothetischen Fixes beginnen, sondern konkrete Reproduktionsbefunde zurückmelden; kein künstlich erzeugter Fehler als Ersatz.

### Navigation und Zeit (T2)

Nach UI-Login alle zehn Sekunden über Sidebar Home/Konten wechseln, jeweils Inhalt und zugehörige tatsächliche API-Antwort abwarten. Bis `initialExp + 12 s` fortsetzen, danach beide Ziele nochmals aufrufen: alle neuen API-Antworten 2xx, korrekter Nutzer, erneuerter Bearer. Browsercookie muss einen anderen Fingerprint und späteres `exp` haben; nur gültiger ursprünglicher Token im ClockSkew genügt nicht.

Reale Zeit, keine Browseruhr-Vorspulung. Szenariodeadline 240 Sekunden nach Login, QuickEdit 300 Sekunden inklusive Setup; gewöhnliche Aktionen 15 Sekunden, Serverstart 90 Sekunden. Kontrollabfragen maximal einmal pro Sekunde. Neue Tests seriell; keine global veränderten Umgebungsvariablen. Optionaler Zusatzlauf mit 30-Minuten-Produktstandard und originalem `exp + 12 s`, separate Deadline 35 Minuten, bestätigt ergänzend das normale Zeitverhältnis; er blockiert weder Abnahme noch Commit. Die echte verkürzte Laufzeit mit unverändertem ClockSkew erfüllt die Kernabnahme.

### Quick Edit: bestehende und neue Zeile ohne Blur (T4)

1. Erst echten UI-Login durchführen. Danach vorhandene `AccountsApiSeedHelper`-/`BrowserApiHelper`-Datenhilfen nutzen: Konto, `/api/statement-drafts/preliminary`, `entries/batch-update` für vollständige bestehende `Open`-Zeile. Datenanlage endet vor Messung und ist kein Loginersatz.
2. Vorhandenes Öffnungsmuster `/card/statement-drafts/{id}?quickEdit=true` verwenden. Bestandsvariante nutzt persistierte Zeilen-ID. Neuzeilenvariante füllt die durch `BeginQuickEditAsync`/`AddPlaceholderRow` erzeugte leere UI-Zeile, kein API-Create als Ersatz. DOM-Zeilen-ID festhalten.
3. In zwei getrennten Fällen Datum, Betrag, Empfänger und Buchungstext ungespeichert ändern; danach `qe_subject_{rowId}` fokussieren. Alle Werte/DOM-ID und letzten tatsächlich ausgestellten Token samt `preEditExp` erfassen.
4. Bis `max(initialExp, preEditExp) + 12 s` alle fünf Sekunden echte Zeichen mit `PressSequentiallyAsync` in genau dieses Feld eingeben. Keine Navigation, Blur, Tab, Focus-Reset, manuellen Pings oder Daten-API-Aufrufe im Messfenster. Blur-/Fokusprobe verlangt dasselbe Feld ohne Unterbrechung; normale Keepalives müssen während der Eingabe den Cookie erneuern.
5. Nach Ablauf weitere Zeichen eingeben; exakten fortgeschriebenen Betreff und alle übrigen ungespeicherten Werte/Zeilen-ID prüfen. Lesend in Testdatenbank bestätigen: Bestandswerte noch unverändert bzw. neue Zeile noch nicht vorhanden. Erst danach vorhandenen Speichern-Ribbon bedienen, tatsächlichen Circuit-Batch-Request prüfen und Werte nach erneutem Öffnen kontrollieren.
6. Bestehende `qe_*`-IDs, `data-fm-quickedit-keepalive`, `SetEditValue`, Placeholder- und Fokus-/F8-Logik wiederverwenden. Kein neues Eingabeformular oder Autosave.

### Inaktivität und Folgeaktion (T5)

1. UI-Login, Home und sämtliche initialen API-/Browserrequests abwarten; letzter durch echte Bedienung gestarteter Ping muss abgeschlossen sein. Bei null offenen Requests `tIdle` und letzten wirklich ausgestellten Token/`exp` erfassen.
2. Bis `lastExp + 12 s` keine Bedien-/Navigations-/Fokus-/Pingereignisse. Nur DOM/Cookies/Protokoll lesend kontrollieren. Ab `tIdle` exakt null Keepalive-Requests und keine automatische Login-Navigation verlangen.
3. Danach Sidebar-Kontenlink klicken. Konkreter Folgeaufruf `GET /api/accounts/statistics` liefert 401; sichtbarer Redirect zu Login mit semantisch decodiertem `returnUrl=/list/accounts`. Der durch Klick mögliche Ping darf abgelaufenen JWT nicht wiederbeleben. Inhalte/Link bleiben bis zur Aktion sichtbar.
4. Separater Fall: nach echtem Login Testnutzer-Security-Stamp direkt in DB ändern, nach 5,2 Sekunden durch echte Eingabe einen erfolglosen Ping auslösen. Bis vor Folgeaktion URL/Inhalt unverändert prüfen; dann Kontenlink, API-401 und ReturnUrl. Kein künstliches Cookie/manueller Ping.

### Sichtbarkeit während Initialisierung (T6)

Vor Seitenöffnung mit `BrowserContext.AddInitScriptAsync` rein beobachtende Mutation-/AnimationFrame-Probe installieren. Ab erstem sichtbaren regulären SSR-Inhalt bis mindestens eine Sekunde nach Hydration/API-Abschluss prüfen: Login `#login-user`, `#login-pass`, Submit; Home `.app-shell`, `.content h1`, Sidebar-Kontenlink/Ribbon; QuickEdit betreffende `qe_*`-Inputs. Bereits sichtbare Elemente dürfen nicht verschwinden, `hidden`/`display:none`/`visibility:hidden`, leere Geometrie oder verdeckende Initialisierungsoverlays erhalten. Dokumentnavigation beendet das vorige Fenster; regulär deaktivierter Submit während Login ist erlaubt. Im Sichtbarkeitstest `_framework/blazor.web.js` lokal verzögert ausliefern, damit SSR sichtbar beobachtet wird, ohne Authantworten zu fälschen. Zeitreihe/Trace/Screenshots bei Fehler speichern; fehlende Inhalte nicht als übersprungene Prüfung behandeln.

## Neue Klassen

| Klasse | Typ | Zweck |
|---|---|---|
| `CircuitAuthState` | Scoped Service | Requestsnapshot, Framework-Persistenz, atomarer Circuitstand, explizite Wiederherstellungskennung und Invalidation. |
| `AuthTokenSnapshot` | Value Object | JWT, Nutzer, Ablauf, API-Basisadresse; keine Live-Objekte. |
| `AuthTokenSnapshotProtector` | Service | Zweckgebundener Data-Protection-Schutz und Prüfung des Refresh-Snapshots. |
| `AuthSessionBridge` | Razor-Komponente | Interaktive JS-Registrierung/Dispose und `AcceptSnapshotAsync` ohne UI-Render. |
| `SessionExpiryWebAppFixture` | Testfixture | Isolierter Host und Zweiminutenkonfiguration. |
| `CircuitApiObservationFilter`, `CircuitApiObservationHandler` | Testinfrastruktur | Ausgehende API-Aufzeichnung im Testhost-Factorytransport. |
| `SessionExpiryProbe` | Testhilfe | Loginresponse-/Cookieauswertung, reale Ablaufgrenzen, API-Wartebedingungen. |
| `InitializationVisibilityProbe` | Testhilfe | Sichtbarkeitszeitreihe und Artefakte. |
| `SessionExpiryPlaywrightTests` | E2E-Testklasse | Login/Navigation/Inaktivität/Sichtbarkeit. |
| `QuickEditSessionExpiryPlaywrightTests` | E2E-Testklasse | Bestands-/Neuzeile mit UI-Login und No-Blur-Eingabe. |
| `CircuitAuthScopeTests` | Integrationstestklasse | Scope-/Nutzertrennung, Wiederherstellung und aktualisierter Bearer. |

Neues ausführbares Testhostprojekt `FinanceManager.Tests.E2E.Server` referenziert `FinanceManager.Web`, wird in Solution/Build aufgenommen und verwendet dieselben öffentlichen Startmethoden `RegisterAppServices`, Migration/Seed, Updatekonfiguration und `ConfigureMiddleware`. Web-ContentRoot und ApplicationName müssen auf die echte Webanwendung zeigen, damit echte Razor-/Controller-/Static-Assets geladen werden. Nur Beobachtungsfilter ergänzen, keine eigenen Authendpunkte/Ersatzcontroller. Instrumentierung wird vor dem Rotnachweis ausschließlich im Testprojekt angelegt.

## Änderungen an bestehenden Klassen

### `ProgramExtensions`

Scoped State/Provider registrieren und Server-only-Persistenz ergänzen. Auth-Handler aus der gepoolten Pipeline entfernen und im Aufrufscope um Factorytransport legen. Beide bisherigen Clientregistrierungen teilen denselben scoped Client; Basisadresse aus Snapshot/Konfiguration. Factorytransport beim Circuit-Dispose nicht selbst zerstören; `UseCookies=false` erhalten.

### `JwtRefreshMiddleware`, `JwtCookieAuthTokenProvider`, `IAuthTokenProvider`

Middleware validiert realen JWT, initialisiert State vor Endpunkten und erneuert nur Browsercookie-Requests. Provider liest State, verwirft gültige Tokens nicht vorzeitig und erneuert nicht selbst. Signaturen `GetAccessTokenAsync`/`InvalidateCache` bleiben. `PrimeCache`-Aufrufer in `AuthController` und zusätzlich gefundenem `UserSettingsController` auf Request-State umstellen; Logout/Profil-/Authentifizierungswechsel dürfen keinen alten State erhalten.

### `AuthenticatedHttpClientHandler`

`SendAsync` verwendet Provider des aufrufenden Scopes, bestehender realer API-Authfehlerpfad und Cancellation-499 bleiben erhalten. Keine Browsercookie-Synchronisierung aus Server-API-Set-Cookie.

### `AuthKeepaliveController`

204-Erfolg mit geschütztem Snapshotheader und `Cache-Control: no-store` aus bereits validiertem/erneuertem Request-State. Fehler liefern keinen neuen Snapshot.

### `App`, `MainLayout`, `CurrentUserService`, `Home`

Bridge in vorhandenen nicht-Help-Bereich einfügen. Listenerlebensdauer aus statischem Layout in Bridge verlegen; Navigationsping gleiche Pipeline. CurrentUser/Home erhalten explizite Circuitquelle. Keine Readiness-Flags zum Ausblenden; vorhandene Help-SSR-Ausnahme/Seiten-Render-Modi erhalten.

### `financeManager.js`, `QuickEditTable`, `StatementDraftEntriesListViewModel`

JS verbindet Bridge, übernimmt Snapshot vor Pingabschluss, entfernt QuickEdit-input-Ausnahme und erhält Drosselung/Single-flight. Dispose entfernt Listener/Referenz und verhindert verspätete Übernahme. Kein periodischer Timer; Requesttimeout bleibt. Für Tabelle/ViewModel keine neue Datenlogik: vorhandene Marker, IDs, Puffer, Placeholder-, Validierungs-/Fokusabläufe nutzen.

### `PlaywrightWebAppFixture`, `AuthGateway`, `PlaywrightTestTiming`

Serverstart extrahieren/parametrieren für eigene Fixture/Hostkonfiguration ohne globale Umgebungsvariablen oder Umschaltung der Defaultfixture. Formularloginhilfe um tatsächlichen Abschluss ergänzen. Reale UTC-/exp-Wartehilfe mit Deadline und passiven Beobachtungen; vorhandene 5,2-Sekunden-Hilfe nutzen.

## Datenbankmigrationen

Keine. Testdaten nur in bestehenden Tabellen der isolierten SQLite-Datei.

## Validierungsregeln

| Feld / Objekt | Regel | Fehlerfall |
|---|---|---|
| JWT | Signatur, Issuer, Audience, Lebensdauer samt zehn Sekunden Skew; Refresh zusätzlich gültiger Stamp/aktiver Nutzer. | Kein Refresh; nächste geschützte Aktion regulär 401. |
| Snapshot | Zweckgebundener Schutz, gültiger JWT, gleicher Circuitnutzer, konsistentes `exp`, kein älterer Stand/Übernahme nach Invalidation. | Verwerfen; Ping allein navigiert nicht. |
| Scope | State pro Request/Circuit; Transport ohne Cookiecontainer/Nutzertoken. | Cross-User-Bearer ist Testfehler. |
| Aktivität | Nur tatsächliche Ereignisse/Navigation, höchstens ein Ping inklusive Callback. | Keine Anfrage während Idle. |

## Konfigurationsänderungen

| Eintrag | Typ | Standardwert | Zweck |
|---|---|---|---|
| `Jwt:LifetimeMinutes` nur neue Fixture | int | 2 | Verkürzter echter Test; Produkt/defaultfixture bleiben 30. |
| Beobachtungspfad/Testlauf-ID | Testhostoption | Testspezifisch temporär | Keine Produktivkonfiguration oder Tokenklartexte. |

Keine neuen Produktpakete: .NET 10 enthält benötigte Frameworkdienste. Testhost nutzt vorhandene Projektabhängigkeiten. Restore bei Bedarf mit lesbarer expliziter NuGet-Konfiguration samt vorhandenen Paketquellen, keine Zugangsdaten einchecken. Vor E2E HTTPS-Dev-Zertifikat reparieren/erstellen und Vertrauen prüfen; Baseline scheiterte beim TLS-Handshake. Kein Produktiv-TLS-Workaround.

## Seiteneffekte und Risiken

- State muss vor erster interaktiver API verfügbar sein, auch bei bestehenden mehreren interaktiven Roots: Scopeintegration plus echter Browsernachweis sind Pflicht. Persistenz ausschließlich Server, kein Auto/WebAssembly.
- Erneuerungsort ändert Provider-/Middlewaretestvertrag: Browser erneuert/synchronisiert, Bearer-API rotiert nicht. API validiert Nutzerzustand weiterhin.
- `CurrentUserService` bleibt in HTTP-Controllern requestgebunden; Circuitfallback ausschließlich bei expliziter Wiederherstellung. Ablauf allein darf keinen UI-Render/Redirect auslösen.
- Single-flight, ältere Antworten, Logout während Antwort und zwei getrennte Nutzer/Circuits testen. Kein geteilter CookieContainer.
- `UserSettingsController` ist weiterer Priming-Aufrufer: Profil-/Tokenwechsel im neuen Vertrag erhalten.
- Fehlende API-Aufzeichnung ist Infrastrukturfehler, kein bestandener Login. Baseline: ein Unit-Assethashfehler, Integration grün, E2E nur TLS-Setupfehler.

## Umsetzungsreihenfolge

1. **Testlauf ermöglichen.** Voraussetzungen: vorhandenes .NET-/Playwright-Projekt/Paketquellen. Zertifikat, Restore, Bau und realen Browserstart herstellen; bekannte Baseline separat protokollieren.
2. **Testhost/Beobachter erstellen.** Voraussetzungen: Schritt 1, öffentliche Startmethoden. Neues Hostprojekt, Filter/Handler, Fixtureparameter, JSONL/Cleanup anlegen. Mit bekannter API-Aktion Reihenfolge/Vollständigkeit prüfen; keine Produktivänderung.
3. **Zeit-/Login-/Daten-/Sichtbarkeitshilfen erstellen.** Voraussetzungen: Schritt 2 und vorhandene Seeder/AuthGateway/QuickEdit-Hilfen. Eigene Nutzer/DB, zwei Minuten, Loginresponseauswertung, Deadlines und Sichtbarkeitsprobe; keine Ersatzcookies/API-Logins.
4. **Primäre Reproduktionsfälle vor Produktivfix schreiben/ausführen.** Voraussetzungen: Schritte 1–3. Erste Home-API, aktive Navigation, beide QuickEdit-Varianten, Inaktivität/Sichtbarkeit. Bei erfolgreichem ersten Aufruf Originalablauf weiterführen. Tatsächlichen fachlichen Rotnachweis samt Trace festhalten; Setupfehler beheben. Ohne Reproduktion keine hypothetischen Fixes.
5. **State-/Scope-Kette implementieren.** Voraussetzungen: fachlicher Rotnachweis, .NET 10. Snapshot/State, persistente Registrierung, Middlewareinitialisierung, Provider, scoped Client/Handler samt Basisadresse/Dispose und Scopeintegrationstests.
6. **Refresh/Rückweg implementieren.** Voraussetzungen: Schritt 5, vorhandener Refresh-Service/Data Protection. Protector, Browser-only-Erneuerung, Keepalive-Snapshot, Bridge, Priming-Aufrufer/Invalidierung; Principal-/Stamp-/Snapshottests.
7. **Aktivität/UI-Nutzerzustand implementieren.** Voraussetzungen: Schritt 6, bestehende UI-Muster. Bridge in App, Layout-/JS-Registrierung/Navigation, QuickEdit-input, Single-flight, CurrentUser/Home. Keine neue QuickEdit-Datenlogik. Erste Home-/Navigations-/Sichtbarkeitstests grün.
8. **QuickEdit/Inaktivität abnehmen.** Voraussetzungen: Schritt 7 und vorab erstellte Tests. Beide Zeilentypen ohne Blur, Werte/Fokus/Speicherung nach Ablauf; null Idle-Pings, erfolgloser Ping ohne Redirect, konkrete Accounts-Folgeaktion. Bei Abweichung gezielt Schritte 5–7 korrigieren, Kriterien erhalten.
9. **Regression und Produktionszeit.** Voraussetzungen: Schritte 5–8 grün. Betroffene Unit-/Integration-/E2E-Suiten durchführen; separaten 30-Minuten-Navigationslauf optional ergänzen, ohne Abnahme oder Commit zu blockieren. Baselinefehler getrennt ausweisen; Healthcheckabbruch ist keine Abnahme. Tasks/Nachweise aktualisieren.

## Tests

### Neue Tests

| Test / Hilfsmethode | Testklasse | Was wird geprüft / bereitgestellt? |
|---|---|---|
| `UiLogin_FirstHomeApiAndInteractiveNavigationRemainAuthenticated` | `SessionExpiryPlaywrightTests` | Erster Notifications-Aufruf, anschließend echte Circuit-Accounts/Home. |
| `ActiveNavigation_BeyondOriginalExpiry_UsesRenewedBrowserAndCircuitToken` | `SessionExpiryPlaywrightTests` | Cookie/Bearer wirklich erneuert, API nach `exp + 12 s`. |
| `Idle_NoKeepalive_ThenAccountsActionRedirectsWithReturnUrl` | `SessionExpiryPlaywrightTests` | Ruhiges Fenster, Accounts-401 und Login/ReturnUrl. |
| `RejectedKeepalive_StaysUntilProtectedAccountsAction` | `SessionExpiryPlaywrightTests` | Echter Stampwiderruf, Pingfehler allein ohne Navigation. |
| `Initialization_KeepsLoginAndApplicationContentVisible` | `SessionExpiryPlaywrightTests` | Sichtbarkeitszeitreihe SSR/Hydration. |
| `QuickEdit_ExistingRow_InputWithoutBlurSurvivesExpiry` | `QuickEditSessionExpiryPlaywrightTests` | Bestehende ungespeicherte Zeile, Fokus/Werte/Speichern. |
| `QuickEdit_NewPlaceholderRow_InputWithoutBlurSurvivesExpiry` | `QuickEditSessionExpiryPlaywrightTests` | Neue UI-Zeile, identische No-Blur-Nachweise. |
| `PrerenderSnapshot_RestoredBeforeFirstApiCall_WithoutHttpContext` | `CircuitAuthScopeTests` | Request beenden, Snapshot in neuem Scope, HttpContext null, erster Bearer korrekt; tatsächliche Frameworkkette zusätzlich E2E. |
| `TwoCallerScopes_SharedTransport_NeverShareTokens` | `CircuitAuthScopeTests` | Zwei Nutzer/Scopes, gemeinsamer Transport, strikt getrennte Bearer. |
| `KeepaliveSnapshot_UpdatesSameCircuitProvider_RejectsOlderOrForeignState` | `CircuitAuthScopeTests` | Nächster Bearer aktualisiert, alte/fremde/ungültige Snapshots und verspätete Antwort nach Logout abgelehnt. |
| `Refresh_UsesValidatedJwtPrincipal_WhenIdentityPrincipalHasNoStamp` | `ApiClientAuthTests` | Identity ohne Stamp plus gültiger JWT erfolgreich; fehlender/geänderter Stamp/deaktivierter Nutzer abgelehnt. |
| `Provider_ReturnsValidTokenInsideRenewalWindow_AndRejectsExpired` | `JwtCookieAuthTokenProviderTests` | Kein vorzeitiges null; tatsächliche Ablaufgrenze/Skew. |
| `BrowserKeepalive_RenewsCookie_BearerApiDoesNotRotate` | `ApiClientAuthTests` | Cookie-/Snapshotvertrag, eindeutiger Erneuerungsort. |
| `ScopeUser_UsesRestoredState_WhileHttpRequestUsesRequestPrincipal` | `CircuitAuthScopeTests` | Home ohne zufälligen HttpContext, HTTP-Nutzerisolation. |

### Betroffene bestehende Tests

| Test / Testklasse | Grund der Anpassung |
|---|---|
| `JwtCookieAuthTokenProviderTests` | Cache-/Cookie-Priorität auf expliziten State umstellen, Refreshnachweis künftig Middleware; Sicherheitsfälle erhalten. |
| `ApiClientAuthTests` | Principalquelle, Keepalive-/Snapshotvertrag, nur Browserrotation; Authfehler-/Stamp-/Deaktivierungsfälle erhalten. |
| `AuthenticationFlowPlaywrightTests.ActiveNavigationAndInteraction_ShouldRefreshNearExpirySessionWithoutLoginRedirect` | Schneller Altfall bleibt; künstliches Cookie ist kein neuer Funktionsnachweis. |
| `AuthenticationFlowPlaywrightTests.Keepalive_FailedRefresh_ShouldNotTriggerLoginRedirect` | Bestehende Semantik erhalten; neuer echter UI-/Stampfall ergänzt sie. |
| `QuickEdit_Blur_ShouldSendKeepaliveAndKeepLocalInputValue`, `QuickEdit_Blur_ShouldCoalesceKeepaliveRequests` | Blur-/Coalescingregression bleibt, ersetzt keine No-Blur-Abdeckung. |
| Bestehende F8-/Ctrl-F8-/Ctrl-Pfeil-/Datum-/Speichern-QuickEdit-Tests | Fokus/Puffer/Placeholder unverändert nachweisen. |

### E2E-Tests (primärer Funktionsnachweis)

| Priorität | Szenario | Testdatei / Testklasse | Abgedecktes Akzeptanzkriterium | Warum E2E nötig ist |
|---|---|---|---|---|
| Pflicht vor Fix | Erste Home-API und Originalablauf; bei grünem ersten Aufruf weiter reproduzieren | `Tests/Auth/SessionExpiryPlaywrightTests.cs` | Echter Login/Rotnachweis, kein initialer Redirect | Browser, Prerender, Circuit und HTTP-Scope gemeinsam. |
| Pflicht | Home/Konten vor/nach originalem `exp + 12 s` | `SessionExpiryPlaywrightTests` | Aktive Navigation/API mit erneuertem Token | Browsercookie und Serverrequests gemeinsam. |
| Pflicht, zwei Fälle | Bestand/Placeholder, Tasten ohne Blur, Fokus/Werte/Speichern | `Tests/StatementDrafts/QuickEditSessionExpiryPlaywrightTests.cs` | Beide Massenbearbeitungsarten behalten Arbeit | DOM, JS und ViewModel benötigen Browser. |
| Pflicht | Idle ohne Ping/Redirect, danach Kontenlink/401/ReturnUrl | `SessionExpiryPlaywrightTests` | Ablauf erst bei geschützter Folgeaktion sichtbar | Reale Zeit/unbeeinflusste UI. |
| Pflicht | Stampwiderruf, Eingabe-Ping scheitert, erst Folgeaktion redirectet | `SessionExpiryPlaywrightTests` | Pingfehler verwirft keine Arbeit | Sichtbare Navigation muss ausbleiben. |
| Pflicht | Login/Home/QuickEdit ab erstem Render sichtbar | `SessionExpiryPlaywrightTests`, `InitializationVisibilityProbe` | Kein Verstecken als Race-Umgehung | Abschlussassertion erkennt zwischenzeitliches Ausblenden nicht. |
| Optional, nicht abnahme-/commitblockierend | Navigation mit 30-Minuten-Produktstandard | `SessionExpiryPlaywrightTests`, separate Langlauffixture | Ergänzende Prüfung des normalen Zeitverhältnisses | Die echte verkürzte Laufzeit mit unverändertem ClockSkew erfüllt bereits die Kernabnahme. |

Bestehende E2E-Anpassungen stehen oben. Alle neuen Nachweise verwenden Formularlogin; API-Datenanlage erst danach und vor Messung ist erlaubt.

## Offene Punkte

Keine fachlichen Nutzerentscheidungen offen. Inaktivität folgt JWT-Laufzeit samt Validierungstoleranz, Ereignisse/Drosselung sind festgelegt. Ob der Originalfehler zuerst bei Home, Navigation nach Laufzeit oder QuickEdit auftritt, ist zu erhebendes Testergebnis. Ohne tatsächlichen Rotnachweis vor Produktivänderungen mit konkretem Befund zurückmelden.
