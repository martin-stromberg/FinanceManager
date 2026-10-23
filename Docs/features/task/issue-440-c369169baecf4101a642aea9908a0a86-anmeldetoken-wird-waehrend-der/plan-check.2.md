# Plan-Gegenprüfung

## Ergebnis

**Status:** Plan vollständig

Der überarbeitete Plan deckt die Anforderung und die Testvoraussetzungen konkret ab. Die bisherigen Lücken T1–T6/P1–P3 sind geschlossen; Umsetzung und Funktionsnachweis stehen weiterhin aus.

## Abgleich Akzeptanzkriterien

| Akzeptanzkriterium | Umsetzung im Plan | Testnachweis im Plan | Status |
|--------------------|-------------------|----------------------|--------|
| Echter Formularlogin ohne API-Login oder Ersatzcookie | Isolierter Testnutzer, `LoginThroughUiAsync`, Auswertung der echten Loginantwort; Schritte 2–4. | Alle neuen E2E-Fälle nutzen diesen Ablauf. | Abgedeckt |
| Reale Reproduktion vor Produktivänderungen | Schritt 4 führt bei erfolgreichem erstem Home-Aufruf Navigation und beide Quick-Edit-Varianten bis über Ablauf fort; nur fachlicher Fehlschlag zählt. | API-Zeitlinie, Tokenmetadaten und Trace; ohne Reproduktion kein hypothetischer Fix. | Abgedeckt |
| Erste Home-API nach Login ohne Authfehler/Redirect | Request-/Circuit-State vor Komponenteninitialisierung; Handler im aufrufenden Scope; Schritte 5–7. | `UiLogin_FirstHomeApiAndInteractiveNavigationRemainAuthenticated`; serverseitiger Beobachter vor Login, erster Notifications-Status und spätere Circuit-Aufrufe. | Abgedeckt |
| Aktive Navigation und zugehörige API-Aufrufe vor/nach ursprünglichem Ablauf | Gemeinsame Browserrefresh-/Circuit-Synchronisierung; Sidebar Home/Konten, Schritte 6–7. | `ActiveNavigation_BeyondOriginalExpiry_UsesRenewedBrowserAndCircuitToken`: API-Erfolg, erneuerter Cookie/Bearer nach `exp + 12 s`. | Abgedeckt |
| Neue und vorhandene Quick-Edit-Zeilen erhalten Sitzung, Fokus und ungespeicherte Werte | Input-Ausnahme entfernen, Puffer/Fokus erhalten, kein Refresh-Render; Schritte 7–8. | Zwei `QuickEdit_*_InputWithoutBlurSurvivesExpiry`-Fälle: echte Eingabe ohne Blur, konstantes Feld, Mehrfeldwerte, Ablauf, spätere Speicherung. | Abgedeckt |
| Ohne Aktivität keine Keepalives; erst spätere geschützte Aktion führt zum Login | Keine periodischen Pings, passiver Messbeginn, Ablauf ab letztem Token; Schritte 7–8. | `Idle_NoKeepalive_ThenAccountsActionRedirectsWithReturnUrl`: null Requests/Redirects im Idle-Fenster, danach Kontenlink und Login mit Rücksprungziel. | Abgedeckt |
| Fehlgeschlagener Keepalive allein verwirft keine Arbeit durch Navigation | Pingfehler navigieren nicht; Schritte 6–8. | `RejectedKeepalive_StaysUntilProtectedAccountsAction`: Stampwiderruf, echte Eingabe, unveränderte Seite bis Folgeaktion. | Abgedeckt |
| Keine ausgeblendeten Login-/Anwendungsinhalte als Race-Umgehung | Bridge ohne Ausblendungsbarriere; Schritte 3–4 und 7. | `Initialization_KeepsLoginAndApplicationContentVisible`: beobachtende Zeitreihe ab SSR, verzögerte Hydration und konkrete Login/Home/Quick-Edit-Elemente. | Abgedeckt |
| Validierter JWT-Principal mit Stamp; Token im tatsächlichen API-Aufruf-Scope | Persistierter State, aufrufender Handler-Scope, geschützter Browserrückweg und Invalidation; Schritte 5–7. | `CircuitAuthScopeTests`, Principal-/Stamp-/Provider-Tests und reale Browserkette; Nutzertrennung sowie alte/fremde Snapshots berücksichtigt. | Abgedeckt |

## Fehlende oder unvollständige Testanforderungen

## E2E-Abdeckung

| Benutzerfluss / Akzeptanzkriterium | Geplanter E2E-Test | Status |
|------------------------------------|--------------------|--------|
| Formularlogin, erste Home-API, interaktive Navigation | `UiLogin_FirstHomeApiAndInteractiveNavigationRemainAuthenticated` | Abgedeckt |
| Navigation/API über ursprünglichen Ablauf | `ActiveNavigation_BeyondOriginalExpiry_UsesRenewedBrowserAndCircuitToken` | Abgedeckt |
| Bestehende Quick-Edit-Zeile ohne Blur | `QuickEdit_ExistingRow_InputWithoutBlurSurvivesExpiry` | Abgedeckt |
| Neue Quick-Edit-UI-Zeile ohne Blur | `QuickEdit_NewPlaceholderRow_InputWithoutBlurSurvivesExpiry` | Abgedeckt |
| Inaktivität und spätere geschützte Aktion | `Idle_NoKeepalive_ThenAccountsActionRedirectsWithReturnUrl` | Abgedeckt |
| Erfolgloser Ping ohne sofortige Navigation | `RejectedKeepalive_StaysUntilProtectedAccountsAction` | Abgedeckt |
| Sichtbare Inhalte während Initialisierung | `Initialization_KeepsLoginAndApplicationContentVisible` | Abgedeckt |

## Fehlende oder unvollständige Planbestandteile

## Hinweise

T1/T2 sind durch Testhost-Beobachtung und isolierte Zweiminutenfixture mit realer Serverzeit, zehn Sekunden ClockSkew, 60/5-Sekunden-Drosselung, Ablaufmetadaten und Deadlines geschlossen. T3–T6 besitzen konkrete Setup-, Aktions- und Ergebnisprüfungen. P1 legt die Tokenübergabe fest; P2/P3 sind in der neunstufigen Umsetzungsreihenfolge und den Tasks berücksichtigt.

Testdaten, Hilfen, TLS-/Restore-Voraussetzungen, betroffene Auth-/Quick-Edit-Regressionen und bekannte Baselinefehler sind benannt. Domänen-/Datenbankänderungen und neue Produkt-Timeoutkonfiguration sind nicht vorgesehen.

Der zusätzlich als Pflicht geplante 30-Minuten-Lauf geht über die erforderliche Kernabnahme hinaus. Er ist keine Forderung aus dem Issue oder dieser Gegenprüfung; die echte verkürzte JWT-Laufzeit mit unverändertem ClockSkew und realen Browseraktionen deckt die Ablaufkriterien ab. Seine Einstufung als optionaler Zusatz würde keine Planlücke öffnen.

Geprüft wurden `issue.md`, `requirement.md`, Bestandsaufnahme einschließlich Detaildokumenten, aktualisierter Plan und Tasks. Dies bestätigt Planvollständigkeit, keine bereits bewiesene Implementierung oder Frameworkintegration. Keine Tests ausgeführt; Plan und Code unverändert.
