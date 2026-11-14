# Tasks: E2E-Testflaw — AuthenticatedUser_ChangesPassword_ViaUi schlägt mit net::ERR_ABORTED fehl (Issue #503)

| # | Bereich | Aufgabe | Status | Testnachweis |
|---|---------|---------|--------|--------------|
| 1 | E2E-Infrastruktur | `AuthGateway`: private Hilfsmethode `GotoToleratingForcedRedirectAsync` anlegen — `GotoAsync` mit `WaitUntil = DOMContentLoaded`, toleriert `net::ERR_ABORTED` (konkurrierende `NavigateTo(forceLoad: true)`-Umleitung des noch verbundenen Blazor-Circuits), wartet anschließend auf `NetworkIdle` und wiederholt die Navigation einmalig, falls die konkurrierende Umleitung die Seite auf eine andere URL gebracht hat | Erledigt | E2E `ChangePasswordWellKnownPlaywrightTests.AuthenticatedUser_ChangesPassword_ViaUi` |
| 2 | E2E-Infrastruktur | `AuthGateway.LoginAsync`: beide Navigationen (`/login`, `/`) über `GotoToleratingForcedRedirectAsync` führen | Erledigt | E2E `AuthenticationFlowPlaywrightTests`, `ChangePasswordWellKnownPlaywrightTests` |
| 3 | E2E-Infrastruktur | `AuthGateway.LogoutAsync`: Navigation nach `ClearCookiesAsync` über `GotoToleratingForcedRedirectAsync` führen | Erledigt | E2E `AuthenticationFlowPlaywrightTests` (Login→Logout→Login-Zyklus) |
| 4 | Verifikation | Betroffene Testklasse und Auth-E2E-Suite ausführen | Erledigt | Testlauf 2026-11-14: `ChangePasswordWellKnownPlaywrightTests` 4/4 bestanden; Auth-Suite (`AuthenticationFlow` + `SessionExpiry` + `ChangePasswordWellKnown`) 24/24 bestanden, kein `net::ERR_ABORTED` |
