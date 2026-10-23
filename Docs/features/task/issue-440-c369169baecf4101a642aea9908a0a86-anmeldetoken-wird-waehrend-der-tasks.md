# Tasks: Anmeldetoken bleibt während aktiver Nutzung gültig

| # | Bereich | Aufgabe | Status | Testnachweis |
|---|---|---|---|---|
| 1 | Testumgebung | HTTPS-Dev-Zertifikat und Vertrauen für echten Browserstart herstellen. | Offen | — |
| 2 | Testumgebung | Restore/Bau mit lesbarer NuGet-Konfiguration und vorhandenen Quellen ermöglichen. | Offen | — |
| 3 | Testumgebung | `FinanceManager.Tests.E2E.Server` mit echter Webstartkonfiguration/ContentRoot/ApplicationName anlegen und in Solution aufnehmen. | Offen | — |
| 4 | Testumgebung | Serverstart der `PlaywrightWebAppFixture` für separaten Host parametrieren, Standardfixture erhalten. | Offen | — |
| 5 | Testumgebung | `SessionExpiryWebAppFixture` mit eigener DB/Ports/Kontext, zwei Minuten JWT und Cleanup anlegen. | Offen | — |
| 6 | Testbeobachtung | `CircuitApiObservationFilter` für benannten `Api`-Transport im Testhost anlegen. | Offen | — |
| 7 | Testbeobachtung | `CircuitApiObservationHandler` mit Sequenz/UTC/Pfad/Status/Tokenmetadaten und testspezifischem JSONL anlegen. | Offen | — |
| 8 | Testbeobachtung | Beobachtungszuordnung einschließlich fehlendem Bearer im exklusiven Testhost prüfen. | Offen | — |
| 9 | Testhilfen | `AuthGateway.LoginThroughUiAsync` um echte Loginresponse-/Zielabschlussbedingungen ergänzen. | Offen | — |
| 10 | Testhilfen | Testnutzer vor Formularlogin mit `TestUserSeeder` direkt in isolierter DB anlegen. | Offen | — |
| 11 | Testhilfen | `SessionExpiryProbe` für ursprünglichen Login-Set-Cookie, passive Cookieauswertung und UTC-/exp-Deadlines anlegen. | Offen | — |
| 12 | Testhilfen | QuickEdit-Datenhilfe mit ausschließlich UI-Login, danach bestehender Konto-/Draft-/Open-Zeilenanlage ergänzen. | Offen | — |
| 13 | Testhilfen | `InitializationVisibilityProbe` mit InitScript, SSR-/Hydrationszeitreihe und Fehlerartefakten anlegen. | Offen | — |
| 14 | E2E | `UiLogin_FirstHomeApiAndInteractiveNavigationRemainAuthenticated` schreiben: erster Notifications-Aufruf, Sidebar Accounts/Home mit Serverbeobachtung. | Offen | — |
| 15 | E2E | `ActiveNavigation_BeyondOriginalExpiry_UsesRenewedBrowserAndCircuitToken` mit Zehnsekundenaktionen und `exp + 12 s` schreiben. | Offen | — |
| 16 | E2E | `QuickEdit_ExistingRow_InputWithoutBlurSurvivesExpiry` mit Fünfsekundeneingaben, Fokus/Mehrfeldwerten und späterer Speicherung schreiben. | Offen | — |
| 17 | E2E | `QuickEdit_NewPlaceholderRow_InputWithoutBlurSurvivesExpiry` für tatsächlich neu gefüllte UI-Zeile schreiben. | Offen | — |
| 18 | E2E | `Idle_NoKeepalive_ThenAccountsActionRedirectsWithReturnUrl` mit ruhigem Messbeginn und konkretem Kontenlink schreiben. | Offen | — |
| 19 | E2E | `RejectedKeepalive_StaysUntilProtectedAccountsAction` mit DB-Stampwiderruf und echter Eingabe schreiben. | Offen | — |
| 20 | E2E | `Initialization_KeepsLoginAndApplicationContentVisible` während SSR/Hydration schreiben. | Offen | — |
| 21 | Reproduktion | Alle Originalabläufe vor Produktivänderungen ausführen; bei grünem ersten Home-Aufruf Navigation/QuickEdit bis Ablauf weiter reproduzieren. | Offen | — |
| 22 | Reproduktion | Tatsächlichen fachlichen Rotnachweis mit Commit/Konfiguration/UTC/API-Zeitlinie/Trace dokumentieren; Setupfehler nicht als Rotnachweis zählen. | Offen | — |
| 23 | Auth-State | `AuthTokenSnapshot` als Value Object für Token/Nutzer/Ablauf/API-Basisadresse anlegen. | Offen | — |
| 24 | Auth-State | `CircuitAuthState` mit Requestinitialisierung, expliziter Circuitwiederherstellung, atomarem Stand und Invalidation anlegen. | Offen | — |
| 25 | Auth-State | Scoped State und `RegisterPersistentService` ausschließlich für Interactive Server in `ProgramExtensions` registrieren. | Offen | — |
| 26 | Auth-State | `JwtRefreshMiddleware` zur Initialisierung des validierten Request-State vor Endpunkten erweitern. | Offen | — |
| 27 | Auth-State | `JwtCookieAuthTokenProvider.GetAccessTokenAsync` auf expliziten State umstellen; gültigen Token im Erneuerungsfenster erhalten, eigenen Refresh entfernen. | Offen | — |
| 28 | API-Scope | `AuthenticatedHttpClientHandler` im Aufrufscope außerhalb Factorypool aufbauen, Cancellation-499 erhalten. | Offen | — |
| 29 | API-Scope | Scoped `HttpClient`/`IApiClient` auf gemeinsame Pipeline, Snapshot-Basisadresse und korrekte Transport-Dispose-Semantik umstellen. | Offen | — |
| 30 | Refresh | `JwtRefreshMiddleware` ausschließlich mit validiertem JWT-Principal samt Stamp erneuern lassen. | Offen | — |
| 31 | Refresh | Erneuerung auf Browsercookie-Requests begrenzen, unabhängige Bearer-API-Rotation entfernen. | Offen | — |
| 32 | Refresh | `AuthTokenSnapshotProtector` mit Zweck-/Nutzer-/Ablaufprüfung anlegen. | Offen | — |
| 33 | Refresh | `AuthKeepaliveController` um geschützten Snapshot/`no-store` bei 204 erweitern. | Offen | — |
| 34 | Refresh | `AuthController`-Priming/Logout an Request-State und Invalidation anpassen. | Offen | — |
| 35 | Refresh | `UserSettingsController`-Priming und Profil-/Tokenwechsel an neuen Vertrag anpassen. | Offen | — |
| 36 | Browserintegration | `AuthSessionBridge` mit interaktiver Registrierung, `AcceptSnapshotAsync` und Dispose ohne UI-Render anlegen. | Offen | — |
| 37 | Browserintegration | Bridge in nicht-Help-Bereich von `App` einfügen, vorhandene Render-Modi erhalten. | Offen | — |
| 38 | Browserintegration | Listenerlebensdauer aus `MainLayout` in Bridge verlegen und Navigationsping an gemeinsame Pipeline binden. | Offen | — |
| 39 | Browserintegration | JS-Ping um geschützte Snapshotübergabe vor Abschluss und Single-flight inklusive Callback erweitern. | Offen | — |
| 40 | Browserintegration | QuickEdit-input-Ausnahme entfernen, vorhandene Marker/Puffer/Fokuslogik und 60/5-s-Drosselung erhalten. | Offen | — |
| 41 | Browserintegration | Listener-/Referenzcleanup und Schutz gegen verspätete Übernahme nach Invalidation ergänzen; Registrierung pingt nicht. | Offen | — |
| 42 | UI-Nutzerzustand | `CurrentUserService` bei explizitem Circuitstate aus validiertem State, im HTTP-Request weiter aus Request-Principal versorgen. | Offen | — |
| 43 | UI-Nutzerzustand | `Home.OnInitializedAsync`-Nutzerkennung auf `ICurrentUserService` umstellen; keine Readiness-Ausblendung. | Offen | — |
| 44 | Integrationstests | `PrerenderSnapshot_RestoredBeforeFirstApiCall_WithoutHttpContext` ergänzen. | Offen | — |
| 45 | Integrationstests | `TwoCallerScopes_SharedTransport_NeverShareTokens` ergänzen. | Offen | — |
| 46 | Integrationstests | `KeepaliveSnapshot_UpdatesSameCircuitProvider_RejectsOlderOrForeignState` inklusive Logout während Antwort ergänzen. | Offen | — |
| 47 | Integrationstests | `Refresh_UsesValidatedJwtPrincipal_WhenIdentityPrincipalHasNoStamp` und fehlenden/geänderten Stamp/deaktivierten Nutzer prüfen. | Offen | — |
| 48 | Unit-Tests | `Provider_ReturnsValidTokenInsideRenewalWindow_AndRejectsExpired` ergänzen. | Offen | — |
| 49 | Integrationstests | `BrowserKeepalive_RenewsCookie_BearerApiDoesNotRotate` ergänzen. | Offen | — |
| 50 | Integrationstests | `ScopeUser_UsesRestoredState_WhileHttpRequestUsesRequestPrincipal` ergänzen. | Offen | — |
| 51 | Regression | Bestehende Provider-/ApiClientAuth-Tests auf neuen State-/Refreshvertrag anpassen und Sicherheitsfälle erhalten. | Offen | — |
| 52 | Regression | Vorhandene NearExpiry-/fehlgeschlagener-Ping-E2E-Fälle ausführen/anpassen; künstliche Cookies nicht als neuen Nachweis verwenden. | Offen | — |
| 53 | Regression | Bestehende QuickEdit-Blur-/Coalescing-/F8-/Pfeil-/Datum-/Speichertests ausführen. | Offen | — |
| 54 | Abnahme | Neue Login-/Navigation-/Sichtbarkeitsfälle nach Fix grün ausführen. | Offen | — |
| 55 | Abnahme | Beide No-Blur-QuickEdit-Fälle samt Werterhalt und späterer Speicherung grün ausführen. | Offen | — |
| 56 | Abnahme | Idle-/abgelehnter-Ping-Fälle mit konkretem Accounts-401/ReturnUrl grün ausführen. | Offen | — |
| 57 | Optionale Zusatzprüfung | Optional separaten Navigationslauf mit 30-Minuten-Produktstandard und Ablauf plus zwölf Sekunden ausführen; nicht abnahme-/commitblockierend. | Offen | — |
| 58 | Abnahme | Betroffene Unit-/Integration-/E2E-Suiten ausführen und bekannte Baselinefehler getrennt ausweisen. | Offen | — |
| 59 | Dokumentation | Reproduktions-/Abnahmenachweise und Tasks nach Review aktualisieren. | Offen | — |
