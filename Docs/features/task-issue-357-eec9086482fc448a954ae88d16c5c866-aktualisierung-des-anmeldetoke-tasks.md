# Tasks: Aktualisierung des Anmeldetokens

| # | Bereich | Aufgabe | Status | Testnachweis |
|---|---------|---------|--------|--------------|
| 1 | Logik | `JwtRefreshMiddleware.InvokeAsync` auf den tatsächlichen Refresh- und Renew-Window prüfen | Offen | — |
| 2 | Logik | `IJwtRefreshService.RefreshAsync` für `security_stamp`-, Userstatus- und Admin-Rollen-Validierung prüfen | Offen | — |
| 3 | Logik | `JwtCookieAuthTokenProvider.ValidateAndRefreshTokenAsync` für Cookie-Refresh bei nahendem Ablauf absichern | Offen | — |
| 4 | UI/Frontend | `window.financeManager.keepalive`-Trigger für aktive Interaktion, Navigation und Quick-Edit-Blur verifizieren | Offen | — |
| 5 | UI/Frontend | `MainLayout`-Keepalive-Ping für Navigation und aktive Nutzung stabilisieren | Offen | — |
| 6 | Auth-Handling | `AuthRedirect`-Redirect-Logik auf echte Auth-Invalidierung begrenzen | Offen | — |
| 7 | API | `AuthKeepaliveController.Get` als No-Op-Endpoint für Refresh-Trigger validieren | Offen | — |
| 8 | Tests | `ApiClientAuthTests` für aktiven Refresh-/Keepalive-Pfad ergänzen oder korrigieren | Offen | — |
| 9 | Tests | `AuthenticationFlowPlaywrightTests` für Login-Redirect-Deduplizierung und Session-Validität erweitern | Offen | — |
| 10 | Tests | `StatementDraftQuickEditValueTakeoverE2ETests` für Quick-Edit-Blur- und Coalescing-Verhalten erweitern | Offen | — |
| 11 | Tests | `JwtRefreshServiceTests` für `security_stamp`- und Rollen-Fehlerpfade verifizieren | Offen | — |
| 12 | Verifikation | Regressionen für aktive Session-Erhaltung und fachliche Invalidierung im Gesamtsystem prüfen | Offen | — |
